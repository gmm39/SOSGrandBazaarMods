using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using Il2CppSystem;
using UnityEngine;

namespace ClothingStallConfigured;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<bool> removeItemRequirement;
    private static ConfigEntry<bool> removeRankRequirement;
    private static ConfigEntry<bool> removeCostRequirement;
    private static ConfigEntry<float> MultiplyCostRequirement;

    public override void Load()
    {
        // Plugin startup logic
        removeItemRequirement = Config.Bind("General", "Remove_Item_Requirement", false,
            "Remove item requirement from clothing stall items");
        removeRankRequirement = Config.Bind("General", "Remove_Rank_Requirement", false,
            "Remove Bazaar rank requirement from clothing stall items (i.e. unlock all clothing stall items");
        removeCostRequirement = Config.Bind("General", "Remove_Cost_Requirement", false,
            "Remove cost requirement from clothing stall items");
        MultiplyCostRequirement = Config.Bind("General", "Multiply_Cost_Requirement", 1.0f,
            "Multiplies cost requirement for clothing stall items");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(ClothingStallPatch));
    }
    
    private static class ClothingStallPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var clothingShopData = MasterDataManager.Instance.ShopAvatarMakeMaster.list;

            if (removeItemRequirement.Value) RemoveItemRequirement(clothingShopData);
            if (removeRankRequirement.Value) RemoveRankRequirement(clothingShopData);
            CostRequirementChange(clothingShopData);
        }

        private static void RemoveItemRequirement(Il2CppSystem.Collections.Generic.List<ShopAvatarMakeMasterData> clothingShopData)
        {
            foreach (var item in clothingShopData)
            {
                for (var i = 0; i < item.RequiredItemId.Count; i++) if (item.RequiredItemId[i] != 0)
                {
                    item.RequiredItemId[i] = 0;
                    item.RequiredItemStack[i] = 0;
                }
            }
        }
        
        private static void RemoveRankRequirement(Il2CppSystem.Collections.Generic.List<ShopAvatarMakeMasterData> clothingShopData)
        {
            foreach (var item in clothingShopData)
            {
                try
                {
                    var ConditionsTypeList = ((Il2CppSystem.Collections.Generic.List<BokuMono.ConditionsType>)
                        (typeof(ShopAvatarMakeMasterData).GetProperty("ConditionsTypeList")?.GetValue(item)));
                    
                    var ConditionsValueList = ((Il2CppSystem.Collections.Generic.List<int>)
                        (typeof(ShopAvatarMakeMasterData).GetProperty("ConditionsValueList")?.GetValue(item)));
                    
                    for (var i = 0; i < ConditionsTypeList.Count; i++)
                    {
                        ConditionsTypeList[i] = 0;
                    }
                    
                    for (var i = 0; i < ConditionsValueList.Count; i++)
                    {
                        ConditionsValueList[i] = 0;
                    }
                }
                catch (System.NullReferenceException e)
                {
                    Log.LogInfo(e.Message);
                    return;
                }
            }
        }
        
        private static void CostRequirementChange(Il2CppSystem.Collections.Generic.List<ShopAvatarMakeMasterData> clothingShopData)
        {
            foreach (var item in clothingShopData)
            {
                if(removeCostRequirement.Value) typeof(ShopAvatarMakeMasterData).GetProperty("Price")?.SetValue(item, 0);
                else
                {
                    try
                    {
                        var priceProp = typeof(ShopAvatarMakeMasterData).GetProperty("Price");
                        if (priceProp != null && priceProp.CanWrite)
                        {
                            priceProp.SetValue(item,
                                (int)(System.Convert.ToInt32(priceProp.GetValue(item)) * MultiplyCostRequirement.Value));
                        }
                    }
                    catch (System.NullReferenceException e)
                    {
                        Log.LogInfo(e.Message);
                        return;
                    }
                    
                }
            }
        }
    }
}