using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace StackSizeTweak;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    private static ConfigEntry<int> StackSize;

    public override void Load()
    {
        // Plugin startup logic
        StackSize = Config.Bind("ItemSettings", "StackSize", 99,
            "Enter max stack size. Over 9999 is untested!");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(StackPatch1));
    }

    private static class StackPatch1
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            foreach (var item in ManagedSingleton<MasterDataManager>.Instance.ItemMasterData) 
                if (item.StackSize != 1) item.StackSize = StackSize.Value;
        }
    }
}