using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace MovementSpeedTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    
    // Movement Tweaks
    private static ConfigEntry<float> PlayerWalkSpeed;
    private static ConfigEntry<float> PlayerRunSpeed;
    private static ConfigEntry<float> PlayerInWeedSpeed;
    private static ConfigEntry<float> PlayerLimitMoveSpeed;
    private static ConfigEntry<float> PlayerBazaarLimitMoveSpeed;
    
    // Jump Tweaks
    private static ConfigEntry<float> FirstJumpVerticalVelocity;
    private static ConfigEntry<float> SecondJumpVerticalVelocity;
    private static ConfigEntry<float> FirstJumpMaxHeight;
    private static ConfigEntry<float> SecondJumpMaxHeight;
    private static ConfigEntry<float> JumpRunSpeed;
    
    // Glide Tweaks
    private static ConfigEntry<bool> EnableGlideTweaks;
    private static ConfigEntry<bool> RemoveAgainstWind;
    private static ConfigEntry<float> GlideMoveSpeedBoost;
    
    public override void Load()
    {
        // Movement Tweaks
        PlayerWalkSpeed = Config.Bind("-----01 MOVEMENT SETTINGS-----", "Walk_Speed", 1.0f,
            "Enter Player walk speed.");
        PlayerRunSpeed = Config.Bind("-----01 MOVEMENT SETTINGS-----", "Run_Speed", 5.0f,
            "Enter Player run speed.");
        PlayerInWeedSpeed = Config.Bind("-----01 MOVEMENT SETTINGS-----", "In_Weed_Speed", 3.0f,
            "Enter Player speed in weeds.");
        PlayerLimitMoveSpeed = Config.Bind("-----01 MOVEMENT SETTINGS-----", "Limit_Move_Speed", 2.3f,
            "Enter Player speed limit during times of limited movement.");
        PlayerBazaarLimitMoveSpeed = Config.Bind("-----01 MOVEMENT SETTINGS-----", "Bazaar_Limit_Move_Speed", 4.0f,
            "Enter Player speed limit during the bazaar.");
        
        // Jump Tweaks
        FirstJumpVerticalVelocity = Config.Bind("-----02 JUMP SETTINGS-----", "First_Jump_Vertical_Velocity", 4.5f,
            "Enter vertical jump velocity for first jump.");
        SecondJumpVerticalVelocity = Config.Bind("-----02 JUMP SETTINGS-----", "Second_Jump_Vertical_Velocity", 5.0f,
            "Enter vertical jump velocity for second (double) jump.");
        FirstJumpMaxHeight = Config.Bind("-----02 JUMP SETTINGS-----", "First_Jump_Max_Height", 1.0f,
            "Enter max jump height for first jump.");
        SecondJumpMaxHeight = Config.Bind("-----02 JUMP SETTINGS-----", "Second_Jump_Max_Height", 1.27f,
            "Enter max jump height for second (double) jump.");
        JumpRunSpeed = Config.Bind("-----02 JUMP SETTINGS-----", "Jump_Run_Speed", 6.2f,
            "Enter movement speed while jumping.");
        
        // Glide Tweaks
        EnableGlideTweaks = Config.Bind("-----03 GLIDE SETTINGS-----", "Enable_Glide_Tweaks", false,
            "Enable changes to the glide settings. Must be 'true' for the following glide settings to be applied");
        RemoveAgainstWind = Config.Bind("-----03 GLIDE SETTINGS-----", "Remove_Against_Wind_Penalty", false,
            "Remove the penalty for gliding into the wind.");
        GlideMoveSpeedBoost = Config.Bind("-----03 GLIDE SETTINGS-----", "Glide_Move_Speed_Boost", 0f,
            "Boosts glide speed by given amount.");
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(MovementPatch));
    }
    
    private static class MovementPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var playSetting = SettingAssetManager.Instance.PlayerSetting;
            
            // Movement Tweaks
            playSetting.PlayerWalkSpeed = PlayerWalkSpeed.Value;
            playSetting.PlayerRunSpeed = PlayerRunSpeed.Value;
            playSetting.PlayerInWeedSpeed = PlayerInWeedSpeed.Value;
            playSetting.PlayerLimitMoveSpeed = PlayerLimitMoveSpeed.Value;
            playSetting.PlayerBazaarLimitMoveSpeed = PlayerBazaarLimitMoveSpeed.Value;

            // Jump Tweaks
            playSetting.FirstJumpHight = FirstJumpVerticalVelocity.Value;
            playSetting.SecondJumpHight = SecondJumpVerticalVelocity.Value;
            playSetting.FirstMaxJumpHight = FirstJumpMaxHeight.Value;
            playSetting.SecondMaxJumpHight = SecondJumpMaxHeight.Value;
            playSetting.JumpRunSpeed = JumpRunSpeed.Value;
            
            // Glide Tweaks
            if (!EnableGlideTweaks.Value) return;
            
            foreach (var move in playSetting.MoveDatas)
            {
                if (RemoveAgainstWind.Value)
                {
                    move.GliderMoveSpeed += move.GliderWindAddVal + GlideMoveSpeedBoost.Value;
                    move.GliderWindAddVal = 0.0f;
                }
                else
                {
                    move.GliderMoveSpeed += GlideMoveSpeedBoost.Value;
                }
                
                move.GliderMoveWaitSpeed = move.GliderMoveSpeed / 2;
            }
        }
    }
}