using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using UnityEngine.Playables;

namespace EasierTea;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<bool> isAllTea;
    private static HashSet<int> fruitTeas = [..Enumerable.Range(107606, 18)];
    private static HashSet<int> allTeas = [..Enumerable.Range(107600, 28).Where(x => x != 107605)];
    private static HashSet<int> teaLeaves = [..Enumerable.Range(105303, 3)];

    public override void Load()
    {
        // Plugin startup logic
        isAllTea = Config.Bind("General", "All_Teas", false,
            "True to change all tea. False to change only fruit teas.");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(TestPatch1));
    }
    
    private static class TestPatch1
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var windCraftData = MasterDataManager.Instance.WindmillCraftingMasterData;
            var idList = isAllTea.Value ? allTeas : fruitTeas;
            
            foreach (var item in windCraftData) if (idList.Contains((int)item.CraftingItemId))
            {
                for (var i = 0; i < item.RequiredItemId.Count; i++) if (teaLeaves.Contains((int)item.RequiredItemId[i]))
                {
                    item.RequiredItemId[i] = 1000152;
                    item.RequiredItemType[i] = RequiredItemType.Group;
                }
            }
        }
    }
}