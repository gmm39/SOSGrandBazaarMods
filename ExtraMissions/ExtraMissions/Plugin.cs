using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using BokuMono.ResidentMission;
using HarmonyLib;
using Il2CppSystem;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;

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
        private static string requestGroupsPath =
            Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/data/RequestGroups.json");
        
        private static List<RequestGroups> requestGroups;
        
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
            requestGroups = jsonHandler.Read(requestGroupsPath);
        }

        [HarmonyPatch(typeof(MissionManager), "FromSaveData")]
        [HarmonyPostfix]
        private static void FromSaveData(MissionManager __instance)
        {
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

            var newRequest = ChooseRequest(newMission.CharaId);
            
            
            
            
            
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
            
            foreach(var mission in selectedMissions) Log.LogInfo($"SelectedId: {mission}");
        }

        private static RequestGroups ChooseRequest(uint charaId)
        {
            var possibleRequests = requestGroups.Where(x => x.Characters.Contains(0) || x.Characters.Contains(charaId)).ToList();
            
            SpecialCheck(possibleRequests);
            
            foreach(var request in possibleRequests) Log.LogInfo($"ChosenRequestIds: {request.Id}");

            return possibleRequests[rnd.Next(possibleRequests.Count)];
        }

        private static void SpecialCheck(List<RequestGroups> requests)
        {
            var today = DateManager.Instance.Now;
            
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                switch (request.Special)
                {
                    case Special.None:
                        break;
                    
                    case Special.Spring:
                        if (today.Season != BokuMonoSeason.Spring) requests.RemoveAt(i);
                        break;
                    
                    case Special.Summer:
                        if (today.Season != BokuMonoSeason.Summer) requests.RemoveAt(i);
                        break;
                    
                    case Special.Autumn:
                        if (today.Season != BokuMonoSeason.Autumn) requests.RemoveAt(i);
                        break;
                    
                    case Special.Winter:
                        if (today.Season != BokuMonoSeason.Winter) requests.RemoveAt(i);
                        break;
                    
                    case Special.FlowerFestival:
                        var flowerFest = new BokuMonoDateTime(today.Year, 1, 11);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, flowerFest) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.AnimalShow:
                        BokuMonoDateTime animalShow;
                        switch (today.Season)
                        {
                            case BokuMonoSeason.Spring:
                                animalShow = new BokuMonoDateTime(today.Year, 1, 16);
                                break;
                            case BokuMonoSeason.Summer:
                                animalShow = new BokuMonoDateTime(today.Year, 2, 11);
                                break;
                            case BokuMonoSeason.Autumn:
                                animalShow = new BokuMonoDateTime(today.Year, 3, 15);
                                break;
                            case BokuMonoSeason.Winter:
                                animalShow = new BokuMonoDateTime(today.Year, 4, 12);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        if ((today is { Season: BokuMonoSeason.Spring, Year: 1 }) || 
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, animalShow) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.HoneyDay:
                        var honeyDay = new BokuMonoDateTime(today.Year, 1, 21);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, honeyDay) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.CropsShow:
                        BokuMonoDateTime cropsShow;
                        switch (today.Season)
                        {
                            case BokuMonoSeason.Spring:
                                cropsShow = new BokuMonoDateTime(today.Year, 1, 26);
                                break;
                            case BokuMonoSeason.Summer:
                                cropsShow = new BokuMonoDateTime(today.Year, 2, 21);
                                break;
                            case BokuMonoSeason.Autumn:
                                cropsShow = new BokuMonoDateTime(today.Year, 3, 25);
                                break;
                            case BokuMonoSeason.Winter:
                                cropsShow = new BokuMonoDateTime(today.Year, 4, 22);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        if ((today is { Season: BokuMonoSeason.Spring, Year: 1 }) || 
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, cropsShow) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.TeaParty:
                        var teaParty = new BokuMonoDateTime(today.Year, 1, 27);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, teaParty) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.PetShow:
                        BokuMonoDateTime petShow;
                        switch (today.Season)
                        {
                            case BokuMonoSeason.Summer:
                                petShow = new BokuMonoDateTime(today.Year, 2, 3);
                                break;
                            case BokuMonoSeason.Winter:
                                petShow = new BokuMonoDateTime(today.Year, 4, 4);
                                break;
                            case BokuMonoSeason.Spring:
                            case BokuMonoSeason.Autumn:
                                return;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, petShow) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.HorseDerby:
                        BokuMonoDateTime horseDerby;
                        switch (today.Season)
                        {
                            case BokuMonoSeason.Summer:
                                horseDerby = new BokuMonoDateTime(today.Year, 2, 16);
                                break;
                            case BokuMonoSeason.Winter:
                                horseDerby = new BokuMonoDateTime(today.Year, 4, 17);
                                break;
                            case BokuMonoSeason.Spring:
                            case BokuMonoSeason.Autumn:
                                return;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, horseDerby) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.CookOff:
                        var cookOff = new BokuMonoDateTime(today.Year, 3, 4);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, cookOff) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.JuiceFestival:
                        var juiceFestival = new BokuMonoDateTime(today.Year, 3, 21);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, juiceFestival) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.PumpkinFestival:
                        var pumpkinFestival = new BokuMonoDateTime(today.Year, 3, 28);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, pumpkinFestival) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.HearthDay:
                        var hearthDay = new BokuMonoDateTime(today.Year, 4, 9);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, hearthDay) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.StarlightNight:
                        var starlightNight = new BokuMonoDateTime(today.Year, 4, 25);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, starlightNight) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.NewYears:
                        var newYears = new BokuMonoDateTime(today.Year, 4, 31);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, newYears) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayJules:
                        var birthdayJules = new BokuMonoDateTime(today.Year, 3, 20);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayJules) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayDerek:
                        var birthdayDerek = new BokuMonoDateTime(today.Year, 2, 12);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayDerek) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayLloyd:
                        var birthdayLloyd = new BokuMonoDateTime(today.Year, 3, 3);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayLloyd) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayGabriel:
                        var birthdayGabriel = new BokuMonoDateTime(today.Year, 4, 28);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayGabriel) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdaySamir:
                        var birthdaySamir = new BokuMonoDateTime(today.Year, 4, 26);
                        if (MasterDataManager.Instance.ConditionMaster.IsSatisfied(ConditionMasterId.Condition_910215) &&
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySamir) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.BirthdayArata:
                        var birthdayArata = new BokuMonoDateTime(today.Year, 4, 17);
                        if (MasterDataManager.Instance.ConditionMaster.IsSatisfied(ConditionMasterId.Condition_910109) &&
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayArata) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.BirthdaySophie:
                        var birthdaySophie = new BokuMonoDateTime(today.Year, 1, 16);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySophie) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayJune:
                        var birthdayJune = new BokuMonoDateTime(today.Year, 4, 12);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayJune) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayFreya:
                        var birthdayFreya = new BokuMonoDateTime(today.Year, 3, 25);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayFreya) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayMaple:
                        var birthdayMaple = new BokuMonoDateTime(today.Year, 3, 14);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMaple) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayKagetsu:
                        var birthdayKagetsu = new BokuMonoDateTime(today.Year, 1, 5);
                        if (MasterDataManager.Instance.ConditionMaster.IsSatisfied(ConditionMasterId.Condition_910514) &&
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayKagetsu) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.BirthdayDiana:
                        var birthdayDiana = new BokuMonoDateTime(today.Year, 3, 22);
                        if (MasterDataManager.Instance.ConditionMaster.IsSatisfied(ConditionMasterId.Condition_910109) &&
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayDiana) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.BirthdayFelix:
                        var birthdayFelix = new BokuMonoDateTime(today.Year, 2, 1);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayFelix) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayErik:
                        var birthdayErik = new BokuMonoDateTime(today.Year, 1, 20);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayErik) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayStuart:
                        var birthdayStuart = new BokuMonoDateTime(today.Year, 3, 11);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayStuart) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdaySonia:
                        var birthdaySonia = new BokuMonoDateTime(today.Year, 1, 25);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySonia) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayMadeleine:
                        var birthdayMadeleine = new BokuMonoDateTime(today.Year, 1, 18);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMadeleine) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayMina:
                        var birthdayMina = new BokuMonoDateTime(today.Year, 4, 4);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMina) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayWilbur:
                        var birthdayWilbur = new BokuMonoDateTime(today.Year, 2, 3);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayWilbur) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayClara:
                        var birthdayClara = new BokuMonoDateTime(today.Year, 3, 29);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayClara) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayKevin:
                        var birthdayKevin = new BokuMonoDateTime(today.Year, 2, 25);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayKevin) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayIsaac:
                        var birthdayIsaac = new BokuMonoDateTime(today.Year, 3, 6);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayIsaac) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayNadine:
                        var birthdayNadine = new BokuMonoDateTime(today.Year, 2, 30);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayNadine) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdaySylvia:
                        var birthdaySylvia = new BokuMonoDateTime(today.Year, 3, 18);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySylvia) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayLaurie:
                        var birthdayLaurie = new BokuMonoDateTime(today.Year, 3, 18);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayLaurie) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayMiguel:
                        var birthdayMiguel = new BokuMonoDateTime(today.Year, 2, 21);
                        if (!(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMiguel) is < 7 and > 0)) requests.RemoveAt(i);
                        break;
                    
                    case Special.BirthdayHarold:
                        var birthdayHarold = new BokuMonoDateTime(today.Year, 2, 7);
                        if (MasterDataManager.Instance.ConditionMaster.IsSatisfied(ConditionMasterId.Condition_910109) &&
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayHarold) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    case Special.BirthdaySherene:
                        var birthdaySherene = new BokuMonoDateTime(today.Year, 2, 16);
                        if (MasterDataManager.Instance.ConditionMaster.IsSatisfied(ConditionMasterId.Condition_910216) &&
                            !(BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySherene) is < 7 and > 0))
                        {
                            requests.RemoveAt(i);
                        }
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    private static class jsonHandler
    {
        public static void Write()
        {
            var toot = new RequestGroups
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

        public static List<RequestGroups> Read(string jsonPath)
        {
            return File.Exists(jsonPath)
                ? JsonSerializer.Deserialize<List<RequestGroups>>(File.ReadAllText(jsonPath))
                : null;
        }
    }

    private class RequestGroups
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