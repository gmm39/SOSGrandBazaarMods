using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;

namespace PowerBerryExpanded;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    
    private static ConfigEntry<bool> EnableMovementFeature;
    private static ConfigEntry<bool> EnableGliderFeature;
    private static ConfigEntry<bool> EnableJumpFeature;
    private static ConfigEntry<bool> EnableWakeTimeFeature;
    
    private static ConfigEntry<int> BerriesToMax;
    private static ConfigEntry<float> MaxStamina;
    
    private static ConfigEntry<float> MaxWalkSpeed;
    private static ConfigEntry<float> MaxRunSpeed;
    private static ConfigEntry<float> MaxInWeedSpeed;
    private static ConfigEntry<float> MaxLimitedSpeed;
    private static ConfigEntry<float> MaxBazaarSpeed;
    
    private static ConfigEntry<float> JumpVerticalVelocityBoost;
    private static ConfigEntry<float> JumpHeightBoost;
    private static ConfigEntry<float> JumpRunSpeedBoost;
    
    private static ConfigEntry<string> AgainstWind;
    private static ConfigEntry<float> MaxGliderSpeedBoost;
    
    private static ConfigEntry<bool> EnableDebug;

    public override void Load()
    {
        // Plugin startup logic
        EnableMovementFeature = Config.Bind("-----00 FEATURE SELECT-----", "Enable_Movement_Feature", true,
            "Enable features in 02 MOVEMENT.");
        EnableGliderFeature = Config.Bind("-----00 FEATURE SELECT-----", "Enable_Glider_Feature", true,
            "Enable features in 03 GLIDER.");
        EnableJumpFeature = Config.Bind("-----00 FEATURE SELECT-----", "Enable_Jump_Feature", true,
            "Enable features in 04 JUMP.");
        EnableWakeTimeFeature = Config.Bind("-----00 FEATURE SELECT-----", "Enable_Wake_Time_Feature", false,
            "Enable wake time feature." +
            "\nFor every 25% of max berries eaten, wake time for late bedtimes are made slightly earlier." +
            "\nDoes not change wake times for fainting. Cannot wake before 6:00am." +
            "\nExample:" +
            "\n25%: 7:00am wake becomes 6:45am" +
            "\n50%: 7:00am wake becomes 6:30am" +
            "\n75%: 7:00am wake becomes 6:15am" +
            "\n100%:7:00am wake becomes 6:00am");
        
        BerriesToMax = Config.Bind("-----01 BERRIES-----", "Berries_To_Max", 20,
            "Number of berries required to reach max improvements.");
        MaxStamina = Config.Bind("-----01 BERRIES-----", "Max_Stamina", 2000.0f,
            "Stamina at max improvement.");
        
        MaxWalkSpeed = Config.Bind("-----02 MOVEMENT-----", "Max_Walk_Speed", 3.0f,
            "Walk speed at max improvement.");
        MaxRunSpeed = Config.Bind("-----02 MOVEMENT-----", "Max_Run_Speed", 7.5f,
            "Run speed at max improvement.");
        MaxInWeedSpeed = Config.Bind("-----02 MOVEMENT-----", "Max_In_Weed_Speed", 5.0f,
            "Speed in weeds at max improvement.");
        MaxLimitedSpeed = Config.Bind("-----02 MOVEMENT-----", "Max_Limited_Speed", 4.0f,
            "Speed during periods of limited movement at max improvement.");
        MaxBazaarSpeed = Config.Bind("-----02 MOVEMENT-----", "Max_Bazaar_Speed", 6.0f,
            "Speed during the bazaar at max improvement.");
        
        AgainstWind = Config.Bind("-----03 GLIDER-----", "Remove_Against_Wind_Penalty", "Progressive",
            "Acceptable Values: Enable, Progressive, AtMax, Disable" +
            "\nEnable: Remove the penalty for gliding into the wind at all times." +
            "\nProgressive: Lessen the penalty for gliding into the wind with each berry consumed." +
            "\nAtMax: Remove the penalty for gliding into the wind at max berries." +
            "\nDisable: Do not remove the penalty for gliding into the wind.");
        MaxGliderSpeedBoost = Config.Bind("-----03 GLIDER-----", "Max_Glider_Speed_Boost", 1.5f,
            "Boosts glider speed by given amount at max improvement.");
        
        JumpVerticalVelocityBoost = Config.Bind("-----04 JUMP-----", "Vertical_Velocity_Boost", 0.75f,
            "Boosts jump velocity by given amount at max improvement.");
        JumpHeightBoost = Config.Bind("-----04 JUMP-----", "Jump_Height_Boost", 0.66f,
            "Boosts jump height by given amount at max improvement.");
        JumpRunSpeedBoost = Config.Bind("-----04 JUMP-----", "Jump_Move_Speed_Boost", 1.0f,
            "Boosts movement speed while jumping by given amount at max improvement.");
        
        EnableDebug = Config.Bind("-----99 DEBUG-----", "Enable_Debug_Logging", false,
            "Enable debug logging.");

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(PowerBerryExpanded));
    }

    private static class PowerBerryExpanded
    {
        private static PlayerSetting playSetting;
        private static Il2CppSystem.Collections.Generic.List<PlayerRecoveryMasterData> recoveryList;
        private static int currentPowerBerries;
        
        // Movement
        private static float baseWalkSpeed;
        private static float baseRunSpeed;
        private static float baseInWeedSpeed;
        private static float baseLimitedSpeed;
        private static float baseBazaarSpeed;
        private static float walkIncre;
        private static float runIncre;
        private static float inWeedIncre;
        private static float limitedIncre;
        private static float bazaarIncre;
        
        // Glider
        private static float gliderIncre;
        private static List<float> baseGliderSpeeds;
        private static List<float> baseWindSpeeds;
        private static List<float> progressiveWindSpeeds;
        
        // Jump
        private static float baseFirstJumpHeight;
        private static float baseSecondJumpHeight;
        private static float baseFirstMaxJumpHight;
        private static float baseSecondMaxJumpHight;
        private static float baseJumpRunSpeed;
        private static float velocityIncre;
        private static float heightIncre;
        private static float jumpSpeedIncre;
        
        // Sleep
        private static List<int> sleepImproveLevels;
        
        static PowerBerryExpanded()
        {
            playSetting = SettingAssetManager.Instance.PlayerSetting;
            recoveryList = MasterDataManager.Instance.PlayerRecoveryMaster.list;
            
            // Movement
            baseWalkSpeed = 1.0f;
            baseRunSpeed = 5.0f;
            baseInWeedSpeed = 3.0f;
            baseLimitedSpeed = 2.3f;
            baseBazaarSpeed = 4.0f;
            walkIncre = (MaxWalkSpeed.Value - baseWalkSpeed) / BerriesToMax.Value;
            runIncre = (MaxRunSpeed.Value - baseRunSpeed) / BerriesToMax.Value;
            inWeedIncre = (MaxInWeedSpeed.Value - baseInWeedSpeed) / BerriesToMax.Value;
            limitedIncre = (MaxLimitedSpeed.Value - baseLimitedSpeed) / BerriesToMax.Value;
            bazaarIncre = (MaxBazaarSpeed.Value - baseBazaarSpeed) / BerriesToMax.Value;
            
            // Glider
            gliderIncre = MaxGliderSpeedBoost.Value / BerriesToMax.Value;
            baseGliderSpeeds = [3.0f, 3.0f, 4.0f, 4.5f];
            baseWindSpeeds = [0.0f, 2.0f, 4.0f, 4.5f];
            progressiveWindSpeeds = baseWindSpeeds.Select(x => x / BerriesToMax.Value).ToList();
            
            // Jump
            baseFirstJumpHeight = 4.5f;
            baseSecondJumpHeight = 5.0f;
            baseFirstMaxJumpHight = 1.0f;
            baseSecondMaxJumpHight = 1.27f;
            baseJumpRunSpeed = 6.2f;
            velocityIncre = JumpVerticalVelocityBoost.Value / BerriesToMax.Value;
            heightIncre = JumpHeightBoost.Value / BerriesToMax.Value;
            jumpSpeedIncre = JumpRunSpeedBoost.Value / BerriesToMax.Value;
            
            // Sleep
            sleepImproveLevels = [(int)(BerriesToMax.Value * 0.25), (int)(BerriesToMax.Value * 0.5), (int)(BerriesToMax.Value * 0.75)];
        }
        
        [HarmonyPatch(typeof(DateManager), "OnStartGame")]
        [HarmonyPrefix]
        private static void OnSaveLoad()
        {
            AdjustBerryStamina();
            UpdatePowerBerries();
            UpdateBerryMessage();
            
            if(EnableMovementFeature.Value) MovementIncrease();
            if(EnableGliderFeature.Value) GliderMovementIncrease();
            if(EnableJumpFeature.Value) JumpIncrease();
            if(EnableWakeTimeFeature.Value) WakeTimeChange();
            if(EnableDebug.Value) Output();
        }

        [HarmonyPatch(typeof(PlayerCharacter), "AddMaxHP")]
        [HarmonyPostfix]
        private static void OnBerryEat()
        {
            UpdatePowerBerries();
            
            if(EnableMovementFeature.Value) MovementIncrease();
            if(EnableGliderFeature.Value) GliderMovementIncrease();
            if(EnableJumpFeature.Value) JumpIncrease();
            if(EnableWakeTimeFeature.Value) WakeTimeChange();
            if(EnableDebug.Value) Output();
        }
        
        private static void AdjustBerryStamina()
        {
            playSetting.PlayerMaxHP = MaxStamina.Value;
            playSetting.MisteriousWaterValue = (playSetting.PlayerMaxHP - playSetting.PlayerHP) / BerriesToMax.Value;
        }
        
        private static void UpdateBerryMessage()
        {
            if (LanguageManager.Instance.CurrentLanguage != Language.en) return;
            if (!EnableMovementFeature.Value && !EnableGliderFeature.Value && !EnableWakeTimeFeature.Value) return;
            
            var berryText = LanguageManager.Instance.GetLocalizeTextData(LocalizeTextTableType.TalkText_Object, 59927011);
            
            const string combo1Text = "Your maximum stamina & movement speed\nhas improved and your stamina is fully restored!";
            const string combo2Text = "Your maximum stamina & glider speed\nhas improved and your stamina is fully restored!";
            const string combo3Text = "Your maximum stamina & wake time\nhas improved and your stamina is fully restored!";
            const string combo4Text = "Your maximum stamina, movement speed,\n& glider speed has improved\nand your stamina is fully restored!";
            const string combo5Text = "Your maximum stamina, movement speed,\n& wake time has improved\nand your stamina is fully restored!";
            const string combo6Text = "Your maximum stamina, glider speed,\n& wake time has improved\nand your stamina is fully restored!";
            const string combo7Text = "Your maximum stamina, movement speed,\nglider speed, & wake time has improved\nand your stamina is fully restored!";

            berryText.Text = EnableMovementFeature.Value switch
            {
                true when !EnableGliderFeature.Value && EnableWakeTimeFeature.Value => combo5Text,
                false when EnableGliderFeature.Value && EnableWakeTimeFeature.Value => combo6Text,
                _ => EnableMovementFeature.Value switch
                {
                    false when !EnableGliderFeature.Value && EnableWakeTimeFeature.Value => combo3Text,
                    true when EnableGliderFeature.Value && !EnableWakeTimeFeature.Value => combo4Text,
                    _ => EnableMovementFeature.Value switch
                    {
                        true when !EnableGliderFeature.Value && !EnableWakeTimeFeature.Value => combo1Text,
                        false when EnableGliderFeature.Value && !EnableWakeTimeFeature.Value => combo2Text,
                        _ => berryText.Text
                    }
                }
            };
            if(EnableMovementFeature.Value && EnableGliderFeature.Value && EnableWakeTimeFeature.Value) berryText.Text = combo7Text;
        }

        private static void UpdatePowerBerries()
        {
            var currentHp = ExternalData.Instance.UserInfo.PlayerMaxHP;
            var startingHp = playSetting.PlayerHP;
            var berryIncrease = playSetting.MisteriousWaterValue;

            currentPowerBerries = (int)Math.Round((currentHp - startingHp) / berryIncrease);
        }

        private static void MovementIncrease()
        {
            playSetting.PlayerWalkSpeed = baseWalkSpeed + walkIncre * currentPowerBerries;
            playSetting.PlayerRunSpeed = baseRunSpeed + runIncre * currentPowerBerries;
            playSetting.PlayerInWeedSpeed = baseInWeedSpeed + inWeedIncre * currentPowerBerries;
            playSetting.PlayerLimitMoveSpeed = baseLimitedSpeed + limitedIncre * currentPowerBerries;
            playSetting.PlayerBazaarLimitMoveSpeed =  baseBazaarSpeed + bazaarIncre * currentPowerBerries;
        }

        private static void JumpIncrease()
        {
            playSetting.FirstJumpHight = baseFirstJumpHeight + velocityIncre * currentPowerBerries;
            playSetting.SecondJumpHight = baseSecondJumpHeight + velocityIncre * currentPowerBerries;
            playSetting.FirstMaxJumpHight = baseFirstMaxJumpHight + heightIncre * currentPowerBerries;
            playSetting.SecondMaxJumpHight = baseSecondMaxJumpHight + heightIncre * currentPowerBerries;
            playSetting.JumpRunSpeed = baseJumpRunSpeed + jumpSpeedIncre * currentPowerBerries;
        }

        private static void GliderMovementIncrease()
        {
            for (var i = 0; i < playSetting.MoveDatas.Count; i++)
            {
                var move = playSetting.MoveDatas[i];

                switch (AgainstWind.Value.ToLower())
                {
                    case "atmax" when currentPowerBerries == BerriesToMax.Value:
                    case "enable":
                        move.GliderMoveSpeed = baseGliderSpeeds[i] + baseWindSpeeds[i] + gliderIncre * currentPowerBerries;
                        move.GliderWindAddVal = 0.0f;
                        break;
                    case "atmax" when  currentPowerBerries < BerriesToMax.Value:
                    case "disable":
                        move.GliderMoveSpeed = baseGliderSpeeds[i] + gliderIncre * currentPowerBerries;
                        break;
                    case "progressive":
                        move.GliderMoveSpeed = baseGliderSpeeds[i] + progressiveWindSpeeds[i] * currentPowerBerries + gliderIncre * currentPowerBerries;
                        move.GliderWindAddVal = baseWindSpeeds[i] - progressiveWindSpeeds[i] * currentPowerBerries;
                        break;
                    default:
                        Log.LogError($"Incorrect config value for AgainstWind: {AgainstWind.Value}");
                        return;
                }

                move.GliderMoveWaitSpeed = move.GliderMoveSpeed / 2;
            }
        }

        private static void WakeTimeChange()
        {
            if (currentPowerBerries >= sleepImproveLevels[0] && currentPowerBerries < sleepImproveLevels[1])
            {
                recoveryList[1].WakeUpTime = "6:45";
                recoveryList[2].WakeUpTime = "7:45";
                recoveryList[3].WakeUpTime = "8:45";
                recoveryList[4].WakeUpTime = "9:45";
                recoveryList[5].WakeUpTime = "9:45";
                recoveryList[6].WakeUpTime = "9:45";
                recoveryList[7].WakeUpTime = "9:45";
            }
            else if (currentPowerBerries >= sleepImproveLevels[1] && currentPowerBerries < sleepImproveLevels[2])
            {
                recoveryList[1].WakeUpTime = "6:30";
                recoveryList[2].WakeUpTime = "7:30";
                recoveryList[3].WakeUpTime = "8:30";
                recoveryList[4].WakeUpTime = "9:30";
                recoveryList[5].WakeUpTime = "9:30";
                recoveryList[6].WakeUpTime = "9:30";
                recoveryList[7].WakeUpTime = "9:30";
            }
            else if (currentPowerBerries >= sleepImproveLevels[2] && currentPowerBerries < BerriesToMax.Value)
            {
                recoveryList[1].WakeUpTime = "6:15";
                recoveryList[2].WakeUpTime = "7:15";
                recoveryList[3].WakeUpTime = "8:15";
                recoveryList[4].WakeUpTime = "9:15";
                recoveryList[5].WakeUpTime = "9:15";
                recoveryList[6].WakeUpTime = "9:15";
                recoveryList[7].WakeUpTime = "9:15";
            }
            else
            {
                recoveryList[1].WakeUpTime = "6:00";
                recoveryList[2].WakeUpTime = "7:00";
                recoveryList[3].WakeUpTime = "8:00";
                recoveryList[4].WakeUpTime = "9:00";
                recoveryList[5].WakeUpTime = "9:00";
                recoveryList[6].WakeUpTime = "9:00";
                recoveryList[7].WakeUpTime = "9:00";
            }
        }

        private static void Output()
        {
            Log.LogInfo("--------Output--------");
            Log.LogInfo("BerryCount:".PadRight(16) + currentPowerBerries.ToString().PadLeft(6));
            Log.LogInfo("MaxHP:".PadRight(16) + playSetting.PlayerMaxHP.ToString().PadLeft(6));
            Log.LogInfo("CurrentMaxHP:".PadRight(16) + ExternalData.Instance.UserInfo.PlayerMaxHP.ToString().PadLeft(6));
            Log.LogInfo("StartHP:".PadRight(16) + playSetting.PlayerHP.ToString().PadLeft(6));
            Log.LogInfo("CurrentWalkSpeed:".PadRight(16) + playSetting.PlayerWalkSpeed.ToString().PadLeft(6));
            Log.LogInfo("CurrentRunSpeed:".PadRight(16) + playSetting.PlayerRunSpeed.ToString().PadLeft(6));
            Log.LogInfo("walkIncre:".PadRight(16) + walkIncre.ToString().PadLeft(6));
            Log.LogInfo("runIncre:".PadRight(16) + runIncre.ToString().PadLeft(6));
            Log.LogInfo("inWeedIncre:".PadRight(16) + inWeedIncre.ToString().PadLeft(6));
            Log.LogInfo("limitedIncre:".PadRight(16) + limitedIncre.ToString().PadLeft(6));
            Log.LogInfo("bazaarIncre:".PadRight(16) + bazaarIncre.ToString().PadLeft(6));
            Log.LogInfo("1stJumpVelocity:".PadRight(16) + playSetting.FirstJumpHight.ToString().PadLeft(6));
            Log.LogInfo("2ndJumpVelocity:".PadRight(16) + playSetting.SecondJumpHight.ToString().PadLeft(6));
            Log.LogInfo("1stJumpHeight:".PadRight(16) + playSetting.FirstMaxJumpHight.ToString().PadLeft(6));
            Log.LogInfo("2ndJumpHeight:".PadRight(16) + playSetting.SecondMaxJumpHight.ToString().PadLeft(6));
            Log.LogInfo("JumpRunSpeed:".PadRight(16) + playSetting.JumpRunSpeed.ToString().PadLeft(6));
            Log.LogInfo("velocityIncre:".PadRight(16) + velocityIncre.ToString().PadLeft(6));
            Log.LogInfo("heightIncre:".PadRight(16) + heightIncre.ToString().PadLeft(6));
            Log.LogInfo("jumpSpeedIncre:".PadRight(16) + jumpSpeedIncre.ToString().PadLeft(6));
            Log.LogInfo("PowerBerryValue:".PadRight(16) + playSetting.MisteriousWaterValue.ToString().PadLeft(6));
            Log.LogInfo("BerriesToMax:".PadRight(16) + BerriesToMax.Value.ToString().PadLeft(6));
            
            foreach(var data in playSetting.MoveDatas) Log.LogInfo("GliderMoveSpeed:".PadRight(16) + data.GliderMoveSpeed.ToString().PadLeft(6));
            foreach(var data in progressiveWindSpeeds) Log.LogInfo("progWindSpeeds:".PadRight(16) + data.ToString().PadLeft(6));
            for (var index = 0; index < 8; index++) Log.LogInfo("WakeTime:".PadRight(16) + recoveryList[index].WakeUpTime.PadLeft(6));
        }
    }
}