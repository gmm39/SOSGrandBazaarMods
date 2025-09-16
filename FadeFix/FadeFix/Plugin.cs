using System.Numerics;
using BepInEx;
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
    
    public override void Load()
    {
        // Plugin startup logic
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
            __instance.fadeParent.localScale = new Vector3(1.5f, 1.5f, 1.0f);
        }
    }
}