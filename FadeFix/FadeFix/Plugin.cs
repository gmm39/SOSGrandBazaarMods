using System.Numerics;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;
using Vector3 = UnityEngine.Vector3;

namespace FadeFix;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<int> Horizontal;
    private static ConfigEntry<int> Vertical;
    
    public override void Load()
    {
        // Plugin startup logic
        Horizontal = Config.Bind("General", "Horizontal", 16, 
            "Enter the horizontal portion of your aspect ratio." +
            "\nExample: For 16:9, enter 16.");
        Vertical = Config.Bind("General", "Vertical", 9, 
            "Enter the vertical portion of your aspect ratio." +
            "\nExample: For 16:9, enter 9.");
            
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        Harmony.CreateAndPatchAll(typeof(Plugin.FadeManagerPatch), null);
    }
    
    private static class FadeManagerPatch
    {
        [HarmonyPatch(typeof(FadeManager), "Awake")]
        [HarmonyPrefix]
        private static void Update_Prefix(FadeManager __instance)
        {
            var horizontal = Horizontal.Value / 16f + 0.05f;
            var vertical = Vertical.Value / 9f + 0.05f;
            __instance.fadeParent.localScale = new Vector3(horizontal, vertical, 1.0f);
        }
    }
}