using System;
using System.Collections.Generic;
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
    private static ConfigEntry<bool> HarderDonations;

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
        HarderDonations = Config.Bind("General", "Harder_Donations", false,
            "Makes post game donations more difficult.");
    }
    
    private static class BazaarRequirementPatch
    {
        private static List<int> postRank =
        [
            40000, 50000, 60000, 70000, 80000, 90000, 100000, 110000, 120000, 150000, 180000, 220000, 260000, 300000,
            340000, 390000, 440000, 490000, 550000, 610000, 670000, 730000, 800000, 870000, 940000, 1000000, 1100000,
            1200000, 1300000, 1400000, 1500000, 1600000, 1700000, 1800000, 1900000, 2000000, 2100000, 2200000, 2300000,
            2400000, 2500000, 2600000, 2700000, 2800000, 2900000, 3100000, 3200000, 3300000, 3500000, 3600000, 3700000,
            3900000, 4000000, 4200000, 4300000, 4400000, 4600000, 4700000, 4900000, 5000000, 5200000, 5400000, 5500000,
            5700000, 5800000, 6000000, 6200000, 6300000, 6500000, 6600000, 6800000, 7000000, 7200000, 7300000, 7500000,
            7700000, 7800000, 8000000, 8200000, 8400000, 8500000, 8700000, 8900000, 9100000, 9200000, 9400000, 9600000,
            9800000, 10000000, 10200000, 10400000, 11000000
        ];
        
        [HarmonyPatch(typeof(BazaarManager), "Initialize")]
        [HarmonyPrefix]
        private static void Update_Prefix()
        {
            var BDPM = MasterDataManager.Instance.BazaarDevelopParamMaster.list;

            BDPM[0].bazaarGoalTotalPrice = Rank2.Value;
            BDPM[1].bazaarGoalTotalPrice = Rank3.Value;
            BDPM[2].bazaarGoalTotalPrice = Rank4.Value;
            BDPM[3].bazaarGoalTotalPrice = Rank5.Value;
            BDPM[4].bazaarGoalTotalPrice = Rank6.Value;
            BDPM[5].bazaarGoalTotalPrice = Rank7.Value;

            if (!HarderDonations.Value) return;
            
            var BECPM = MasterDataManager.Instance.BazaarEndContentsParamMasterData;

            for (var i = 0; i < BECPM.Count; i++)
            {
                BECPM[i].donationPrice = postRank[i];
            }
        }
    }
}