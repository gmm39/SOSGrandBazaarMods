using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;
using UnityEngine;

namespace PetAnimalTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<float> FriendHappyMulti;
    private static ConfigEntry<float> PetTrainMulti;
    private static ConfigEntry<bool> EnableDebug;

    public override void Load()
    {
        // Plugin startup logic
        FriendHappyMulti = Config.Bind("-----01 GENERAL-----", "Friendship_Happiness_Multiplier", 1.0f,
            "Multiples pet/animal friendship and happiness gain by given amount.");
        PetTrainMulti = Config.Bind("-----01 GENERAL-----", "Pet_Training_Multiplier", 1.0f,
            "Multiples pet training point gain by given amount.");
        EnableDebug = Config.Bind("-----99 DEBUG-----", "Enable_Debug_Logging", false,
            "Enable debug logging.");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        Harmony.CreateAndPatchAll(typeof(PetAnimalPatch));
    }

    private static class PetAnimalPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            BreadSettings();
            PetAnimalCommonSettings();
            PetTrainSettings();
            
            if(EnableDebug.Value) DebugLog();
        }

        private static int BSettingCalc(int a)
        {
            return Mathf.Approximately(FriendHappyMulti.Value, 1.0f) ? a : Math.Clamp((int)Math.Round(((a == 0) ? 1 : a) * FriendHappyMulti.Value), 0, int.MaxValue);
        }

        private static void BreadSettings()
        {
            var bSettings = SettingAssetManager.Instance.BreadingSetting;

            bSettings.ChangeParamEatFeedF = BSettingCalc(bSettings.ChangeParamEatFeedF);
            bSettings.ChangeParamEatFeedHappy = BSettingCalc(bSettings.ChangeParamEatFeedHappy);
            bSettings.ChangeParamEatFeedHealth = BSettingCalc(bSettings.ChangeParamEatFeedHealth);
            bSettings.ChangeParamEatGrassF = BSettingCalc(bSettings.ChangeParamEatGrassF);
            bSettings.ChangeParamEatGrassHappy = BSettingCalc(bSettings.ChangeParamEatGrassHappy);
            bSettings.ChangeParamEatGrassHealth = BSettingCalc(bSettings.ChangeParamEatGrassHealth);
            bSettings.ChangeParamEatGrassAF = BSettingCalc(bSettings.ChangeParamEatGrassAF);
            bSettings.ChangeParamEatGrassAHappy = BSettingCalc(bSettings.ChangeParamEatGrassAHappy);
            bSettings.ChangeParamEatGrassAHealth = BSettingCalc(bSettings.ChangeParamEatGrassAHealth);
            bSettings.ChangeParamEatGrassBF = BSettingCalc(bSettings.ChangeParamEatGrassBF);
            bSettings.ChangeParamEatGrassBHappy = BSettingCalc(bSettings.ChangeParamEatGrassBHappy);
            bSettings.ChangeParamEatGrassBHealth = BSettingCalc(bSettings.ChangeParamEatGrassBHealth);
            bSettings.ChangeParamEatGrassCF = BSettingCalc(bSettings.ChangeParamEatGrassCF);
            bSettings.ChangeParamEatGrassCHappy = BSettingCalc(bSettings.ChangeParamEatGrassCHappy);
            bSettings.ChangeParamEatGrassCHealth = BSettingCalc(bSettings.ChangeParamEatGrassCHealth);
            bSettings.ChangeParamEatGrassDF = BSettingCalc(bSettings.ChangeParamEatGrassDF);
            bSettings.ChangeParamEatGrassDHappy = BSettingCalc(bSettings.ChangeParamEatGrassDHappy);
            bSettings.ChangeParamEatGrassDHealth = BSettingCalc(bSettings.ChangeParamEatGrassDHealth);
            bSettings.ChangeParamGrazingSunnyF = BSettingCalc(bSettings.ChangeParamGrazingSunnyF);
            bSettings.ChangeParamGrazingSunnyH = BSettingCalc(bSettings.ChangeParamGrazingSunnyH);
            bSettings.ChangeParamGrazingSunnyHealth = BSettingCalc(bSettings.ChangeParamGrazingSunnyHealth);
            bSettings.ChangeParamGrazingCloudF = BSettingCalc(bSettings.ChangeParamGrazingCloudF);
            bSettings.ChangeParamGrazingCloudH = BSettingCalc(bSettings.ChangeParamGrazingCloudH);
            bSettings.ChangeParamGrazingCloudHealth = BSettingCalc(bSettings.ChangeParamGrazingCloudHealth);
            bSettings.ChangeParamGrazingSnowyF = BSettingCalc(bSettings.ChangeParamGrazingSnowyF);
            bSettings.ChangeParamGrazingSnowyH = BSettingCalc(bSettings.ChangeParamGrazingSnowyH);
            bSettings.ChangeParamGrazingSnowyHealth = BSettingCalc(bSettings.ChangeParamGrazingSnowyHealth);
            bSettings.ChangeParamStrokingF = BSettingCalc(bSettings.ChangeParamStrokingF);
            bSettings.ChangeParamStrokingH = BSettingCalc(bSettings.ChangeParamStrokingH);
            bSettings.ChangeParamStrokingHealth = BSettingCalc(bSettings.ChangeParamStrokingHealth);
            bSettings.ChangeParamBrushingF = BSettingCalc(bSettings.ChangeParamBrushingF);
            bSettings.ChangeParamBrushingH = BSettingCalc(bSettings.ChangeParamBrushingH);
            bSettings.ChangeParamBrushingHealth = BSettingCalc(bSettings.ChangeParamBrushingHealth);
            bSettings.ChangeParamBellF = BSettingCalc(bSettings.ChangeParamBellF);
            bSettings.ChangeParamBellH = BSettingCalc(bSettings.ChangeParamBellH);
            bSettings.ChangeParamBellHealth = BSettingCalc(bSettings.ChangeParamBellHealth);
            bSettings.ChangeParamPickUpF = BSettingCalc(bSettings.ChangeParamPickUpF);
            bSettings.ChangeParamPickUpH = BSettingCalc(bSettings.ChangeParamPickUpH);
            bSettings.ChangeParamPickUpHealth = BSettingCalc(bSettings.ChangeParamPickUpHealth);
        }

        private static void PetAnimalCommonSettings()
        {
            var paSettings = SettingAssetManager.Instance.PetAnimalCommonSetting;

            foreach (var item in paSettings.SnackSettings)
            {
                item.AddFriendPoint = Math.Clamp((int)Math.Round(item.AddFriendPoint * FriendHappyMulti.Value), 0, int.MaxValue);
            }
            
            paSettings.ExtraSetting.StrokeFriendPoint = Math.Clamp((int)Math.Round(paSettings.ExtraSetting.StrokeFriendPoint * FriendHappyMulti.Value), 0, int.MaxValue);
            paSettings.ExtraSetting.BrushFriendPoint = Math.Clamp((int)Math.Round(paSettings.ExtraSetting.BrushFriendPoint * FriendHappyMulti.Value), 0, int.MaxValue);
            paSettings.ExtraSetting.PickFriendPoint = Math.Clamp((int)Math.Round(paSettings.ExtraSetting.PickFriendPoint * FriendHappyMulti.Value), 0, int.MaxValue);
            paSettings.ExtraSetting.CareTrainingPoint = Math.Clamp((int)Math.Round(paSettings.ExtraSetting.CareTrainingPoint * FriendHappyMulti.Value), 0, int.MaxValue);
            paSettings.ExtraSetting.RideTrainingPoint = Math.Clamp((int)Math.Round(paSettings.ExtraSetting.RideTrainingPoint * FriendHappyMulti.Value), 0, int.MaxValue);
        }

        private static void PetTrainSettings()
        {
            var pSettings = SettingAssetManager.Instance.PetTrainingSetting;
            
            pSettings.SuccessTrainingPoint = Math.Clamp((int)Math.Round(pSettings.SuccessTrainingPoint * PetTrainMulti.Value), 0, int.MaxValue);
        }

        private static void DebugLog()
        {
            var bSettings = SettingAssetManager.Instance.BreadingSetting;
            var paSettings = SettingAssetManager.Instance.PetAnimalCommonSetting;
            var pSettings = SettingAssetManager.Instance.PetTrainingSetting;
            
            Log.LogInfo($"ChangeParamEatFeedF: {bSettings.ChangeParamEatFeedF}");
            Log.LogInfo($"ChangeParamEatFeedHappy: {bSettings.ChangeParamEatFeedHappy}");
            Log.LogInfo($"ChangeParamEatFeedHealth: {bSettings.ChangeParamEatFeedHealth}");
            Log.LogInfo($"ChangeParamEatGrassF: {bSettings.ChangeParamEatGrassF}");
            Log.LogInfo($"ChangeParamEatGrassHappy: {bSettings.ChangeParamEatGrassHappy}");
            Log.LogInfo($"ChangeParamEatGrassHealth: {bSettings.ChangeParamEatGrassHealth}");
            Log.LogInfo($"ChangeParamEatGrassAF: {bSettings.ChangeParamEatGrassAF}");
            Log.LogInfo($"ChangeParamEatGrassAHappy: {bSettings.ChangeParamEatGrassAHappy}");
            Log.LogInfo($"ChangeParamEatGrassAHealth: {bSettings.ChangeParamEatGrassAHealth}");
            Log.LogInfo($"ChangeParamEatGrassBF: {bSettings.ChangeParamEatGrassBF}");
            Log.LogInfo($"ChangeParamEatGrassBHappy: {bSettings.ChangeParamEatGrassBHappy}");
            Log.LogInfo($"ChangeParamEatGrassBHealth: {bSettings.ChangeParamEatGrassBHealth}");
            Log.LogInfo($"ChangeParamEatGrassCF: {bSettings.ChangeParamEatGrassCF}");
            Log.LogInfo($"ChangeParamEatGrassCHappy: {bSettings.ChangeParamEatGrassCHappy}");
            Log.LogInfo($"ChangeParamEatGrassCHealth: {bSettings.ChangeParamEatGrassCHealth}");
            Log.LogInfo($"ChangeParamEatGrassDF: {bSettings.ChangeParamEatGrassDF}");
            Log.LogInfo($"ChangeParamEatGrassDHappy: {bSettings.ChangeParamEatGrassDHappy}");
            Log.LogInfo($"ChangeParamEatGrassDHealth: {bSettings.ChangeParamEatGrassDHealth}");
            Log.LogInfo($"ChangeParamGrazingSunnyF: {bSettings.ChangeParamGrazingSunnyF}");
            Log.LogInfo($"ChangeParamGrazingSunnyH: {bSettings.ChangeParamGrazingSunnyH}");
            Log.LogInfo($"ChangeParamGrazingSunnyHealth: {bSettings.ChangeParamGrazingSunnyHealth}");
            Log.LogInfo($"ChangeParamGrazingCloudF: {bSettings.ChangeParamGrazingCloudF}");
            Log.LogInfo($"ChangeParamGrazingCloudH: {bSettings.ChangeParamGrazingCloudH}");
            Log.LogInfo($"ChangeParamGrazingCloudHealth: {bSettings.ChangeParamGrazingCloudHealth}");
            Log.LogInfo($"ChangeParamGrazingSnowyF: {bSettings.ChangeParamGrazingSnowyF}");
            Log.LogInfo($"ChangeParamGrazingSnowyH: {bSettings.ChangeParamGrazingSnowyH}");
            Log.LogInfo($"ChangeParamGrazingSnowyHealth: {bSettings.ChangeParamGrazingSnowyHealth}");
            Log.LogInfo($"ChangeParamStrokingF: {bSettings.ChangeParamStrokingF}");
            Log.LogInfo($"ChangeParamStrokingH: {bSettings.ChangeParamStrokingH}");
            Log.LogInfo($"ChangeParamStrokingHealth: {bSettings.ChangeParamStrokingHealth}");
            Log.LogInfo($"ChangeParamBrushingF: {bSettings.ChangeParamBrushingF}");
            Log.LogInfo($"ChangeParamBrushingH: {bSettings.ChangeParamBrushingH}");
            Log.LogInfo($"ChangeParamBrushingHealth: {bSettings.ChangeParamBrushingHealth}");
            Log.LogInfo($"ChangeParamBellF: {bSettings.ChangeParamBellF}");
            Log.LogInfo($"ChangeParamBellH: {bSettings.ChangeParamBellH}");
            Log.LogInfo($"ChangeParamBellHealth: {bSettings.ChangeParamBellHealth}");
            Log.LogInfo($"ChangeParamPickUpF: {bSettings.ChangeParamPickUpF}");
            Log.LogInfo($"ChangeParamPickUpH: {bSettings.ChangeParamPickUpH}");
            Log.LogInfo($"ChangeParamPickUpHealth: {bSettings.ChangeParamPickUpHealth}");
            
            foreach (var item in paSettings.SnackSettings)
            {
                Log.LogInfo($"AddFriendPoint: {item.AddFriendPoint}");
            }
            
            Log.LogInfo($"StrokeFriendPoint: {paSettings.ExtraSetting.StrokeFriendPoint}");
            Log.LogInfo($"BrushFriendPoint: {paSettings.ExtraSetting.BrushFriendPoint}");
            Log.LogInfo($"PickFriendPoint: {paSettings.ExtraSetting.PickFriendPoint}");
            Log.LogInfo($"CareTrainingPoint: {paSettings.ExtraSetting.CareTrainingPoint}");
            Log.LogInfo($"RideTrainingPoint: {paSettings.ExtraSetting.RideTrainingPoint}");
            
            Log.LogInfo($"SuccessTrainingPoint: {pSettings.SuccessTrainingPoint}");
        }
    }
}