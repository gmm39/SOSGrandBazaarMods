using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;
using Ease = DG.Tweening.Ease;

namespace SkipSplash;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(SplashPatch));
    }
    
    private static class SplashPatch
    {
        [HarmonyPatch(typeof(CompanyLogoAnimation), "EnterPhase")]
        [HarmonyPrefix]
        private static void Prefix(CompanyLogoAnimation __instance, ref CompanyLogoAnimation.LogoAnimationPhase phase)
        {
            phase = CompanyLogoAnimation.LogoAnimationPhase.FadeOut;
            __instance.animManager.ChangeBackwardFade(0,0, 0, Ease.Linear);
            __instance.animManager.ChangeForwardFade(0,0, 0, Ease.Linear);
        }
        
        [HarmonyPatch(typeof(CompanyLogo), "Update")]
        [HarmonyPostfix]
        private static void Postfix(CompanyLogo __instance)
        {
            __instance.fadeTimer = 0.0f;
            __instance.bg.SetAlpha(0.0f);
        }
    }
}