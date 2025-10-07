using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using Il2CppSystem;

namespace FriendshipTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<string> FriendshipCurve;
    private static ConfigEntry<float> FriendshipCurveMulti;

    public override void Load()
    {
        // Plugin startup logic
        FriendshipCurve = Config.Bind("Friendship", "Friendship_Curve", "original",
            "Acceptable Values: original, custom" +
            "\nOriginal: Games default friendship level values" +
            "\nCustom: Slightly harder levels 1-7 with easier 8-10");
        FriendshipCurveMulti = Config.Bind("Friendship", "Friendship_Curve_Multi", 1.0f,
            "Multiplies the friendship curve by a given amount." +
            "1.0 = 100%, 0.5 = 50%, 2.0 = 200%, etc.");
        
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(FriendPatch));
    }
    
    private static class FriendPatch
    {
        private static List<int> customLevels = [0, 1700, 4500, 7400, 12000, 16500, 22300, 27500, 32200, 35900, 39000];
        
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPrefix]
        private static void Prefix()
        {
            FriendLevels();
        }

        private static void FriendLevels()
        {
            var likeLevels = SettingAssetManager.Instance.LikeabilitySetting.LevelList;

            switch (FriendshipCurve.Value.ToLower())
            {
                case "original":
                    SetLevels(likeLevels);
                    break;
                case "custom":
                    SetLevels(customLevels);
                    break;
                default:
                    Log.LogError($"Incorrect config value for Friendship_Curve: {FriendshipCurve.Value}");
                    break;
            }
        }

        private static void SetLevels(List<int> newValues)
        {
            var likeLevels = SettingAssetManager.Instance.LikeabilitySetting.LevelList;

            for (var i = 0; i < likeLevels.Count; i++)
            {
                likeLevels[i] = (int)(newValues[i] * FriendshipCurveMulti.Value);
            }
        }

        private static void SetLevels(Il2CppSystem.Collections.Generic.List<int> newValues)
        {
            var newList = new List<int>();
            foreach(var value in newValues) newList.Add(value);
            
            SetLevels(newList);
        }
    }
}