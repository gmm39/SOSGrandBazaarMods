using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace NameChanger;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private static new ManualLogSource Log;
    private static ConfigEntry<string> FarmNameChange;
    private static ConfigEntry<string> PlayerNameChange;
    private static ConfigEntry<int> PlayerPronounChange;

    public override void Load()
    {
        
        // Plugin startup logic
        FarmNameChange = Config.Bind("General",
            "FarmNameChange",
            "DefaultName",
            "Enter the name for your farm. MAX LENGTH 12 characters!");
        PlayerNameChange = Config.Bind("General",
            "PlayerNameChange",
            "DefaultName",
            "Enter the name for your character. MAX LENGTH 8 characters!");
        PlayerPronounChange = Config.Bind("General",
            "PlayerPronounChange",
            2,
            "Enter the number for your preferred pronouns for your character:" + 
            "\n0 = he/him" +
            "\n1 = she/her" +
            "\n2 = they/them");

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(NamePatch));
    }

    private static class NamePatch
    {
        [HarmonyPatch(typeof(UserInfo), "ToSaveData")]
        [HarmonyPrefix]
        private static void Prefix(UserInfo __instance)
        {
            __instance.FarmName = FarmNameChange.Value;
            __instance.PlayerName = PlayerNameChange.Value;
            __instance.PlayerGender = PlayerPronounChange.Value;
        }
    }
}