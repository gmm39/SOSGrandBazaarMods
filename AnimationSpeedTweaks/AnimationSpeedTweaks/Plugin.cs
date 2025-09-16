using System.Numerics;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace AnimationSpeedTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    
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

        Harmony.CreateAndPatchAll(typeof(AnimationPatch));
    }

    private void LoadConfig()
    {
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
    
    private static class AnimationPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var playSetting = SettingAssetManager.Instance.PlayerSetting;
            
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
        } 
    }
}