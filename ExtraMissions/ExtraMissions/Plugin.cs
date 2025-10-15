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
            var possibleRequests =
                _requestGroups.Where(x => x.Characters.Contains(0) || x.Characters.Contains(charaId)).ToList();
            CheckSpecial(possibleRequests);

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
        
        private static void CheckSpecial(List<RequestGroups> requests)
        {
            var today = DateManager.Instance.Now;
            
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                switch (request.Special)
                {
                    case Special.None:
                        break;
                    // TODO: MORE SENSICAL SEASONAL DETERMINATION
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