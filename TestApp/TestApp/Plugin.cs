using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;
using UnityEngine;

namespace TestApp;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    
    // Item Tweaks
    private static ConfigEntry<int> StackSize;
    
    // Windmill Buff Tweaks
    private static ConfigEntry<float> WindBuffNone;
    private static ConfigEntry<float> WindBuffSlight;
    private static ConfigEntry<float> WindBuffStrong;
    private static ConfigEntry<float> WindBuffTyphoon;
    
    // Typhoon Damage Tweaks
    private static ConfigEntry<int> BeeTyphoonDam;
    private static ConfigEntry<int> MushTyphoonDam;
    private static ConfigEntry<int> CropTyphoonDam;
    
    // Movement Tweaks
    private static ConfigEntry<float> HorseMaxSpeedBoost;
    private static ConfigEntry<float> HorseAccelerationBoost;
    private static ConfigEntry<float> HorseTurnSpeedBoost;
    
    // Greeting Tweaks
    private static ConfigEntry<float> GreetRange;
    private static ConfigEntry<float> GreetSearchWaitTime;
    private static ConfigEntry<float> GreetHorseSearchWaitTime;
    private static ConfigEntry<float> GreetUIShowDelay;
    private static ConfigEntry<float> GreetReactionRangeMin;
    private static ConfigEntry<float> GreetReactionRangeMax;
    private static ConfigEntry<bool>  GreetQuickReactions;
    
    public override void Load()
    {
        // Plugin startup logic
        LoadConfig();
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(TestPatch1));
    }

    private void LoadConfig()
    {
        // Item Tweaks
        StackSize = Config.Bind("ItemSettings", "StackSize", 99,
            "Enter max stack size. Over 9999 is untested!");
        
        // Windmill Buff Tweaks
        WindBuffNone = Config.Bind("WindmillSettings", "WindBuff_None", 1.0f,
            "Enter wind buff multiplier for no wind. Lower is faster.");
        WindBuffSlight = Config.Bind("WindmillSettings", "WindBuff_Slight", 1.0f,
            "Enter wind buff multiplier for slight wind. Lower is faster.");
        WindBuffStrong = Config.Bind("WindmillSettings", "WindBuff_Strong", 0.75f,
            "Enter wind buff multiplier for strong wind. Lower is faster.");
        WindBuffTyphoon = Config.Bind("WindmillSettings", "WindBuff_Typhoon", 0.5f,
            "Enter wind buff multiplier for typhoon wind. Lower is faster.");
        
        // Typhoon Damage Tweaks
        BeeTyphoonDam = Config.Bind("TyphoonDamage", "Bee_Typhoon_Damage", 70,
            "Enter damage sustained from typhoons.");
        MushTyphoonDam = Config.Bind("TyphoonDamage", "Mushroom_Typhoon_Damage", 70,
            "Enter damage sustained from typhoons.");
        CropTyphoonDam = Config.Bind("TyphoonDamage", "Crop_Typhoon_Damage", 70,
            "Enter damage sustained from typhoons.");
        
        // Movement Tweaks
        HorseMaxSpeedBoost = Config.Bind("MovementSettings", "Horse_Max_Speed_Boost", 0.0f,
            "Enter amount to boost max horse speed. Base horse max speed = 7.0");
        HorseAccelerationBoost = Config.Bind("MovementSettings", "Horse_Acceleration_Boost", 0.0f,
            "Enter amount to boost horse acceleration. Base horse acceleration = 3.0");
        HorseTurnSpeedBoost = Config.Bind("MovementSettings", "Horse_Turn_Speed_Boost", 0.0f,
            "Enter amount to boost horse turn speed. Base horse turn speed = 120.0");
        
        // Greeting Tweaks
        GreetRange = Config.Bind("GreetingSettings", "Greeting_Range", 2.7f,
            "Enter greeting range.");
        GreetSearchWaitTime = Config.Bind("GreetingSettings", "Greeting_Search_Wait_Time", 0.2f,
            "Enter greeting search wait time for player.");
        GreetHorseSearchWaitTime = Config.Bind("GreetingSettings", "Greeting_Horse_Search_Wait_Time", 0.2f,
            "Enter greeting search wait time for horse.");
        GreetUIShowDelay = Config.Bind("GreetingSettings", "Greeting_UI_Show_Delay", 2.0f,
            "Enter delay for greeting UI indicator.");
        GreetReactionRangeMin = Config.Bind("GreetingSettings", "Greeting_Reaction_Range_Min", 0.0f,
            "Enter the minimum reaction range. Minimum value of 0.0");
        GreetReactionRangeMax = Config.Bind("GreetingSettings", "Greeting_Reaction_Range_Max", 5.0f,
            "Enter the maximum reaction range. Typically >= Greeting_Range.");
        GreetQuickReactions = Config.Bind("GreetingSettings", "Greeting_Quick_Reactions", false,
            "Enable for instant reactions.");
    }
    
    private static string PropertyList(object obj)
    {
        var props = obj.GetType().GetProperties();
        var sb = new StringBuilder();
        foreach (var p in props)
        {
            sb.AppendLine(p.Name + ": " + p.GetValue(obj, null));    
        }
        return sb.ToString();
    }
    
    private static class TestPatch1
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var masterDMgr = MasterDataManager.Instance.ItemMasterData;
            var gameSetting = SettingAssetManager.Instance.GameSetting;
            var beeSetting  = SettingAssetManager.Instance.BeekeepingSetting;
            var mushSetting = SettingAssetManager.Instance.MushroomFarmingSetting;
            var farmSetting = SettingAssetManager.Instance.FarmSetting;
            var playSetting = SettingAssetManager.Instance.PlayerSetting;
            var hrSpdSetting = SettingAssetManager.Instance.PetAnimalCommonSetting.HorseSpeedSettings;
            
            // Item Tweaks
            foreach (var item in masterDMgr) if (item.StackSize != 1) item.StackSize = StackSize.Value;

            // Windmill Buff Tweaks
            gameSetting.WindmillSettings.WIND_BUFF[0] = WindBuffNone.Value;    // No wind
            gameSetting.WindmillSettings.WIND_BUFF[1] = WindBuffSlight.Value;  // Slight wind
            gameSetting.WindmillSettings.WIND_BUFF[2] = WindBuffStrong.Value;  // Strong wind
            gameSetting.WindmillSettings.WIND_BUFF[3] = WindBuffTyphoon.Value; // Typhoon

            // Typhoon Damage Tweaks
            beeSetting.BadWeatherWitherRate = BeeTyphoonDam.Value;
            mushSetting.TyphoonDamageRate = MushTyphoonDam.Value;
            farmSetting.TyphoonDamageRate = CropTyphoonDam.Value;
            
            // Movement Tweaks
            foreach (var speed in hrSpdSetting)
            {
                speed.MaxSpeed += HorseMaxSpeedBoost.Value;
                speed.AccelerationWalk += HorseAccelerationBoost.Value;
                speed.AccelerationRun += HorseAccelerationBoost.Value;
                speed.TurnSpeed += HorseTurnSpeedBoost.Value;
            }
            
            
            // Greeting Tweaks
            playSetting.GreetingRange = new Vector3(GreetRange.Value, 0.0f, 0.0f);
            playSetting.GreetingSarchWaitTime = GreetSearchWaitTime.Value;
            playSetting.GreetingHorseSarchWaitTime = GreetHorseSearchWaitTime.Value;
            playSetting.GreetUIShowDelay = GreetUIShowDelay.Value;

            playSetting.GreetingReactionDatas[0].RangeMin = GreetReactionRangeMin.Value;
            playSetting.GreetingReactionDatas[0].RangeMax = GreetReactionRangeMax.Value * 0.4f;
            playSetting.GreetingReactionDatas[1].RangeMin = GreetReactionRangeMax.Value * 0.4f;
            playSetting.GreetingReactionDatas[1].RangeMax = GreetReactionRangeMax.Value * 0.7f;
            playSetting.GreetingReactionDatas[2].RangeMin = GreetReactionRangeMax.Value * 0.7f;
            playSetting.GreetingReactionDatas[2].RangeMax = GreetReactionRangeMax.Value;

            if (GreetQuickReactions.Value)
            {
                playSetting.GreetingReactionDatas[0].DelayMin = 0.0f;
                playSetting.GreetingReactionDatas[0].DelayMax = 0.1f;
                playSetting.GreetingReactionDatas[1].DelayMin = 0.0f;
                playSetting.GreetingReactionDatas[1].DelayMax = 0.1f;
                playSetting.GreetingReactionDatas[2].DelayMin = 0.0f;
                playSetting.GreetingReactionDatas[2].DelayMax = 0.1f;
            }
            else // Game Default Values
            {
                playSetting.GreetingReactionDatas[0].DelayMin = 0.4f;
                playSetting.GreetingReactionDatas[0].DelayMax = 0.7f;
                playSetting.GreetingReactionDatas[1].DelayMin = 1.1f;
                playSetting.GreetingReactionDatas[1].DelayMax = 1.3f;
                playSetting.GreetingReactionDatas[2].DelayMin = 1.5f;
                playSetting.GreetingReactionDatas[2].DelayMax = 1.8f;
            }
            
            
        } 
    }
}