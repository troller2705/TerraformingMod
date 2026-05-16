using System.Xml.Serialization;
using System.Collections.Generic;
using System;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects;

namespace TerraformingMod
{
    // We recreate the missing save class here
    public class GasMixSaveData
    {
        public Dictionary<string, float> Moles = new Dictionary<string, float>();

        public GasMixSaveData() { }

        public GasMixSaveData(GasMixture mix)
        {
            foreach (var type in SimpleGasMixture.BaseGases)
            {
                Moles[type.ToString()] = mix.GetMoleValue(type).Quantity.ToFloat();
            }
        }

        public GasMixture Apply()
        {
            GasMixture mix = GasMixtureHelper.Create();
            foreach (var kvp in Moles)
            {
                if (Enum.TryParse(kvp.Key, out Chemistry.GasType type))
                {
                    mix.Add(new Mole(type, new MoleQuantity((double)kvp.Value), MoleEnergy.Zero));
                }
            }
            return mix;
        }
    }

    [XmlInclude(typeof(ThingSaveData))]
    [XmlRoot("TerraformingAtmosphere")]
    public class TerraformingAtmosphere
    {
        [XmlElement]
        public GasMixSaveData GasMix = null;
    }
}