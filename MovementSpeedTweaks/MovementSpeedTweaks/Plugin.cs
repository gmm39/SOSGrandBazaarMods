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
    
    // Glide Tweaks
    private static ConfigEntry<bool> EnableGlideTweaks;
    private static ConfigEntry<bool> RemoveAgainstWind;
    private static ConfigEntry<float> GlideMoveSpeed;
    
    public override void Load()
    {
        // Movement Tweaks
        PlayerWalkSpeed = Config.Bind("MovementSettings", "Walk_Speed", 1.0f,
            "Enter Player walk speed.");
        PlayerRunSpeed = Config.Bind("MovementSettings", "Run_Speed", 5.0f,
            "Enter Player run speed.");
        PlayerInWeedSpeed = Config.Bind("MovementSettings", "In_Weed_Speed", 3.0f,
            "Enter Player speed in weeds.");
        PlayerLimitMoveSpeed = Config.Bind("MovementSettings", "Limit_Move_Speed", 2.3f,
            "Enter Player speed limit during times of limited movement.");
        PlayerBazaarLimitMoveSpeed = Config.Bind("MovementSettings", "Bazaar_Limit_Move_Speed", 4.0f,
            "Enter Player speed limit during the bazaar.");
        
        EnableGlideTweaks = Config.Bind("GlideSettings", "Enable_Glide_Tweaks", false,
            "Enable changes to the glide settings. Must be 'true' for the following glide settings to be applied");
        RemoveAgainstWind = Config.Bind("GlideSettings", "Remove_Against_Wind_Penalty", false,
            "Remove the penalty for gliding into the wind. " +
            "Also removes wind boost. Compensate by changing glide speed below.");
        GlideMoveSpeed = Config.Bind("GlideSettings", "Glide_Move_Speed", 4.5f,
            "Enter glide speed for all wind levels.");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(TestPatch1));
    }
    
    private static class TestPatch1
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

            if (!EnableGlideTweaks.Value) return;
            
            foreach (var move in playSetting.MoveDatas)
            {
                move.GliderMoveSpeed = GlideMoveSpeed.Value;
                move.GliderMoveWaitSpeed = GlideMoveSpeed.Value / 2;
                if(RemoveAgainstWind.Value) move.GliderWindAddVal = 0.0f;
            }
        } 
    }
}