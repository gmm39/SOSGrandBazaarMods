using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using BokuMono.ResidentMission;
using HarmonyLib;
using Il2CppSystem;

namespace ExtraMissions;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(TestPatch));
    }
    
    private static class TestPatch
    {
        private static Random rnd = new();
        
        private static HashSet<uint> finalMissions =
        [
            30104, 30204, 30304, 30404, 30504, 30604, 30704, 30804, 30904, 31004, 31104, 31204, 31304, 31404, 31504,
            31604, 31704, 31804, 31904, 32004, 32104, 32204, 32304, 32404, 32504, 32604, 32704, 32804
        ];
        private static List<MissionManager.OrderData> availableMissions = [];
        private static List<uint> selectedMissions = [];

        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            jsonHandler.JSONTestIn();
        }

        [HarmonyPatch(typeof(MissionManager), "FromSaveData")]
        [HarmonyPostfix]
        private static void FromSaveData(MissionManager __instance)
        {
            Log.LogInfo("FromSaveData");
            
            RefreshAvailableMissions(__instance.OrderDatas);
        }
        
        [HarmonyPatch(typeof(MissionManager), "MissionComplete")]
        [HarmonyPostfix]
        private static void MissionComplete(MissionManager __instance, uint id)
        {
            Log.LogInfo("MissionComplete");

            if (!selectedMissions.Contains(id)) return;
            
            selectedMissions.Remove(id);
            RefreshAvailableMissions(__instance.OrderDatas);
        }
        
        [HarmonyPatch(typeof(MissionManager), "UpdateOnChangeDate")]
        [HarmonyPostfix]
        private static void UpdateOnChangeDate(MissionManager __instance)
        {
            Log.LogInfo("UpdateOnChangeDate");

            GenerateNewMission();
        }

        private static void RefreshAvailableMissions(Il2CppSystem.Collections.Generic.List<MissionManager.OrderData> orderDatas)
        {
            availableMissions.Clear();

            foreach (var item in orderDatas)
            {
                Log.LogInfo($"Id: {item.Id}");
                Log.LogInfo($"State: {item.State}");
                Log.LogInfo($"Name: {item.MissionData.Name}");
                
                if (finalMissions.Contains(item.Id) && item.State is MissionManager.OrderState.Complete or MissionManager.OrderState.OpenShop)
                {
                    availableMissions.Add(item);
                    Log.LogInfo($"Mission {item.Id} added to available missions!");
                }
            }
        }

        private static void GenerateNewMission()
        {
            if (availableMissions.Count == 0) return;
            if (selectedMissions.Count >= 3) return;

            var index = rnd.Next(availableMissions.Count);
            var newMission = availableMissions[index].MissionData;
            availableMissions[index].State = MissionManager.OrderState.Available;
            availableMissions.RemoveAt(index);

            newMission.RewardCategory = RewardsType.Item;
            newMission.RewardId = 111000;
            newMission.RewardNum = 3;
            newMission.RewardQuality = 6;
            newMission.RewardIcon = null;
            
            var requiredItemType = new Il2CppSystem.Collections.Generic.List<RequiredItemType>();
            requiredItemType.Add(RequiredItemType.Item);
            requiredItemType.Add(RequiredItemType.Item);
            requiredItemType.Add(RequiredItemType.Item);
            newMission.RequiredItemType = requiredItemType;
            
            var requiredItemId = new Il2CppSystem.Collections.Generic.List<uint>();
            requiredItemId.Add(110500);
            requiredItemId.Add(0);
            requiredItemId.Add(0);
            newMission.RequiredItemId = requiredItemId;
            
            var requiredItemStack = new Il2CppSystem.Collections.Generic.List<int>();
            requiredItemStack.Add(1);
            requiredItemStack.Add(0);
            requiredItemStack.Add(0);
            newMission.RequiredItemStack = requiredItemStack;
            
            var requiredItemQuality = new Il2CppSystem.Collections.Generic.List<int>();
            requiredItemQuality.Add(-1);
            requiredItemQuality.Add(0);
            requiredItemQuality.Add(0);
            newMission.RequiredItemQuality = requiredItemQuality;
            
            var requiredItemFreshness = new Il2CppSystem.Collections.Generic.List<int>();
            requiredItemFreshness.Add(-1);
            requiredItemFreshness.Add(-1);
            requiredItemFreshness.Add(-1);
            newMission.RequiredItemFreshness = requiredItemFreshness;
            
            var unlockConditions = new Il2CppSystem.Collections.Generic.List<UnlockCondition>();
            unlockConditions.Add(UnlockCondition.None);
            unlockConditions.Add(UnlockCondition.None);
            unlockConditions.Add(UnlockCondition.None);
            newMission.UnlockConditions = unlockConditions;
            
            newMission.ConditionParams = new();
            newMission.CompleteCondition = CompleteCondition.Delivery;
            newMission.CompleteSubCondition = 0;
            newMission.CompleteSubConditionParam = 0;
            newMission.CompleteValue = 0;
            
            selectedMissions.Add(newMission.Id);
            
            foreach(var mission in selectedMissions) Log.LogInfo($"Id: {mission}");
        }
    }

    private static class jsonHandler
    {
        public static void JSONTestOut()
        {
            var toot = new Requests
            {
                Id = 10000,
                Category = RequestCategory.None,
                Items = [new ItemList
                    {
                    Items = [110000, 110100],
                    ItemType = [RequiredItemType.Item, RequiredItemType.Item],
                    Difficulty = 0,
                    PickOne = true
                }, new ItemList
                    {
                    Items = [110200, 110201],
                    ItemType = [RequiredItemType.Item, RequiredItemType.Item],
                    Difficulty = 0,
                    PickOne = true
                }, new ItemList
                    {
                    Items = [111000, 111001, 111002, 111003, 111004, 111005, 111006],
                    ItemType = [RequiredItemType.Item, RequiredItemType.Item, RequiredItemType.Item, RequiredItemType.Item, RequiredItemType.Item, RequiredItemType.Item, RequiredItemType.Item],
                    Difficulty = 1,
                    PickOne = true
                }
                ],
                Special = Special.None,
                Characters = [100, 101, 102, 103, 104, 105],
                MissionName = "Boys love mulch!",
                MissionCaption = "Filthy outdoor items make any guy smile.",
                MissionDescription = "For some reason, every guy\nwants nothing more than junk!",
                DebugInfo = "Test Json"
            };
            
            string jsonPath = Path.Combine(Paths.PluginPath, "Test1.json");
            string json = JsonSerializer.Serialize(toot);
            File.WriteAllText(jsonPath, json);
        }

        public static void JSONTestIn()
        {
            string jsonPath = Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/Test.json");
            var obj = JsonSerializer.Deserialize<Requests>(File.ReadAllText(jsonPath));
            Log.LogInfo("\n" + JsonSerializer.Serialize(obj, new JsonSerializerOptions() { WriteIndented = true }));
        }
    }

    private class Requests
    {
        public uint Id { get; set; }
        public RequestCategory Category { get; set; }
        public List<ItemList> Items { get; set; }
        public Special Special {get; set;}
        public List<uint> Characters { get; set; }
        public string MissionName { get; set; }
        public string MissionCaption { get; set; }
        public string MissionDescription { get; set; }
        public string DebugInfo { get; set; }
    }

    private class ItemList
    {
        public List<uint> Items { get; set; }
        public List<RequiredItemType> ItemType { get; set; }
        public uint Difficulty { get; set; }
        public bool PickOne { get; set; }
    }

    private enum RequestCategory
    {
        None = 0,
        AnimalProduct,
        Bug,
        Crop,
        Fish,
        Flower,
        Food,
        Forage,
        Gemstone,
        Gift,
        Ingredient,
        Other,
        Mineral,
        Misc,
        Seed
    }
    
    private enum Special
    {
        None = 0,
        Spring,
        Summer,
        Autumn,
        Winter,
        FlowerFestival,
        AnimalShow,
        HoneyDay,
        CropsShow,
        TeaParty,
        PetShow,
        HorseDerby,
        CookOff,
        JuiceFestival,
        PumpkinFestival,
        HearthDay,
        StarlightNight,
        NewYears,
        BirthdayJules,
        BirthdayDerek,
        BirthdayLloyd,
        BirthdayGabriel,
        BirthdaySamir,
        BirthdayArata,
        BirthdaySophie,
        BirthdayJune,
        BirthdayFreya,
        BirthdayMaple,
        BirthdayKagetsu,
        BirthdayDiana,
        BirthdayFelix,
        BirthdayErik,
        BirthdayStuart,
        BirthdaySonia,
        BirthdayMadeleine,
        BirthdayMina,
        BirthdayWilbur,
        BirthdayClara,
        BirthdayKevin,
        BirthdayIsaac,
        BirthdayNadine,
        BirthdaySylvia,
        BirthdayLaurie,
        BirthdayMiguel,
        BirthdayHarold,
        BirthdaySherene
    }
}