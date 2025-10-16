using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Animal;
using BokuMono.Data;
using BokuMono.ResidentMission;
using HarmonyLib;
using Il2CppSystem;
using AnimalType = BokuMono.Data.AnimalType;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;
using NullReferenceException = System.NullReferenceException;

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
        private static readonly Random Rnd;
        private static readonly string RequestGroupsPath;
        private static readonly string RewardGroupsPath;
        
        private static List<RequestGroups> _requestGroups;
        private static List<RewardGroups> _rewardGroups;

        private static readonly HashSet<uint> FinalMissions;
        private static List<MissionManager.OrderData> _availableMissions;
        private static List<ResidentMissionMasterData> _selectedMissions;

        private const int MAX_REQ_COUNT = 3;
        

        static TestPatch()
        {
            Rnd = new Random();
            
            RequestGroupsPath = Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/data/RequestGroups.json");
            RewardGroupsPath = Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/data/RewardGroups.json");
            
            FinalMissions = [
                30104, 30204, 30304, 30404, 30504, 30604, 30704, 30804, 30904, 31004, 31104, 31204, 31304, 31404, 31504,
                31604, 31704, 31804, 31904, 32004, 32104, 32204, 32304, 32404, 32504, 32604, 32704, 32804
            ];
            _availableMissions = [];
            _selectedMissions = [];
        }

        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            _requestGroups = jsonHandler.Read<List<RequestGroups>>(RequestGroupsPath);
            _rewardGroups = jsonHandler.Read<List<RewardGroups>>(RewardGroupsPath);
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

            if (_selectedMissions.All(x => x.Id != id)) return;
            
            _selectedMissions.RemoveAll(x => x.Id == id);
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
            _availableMissions.Clear();

            foreach (var item in orderDatas)
            {
                if (FinalMissions.Contains(item.Id) && item.State is MissionManager.OrderState.Complete or MissionManager.OrderState.OpenShop)
                {
                    _availableMissions.Add(item);
                    Log.LogInfo($"Mission {item.Id} added to available missions!");
                }
            }
        }

        private static void GenerateNewMission()
        {
            if (_availableMissions.Count == 0) return;
            if (_selectedMissions.Count >= MAX_REQ_COUNT) return;

            // Get random mission from available missions
            var index = Rnd.Next(_availableMissions.Count);
            var mission = _availableMissions[index].MissionData;
            _availableMissions[index].State = MissionManager.OrderState.Available;
            _availableMissions.RemoveAt(index);
            
            var request = ChooseRequest(mission.CharaId);
            Log.LogInfo(JsonSerializer.Serialize(request, new JsonSerializerOptions() { WriteIndented = true }));
            ApplyRequestGroup(mission, request);
            
            _selectedMissions.Add(mission);
        }

        private static MissionInfo ChooseRequest(uint charaId)
        {
            var activeSpecials = ActiveSpecials();
            var possibleRequests = _requestGroups.Where(x =>
                    (x.Characters.Contains(0) || x.Characters.Contains(charaId)) && activeSpecials.Contains(x.Special))
                .ToList();

            var groupIndex = Rnd.Next(possibleRequests.Count);
            var itemGroupIndex = Rnd.Next(possibleRequests[groupIndex].Items.Count);

            var tempItemList = possibleRequests[groupIndex].Items[itemGroupIndex];
            
            var itemIds = new List<uint>();
            var itemTypes = new List<RequiredItemType>();
            var itemStack = new List<int>();
            var itemQuality = new List<int>();
            
            // Calculate item list
            if (tempItemList.PickOne)
            {
                var itemIndex = Rnd.Next(tempItemList.ItemIds.Count);

                itemIds.Add(tempItemList.ItemIds[itemIndex]);
                itemIds.Add(0);
                itemIds.Add(0);
                itemTypes.Add(tempItemList.ItemType[itemIndex]);
                itemTypes.Add(RequiredItemType.Item);
                itemTypes.Add(RequiredItemType.Item);
                itemStack.Add(tempItemList.ItemStack[itemIndex]);
                itemStack.Add(0);
                itemStack.Add(0);
                itemQuality.Add(tempItemList.ItemQuality[itemIndex]);
                itemQuality.Add(0);
                itemQuality.Add(0);
            }
            else
            {
                itemIds = tempItemList.ItemIds;
                itemTypes = tempItemList.ItemType;
                itemStack = tempItemList.ItemStack;
                itemQuality = tempItemList.ItemQuality;
            }
            
            // Calculate reward
            var rewardGroup = _rewardGroups.Find(x => x.Category == possibleRequests[groupIndex].Category);
            var rewardItemIndex = Rnd.Next(rewardGroup.ItemIds.Count);
            
            var rewardItemId = rewardGroup.ItemIds[rewardItemIndex];
            var rewardItemStack = rewardGroup.ItemStack[rewardItemIndex];
            var rewardItemQuality = rewardGroup.ItemQuality[rewardItemIndex];

            // Calculate Difficulty
            if (tempItemList.DifficultChance > Rnd.NextDouble())
            {
                for (var i = 0; i < itemIds.Count; i++)
                {
                    if(itemStack[i] != 0)
                        itemStack[i] = (int)Math.Round(itemStack[i] * 2.0f);
                    if(itemQuality[i] > 0)
                        itemQuality[i] = Math.Clamp(itemQuality[i] + 2, 1, 14);
                }

                rewardItemStack = (int)Math.Round(rewardItemStack * 2.0f);
                rewardItemQuality = Math.Clamp(rewardItemQuality + 2, 1, 14);
            }
            
            return new MissionInfo
            {
                RequestGroupId = possibleRequests[groupIndex].Id,
                RewardGroupId = rewardGroup.Id,
                Category = possibleRequests[groupIndex].Category,
                ItemId = itemIds,
                ItemType = itemTypes,
                ItemStack = itemStack,
                ItemQuality = itemQuality,
                RewardItemId = rewardItemId,
                RewardItemStack = rewardItemStack,
                RewardItemQuality = rewardItemQuality,
                Special = possibleRequests[groupIndex].Special,
                MissionName = possibleRequests[groupIndex].MissionName,
                MissionCaption = possibleRequests[groupIndex].MissionCaption,
                MissionDescription = possibleRequests[groupIndex].MissionDescription,
                DebugInfo = possibleRequests[groupIndex].DebugInfo
            };
        }
        
        private static HashSet<Special> ActiveSpecials()
        {
            var today = (DateManager.Instance.Now).AddDays(1);  // Function is called before the true date is change so add day
            var active = new HashSet<Special>() { Special.None };
            
            var fests = FestivalExecutor.Instance.m_Festivals;
            var likeManager = LikeabilityManager.Instance;
            
            // Season Checks
            if (today.Season is BokuMonoSeason.Winter && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year + 1, 1, 1)) is < 5 and >= 0))
                active.Add(Special.Spring);
            
            if (today.Season is BokuMonoSeason.Spring && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 1, 31)) >= 5))
                active.Add(Special.Spring);
            
            if (today.Season is BokuMonoSeason.Spring && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 2, 1)) is < 5 and >= 0))
                active.Add(Special.Summer);
            
            if (today.Season is BokuMonoSeason.Summer && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 2, 31)) is >= 5))
                active.Add(Special.Summer);
            
            if (today.Season is BokuMonoSeason.Summer && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 3, 1)) is < 5 and >= 0))
                active.Add(Special.Autumn);
            
            if (today.Season is BokuMonoSeason.Autumn && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 3, 31)) is >= 5))
                active.Add(Special.Autumn);
                    
            if (today.Season is BokuMonoSeason.Autumn && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 4, 1)) is < 5 and >= 0))
                active.Add(Special.Winter);
            
            if (today.Season is BokuMonoSeason.Winter && (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 4, 31)) is >= 5))
                active.Add(Special.Winter);
            
            // Festival Checks
            var flowerFest = fests[FestivalExecutor.FestivalCategory.Flower].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, flowerFest) is < 7 and >= 0)
                active.Add(Special.FlowerFestival);
            
            BokuMonoDateTime animalShow;
            switch (today.Season)
            {
                case BokuMonoSeason.Spring:
                    animalShow = fests[FestivalExecutor.FestivalCategory.CompetitionAnimal_Spring].Schedule.GetOpenDate();
                    break;
                case BokuMonoSeason.Summer:
                    animalShow = fests[FestivalExecutor.FestivalCategory.CompetitionAnimal_Summer].Schedule.GetOpenDate();
                    break;
                case BokuMonoSeason.Autumn:
                    animalShow = fests[FestivalExecutor.FestivalCategory.CompetitionAnimal_Autumn].Schedule.GetOpenDate();
                    break;
                case BokuMonoSeason.Winter:
                    animalShow = fests[FestivalExecutor.FestivalCategory.CompetitionAnimal_Winter].Schedule.GetOpenDate();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if ((today is not { Season: BokuMonoSeason.Spring, Year: 1 }) && (BokuMonoDateTimeUtility.GetElapsedDays(today, animalShow) is < 7 and >= 0)) 
                active.Add(Special.AnimalShow);
                    
            var honeyDay = fests[FestivalExecutor.FestivalCategory.MyHoney].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, honeyDay) is < 7 and >= 0)
                active.Add(Special.HoneyDay);

            BokuMonoDateTime cropsShow;
            switch (today.Season)
            {
                case BokuMonoSeason.Spring:
                    cropsShow = fests[FestivalExecutor.FestivalCategory.CompetitionCrop_Spring].Schedule.GetOpenDate();
                    break;
                case BokuMonoSeason.Summer:
                    cropsShow = fests[FestivalExecutor.FestivalCategory.CompetitionCrop_Summer].Schedule.GetOpenDate();
                    break;
                case BokuMonoSeason.Autumn:
                    cropsShow = fests[FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn].Schedule.GetOpenDate();
                    break;
                case BokuMonoSeason.Winter:
                    cropsShow = fests[FestivalExecutor.FestivalCategory.CompetitionCrop_Winter].Schedule.GetOpenDate();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if ((today is not { Season: BokuMonoSeason.Spring, Year: 1 }) && (BokuMonoDateTimeUtility.GetElapsedDays(today, cropsShow) is < 7 and >= 0))
                active.Add(Special.CropsShow);
                    
            var teaParty = fests[FestivalExecutor.FestivalCategory.TeaParty].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, teaParty) is < 7 and >= 0)
                active.Add(Special.TeaParty);

            BokuMonoDateTime petShow;
            switch (today.Season)
            {
                case BokuMonoSeason.Spring:
                case BokuMonoSeason.Summer:
                    petShow = fests[FestivalExecutor.FestivalCategory.PetContest_Summer].Schedule.GetOpenDate();
                    break;
                
                case BokuMonoSeason.Autumn:
                case BokuMonoSeason.Winter:
                    petShow = fests[FestivalExecutor.FestivalCategory.PetContest_Winter].Schedule.GetOpenDate();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (BokuMonoDateTimeUtility.GetElapsedDays(today, petShow) is < 7 and >= 0)
                active.Add(Special.PetShow);

            BokuMonoDateTime horseDerby;
            switch (today.Season)
            {
                case BokuMonoSeason.Summer:
                    horseDerby = fests[FestivalExecutor.FestivalCategory.HorseRacing_Summer].Schedule.GetOpenDate();
                    if (BokuMonoDateTimeUtility.GetElapsedDays(today, horseDerby) is < 7 and >= 0)
                        active.Add(Special.HorseDerby);
                    break;
                case BokuMonoSeason.Winter:
                    horseDerby = fests[FestivalExecutor.FestivalCategory.HorseRacing_Winter].Schedule.GetOpenDate();
                    if (BokuMonoDateTimeUtility.GetElapsedDays(today, horseDerby) is < 7 and >= 0)
                        active.Add(Special.HorseDerby);
                    break;
                case BokuMonoSeason.Spring:
                case BokuMonoSeason.Autumn:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
                    
            var cookOff = fests[FestivalExecutor.FestivalCategory.Food_Main].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, cookOff) is < 7 and >= 0)
                active.Add(Special.CookOff);
                    
            var juiceFestival = fests[FestivalExecutor.FestivalCategory.Juice].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, juiceFestival) is < 7 and >= 0)
                active.Add(Special.JuiceFestival);
                    
            var pumpkinFestival = fests[FestivalExecutor.FestivalCategory.Halloween].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, pumpkinFestival) is < 7 and >= 0)
                active.Add(Special.PumpkinFestival);
                    
            var hearthDay = fests[FestivalExecutor.FestivalCategory.Warm].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, hearthDay) is < 7 and >= 0)
                active.Add(Special.HearthDay);
                    
            var starlightNight = fests[FestivalExecutor.FestivalCategory.StarNight].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, starlightNight) is < 7 and >= 0)
                active.Add(Special.StarlightNight);
                    
            var newYears = fests[FestivalExecutor.FestivalCategory.CountDownParty].Schedule.GetOpenDate();
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, newYears) is < 7 and >= 0)
                active.Add(Special.NewYears);
            
            // Birthday Checks
            var birthdayJules = new BokuMonoDateTime(today.Year, 3, 20);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayJules) is < 7 and >= 0)
                active.Add(Special.BirthdayJules);
                    
            var birthdayDerek = new BokuMonoDateTime(today.Year, 2, 12);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayDerek) is < 7 and >= 0)
                active.Add(Special.BirthdayDerek);
                    
            var birthdayLloyd = new BokuMonoDateTime(today.Year, 3, 3);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayLloyd) is < 7 and >= 0)
                active.Add(Special.BirthdayLloyd);
                    
            var birthdayGabriel = new BokuMonoDateTime(today.Year, 4, 28);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayGabriel) is < 7 and >= 0)
                active.Add(Special.BirthdayGabriel);
                    
            var birthdaySamir = new BokuMonoDateTime(today.Year, 4, 26);
            if (likeManager.GetPoint(104) > 0 && (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySamir) is < 7 and >= 0))
                active.Add(Special.BirthdaySamir);
                
            var birthdayArata = new BokuMonoDateTime(today.Year, 4, 17);
            if (likeManager.GetPoint(105) > 0 && (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayArata) is < 7 and >= 0))
                active.Add(Special.BirthdayArata);
                    
            var birthdaySophie = new BokuMonoDateTime(today.Year, 1, 16);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySophie) is < 7 and >= 0)
                active.Add(Special.BirthdaySophie);
                    
            var birthdayJune = new BokuMonoDateTime(today.Year, 4, 12);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayJune) is < 7 and >= 0)
                active.Add(Special.BirthdayJune);
                    
            var birthdayFreya = new BokuMonoDateTime(today.Year, 3, 25);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayFreya) is < 7 and >= 0)
                active.Add(Special.BirthdayFreya);
                    
            var birthdayMaple = new BokuMonoDateTime(today.Year, 3, 14);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMaple) is < 7 and >= 0)
                active.Add(Special.BirthdayMaple);

            if (likeManager.GetPoint(204) > 0)
            {
                switch (today.Season)
                {
                    case BokuMonoSeason.Winter:
                    {
                        if (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year + 1, 1, 5)) is < 7 and >= 0)
                            active.Add(Special.BirthdayKagetsu);
                        break;
                    }
                    case BokuMonoSeason.Spring:
                    {
                        if (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 1, 5)) is < 7 and >= 0)
                            active.Add(Special.BirthdayKagetsu);
                        break;
                    }
                    case BokuMonoSeason.Summer:
                    case BokuMonoSeason.Autumn:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            var birthdayDiana = new BokuMonoDateTime(today.Year, 3, 22);
            if (likeManager.GetPoint(205) > 0 && (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayDiana) is < 7 and >= 0))
                active.Add(Special.BirthdayDiana);
                    
            var birthdayFelix = new BokuMonoDateTime(today.Year, 2, 1);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayFelix) is < 7 and >= 0)
                active.Add(Special.BirthdayFelix);
            
            var birthdayErik = new BokuMonoDateTime(today.Year, 1, 20);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayErik) is < 7 and >= 0)
                active.Add(Special.BirthdayErik);
            
            var birthdayStuart = new BokuMonoDateTime(today.Year, 3, 11);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayStuart) is < 7 and >= 0)
                active.Add(Special.BirthdayStuart);
                    
            var birthdaySonia = new BokuMonoDateTime(today.Year, 1, 25);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySonia) is < 7 and >= 0)
                active.Add(Special.BirthdaySonia);
                    
            var birthdayMadeleine = new BokuMonoDateTime(today.Year, 1, 18);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMadeleine) is < 7 and >= 0)
                active.Add(Special.BirthdayMadeleine);
                    
            var birthdayMina = new BokuMonoDateTime(today.Year, 4, 4);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMina) is < 7 and >= 0)
                active.Add(Special.BirthdayMina);
                    
            var birthdayWilbur = new BokuMonoDateTime(today.Year, 2, 3);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayWilbur) is < 7 and >= 0)
                active.Add(Special.BirthdayWilbur);
                    
            var birthdayClara = new BokuMonoDateTime(today.Year, 3, 29);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayClara) is < 7 and >= 0)
                active.Add(Special.BirthdayClara);
                    
            var birthdayKevin = new BokuMonoDateTime(today.Year, 2, 25);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayKevin) is < 7 and >= 0)
                active.Add(Special.BirthdayKevin);
                    
            var birthdayIsaac = new BokuMonoDateTime(today.Year, 3, 6);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayIsaac) is < 7 and >= 0)
                active.Add(Special.BirthdayIsaac);
                    
            var birthdayNadine = new BokuMonoDateTime(today.Year, 2, 30);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayNadine) is < 7 and >= 0)
                active.Add(Special.BirthdayNadine);
            
            var birthdaySylvia = new BokuMonoDateTime(today.Year, 3, 18);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySylvia) is < 7 and >= 0)
                active.Add(Special.BirthdaySylvia);
                    
            var birthdayLaurie = new BokuMonoDateTime(today.Year, 3, 18);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayLaurie) is < 7 and >= 0)
                active.Add(Special.BirthdayLaurie);
                    
            var birthdayMiguel = new BokuMonoDateTime(today.Year, 2, 21);
            if (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayMiguel) is < 7 and >= 0)
                active.Add(Special.BirthdayMiguel);
                    
            var birthdayHarold = new BokuMonoDateTime(today.Year, 2, 7);
            if (likeManager.GetPoint(414) > 0 && (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayHarold) is < 7 and >= 0))
                active.Add(Special.BirthdayHarold);
            
            var birthdaySherene = new BokuMonoDateTime(today.Year, 2, 16);
            if (likeManager.GetPoint(415) > 0 && (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySherene) is < 7 and >= 0))
                active.Add(Special.BirthdaySherene);
            
            return active;
        }
        
        private static void ApplyRequestGroup(ResidentMissionMasterData mission, MissionInfo request)
        { 
            // TODO: Dynamic quality determination
            
            // Set rewards
            mission.RewardCategory = RewardsType.Item;
            mission.RewardId = request.RewardItemId;
            mission.RewardNum = request.RewardItemStack;
            mission.RewardQuality = request.RewardItemQuality;
            mission.RewardIcon = null;
            
            // Set required item fields
            mission.RequiredItemType = Il2CppHelper.ToIl2CppList(request.ItemType);
            mission.RequiredItemId = Il2CppHelper.ToIl2CppList(request.ItemId);
            mission.RequiredItemStack = Il2CppHelper.ToIl2CppList(request.ItemStack);
            mission.RequiredItemQuality = Il2CppHelper.ToIl2CppList(request.ItemQuality);
            
            var requiredItemFreshness = new Il2CppSystem.Collections.Generic.List<int>();
            requiredItemFreshness.Add(-1);
            requiredItemFreshness.Add(-1);
            requiredItemFreshness.Add(-1);
            mission.RequiredItemFreshness = requiredItemFreshness;
            
            // Set unlock conditions to none as safety
            var unlockConditions = new Il2CppSystem.Collections.Generic.List<UnlockCondition>();
            unlockConditions.Add(UnlockCondition.None);
            unlockConditions.Add(UnlockCondition.None);
            unlockConditions.Add(UnlockCondition.None);
            mission.UnlockConditions = unlockConditions;
            
            // Set mission to delivery type
            mission.ConditionParams = new();
            mission.CompleteCondition = CompleteCondition.Delivery;
            mission.CompleteSubCondition = 0;
            mission.CompleteSubConditionParam = 0;
            mission.CompleteValue = 0;
        }
    }
    
    // TODO: Future use in dynamic quality calculations
    private static void CheckAnimals()
    {
        var farmAnimals = Il2CppHelper.ToSystemList(new Il2CppSystem.Collections.Generic.List<AnimalParemeterBase>(AnimalManager.Instance.AllAnimalParams))
            .Where(x => x.ToFarmAnimalParameter != null)
            .Select(x => x.ToFarmAnimalParameter).ToList();
            
        foreach (var animal in farmAnimals)
        {   
            Log.LogInfo($"FarmAnimalTypeId: {(AnimalType)animal.FarmAnimalTypeId}");
            Log.LogInfo($"ProductType: {animal.GetProductType()}");
            Log.LogInfo($"ProductQuality: {animal.GetProductQuality()}");
        }
    }
    
    private static int CropFestWins()
            {
                var festExe = FestivalExecutor.Instance;
                var cropWins = 0;
                
                cropWins += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Spring,
                               FestivalExecutor.CompetitionRank.Bronze) +
                           festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Spring,
                               FestivalExecutor.CompetitionRank.Silver) +
                           festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Spring,
                               FestivalExecutor.CompetitionRank.Gold);
                cropWins += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Summer,
                                 FestivalExecutor.CompetitionRank.Bronze) +
                             festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Summer,
                                 FestivalExecutor.CompetitionRank.Silver) +
                             festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Summer,
                                 FestivalExecutor.CompetitionRank.Gold);
                cropWins += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn,
                                FestivalExecutor.CompetitionRank.Bronze) +
                            festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn,
                                FestivalExecutor.CompetitionRank.Silver) +
                            festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn,
                                FestivalExecutor.CompetitionRank.Gold);
                cropWins += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Winter,
                                FestivalExecutor.CompetitionRank.Bronze) +
                            festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Winter,
                                FestivalExecutor.CompetitionRank.Silver) +
                            festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Winter,
                                FestivalExecutor.CompetitionRank.Gold);
                
                return cropWins;
            }

    private static class Il2CppHelper
    {
        public static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(List<T> list)
        {
            var Il2CppList = new Il2CppSystem.Collections.Generic.List<T>();
        
            foreach (var item in list)
            {
                Il2CppList.Add(item);
            }
        
            return Il2CppList;
        }
        
        public static List<T> ToSystemList<T>(Il2CppSystem.Collections.Generic.List<T> list)
        {
            var SystemList = new List<T>();
        
            foreach (var item in list)
            {
                SystemList.Add(item);
            }
        
            return SystemList;
        }
    }

    private static class jsonHandler
    {
        public static void Write()
        {
            //string jsonPath = Path.Combine(Paths.PluginPath, "Test1.json");
            //string json = JsonSerializer.Serialize(toot);
            //File.WriteAllText(jsonPath, json);
        }

        public static T Read<T>(string jsonPath) where T : class
        {
            return File.Exists(jsonPath)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(jsonPath))
                : null;
        }
    }

    private class MissionInfo
    {
        public uint RequestGroupId { get; set; }
        public uint RewardGroupId { get; set; }
        public RequestCategory Category { get; set; }
        public List<uint> ItemId { get; set; }
        public List<RequiredItemType> ItemType { get; set; }
        public List<int> ItemStack { get; set; }
        public List<int> ItemQuality { get; set; }
        public uint RewardItemId { get; set; }
        public int RewardItemStack { get; set; }
        public int RewardItemQuality { get; set; }
        public Special Special {get; set;}
        public string MissionName { get; set; }
        public string MissionCaption { get; set; }
        public string MissionDescription { get; set; }
        public string DebugInfo { get; set; }
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
        public List<uint> ItemIds { get; set; }
        public List<RequiredItemType> ItemType { get; set; }
        public List<int> ItemStack { get; set; }
        public List<int> ItemQuality { get; set; }
        public float DifficultChance { get; set; }
        public bool PickOne { get; set; }
    }
    
    private class RewardGroups
    {
        public uint Id { get; set; }
        public RequestCategory Category { get; set; }
        public List<uint> ItemIds { get; set; }
        public List<int> ItemStack { get; set; }
        public List<int> ItemQuality { get; set; }
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