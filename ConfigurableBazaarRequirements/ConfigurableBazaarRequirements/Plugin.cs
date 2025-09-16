using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace ConfigurableBazaarRequirements;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private static new ManualLogSource Log;
    private static ConfigEntry<int> Rank2;
    private static ConfigEntry<int> Rank3;
    private static ConfigEntry<int> Rank4;
    private static ConfigEntry<int> Rank5;
    private static ConfigEntry<int> Rank6;
    private static ConfigEntry<int> Rank7;

    public override void Load()
    {
        Log = base.Log;
        LoadConfig();
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(Plugin.BazaarRequirementPatch), null);
    }

    private void LoadConfig()
    {
        Rank2 = Config.Bind("General", "Rank2", 10000,
            "Rank 2 requirement. Game Default: 1,000g");
        Rank3 = Config.Bind("General", "Rank3", 100000,
            "Rank 3 requirement. Game Default: 50,000g");
        Rank4 = Config.Bind("General", "Rank4", 400000,
            "Rank 4 requirement. Game Default: 200,000g");
        Rank5 = Config.Bind("General", "Rank5", 1000000,
            "Rank 5 requirement. Game Default: 500,000g");
        Rank6 = Config.Bind("General", "Rank6", 2000000,
            "Rank 6 requirement. Game Default: 1,000,000g");
        Rank7 = Config.Bind("General", "Rank7", 4000000,
            "Rank 7 requirement. Game Default: 2,000,000g");
    }
    
    private static class BazaarRequirementPatch
    {
        [HarmonyPatch(typeof(BazaarManager), "Initialize")]
        [HarmonyPrefix]
        private static void Update_Prefix()
        {
            var BDPM = ManagedSingleton<MasterDataManager>.Instance.BazaarDevelopParamMaster.list;

            BDPM[0].bazaarGoalTotalPrice = Rank2.Value;
            BDPM[1].bazaarGoalTotalPrice = Rank3.Value;
            BDPM[2].bazaarGoalTotalPrice = Rank4.Value;
            BDPM[3].bazaarGoalTotalPrice = Rank5.Value;
            BDPM[4].bazaarGoalTotalPrice = Rank6.Value;
            BDPM[5].bazaarGoalTotalPrice = Rank7.Value;
        }
    }
}