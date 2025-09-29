using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace BenchSpeedup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(BenchSpeedup));
    }
    
    private static class BenchSpeedup
    {
        private static float _timescale;
        
        [HarmonyPatch(typeof(HumanActionSit), "Action")]
        [HarmonyPostfix]
        private static void BeginSit()
        {
            if (FieldManager.Instance.CurrentFieldMasterData.FieldId != FieldMasterId.Field_1) return;
            
            _timescale = DateManager.Instance.TimeScale;

            DateManager.Instance.TimeScale = 600f;
        }
        
        [HarmonyPatch(typeof(FieldChair2), "GetSittingPlayerFieldChair")]
        [HarmonyPostfix]
        private static void EndSit(FieldChair __result)
        {
            if (FieldManager.Instance.CurrentFieldMasterData.FieldId != FieldMasterId.Field_1) return;
            
            DateManager.Instance.TimeScale = _timescale;
        }
    }
}