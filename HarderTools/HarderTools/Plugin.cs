using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace HarderTools;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<float> copperMineralMulti;
    private static ConfigEntry<float> copperMaterialMulti;
    private static ConfigEntry<float> silverMineralMulti;
    private static ConfigEntry<float> silverMaterialMulti;
    private static ConfigEntry<float> goldenMineralMulti;
    private static ConfigEntry<float> goldenMaterialMulti;
    private static ConfigEntry<float> orichaMineralMulti;
    private static ConfigEntry<float> orichaMaterialMulti;
    private static ConfigEntry<float> ultimaMineralMulti;
    private static ConfigEntry<float> ultimaMaterialMulti;
    

    public override void Load()
    {
        // Plugin startup logic
        copperMineralMulti = Config.Bind("General", "Copper_Tools_Mineral_Multiplier", 1.0f,
            "Multiples the metal (iron, copper, etc.) components by the given value.");
        copperMaterialMulti = Config.Bind("General", "Copper_Tools_Material_Multiplier", 1.0f,
            "Multiples the lumbar and stone components by the given value.");
        silverMineralMulti = Config.Bind("General", "Silver_Tools_Mineral_Multiplier", 1.0f,
            "Multiples the metal (iron, copper, etc.) components by the given value.");
        silverMaterialMulti = Config.Bind("General", "Silver_Tools_Material_Multiplier", 1.0f,
            "Multiples the lumbar and stone components by the given value.");
        goldenMineralMulti = Config.Bind("General", "Golden_Tools_Mineral_Multiplier", 1.0f,
            "Multiples the metal (iron, copper, etc.) components by the given value.");
        goldenMaterialMulti = Config.Bind("General", "Golden_Tools_Material_Multiplier", 1.0f,
            "Multiples the lumbar and stone components by the given value.");
        orichaMineralMulti = Config.Bind("General", "Orichalcum_Tools_Mineral_Multiplier", 1.0f,
            "Multiples the metal (iron, copper, etc.) components by the given value.");
        orichaMaterialMulti = Config.Bind("General", "Orichalcum_Tools_Material_Multiplier", 1.0f,
            "Multiples the lumbar and stone components by the given value.");
        ultimaMineralMulti = Config.Bind("General", "Ultimate_Tools_Mineral_Multiplier", 1.0f,
            "Multiples the metal (iron, copper, etc.) components by the given value.");
        ultimaMaterialMulti = Config.Bind("General", "Ultimate_Tools_Material_Multiplier", 1.0f,
            "Multiples the lumbar and stone components by the given value.");

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(ToolPatch));
    }

    private static class ToolPatch
    {
        private static HashSet<int> copperTools = [101, 202, 203, 502, 503, 602, 603, 702, 703];
        private static HashSet<int> silverTools = [102, 204, 205, 504, 505, 604, 605, 704, 705];
        private static HashSet<int> goldenTools = [103, 206, 207, 506, 507, 606, 607, 706, 707];
        private static HashSet<int> orichaTools = [104, 208, 508, 608, 708];
        private static HashSet<int> ultimaTools = [105, 210, 510, 610, 710];
        private static HashSet<int> material = [112000, 112002, 112004, 112001, 112003, 112005];
        private static HashSet<int> minerals = [112100, 112101, 112102, 112103, 112104, 112105, 112106];

        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            ToolChange(copperTools, copperMineralMulti.Value, copperMaterialMulti.Value);
            ToolChange(silverTools, silverMineralMulti.Value, silverMaterialMulti.Value);
            ToolChange(goldenTools, goldenMineralMulti.Value, goldenMaterialMulti.Value);
            ToolChange(orichaTools, orichaMineralMulti.Value, orichaMaterialMulti.Value);
            ToolChange(ultimaTools, ultimaMineralMulti.Value, ultimaMaterialMulti.Value);
        }

        private static void ToolChange(HashSet<int> tools, float mineralMulti, float materialMulti)
        {
            var windCraftData = MasterDataManager.Instance.WindmillCraftingMasterData;
            
            foreach (var item in windCraftData) if (tools.Contains((int)item.CraftingItemId))
            {
                for (var i = 0; i < item.RequiredItemStack.Count; i++)
                {
                    if (minerals.Contains((int)item.RequiredItemId[i])) item.RequiredItemStack[i] = (int)Math.Round(item.RequiredItemStack[i] * mineralMulti);
                    if (material.Contains((int)item.RequiredItemId[i])) item.RequiredItemStack[i] = (int)Math.Round(item.RequiredItemStack[i] * materialMulti);
                }
            }
        }
    }
}