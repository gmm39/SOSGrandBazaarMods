using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;

namespace WindmillRecipeEdits;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(WindmillPatch));
    }
    
    private static class WindmillPatch
    {
        private static Dictionary<uint, int> windmillIds = new() { {120000, 1}, {120010, 3}, {120020, 2} };

        [HarmonyPatch(typeof(FieldManager), "ChangeField",
            new Type[]
            {
                typeof(FieldMasterId), typeof(string), typeof(bool), typeof(Il2CppSystem.Action),
                typeof(Il2CppSystem.Action<uint>), typeof(bool)
            })]
        [HarmonyPostfix]
        private static void Postfix(FieldManager __instance, FieldMasterId fieldId)
        {
            if (!windmillIds.ContainsKey((uint)fieldId)) return;

            var windData = MasterDataManager.Instance.WindmillCraftingMasterData;

            foreach (var item in windData)
            {
                if(item.CraftingItemId != 103000) item.WindmillType = windmillIds.GetValueSafe((uint)fieldId); // No fertilizers
            }

            // Sort List
            var sortMe = new List<WindmillCraftingMasterData>(windData.ToArray())
                .OrderBy(x => x.CraftCategory).ThenBy(x => x.CraftingItemId);
            windData.Clear();

            foreach (var item in sortMe)
            {
                windData.Add(item);
            }
        }
    }
}