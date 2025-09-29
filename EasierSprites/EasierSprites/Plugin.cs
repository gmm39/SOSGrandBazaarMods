using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace EasierSprites;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<string> ExpSetting;

    public override void Load()
    {
        // Plugin startup logic
        ExpSetting = Config.Bind("General", "Exp_Setting", "easy",
            "Acceptable Values: easy, easier, easiest, hard, harder, hardest" +
            "\nEasy: 75% total exp of original" +
            "\nEasier: 50% total exp of original" +
            "\nEasiest: 33% total exp of original" +
            "\nHard: 125% total exp of original" +
            "\nHarder: 150% total exp of original" +
            "\nHardest: 166% total exp of original");

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(SpritePatch));
    }

    private static class SpritePatch
    {
        private static float Base = 0.00605f;
        private static float Baser = 0.0121f;
        private static float Basest = 0.01613f;
        
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var coroData = MasterDataManager.Instance.CoroMissionRankMaster.list;
            float multiplier;

            switch (ExpSetting.Value.ToLower())
            {
                case "easy":
                    multiplier = Base;
                    break;
                case "easier":
                    multiplier = Baser;
                    break;
                case "easiest":
                    multiplier = Basest;
                    break;
                case "hard":
                    multiplier = -Base;
                    break;
                case "harder":
                    multiplier = -Baser;
                    break;
                case "hardest":
                    multiplier = -Basest;
                    break;
                default:
                    Log.LogError("Incorrect config value for Exp_Setting");
                    return;
            }
            
            foreach (var item in coroData)
            {
                item.NextExp = (uint)Math.Ceiling(item.NextExp * (1 - (item.Rank + 1) * multiplier));
            }
        }
    }
}