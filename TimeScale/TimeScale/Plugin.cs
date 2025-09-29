using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace TimeScale;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private static ConfigEntry<float> TimeScaleEntry;

    public override void Load()
    {
        // Plugin startup logic
        TimeScaleEntry = Config.Bind("General",
            "TimeScale",
            30f,
            "Scalar for game time. GameDefault: 60 | HalfSpeed: 30");

        var Log = this.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Log.LogInfo($"TimeScale: {TimeScaleEntry.Value}");

        Harmony.CreateAndPatchAll(typeof(DateManagerPatch));
    }

    private static class DateManagerPatch
    {
        [HarmonyPatch(typeof(DateManager), "OnStartGame")]
        [HarmonyPostfix]
        private static void Update_Postfix(DateManager __instance)
        {
            __instance.TimeScale = TimeScaleEntry.Value;
        }
    }
}