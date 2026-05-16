#pragma warning disable IDE1006 // Suppress Harmony Naming Style Warnings

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;
using System.Xml.Serialization;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects;
using Assets.Scripts.Networking;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Serialization;
using static Assets.Scripts.Atmospherics.Chemistry;
using TerraformingMod.Tools;
using System.Reflection;

namespace TerraformingMod
{
    // 1. Capture gas leaving pipes/rooms and entering the world
    [HarmonyPatch(typeof(PlanetaryAtmosphereSimulation), "GiveToGlobal")]
    public class PlanetaryAtmosphereGivePatch
    {
        [HarmonyPrefix]
        public static void Prefix(GasMixture gasMixture)
        {
            if (!NetworkManager.IsClient && TerraformingFunctions.ThisGlobalPrecise != null && gasMixture.IsValid)
            {
                var change = new SimpleGasMixture(gasMixture);
                TerraformingFunctions.ThisGlobalPrecise.UpdateGlobalAtmosphereChange(change);
            }
        }
    }

    // 2. Capture gas being vacuumed out of the world into pipes/rooms
    [HarmonyPatch(typeof(PlanetaryAtmosphereSimulation), "TakeGlobalMoles")]
    public class PlanetaryAtmosphereTakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(GasMixture __result)
        {
            if (!NetworkManager.IsClient && TerraformingFunctions.ThisGlobalPrecise != null && __result.IsValid)
            {
                var change = new SimpleGasMixture(__result);
                change.Scale(-1.0); // Negate because gas is being removed from the planet
                TerraformingFunctions.ThisGlobalPrecise.UpdateGlobalAtmosphereChange(change);
            }
        }
    }

    [HarmonyPatch(typeof(AtmosphericsManager), "Deregister", new Type[] { typeof(Atmosphere) })]
    public class AtmosphericsControllerDeregisterPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Atmosphere atmosphere)
        {
            // CRITICAL: Abort if we are on the main menu or global is null
            if (TerraformingFunctions.ThisGlobalPrecise == null || TerraformingFunctions.GlobalAtmosphere == null) return;

            if (!NetworkManager.IsClient && atmosphere != null && atmosphere.Mode == AtmosphereHelper.AtmosphereMode.World && atmosphere.Room == null && atmosphere.IsCloseToGlobal(new PressurekPa((double)AtmosphereHelper.GlobalAtmosphereNeighbourThreshold / 6.0 * (double)AtmosphereHelper.NewAtmosSupressionMultiplier())))
            {
                var mixture = GasMixtureHelper.Create();
                mixture.Add(atmosphere.GasMixture);
                
                mixture.Scale(TerraformingFunctions.GlobalAtmosphere.Volume.ToDouble() / atmosphere.Volume.ToDouble());

                var change = TerraformingFunctions.GasMixCompair(TerraformingFunctions.GlobalAtmosphere.GasMixture, mixture);
                TerraformingFunctions.ThisGlobalPrecise.UpdateGlobalAtmosphereChange(change);
            }
        }
    }

    [HarmonyPatch(typeof(WorldManager), "StartWorld")]
    public class WorldManagerStartWorldPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (NetworkManager.IsClient) 
                return;

            LightManager.SunPathTraceWorldAtmos = true;
            TerraformingFunctions.ThisGlobalPrecise = new GlobalAtmospherePrecise(Mathf.Abs(WorldSetting.Current.Gravity));

            // Removed the broken OnLoadMix wipe here. The engine handles the base atmosphere now.

            TerraformingFunctions.ReloadGlobalAtmosphere();
            var globalAtmo = TerraformingFunctions.GlobalAtmosphere;
            if (globalAtmo != null)
                AtmosphericsManager.AllAtmospheres.Add(globalAtmo);
            else
                ConsoleWindow.Print("Terraforming: Global Atmosphere is not valid");

            if (OrbitalSimulation.System != null)
            {
                var value = typeof(OrbitalSimulation).GetMethod("CalculateSolarIrradiance", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null)?.Invoke(OrbitalSimulation.System, null);
                if (value != null)
                {
                    Traverse.Create(typeof(OrbitalSimulation)).Property("SolarIrradiance").SetValue(value);
                }
            }

            ConsoleWindow.Print($"Terraforming: GlobalPrecise generated (Terraforming mod loaded on server)");
        }
    }

    [HarmonyPatch(typeof(NetworkClient), "ProcessJoinData")]
    public class NetworkClientProcessJoinDataPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!NetworkManager.IsClient) 
                return;

            LightManager.SunPathTraceWorldAtmos = true;
            TerraformingFunctions.ThisGlobalPrecise = new GlobalAtmospherePrecise(Mathf.Abs(WorldSetting.Current.Gravity));
            TerraformingFunctions.ReloadGlobalAtmosphere();
            ConsoleWindow.Print("GlobalPrecise generated (Terraforming mod loaded on client)");
        }
    }

    [HarmonyPatch(typeof(AtmosphereHelper), "IsValidForNetworkSend")]
    public class AtmosphereHelperIsValidForNetworkSendPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Atmosphere atmos, ref bool __result)
        {
            if (atmos != null && !atmos.BeingDestroyed && !atmos.IsNaN() && atmos == TerraformingFunctions.GlobalAtmosphere)
            {
                __result = (atmos.GasMixture.GasQuantitiesDirtied() != 0) || TerraformingFunctions.JoinInProgress;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(AtmosphericsManager), "SerialiseOnJoin")]
    public class AtmosphericsManagerSerialiseOnJoin
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            TerraformingFunctions.JoinInProgress = true;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            TerraformingFunctions.JoinInProgress = false;
        }
    }

    // 3. Override the engine's core weather/temperature calculations with Terraforming Math
    [HarmonyPatch(typeof(PlanetaryAtmosphereSimulation), "CacheTemperatureCurveOffsets")]
    public class PlanetaryAtmosphereSimulationTemperaturePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (TerraformingFunctions.ThisGlobalPrecise != null)
            {
                Atmosphere readOnlyGlobal = PlanetaryAtmosphereSimulation.ReadOnlyGlobal(new WorldGrid(0, 0, 0));
                if (readOnlyGlobal != null && !readOnlyGlobal.BeingDestroyed)
                {
                    float terraformTemp = TerraformingFunctions.GetTemperature(OrbitalSimulation.TimeOfDay, readOnlyGlobal.GasMixture);
                    PlanetaryAtmosphereSimulation.AggregateTemperature = new TemperatureKelvin(terraformTemp);
                }
            }
        }
    }

    // 4. Force the planet to use the Terraforming gas mixture every tick
    [HarmonyPatch(typeof(PlanetaryAtmosphereSimulation), "TickPlanetarySimulation")]
    public class PlanetaryAtmosphereSimulationTickPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!NetworkManager.IsClient && TerraformingFunctions.ThisGlobalPrecise != null)
            {
                Atmosphere readOnlyGlobal = PlanetaryAtmosphereSimulation.ReadOnlyGlobal(new WorldGrid(0, 0, 0));
                if (readOnlyGlobal != null && !readOnlyGlobal.BeingDestroyed)
                {
                    TerraformingFunctions.ThisGlobalPrecise.UpdateGlobalAtmosphere(PlanetaryAtmosphereSimulation.AggregateTemperature.ToFloat(), readOnlyGlobal);
                }
            }
        }
    }

    public class TerraformingFunctions
    {
        public static GlobalAtmospherePrecise ThisGlobalPrecise;

        [ThreadStatic]
        public static bool JoinInProgress = false;

        public static Atmosphere GlobalAtmosphere;

        public static void ReloadGlobalAtmosphere()
        {
            GlobalAtmosphere = AtmosphericsController.ReadonlyGlobalAtmosphere(new Grid3(0));
        }

        public static float GetTemperature(float timeOfDay, GasMixture gasMix)
        {
            if (ThisGlobalPrecise == null || !gasMix.IsValid) return 273.15f; 

            double solarIrradiance = 0;
            if (OrbitalSimulation.System != null)
            {
                solarIrradiance = OrbitalSimulation.SolarIrradiance;
            }

            if (double.IsNaN(solarIrradiance) || solarIrradiance < 0) solarIrradiance = 0;

            double rootIrridiance = Math.Sqrt(solarIrradiance);

            float temperatureBase = ThisGlobalPrecise.GetWorldBaseTemperature(rootIrridiance, gasMix);
            float temperatureDelta = ThisGlobalPrecise.GetWorldDeltaTemperature(temperatureBase, rootIrridiance, gasMix);
            float temp = temperatureBase + Mathf.Sin(timeOfDay * 2f * Mathf.PI - Mathf.PI / 4) * temperatureDelta / 2;
            
            if (float.IsNaN(temp)) return 273.15f; 
            
            return temp;
        }

        public static List<SpawnGas> UpdateWorldSetting(GasMixture globalGasMixture)
        {
            List<SpawnGas> currentSpawnGas = new List<SpawnGas>();
            foreach (GasType type in GlobalAtmospherePrecise.thermoGases)
            {
                float qty = globalGasMixture.GetMoleValue(type).Quantity.ToFloat();
                if (qty > 0)
                {
                    currentSpawnGas.Add(new SpawnGas(type, qty));
                }
            }
            return currentSpawnGas;
        }

        public static SimpleGasMixture GasMixCompair(GasMixture original1, GasMixture original2)
        {
            SimpleGasMixture result = new SimpleGasMixture();
            foreach (GasType type in SimpleGasMixture.BaseGases)
            {
                double num = original2.GetMoleValue(type).Quantity.ToDouble() - original1.GetMoleValue(type).Quantity.ToDouble();
                result.SetType(type, num);
            }
            return result;
        }

        private static XmlSerializer _atmoSerializer = null;
        public static XmlSerializer AtmoSerializer
        {
            get
            {
                if (_atmoSerializer != null)
                {
                    return _atmoSerializer;
                }

                _atmoSerializer = new XmlSerializer(typeof(TerraformingAtmosphere), XmlSaveLoad.ExtraTypes);
                return _atmoSerializer;
            }
        }
    }

    public class SimpleGasMixture
    {
        public static readonly GasType[] BaseGases = new GasType[]
        {
            GasType.Oxygen, GasType.Nitrogen, GasType.CarbonDioxide, GasType.Methane,
            GasType.Pollutant, GasType.Water, GasType.NitrousOxide, GasType.LiquidNitrogen,
            GasType.LiquidOxygen, GasType.LiquidMethane, GasType.Steam, GasType.LiquidCarbonDioxide,
            GasType.LiquidPollutant, GasType.LiquidNitrousOxide, GasType.Hydrogen, GasType.LiquidHydrogen,
            GasType.PollutedWater, GasType.Hydrazine, GasType.LiquidHydrazine, GasType.LiquidAlcohol,
            GasType.Helium, GasType.LiquidSodiumChloride, GasType.Silanol, GasType.LiquidSilanol,
            GasType.HydrochloricAcid, GasType.LiquidHydrochloricAcid, GasType.Ozone, GasType.LiquidOzone
        };

        public Dictionary<GasType, double> Moles = new Dictionary<GasType, double>();

        public SimpleGasMixture()
        {
            foreach (var type in BaseGases) Moles[type] = 0.0;
        }

        public SimpleGasMixture(GasMixture gasMixture) : this()
        {
            foreach (var type in BaseGases)
            {
                Moles[type] = gasMixture.GetMoleValue(type).Quantity.ToDouble();
            }
        }

        public void Reset()
        {
            var keys = new List<GasType>(Moles.Keys);
            foreach (var key in keys) Moles[key] = 0.0;
        }

        public void Scale(double scale)
        {
            var keys = new List<GasType>(Moles.Keys);
            foreach (var key in keys) Moles[key] *= scale;
        }

        public double Add(SimpleGasMixture gasMix)
        {
            double addedMoles = 0;
            foreach (var type in BaseGases)
            {
                Moles[type] += gasMix.Moles[type];
                addedMoles += Math.Abs(gasMix.Moles[type]); // Ensure vacuums trip the accumulator too
            }
            return addedMoles;
        }

        public void SetType(GasType gasType, double quantity)
        {
            if (Moles.ContainsKey(gasType)) Moles[gasType] = quantity;
        }

        public double GetType(GasType gasType)
        {
            return Moles.ContainsKey(gasType) ? Moles[gasType] : 0.0;
        }
    }

    public class GlobalAtmospherePrecise : SimpleGasMixture
    {
        public static GasType[] thermoGases = new GasType[]
        {
            GasType.Pollutant, 
            GasType.CarbonDioxide,
            GasType.Oxygen,
            GasType.Methane, 
            GasType.Nitrogen, 
            GasType.NitrousOxide, 
            GasType.Water, 
            GasType.LiquidPollutant, 
            GasType.LiquidCarbonDioxide,
            GasType.LiquidOxygen,
            GasType.LiquidMethane, 
            GasType.LiquidNitrogen, 
            GasType.LiquidNitrousOxide, 
            GasType.Steam, 
            GasType.PollutedWater
        };
        
        public static double worldSize;
        public static double[] baseFactors = new double[]
        {
            10.651413866149, 1.00348304229291, 0.202490458429832, 8.55023708508486,
            -0.320563285816776, -1.288345881, 0, 10.651413866149, 1.00348304229291,
            0.202490458429832, 8.55023708508486, -0.320563285816776, -1.288345881,
            -3.14159265359, 0
        };
        
        public static double[] deltaFactors = new double[]
        {
            1.03921006683661, -0.014557418735896, -0.0250754001733472, 19.5280403386664,
            0.314249023692835, -0.987064019, -3.14159265359, 1.03921006683661,
            -0.014557418735896, -0.0250754001733472, 19.5280403386664, 0.314249023692835,
            -0.987064019, -1.14159265359, 0
        };
        
        public static double baseSolarScale = 8.99241762372131;
        public static double deltaSolarScale = 3.21847718465672;
        public static double baseTQ = -0.0128394753903387;
        public static double deltaTQ = 0.0948443729002513;
        public static double deltaPa = -0.000838897191503017;
        public static double pressureGravityFactorInPa = 180 * 1000f;

        public GlobalAtmospherePrecise(float gravity)
        {
            // CHANGED FOR TESTING: Reduced planet size mathematically by 100x
            // Change back to `7 * Math.Pow(10, 6)` for the real workshop release
            worldSize = 70000; 
            worldScale = 1 / worldSize;
            this.gravity = Mathf.Abs(gravity);
            rootGravity = Mathf.Sqrt(this.gravity);
        }

        private float gravity;
        public float rootGravity;
        public double worldScale;

        private SimpleGasMixture GasMixAccumulater = new SimpleGasMixture();
        private double GasMixAccumulatorMoles = 0;

        public void UpdateGlobalAtmosphereChange(SimpleGasMixture change)
        {
            lock (this)
            {
                GasMixAccumulatorMoles += GasMixAccumulater.Add(change);
                if (Math.Abs(GasMixAccumulatorMoles) <= 1)
                    return;

                GasMixAccumulater.Scale(worldScale);
                Add(GasMixAccumulater);
                GasMixAccumulater.Reset();
                GasMixAccumulatorMoles = 0;
            }
        }

        public void UpdateGlobalAtmosphere(float temp, Atmosphere GlobalAtmosphere)
        {
            GlobalAtmosphere.GasMixture.SetReadOnly(false);
            if (!NetworkManager.IsClient) 
            {
                // NO MORE WIPING THE PLANET!
                // We just ADD our terraformed deltas on top of the engine's base planetary grid.
                foreach (var type in BaseGases)
                {
                    double extraMoles = Moles[type];
                    if (extraMoles > 0)
                    {
                        GlobalAtmosphere.GasMixture.Add(new Mole(type, new MoleQuantity(extraMoles), MoleEnergy.Zero));
                    }
                    else if (extraMoles < 0)
                    {
                        GlobalAtmosphere.GasMixture.Remove(type, new MoleQuantity(Math.Abs(extraMoles)));
                    }
                }
            }
            
            float num = temp * GlobalAtmosphere.GasMixture.HeatCapacity.ToFloat();
            if (!float.IsNaN(temp))
            {
                GlobalAtmosphere.GasMixture.TotalEnergy = new MoleEnergy((double)num);
            }
            
            if (!NetworkManager.IsClient && GlobalAtmosphere.PressureGassesAndLiquidsInPa > rootGravity * pressureGravityFactorInPa)
            {
                float num1 = (float)(rootGravity * pressureGravityFactorInPa / GlobalAtmosphere.PressureGassesAndLiquidsInPa);
                Scale(num1);
            }
            
            GlobalAtmosphere.GasMixture.SetReadOnly(true);
            GlobalAtmosphere.UpdateCache();
        }

        public float GetWorldBaseTemperature(double rootIrridiance, GasMixture globalMix)
        {
            double temperature = 0;
            temperature += baseSolarScale * rootIrridiance;
            for (int i = 0; i < thermoGases.Length; i++)
            {
                if (baseFactors[i] != 0)
                {
                    temperature += baseFactors[i] * Math.Sqrt(globalMix.GetGasTypeRatio(thermoGases[i])) * globalMix.GetMoleValue(thermoGases[i]).Quantity.ToDouble();
                }
            }
            temperature += baseTQ * globalMix.GetTotalMolesGassesAndLiquids.ToDouble();
            return (float)Math.Max(temperature, 0);
        }

        public float GetWorldDeltaTemperature(float baseTemp, double rootIrridiance, GasMixture globalMix)
        {
            double temperature = 0;
            temperature += deltaSolarScale * rootIrridiance;
            for (int i = 0; i < thermoGases.Length; i++)
            {
                if (deltaFactors[i] != 0)
                {
                    temperature += deltaFactors[i] * Math.Sqrt(globalMix.GetGasTypeRatio(thermoGases[i])) * globalMix.GetMoleValue(thermoGases[i]).Quantity.ToDouble();
                }
            }
            temperature += deltaTQ * globalMix.GetTotalMolesGassesAndLiquids.ToDouble();
            temperature += deltaPa * globalMix.GetTotalMolesGassesAndLiquids.ToDouble() * baseTemp;
            return (float)Math.Max(temperature, 0);
        }
    }
}