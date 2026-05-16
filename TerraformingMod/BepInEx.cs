using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;

namespace TerraformingMod
{
    #region BepInEx
    [BepInEx.BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class TerraformingMod : BepInEx.BaseUnityPlugin
    {
        public const string pluginGuid = "net.elmo.stationeers.Terraforming";
        public const string pluginName = "Terraforming Mod";
        public const string pluginVersion = "0.24";
        
        public static ConfigEntry<bool> DisableOutdoorCondensationConfig;
        public static void Log(string line)
        {
            Debug.Log("[" + pluginName + "]: " + line);
        }
        void Awake()
        {
            
            DisableOutdoorCondensationConfig = Config.Bind(
                "Performance Options",                  // The category tab it will appear under
                "Disable Outdoor Condensation",         // The name of the setting in the UI
                false,                                  // The default value
                "True = Max performance (no ice drops). False = Normal snow allowed but prevents massive terraforming server crashes." // The UI tooltip
            );
            
            try
            {
                var harmony = new Harmony(pluginGuid);
                harmony.PatchAll();
                Log("Patch succeeded");
            }
            catch (Exception e)
            {
                Log("Patch Failed");
                Log(e.ToString());
            }
        }
    }
    #endregion
}
