using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace BetterFreshness;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    private static ConfigEntry<int> SpringFresh;
    private static ConfigEntry<int> SummerFresh;
    private static ConfigEntry<int> AutumnFresh;
    private static ConfigEntry<int> WinterFresh;
    
    private static ConfigEntry<int> SunnyFresh;
    private static ConfigEntry<int> RainyFresh;
    private static ConfigEntry<int> SnowyFresh;
    private static ConfigEntry<int> TyphoonFresh;
    private static ConfigEntry<int> HeavySnowFresh;
    private static ConfigEntry<int> CloudyFresh;
    private static ConfigEntry<int> HeavyRainFresh;
    
    private static ConfigEntry<int> StoreFresh0;
    private static ConfigEntry<int> StoreFresh1;
    private static ConfigEntry<int> StoreFresh2;
    private static ConfigEntry<int> StoreFresh3;
    private static ConfigEntry<int> StoreFresh4;

    public override void Load()
    {
        // Plugin startup logic
        SpringFresh = Config.Bind("-----01 SEASON-----", "Spring_Freshness", 1,
            "Freshness reduction for spring. GameDefault: 2");
        SummerFresh = Config.Bind("-----01 SEASON-----", "Summer_Freshness", 2,
            "Freshness reduction for summer. GameDefault: 3");
        AutumnFresh = Config.Bind("-----01 SEASON-----", "Autumn_Freshness", 1,
            "Freshness reduction for autumn. GameDefault: 2");
        WinterFresh = Config.Bind("-----01 SEASON-----", "Winter_Freshness", 1,
            "Freshness reduction for winter. GameDefault: 1");
        
        SunnyFresh = Config.Bind("-----02 WEATHER-----", "Sunny_Freshness", 1,
            "Freshness reduction for sunny weather. GameDefault: 1");
        RainyFresh = Config.Bind("-----02 WEATHER-----", "Rainy_Freshness", 1,
            "Freshness reduction for rainy weather. GameDefault: 2");
        SnowyFresh = Config.Bind("-----02 WEATHER-----", "Snowy_Freshness", 0,
            "Freshness reduction for snowy weather. GameDefault: 0");
        TyphoonFresh = Config.Bind("-----02 WEATHER-----", "Typhoon_Freshness", 2,
            "Freshness reduction for typhoon weather. GameDefault: 3");
        HeavySnowFresh = Config.Bind("-----02 WEATHER-----", "Heavy_Snow_Freshness", 0,
            "Freshness reduction for heavy snow weather. GameDefault: 0");
        CloudyFresh = Config.Bind("-----02 WEATHER-----", "Cloudy_Freshness", 1,
            "Freshness reduction for cloudy weather. GameDefault: 1");
        HeavyRainFresh = Config.Bind("-----02 WEATHER-----", "Heavy_Rain_Freshness", 1,
            "Freshness reduction for heavy rain weather. GameDefault: 2");
        
        StoreFresh0 = Config.Bind("-----03 STORAGE UPGRADES-----", "Freshness_Upgrade_0", 5,
            "Freshness reduction for storage upgrade 0. GameDefault: 10");
        StoreFresh1 = Config.Bind("-----03 STORAGE UPGRADES-----", "Freshness_Upgrade_1", 2,
            "Freshness reduction for storage upgrade 1. GameDefault: 4");
        StoreFresh2 = Config.Bind("-----03 STORAGE UPGRADES-----", "Freshness_Upgrade_2", 1,
            "Freshness reduction for storage upgrade 2. GameDefault: 3");
        StoreFresh3 = Config.Bind("-----03 STORAGE UPGRADES-----", "Freshness_Upgrade_3", 1,
            "Freshness reduction for storage upgrade 3. GameDefault: 2");
        StoreFresh4 = Config.Bind("-----03 STORAGE UPGRADES-----", "Freshness_Upgrade_4", 1,
            "Freshness reduction for storage upgrade 4. GameDefault: 1");
        
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
            var freshSet = SettingAssetManager.Instance.FreshnessSetting;

            freshSet.SeasonReduceValue[0] = SpringFresh.Value;
            freshSet.SeasonReduceValue[1] = SummerFresh.Value;
            freshSet.SeasonReduceValue[2] = AutumnFresh.Value;
            freshSet.SeasonReduceValue[3] = WinterFresh.Value;
            
            freshSet.WeatherReduceValue[0] = SunnyFresh.Value;
            freshSet.WeatherReduceValue[1] = RainyFresh.Value;
            freshSet.WeatherReduceValue[2] = SnowyFresh.Value;
            freshSet.WeatherReduceValue[3] = TyphoonFresh.Value;
            freshSet.WeatherReduceValue[4] = HeavySnowFresh.Value;
            freshSet.WeatherReduceValue[5] = CloudyFresh.Value;
            freshSet.WeatherReduceValue[6] = HeavyRainFresh.Value;
            
            freshSet.HouseStorageReduceValue[0] = StoreFresh4.Value;
            freshSet.HouseStorageReduceValue[1] = StoreFresh3.Value;
            freshSet.HouseStorageReduceValue[2] = StoreFresh2.Value;
            freshSet.HouseStorageReduceValue[3] = StoreFresh1.Value;
            freshSet.HouseStorageReduceValue[4] = StoreFresh0.Value;
        }
    }
}