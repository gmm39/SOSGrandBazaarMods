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
    private static ConfigEntry<float> PlayerWalkSpeed;
    private static ConfigEntry<float> PlayerRunSpeed;
    private static ConfigEntry<float> PlayerInWeedSpeed;
    private static ConfigEntry<float> PlayerLimitMoveSpeed;
    private static ConfigEntry<float> PlayerBazaarLimitMoveSpeed;
    
    // Greeting Tweaks
    private static ConfigEntry<float> GreetRange;
    private static ConfigEntry<float> GreetSearchWaitTime;
    private static ConfigEntry<float> GreetHorseSearchWaitTime;
    private static ConfigEntry<float> GreetUIShowDelay;
    private static ConfigEntry<float> GreetReactionRangeMin;
    private static ConfigEntry<float> GreetReactionRangeMax;
    private static ConfigEntry<bool>  GreetQuickReactions;
    
    // Dive Tweaks
    private static ConfigEntry<float> DiveFadeOutTime;
    private static ConfigEntry<float> DiveFadeWaitingTime;
    private static ConfigEntry<float> DiveBeforeFadeInWaitingTime;
    private static ConfigEntry<float> DiveFadeInTime;
    
    // Animation Speed Tweaks
    private static ConfigEntry<float> AnimationPutinItem;
    private static ConfigEntry<float> AnimationPickupItem;
    private static ConfigEntry<float> AnimationWateringPotMotionCancel;
    private static ConfigEntry<float> AnimationPlantFlower;
    private static ConfigEntry<float> AnimationPlantFlowerMotionCancel;
    private static ConfigEntry<float> AnimationFertillze;
    private static ConfigEntry<float> AnimationFertillzeMotionCancel;
    private static ConfigEntry<float> AnimationFertillzeEnableItem;
    private static ConfigEntry<float> AnimationSickle;
    private static ConfigEntry<float> AnimationSickleMotionCancel;
    private static ConfigEntry<float> AnimationJumpSickle;
    private static ConfigEntry<float> AnimationDisplayDelay;
    private static ConfigEntry<float> AnimationHappy;
    private static ConfigEntry<float> AnimationBell;
    private static ConfigEntry<float> AnimationAnimalLiftUp;
    private static ConfigEntry<float> AnimationAnimalTakeDown;
    private static ConfigEntry<float> AnimationHammer;
    private static ConfigEntry<float> AnimationHammerMotionCancel;
    private static ConfigEntry<float> AnimationAx;
    private static ConfigEntry<float> AnimationAxMotionCancel;
    private static ConfigEntry<float> AnimationScoop;
    private static ConfigEntry<float> AnimationScoopMotionCancel;
    private static ConfigEntry<float> AnimationHorseSaddleOff;
    private static ConfigEntry<float> AnimationHorseCollect;
    private static ConfigEntry<float> AnimationLeverDownBreak;
    private static ConfigEntry<float> AnimationEat;
    private static ConfigEntry<float> AnimationEatEndStart;
    private static ConfigEntry<float> AnimationShowItemEffect;
    private static ConfigEntry<float> AnimationChangeItemDisableTool;
    private static ConfigEntry<float> AnimationChangeItem;
    private static ConfigEntry<float> AnimationHarvstChangeItem;
    private static ConfigEntry<float> AnimationHarvstTool;
    private static ConfigEntry<float> AnimationTreeHarvstAction;
    private static ConfigEntry<float> AnimationTreeHarvst;
    private static ConfigEntry<float> AnimationJumpHarvst;
    private static ConfigEntry<float> AnimationTakeoutItemEnd;
    private static ConfigEntry<float> AnimationMushroomTakeoutItem;
    private static ConfigEntry<float> AnimationBasketPickup;
    private static ConfigEntry<float> AnimationBasketPickupEnd;
    private static ConfigEntry<float> AnimationSiloIn;
    private static ConfigEntry<float> AnimationSiloOut;
    private static ConfigEntry<float> AnimationPutItem;
    private static ConfigEntry<float> AnimationTired;
    
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
        PlayerWalkSpeed = Config.Bind("PlayerMovement", "Walk_Speed", 1.0f,
            "Enter Player walk speed.");
        PlayerRunSpeed = Config.Bind("PlayerMovement", "Run_Speed", 5.0f,
            "Enter Player run speed.");
        PlayerInWeedSpeed = Config.Bind("PlayerMovement", "In_Weed_Speed", 3.0f,
            "Enter Player speed in weeds.");
        PlayerLimitMoveSpeed = Config.Bind("PlayerMovement", "Limit_Move_Speed", 2.3f,
            "Enter Player speed limit during times of limited movement.");
        PlayerBazaarLimitMoveSpeed = Config.Bind("PlayerMovement", "Bazaar_Limit_Move_Speed", 4.0f,
            "Enter Player speed limit during the bazaar.");
        
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
        
        // Dive Tweaks
        DiveFadeOutTime = Config.Bind("DiveSettings", "Dive_Fade_Out_Time", 0.5f, 
            "Enter dive fade out time.");
        DiveFadeWaitingTime = Config.Bind("DiveSettings",  "Dive_Fade_Waiting_Time", 0.5f, 
            "Enter dive fade waiting time.");
        DiveBeforeFadeInWaitingTime = Config.Bind("DiveSettings",  "Dive_Before_Fade_In_Waiting_Time", 0.2f, 
            "Enter dive fade waiting time.");
        DiveFadeInTime = Config.Bind("DiveSettings",  "Dive_Fade_In_Time", 0.5f, 
            "Enter dive fade in.");
        
        // Animation Speed Tweaks
        AnimationPutinItem = Config.Bind("AnimationSpeedTweaks", "Animation_Putin_Item", 10.0f,
            "");
        AnimationPickupItem = Config.Bind("AnimationSpeedTweaks", "Animation_Pickup_Item", 8.0f, 
            "");
        AnimationWateringPotMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Watering_Pot_Motion_Cancel", 10.0f, 
            "");
        AnimationPlantFlower = Config.Bind("AnimationSpeedTweaks", "Animation_Plant_Flower", 10.0f, 
            "");
        AnimationPlantFlowerMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Plant_Flower_Motion_Cancel", 4.0f, 
            "");
        AnimationFertillze = Config.Bind("AnimationSpeedTweaks", "Animation_Fertillze", 22.0f, 
            "");
        AnimationFertillzeMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Fertillze_Motion_Cancel", 3.0f, 
            "");
        AnimationFertillzeEnableItem = Config.Bind("AnimationSpeedTweaks", "Animation_Fertillze_Enable_Item", 6.0f, 
            "");
        AnimationSickle = Config.Bind("AnimationSpeedTweaks", "Animation_Sickle", 3.0f, 
            "");
        AnimationSickleMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Sickle_Motion_Cancel", 3.0f, 
            "");
        AnimationJumpSickle = Config.Bind("AnimationSpeedTweaks", "Animation_Jump_Sickle", 4.0f, 
            "");
        AnimationDisplayDelay = Config.Bind("AnimationSpeedTweaks", "Animation_Display_Delay", 0.5f, 
            "");
        AnimationHappy = Config.Bind("AnimationSpeedTweaks", "Animation_Happy", 2.0f, 
            "");
        AnimationBell = Config.Bind("AnimationSpeedTweaks", "Animation_Bell", 12.0f, 
            "");
        AnimationAnimalLiftUp = Config.Bind("AnimationSpeedTweaks", "Animation_Animal_Lift_Up", 10.0f, 
            "");
        AnimationAnimalTakeDown = Config.Bind("AnimationSpeedTweaks", "Animation_Animal_Take_Down", 10.0f, 
            "");
        AnimationHammer = Config.Bind("AnimationSpeedTweaks", "Animation_Hammer", 3.5f, 
            "");
        AnimationHammerMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Hammer_Motion_Cancel", 2.0f, 
            "");
        AnimationAx = Config.Bind("AnimationSpeedTweaks", "Animation_Ax", 8.0f, 
            "");
        AnimationAxMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Ax_Motion_Cancel", 2.0f, 
            "");
        AnimationScoop = Config.Bind("AnimationSpeedTweaks", "Animation_Scoop", 30.0f, 
            "");
        AnimationScoopMotionCancel = Config.Bind("AnimationSpeedTweaks", "Animation_Scoop_Motion_Cancel", 4.0f, 
            "");
        AnimationHorseSaddleOff = Config.Bind("AnimationSpeedTweaks", "Animation_Horse_Saddle_Off", 20.0f, 
            "");
        AnimationHorseCollect = Config.Bind("AnimationSpeedTweaks", "Animation_Horse_Collect", 22.0f, 
            "");
        AnimationLeverDownBreak = Config.Bind("AnimationSpeedTweaks", "Animation_Lever_Down_Break", 95.0f, 
            "");
        AnimationEat = Config.Bind("AnimationSpeedTweaks", "Animation_Eat", 1.0f, 
            "");
        AnimationEatEndStart = Config.Bind("AnimationSpeedTweaks", "Animation_Eat_End_Start", 25.0f, 
            "");
        AnimationShowItemEffect = Config.Bind("AnimationSpeedTweaks", "Animation_Show_Item_Effect", 10.0f, 
            "");
        AnimationChangeItemDisableTool = Config.Bind("AnimationSpeedTweaks", "Animation_Change_Item_Disable_Tool", 16.0f, 
            "");
        AnimationChangeItem = Config.Bind("AnimationSpeedTweaks", "Animation_Change_Item", 1.0f, 
            "");
        AnimationHarvstChangeItem = Config.Bind("AnimationSpeedTweaks", "Animation_Harvst_Change_Item", 14.0f, 
            "");
        AnimationHarvstTool = Config.Bind("AnimationSpeedTweaks", "Animation_Harvst_Tool", 8.0f, 
            "");
        AnimationTreeHarvstAction = Config.Bind("AnimationSpeedTweaks", "Animation_Tree_Harvst_Action", 3.0f, 
            "");
        AnimationTreeHarvst = Config.Bind("AnimationSpeedTweaks", "Animation_Tree_Harvst", 10.0f, 
            "");
        AnimationJumpHarvst = Config.Bind("AnimationSpeedTweaks", "Animation_Jump_Harvst", 1.0f, 
            "");
        AnimationTakeoutItemEnd = Config.Bind("AnimationSpeedTweaks", "Animation_Takeout_Item_End", 12.0f, 
            "");
        AnimationMushroomTakeoutItem = Config.Bind("AnimationSpeedTweaks", "Animation_Mushroom_Takeout_Item", 21.0f, 
            "");
        AnimationBasketPickup = Config.Bind("AnimationSpeedTweaks", "Animation_Basket_Pickup", 0.6f, 
            "");
        AnimationBasketPickupEnd = Config.Bind("AnimationSpeedTweaks", "Animation_Basket_Pickup_End", 9.0f, 
            "");
        AnimationSiloIn = Config.Bind("AnimationSpeedTweaks", "Animation_Silo_In", 11.0f, 
            "");
        AnimationSiloOut = Config.Bind("AnimationSpeedTweaks", "Animation_Silo_Out", 38.0f, 
            "");
        AnimationPutItem = Config.Bind("AnimationSpeedTweaks", "Animation_Put_Item", 10.0f, 
            "");
        AnimationTired = Config.Bind("AnimationSpeedTweaks", "Animation_Tired", 30.0f, 
            "");
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
            var beeSetting = SettingAssetManager.Instance.BeekeepingSetting;
            var mushSetting = SettingAssetManager.Instance.MushroomFarmingSetting;
            var farmSetting = SettingAssetManager.Instance.FarmSetting;
            var playSetting = SettingAssetManager.Instance.PlayerSetting;
            
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
            playSetting.PlayerWalkSpeed = PlayerWalkSpeed.Value;
            playSetting.PlayerRunSpeed = PlayerRunSpeed.Value;
            playSetting.PlayerInWeedSpeed = PlayerInWeedSpeed.Value;
            playSetting.PlayerLimitMoveSpeed = PlayerLimitMoveSpeed.Value;
            playSetting.PlayerBazaarLimitMoveSpeed = PlayerBazaarLimitMoveSpeed.Value;
            
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
            
            // Dive Tweaks
            playSetting.DiveFadeOutTime = DiveFadeOutTime.Value;
            playSetting.DiveFadeWaitingTime = DiveFadeWaitingTime.Value;
            playSetting.DiveBeforeFadeInWaitingTime = DiveBeforeFadeInWaitingTime.Value;
            playSetting.DiveFadeInTime = DiveFadeInTime.Value;
            
            // Animation Speed Tweaks
            playSetting.AnimationPutinItem = AnimationPutinItem.Value;
            playSetting.AnimationPickupItem = AnimationPickupItem.Value;
            playSetting.AnimationWateringPotMotionCancel = AnimationWateringPotMotionCancel.Value;
            playSetting.AnimationPlantFlower = AnimationPlantFlower.Value;
            playSetting.AnimationPlantFlowerMotionCancel = AnimationPlantFlowerMotionCancel.Value;
            playSetting.AnimationFertillze = AnimationFertillze.Value;
            playSetting.AnimationFertillzeMotionCancel = AnimationFertillzeMotionCancel.Value;
            playSetting.AnimationFertillzeEnableItem = AnimationFertillzeEnableItem.Value;
            playSetting.AnimationSickle = AnimationSickle.Value;
            playSetting.AnimationSickleMotionCancel = AnimationSickleMotionCancel.Value;
            playSetting.AnimationJumpSickle = AnimationJumpSickle.Value;
            playSetting.AnimationDisplayDelay = AnimationDisplayDelay.Value;
            playSetting.AnimationHappy = AnimationHappy.Value;
            playSetting.AnimationBell = AnimationBell.Value;
            playSetting.AnimationAnimalLiftUp = AnimationAnimalLiftUp.Value;
            playSetting.AnimationAnimalTakeDown = AnimationAnimalTakeDown.Value;
            playSetting.AnimationHammer = AnimationHammer.Value;
            playSetting.AnimationHammerMotionCancel = AnimationHammerMotionCancel.Value;
            playSetting.AnimationAx = AnimationAx.Value;
            playSetting.AnimationAxMotionCancel = AnimationAxMotionCancel.Value;
            playSetting.AnimationScoop = AnimationScoop.Value;
            playSetting.AnimationScoopMotionCancel = AnimationScoopMotionCancel.Value;
            playSetting.AnimationHorseSaddleOff = AnimationHorseSaddleOff.Value;
            playSetting.AnimationHorseCollect = AnimationHorseCollect.Value;
            playSetting.AnimationLeverDownBreak = AnimationLeverDownBreak.Value;
            playSetting.AnimationEat = AnimationEat.Value;
            playSetting.AnimationEatEndStart = AnimationEatEndStart.Value;
            playSetting.AnimationShowItemEffect = AnimationShowItemEffect.Value;
            playSetting.AnimationChangeItemDisableTool = AnimationChangeItemDisableTool.Value;
            playSetting.AnimationChangeItem = AnimationChangeItem.Value;
            playSetting.AnimationHarvstChangeItem = AnimationHarvstChangeItem.Value;
            playSetting.AnimationHarvstTool = AnimationHarvstTool.Value;
            playSetting.AnimationTreeHarvstAction = AnimationTreeHarvstAction.Value;
            playSetting.AnimationTreeHarvst = AnimationTreeHarvst.Value;
            playSetting.AnimationJumpHarvst = AnimationJumpHarvst.Value;
            playSetting.AnimationTakeoutItemEnd = AnimationTakeoutItemEnd.Value;
            playSetting.AnimationMushroomTakeoutItem = AnimationMushroomTakeoutItem.Value;
            playSetting.AnimationBasketPickup = AnimationBasketPickup.Value;
            playSetting.AnimationBasketPickupEnd = AnimationBasketPickupEnd.Value;
            playSetting.AnimationSiloIn = AnimationSiloIn.Value;
            playSetting.AnimationSiloOut = AnimationSiloOut.Value;
            playSetting.AnimationPutItem = AnimationPutItem.Value;
            playSetting.AnimationTired = AnimationTired.Value;
            
            Log.LogInfo(PropertyList(playSetting));
        } 
    }
}