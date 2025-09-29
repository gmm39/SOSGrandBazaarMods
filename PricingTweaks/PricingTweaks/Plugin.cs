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

    private static ConfigEntry<float> AnimalBazaarPriceMulti;
    private static ConfigEntry<float> ExpansionPriceMulti;
    private static ConfigEntry<float> DecorPriceMulti;

    public override void Load()
    {
        // Plugin startup logic
        Enable_01_ANIMALS = Config.Bind("-----00 FEATURE SELECT-----", "Enable_01_ANIMALS", true,
            "Enable tweaks in 01 ANIMALS");
        Enable_02_EXPANSIONS = Config.Bind("-----00 FEATURE SELECT-----", "Enable_02_EXPANSIONS", true,
            "Enable tweaks in 02 EXPANSIONS");
        Enable_03_DECOR = Config.Bind("-----00 FEATURE SELECT-----", "Enable_02_DECOR", true,
            "Enable tweaks in 03 DECOR");

        AnimalBazaarPriceMulti = Config.Bind("-----01 ANIMALS-----", "Animal_Bazaar_Price_Multiplier", 1.0f,
            "Multiplies the bazaar animal prices by the given amount.");
        ExpansionPriceMulti = Config.Bind("-----02 EXPANSIONS-----", "Expansion_Price_Multiplier", 1.0f,
            "Multiplies the expansion prices by the given amount.");
        DecorPriceMulti = Config.Bind("-----03 DECOR-----", "Decor_Price_Multiplier", 1.0f,
            "Multiplies the decor prices by the given amount.");

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
    }
}