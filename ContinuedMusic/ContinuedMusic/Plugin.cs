using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono.Data;
using BokuMono.Sound;
using HarmonyLib;

namespace ContinuedMusic;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        Harmony.CreateAndPatchAll(typeof(MusicPatch));
    }
    
    private static class MusicPatch
    {
        private static HashSet<uint> ignoreIds = [20301, 20302, 20101];
        
        [HarmonyPatch(typeof(SoundBgmController), "PlayBgm", typeof(BgmMasterData), typeof(float), typeof(float), typeof(float))]
        [HarmonyPrefix]
        private static bool Prefix(SoundBgmController __instance, BgmMasterData bgmData)
        {
            if (__instance.CurrentBgmId == 60101)
            {
                return true;
            }

            return !ignoreIds.Contains(bgmData.Id);
        }
    }
}
