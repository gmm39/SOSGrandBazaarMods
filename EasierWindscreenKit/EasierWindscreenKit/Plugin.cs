using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using Il2CppSystem;

namespace EasierWindscreenKit;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<int> woodStoneCount;
    private static ConfigEntry<int> time;

    public override void Load()
    {
        // Plugin startup logic
        woodStoneCount = Config.Bind("General", "Wood_Stone_Count", 5,
            "Enter the amount of wood and stone it costs to build a windscreen kit.");
        time = Config.Bind("General", "time", 60,
            "Enter the amount of time it takes to build a windscreen kit.");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(WindscreenPatch));
    }
    
    private static class WindscreenPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var windCraftData = MasterDataManager.Instance.WindmillCraftingMasterData;

            var result = windCraftData.Find((Predicate<WindmillCraftingMasterData>)
                (x => x.CraftingItemId == 121900));
            
            result.RequiredItemId[0] = 110100;
            result.RequiredItemStack[1] = woodStoneCount.Value;
            
            result.RequiredItemId[1] = 110000;
            result.RequiredItemStack[2] = woodStoneCount.Value;
            
            result.Time = time.Value;
        }
    }
}