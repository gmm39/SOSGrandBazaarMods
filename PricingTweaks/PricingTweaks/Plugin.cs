using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using Il2CppSystem;

namespace PricingTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    private static ConfigEntry<bool> Enable_01_ANIMALS;
    private static ConfigEntry<bool> Enable_02_EXPANSIONS;
    private static ConfigEntry<bool> Enable_03_DECOR;
    private static ConfigEntry<bool> Enable_04_CLOTHING;
    private static ConfigEntry<bool> Enable_05_ITEMS;

    private static ConfigEntry<float> AnimalBazaarPriceMulti;
    private static ConfigEntry<float> ExpansionPriceMulti;
    private static ConfigEntry<float> DecorPriceMulti;
    private static ConfigEntry<float> ClothingPriceMulti;
    private static ConfigEntry<float> ShopBuyPriceMulti;
    private static ConfigEntry<float> ShopSalePriceMulti;
    private static ConfigEntry<float> BazaarSalePriceMulti;
    private static ConfigEntry<float> TrendPriceMulti;

    public override void Load()
    {
        // Plugin startup logic
        Enable_01_ANIMALS = Config.Bind("-----00 FEATURE SELECT-----", "Enable_01_ANIMALS", true,
            "Enable tweaks in 01 ANIMALS");
        Enable_02_EXPANSIONS = Config.Bind("-----00 FEATURE SELECT-----", "Enable_02_EXPANSIONS", true,
            "Enable tweaks in 02 EXPANSIONS");
        Enable_03_DECOR = Config.Bind("-----00 FEATURE SELECT-----", "Enable_03_DECOR", true,
            "Enable tweaks in 03 DECOR");
        Enable_04_CLOTHING = Config.Bind("-----00 FEATURE SELECT-----", "Enable_04_CLOTHING", true,
            "Enable tweaks in 04 CLOTHING");
        Enable_05_ITEMS = Config.Bind("-----00 FEATURE SELECT-----", "Enable_05_ITEMS", true,
            "Enable tweaks in 05 ITEMS");

        AnimalBazaarPriceMulti = Config.Bind("-----01 ANIMALS-----", "Animal_Bazaar_Price_Multiplier", 1.0f,
            "Multiplies bazaar animal prices by the given amount.");
        ExpansionPriceMulti = Config.Bind("-----02 EXPANSIONS-----", "Expansion_Price_Multiplier", 1.0f,
            "Multiplies the expansion prices by the given amount.");
        DecorPriceMulti = Config.Bind("-----03 DECOR-----", "Decor_Price_Multiplier", 1.0f,
            "Multiplies decor prices by the given amount.");
        ClothingPriceMulti = Config.Bind("-----04 CLOTHING-----", "Clothing_Price_Multiplier", 1.0f,
            "Multiplies clothing prices by the given amount.");
        
        ShopBuyPriceMulti = Config.Bind("-----05 ITEMS-----", "Shop_Buy_Price_Multiplier", 1.4f,
            "Multiplies item prices when buying from a shop by the given amount. GameDefault: 1.4");
        ShopSalePriceMulti = Config.Bind("-----05 ITEMS-----", "Shop_Sale_Price_Multiplier", 1.0f,
            "Multiplies item prices when selling to a shop by the given amount.");
        BazaarSalePriceMulti = Config.Bind("-----05 ITEMS-----", "Bazaar_Sale_Price_Multiplier", 1.0f,
            "Multiplies item prices when selling at the bazaar by the given amount.");
        TrendPriceMulti = Config.Bind("-----05 ITEMS-----", "Trend_Price_Multiplier", 1.3f,
            "Multiplies item prices when buying from a shop by the given amount. GameDefault: 1.3");

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(PriceTweaks));
    }

    private static class PriceTweaks
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (Enable_01_ANIMALS.Value) AnimalPriceTweak();
            if (Enable_02_EXPANSIONS.Value) ExpansionPriceTweak();
            if (Enable_03_DECOR.Value) DecorStallPriceTweak();
            if (Enable_04_CLOTHING.Value) ClothingPriceTweak();
            if (Enable_05_ITEMS.Value)
            {
                var gSetting = SettingAssetManager.Instance.GameSetting;
                
                gSetting.BuyPriceMagnification = ShopBuyPriceMulti.Value;
                gSetting.SaleShopPriceMagnification = ShopSalePriceMulti.Value;
                gSetting.SaleBazaarPriceMagnification = BazaarSalePriceMulti.Value;
                gSetting.SaleTrendPriceMagnification = TrendPriceMulti.Value;
            }
        }

        private static void AnimalPriceTweak()
        {
            foreach (var animal in MasterDataManager.Instance.ShopAnimalMaster.list)
            {
                try
                {
                    var price = (int)typeof(ShopAnimalMasterData).GetProperty("Price")?.GetValue(animal);

                    typeof(ShopAnimalMasterData).GetProperty("Price")
                        ?.SetValue(animal, (int)(price * AnimalBazaarPriceMulti.Value));
                }
                catch (System.NullReferenceException e)
                {
                    Log.LogInfo(e.Message);
                    return;
                }
            }
        }

        private static void ExpansionPriceTweak()
        {
            foreach (var expansion in MasterDataManager.Instance.ExpansionMaster.list)
            {
                expansion.Price = (int)(expansion.Price * ExpansionPriceMulti.Value);
            }
        }

        private static void DecorStallPriceTweak()
        {
            var decorShopIds = new HashSet<uint>();
            foreach (var item in MasterDataManager.Instance.ShopItemMaster.list.FindAll(
                         (Predicate<ShopItemMasterData>)(x => x.Category == ShopCategory.Shop20))) decorShopIds.Add(item.ItemId);

            foreach (var item in MasterDataManager.Instance.ItemMasterData)
                if (decorShopIds.Contains(item.Id))
                    item.Price = (int)(item.Price * DecorPriceMulti.Value);
        }
        
        private static void ClothingPriceTweak()
        {
            foreach (var item in MasterDataManager.Instance.ShopAvatarMakeMaster.list)
            {
                try
                {
                    var priceProp = typeof(ShopAvatarMakeMasterData).GetProperty("Price");
                    if (priceProp != null && priceProp.CanWrite)
                    {
                        priceProp.SetValue(item,
                            (int)(System.Convert.ToInt32(priceProp.GetValue(item)) * ClothingPriceMulti.Value));
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