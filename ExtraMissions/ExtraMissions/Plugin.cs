using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Animal;
using BokuMono.Data;
using BokuMono.ResidentMission;
using BokuMono.SaveData;
using HarmonyLib;
using Il2CppSystem;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;
using StringSplitOptions = System.StringSplitOptions;
using Math = System.Math;

namespace ExtraMissions;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<float> _chanceMissionRoll;
    private static ConfigEntry<float> _dynamRewardMulti;
    private static ConfigEntry<int> _friendshipReward;
    private static ConfigEntry<float> _difStackMulti;
    private static ConfigEntry<int> _difQualityAdd;
    private static ConfigEntry<bool> _debugLogging;
    private static ConfigEntry<bool> _rgPriceLog;

    public override void Load()
    {
        // Plugin startup logic
        _chanceMissionRoll =  Config.Bind("-----01 General-----", "Chance_For_Mission_Selection", 0.5f,
            "Every morning if there are missions available to be selected to become random missions, " +
            "this is the chance for a mission to be selected.\n" + "Set between 0.0 and 1.0. Represents a percentage chance (e.g. 0.5 is 50% chance)");
        _dynamRewardMulti =  Config.Bind("-----01 General-----", "Dynamic_Reward_Multiplier", 1.0f,
            "Used to tune the dynamic function used to determine the number of reward items to give. " +
            "Only affects rewards that use this dynamic calculation");
        _friendshipReward =  Config.Bind("-----01 General-----", "Friendship_Points_Reward", 2000,
            "The number of friendship points a character gains upon completing a request");
        _difStackMulti =  Config.Bind("-----02 Difficulty-----", "Difficulty_Stack_Multiplier", 1.5f,
            "A mission has a chance to become a difficult mission. This means, the number of items you turn in " +
            "and the amount of reward items you get is multiplied by the given number.");
        _difQualityAdd =  Config.Bind("-----02 Difficulty-----", "Difficulty_Quality_Increase", 2,
            "A mission has a chance to become a difficult mission. This means, the quality of items you turn in " +
            "and the amount of reward items you get is increased by the given amount.\n" +
            "Every point added is 0.5 stars increased on quality.");
        _debugLogging =  Config.Bind("-----99 DEBUG-----", "Enable_Debug_Logging", false,
            "Enable debug logging.");
        _rgPriceLog =  Config.Bind("-----99 DEBUG-----", "RequestGroups_Price_Log", false,
            "Outputs the total price of each item in each request group." +
            "\nBased on item price and quantity. Quality is not considered.");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(DynamicMissionPatch));
    }

    /// <summary>
    /// Manages the generation, application, and persistence of dynamic request missions
    /// </summary>
    private static class DynamicMissionPatch
    {
        private static readonly Random Rnd;
        private static readonly string RequestGroupsPath;
        private static readonly string RewardGroupsPath;
        private static string SavePathBase;

        private static List<RequestGroups> _requestGroups;
        private static List<RewardGroups> _rewardGroups;

        private static readonly HashSet<uint> FinalMissions;
        private static List<MissionManager.OrderData> _availableMissions;
        private static List<ResidentMissionMasterData> _selectedMissions;

        private static int MAX_REQ_COUNT = 3;


        static DynamicMissionPatch()
        {
            Rnd = new Random();

            RequestGroupsPath = Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/data/RequestGroups.json");
            RewardGroupsPath = Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/data/RewardGroups.json");
            SavePathBase = Path.Combine(Paths.PluginPath, $"{MyPluginInfo.PLUGIN_NAME}/saves/");

            FinalMissions =
            [
                30104, 30204, 30304, 30404, 30504, 30604, 30704, 30804, 30904, 31004, 31104, 31204, 31304, 31404, 31504,
                31604, 31704, 31804, 31904, 32004, 32104, 32204, 32304, 32404, 32504, 32604, 32704, 32804
            ];
            _availableMissions = [];
            _selectedMissions = [];
        }

        /// <summary>
        ///  Loads RequestGroups.json and RewardGroups.json data files
        /// </summary>
        /// <remarks>
        /// RequestGroups is validated for unique Id values, that inner item ids, types, stacks, and quality
        /// arrays are equal length, and that type values match Id ranges properly.
        /// </remarks>
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void LoadDataFiles()
        {
            _requestGroups = JsonHandler.Read<List<RequestGroups>>(RequestGroupsPath);
            _rewardGroups = JsonHandler.Read<List<RewardGroups>>(RewardGroupsPath);

            if (_requestGroups is null || _rewardGroups is null)
                Log.LogError("RequestGroups or RewardGroups is null!");
            else if (_debugLogging.Value) Log.LogMessage("LoadDataFiles: Data files loaded!");

            var duplicateIds = JsonHandler.ValidateUniqueIds(_requestGroups);
            if(duplicateIds.Count != 0)
                Log.LogError($"RequestGroups contain duplicate ids: {duplicateIds.Join()}");

            var nonequalLengthIds = JsonHandler.ValidateItemListLength(_requestGroups);
            if(nonequalLengthIds.Count != 0)
                Log.LogError($"RequestGroups contain itemLists arrays of non equal length: {nonequalLengthIds.Join()}");
            
            var invalidItemTypeIds = JsonHandler.ValidateItemTypes(_requestGroups);
            if(invalidItemTypeIds.Count != 0)
                Log.LogError($"RequestGroups contain mismatched or invalid ItemTypes: {invalidItemTypeIds.Join()}");

            if (_rgPriceLog.Value) Troubleshoot.OutputPriceList(_requestGroups);
        }

        /// <summary>
        /// Saves currently generated missions to save file matching game save slot.
        /// </summary>
        /// <param name="slot">The slot being saved to.</param>
        [HarmonyPatch(typeof(SaveDataManager), "SlotSaveAsync")]
        [HarmonyPostfix]
        private static void OnGameSave(int slot)
        {
            var langMan = LanguageManager.Instance;
            var toSave = _selectedMissions.Select(mission => new MissionInfo
            {
                MissionId = mission.Id,
                ItemId = Il2CppHelper.ToSystemList(mission.RequiredItemId),
                ItemType = Il2CppHelper.ToSystemList(mission.RequiredItemType),
                ItemStack = Il2CppHelper.ToSystemList(mission.RequiredItemStack),
                ItemQuality = Il2CppHelper.ToSystemList(mission.RequiredItemQuality),
                RewardItemId = mission.RewardId,
                RewardItemStack = mission.RewardNum,
                RewardItemQuality = mission.RewardQuality,
                MissionName =
                    langMan.GetLocalizeText(LocalizeTextTableType.MissionNameText, mission.NameId),
                MissionCondition =
                    langMan.GetLocalizeText(LocalizeTextTableType.MissionConditionsText, mission.ConditionsTextId),
                MissionCaption =
                    langMan.GetLocalizeText(LocalizeTextTableType.MissionCaptionText, mission.CaptionId).Replace("\n", " "),
            }).ToList();

            if (_debugLogging.Value && toSave.Count > 0)
                Log.LogMessage($"OnGameSave: Saving mission(s) {toSave.Join(x => x.MissionId.ToString())}");
            

            var success = JsonHandler.Write(SavePathBase + $"Save{slot}.json", toSave);

            if (_debugLogging.Value)
                Log.LogMessage($"OnGameSave: Save to slot {slot} " + (success ? "successful!" : "failed!"));
        }

        /// <summary>
        /// Attempts to load in save file matching current load slot and applies missions.
        /// </summary>
        [HarmonyPatch(typeof(UserInfoMediator), "ApplyFromUserInfo")]
        [HarmonyPostfix]
        private static void OnGameLoad()
        {
            _availableMissions.Clear();
            _selectedMissions.Clear();

            var missionManager = MissionManager.Instance;
            var loadData =
                JsonHandler.Read<List<MissionInfo>>(SavePathBase + $"Save{SaveDataManager.Instance.LoadSlot}.json");
            if (loadData != null)
            {
                var orderDatas = Il2CppHelper.ToSystemList(missionManager.OrderDatas);

                foreach (var mission in loadData)
                {
                    var missionData = orderDatas.Find(x => x.Id == mission.MissionId).MissionData;
                    ApplyRequest(missionData, mission);
                    _selectedMissions.Add(missionData);
                }

                if (_debugLogging.Value)
                    Log.LogMessage($"OnGameLoad: Load from slot {SaveDataManager.Instance.LoadSlot} successful!");
            }
            else if (_debugLogging.Value)
                Log.LogMessage($"OnGameLoad: Save{SaveDataManager.Instance.LoadSlot} does not exist or failed to load!");

            Quality.UpdateQuality();
            RefreshAvailableMissions(missionManager.OrderDatas);
        }

        /// <summary>
        /// If a generated mission is completed, refreshes pool of available missions.
        /// </summary>
        /// <param name="__instance">Mission manager</param>
        /// <param name="id">Id of completed mission</param>
        [HarmonyPatch(typeof(MissionManager), "MissionComplete")]
        [HarmonyPostfix]
        private static void OnMissionComplete(MissionManager __instance, uint id)
        {
            if (_debugLogging.Value) Log.LogMessage("OnMissionComplete: Function fired!");

            if (_selectedMissions.All(x => x.Id != id)) return;

            _selectedMissions.RemoveAll(x => x.Id == id);
            RefreshAvailableMissions(__instance.OrderDatas);
        }

        [HarmonyPatch(typeof(MissionManager), "UpdateOnChangeDate")]
        [HarmonyPostfix]
        private static void OnChangeDate()
        {
            if (_debugLogging.Value) Log.LogMessage("OnChangeDate: Function fired!");

            Quality.UpdateQuality();
            GenerateNewMission();
        }

        /// <summary>
        /// Refreshes pool of available missions.
        /// </summary>
        /// <param name="orderDatas">List of mission data</param>
        private static void RefreshAvailableMissions(
            Il2CppSystem.Collections.Generic.List<MissionManager.OrderData> orderDatas)
        {
            _availableMissions.Clear();

            foreach (var item in orderDatas)
            {
                if (FinalMissions.Contains(item.Id) &&
                    item.State is MissionManager.OrderState.Complete or MissionManager.OrderState.OpenShop) 
                    _availableMissions.Add(item);
            }
            
            if (_debugLogging.Value)
                Log.LogMessage($"RefreshAvailableMissions: Mission(s) {_availableMissions.Join(x => x.Id.ToString())} available!");
        }

        /// <summary>
        /// Chooses an available mission from the pool and generates a randomized request mission if available missions
        /// exist and concurrent cap is not met.
        /// </summary>
        private static void GenerateNewMission()
        {
            if (_availableMissions.Count == 0 || _selectedMissions.Count >= MAX_REQ_COUNT) return;
            if (!(_chanceMissionRoll.Value > Rnd.NextDouble()))
            {
                if(_debugLogging.Value)
                    Log.LogMessage("GenerateNewMission: Roll to generate mission failed. No mission generated today.");
                return;
            }
                
            // Get random mission from available missions
            var index = Rnd.Next(_availableMissions.Count);
            var mission = _availableMissions[index].MissionData;
            _availableMissions[index].State = MissionManager.OrderState.Available;
            _availableMissions.RemoveAt(index);

            if (_debugLogging.Value)
                Log.LogMessage($"GenerateNewMission: Mission {mission.Id} selected from availableMissions!");

            ApplyRequest(mission, GenerateRequest(mission.CharaId));

            _selectedMissions.Add(mission);
        }

        /// <summary>
        /// Generates random request mission from RequestGroups from data file.
        /// </summary>
        /// <param name="charaId">Character the mission is for.</param>
        /// <returns>Generated mission details</returns>
        private static MissionInfo GenerateRequest(uint charaId)
        {
            if (_debugLogging.Value) Log.LogMessage($"ChooseRequest: Choose request for {charaId} begun!");
            var activeSpecials = GetActiveSpecials();
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
                itemQuality.Add(tempItemList.ItemQuality[itemIndex] == -2
                ? Quality.GetQuality(tempItemList.ItemIds[itemIndex], tempItemList.ItemType[itemIndex])
                : tempItemList.ItemQuality[itemIndex]);
                itemQuality.Add(0);
                itemQuality.Add(0);
            }
            else
            {
                for (var i = 0; i < tempItemList.ItemIds.Count; i++)
                {
                    itemIds.Add(tempItemList.ItemIds[i]);
                    itemTypes.Add(tempItemList.ItemType[i]);
                    itemStack.Add(tempItemList.ItemStack[i]);
                    itemQuality.Add(tempItemList.ItemQuality[i] == -2
                    ?  Quality.GetQuality(tempItemList.ItemIds[i], tempItemList.ItemType[i])
                    :  tempItemList.ItemQuality[i]);
                }
            }

            // Calculate reward
            var rewardGroup = _rewardGroups.Find(x => x.Category == tempItemList.Category);
            var rewardItemIndex = Rnd.Next(rewardGroup.ItemIds.Count);

            var rewardItemId = rewardGroup.ItemIds[rewardItemIndex];
            var rewardItemStack = rewardGroup.ItemStack[rewardItemIndex] == -2
                ? GetRewardQuantity(rewardItemId, itemIds, itemTypes, itemStack)
                : rewardGroup.ItemStack[rewardItemIndex];
            var rewardItemQuality = rewardGroup.ItemQuality[rewardItemIndex] == -2
                ? Quality.GetQuality(rewardItemId, RequiredItemType.Item)
                : rewardGroup.ItemQuality[rewardItemIndex];

            // Calculate Difficulty
            if (tempItemList.DifficultChance > Rnd.NextDouble())
            {
                for (var i = 0; i < itemIds.Count; i++)
                {
                    if (itemStack[i] != 0)
                        itemStack[i] = (int)Math.Round(itemStack[i] * _difStackMulti.Value);
                    if (itemQuality[i] > 0)
                        itemQuality[i] = Math.Clamp(itemQuality[i] + _difQualityAdd.Value, 1, 14);
                }

                rewardItemStack = (int)Math.Round(rewardItemStack * _difStackMulti.Value);
                rewardItemQuality = Math.Clamp(rewardItemQuality + _difQualityAdd.Value, 1, 14);
            }

            return new MissionInfo
            {
                ItemId = itemIds,
                ItemType = itemTypes,
                ItemStack = itemStack,
                ItemQuality = itemQuality,
                RewardItemId = rewardItemId,
                RewardItemStack = rewardItemStack,
                RewardItemQuality = rewardItemQuality,
                MissionName = possibleRequests[groupIndex].MissionName,
                MissionCondition = possibleRequests[groupIndex].MissionCondition,
                MissionCaption = possibleRequests[groupIndex].MissionCaption,
            };
        }

        /// <summary>
        /// Generates a set of all currently active special conditions.
        /// </summary>
        /// <returns>Set of all currently active special conditions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Unacceptable enum value</exception>
        private static HashSet<Special> GetActiveSpecials()
        {
            if (_debugLogging.Value) Log.LogMessage($"GetActiveSpecials: Get active specials has begun!");
            var today = (DateManager.Instance.Now)
                .AddDays(1); // Function is called before the true date is change so add day
            var active = new HashSet<Special>() { Special.None };

            var fests = FestivalExecutor.Instance.m_Festivals;
            var likeManager = LikeabilityManager.Instance;

            // Season Checks
            switch (today.Season)
            {
                case BokuMonoSeason.Winter
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year + 1, 1, 1)) is < 7 and >= 0):
                case BokuMonoSeason.Spring
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 1, 31)) >= 7):
                    active.Add(Special.Spring);
                    break;
            }

            switch (today.Season)
            {
                case BokuMonoSeason.Spring
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 2, 1)) is < 7 and >= 0):
                case BokuMonoSeason.Summer
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 2, 31)) is >= 7):
                    active.Add(Special.Summer);
                    break;
            }

            switch (today.Season)
            {
                case BokuMonoSeason.Summer
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 3, 1)) is < 7 and >= 0):
                case BokuMonoSeason.Autumn
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 3, 31)) is >= 7):
                    active.Add(Special.Autumn);
                    break;
            }

            switch (today.Season)
            {
                case BokuMonoSeason.Autumn
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 4, 1)) is < 7 and >= 0):
                case BokuMonoSeason.Winter
                    when (BokuMonoDateTimeUtility.GetElapsedDays(today, new BokuMonoDateTime(today.Year, 4, 31)) is >= 7):
                    active.Add(Special.Winter);
                    break;
            }

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

            if ((today is not { Season: BokuMonoSeason.Spring, Year: 1 }) &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, animalShow) is < 7 and >= 0))
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

            if ((today is not { Season: BokuMonoSeason.Spring, Year: 1 }) &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, cropsShow) is < 7 and >= 0))
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
            if (likeManager.GetPoint(104) > 0 &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySamir) is < 7 and >= 0))
                active.Add(Special.BirthdaySamir);

            var birthdayArata = new BokuMonoDateTime(today.Year, 4, 17);
            if (likeManager.GetPoint(105) > 0 &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayArata) is < 7 and >= 0))
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
            if (likeManager.GetPoint(205) > 0 &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayDiana) is < 7 and >= 0))
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
            if (likeManager.GetPoint(414) > 0 &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdayHarold) is < 7 and >= 0))
                active.Add(Special.BirthdayHarold);

            var birthdaySherene = new BokuMonoDateTime(today.Year, 2, 16);
            if (likeManager.GetPoint(415) > 0 &&
                (BokuMonoDateTimeUtility.GetElapsedDays(today, birthdaySherene) is < 7 and >= 0))
                active.Add(Special.BirthdaySherene);

            return active;
        }
        
        /// <summary>
        /// Generates quantity value for provided reward item. Based on the total value of the requested items.
        /// For groups and categories, trimmed mean is used as base price.
        /// For items considered recyclable, Treasureland sell price used as base price.
        /// </summary>
        /// <param name="rewardItem">Reward item id</param>
        /// <param name="itemIds">Required item list</param>
        /// <param name="itemTypes">Required item type</param>
        /// <param name="itemStack">Required item quantity</param>
        /// <returns>Recommended quantity of provided reward item</returns>
        /// <exception cref="ArgumentOutOfRangeException">Invalid RequiredItemType</exception>
        private static int GetRewardQuantity(uint rewardItem, List<uint> itemIds, List<RequiredItemType> itemTypes, List<int> itemStack)
        {
            var itemMaster = MasterDataManager.Instance.ItemMaster;
            var rewardData = MasterDataManager.Instance.ItemMaster.GetData(rewardItem);
            var recyclable = new Dictionary<uint, int>
            {
                {115000, 200},
                {115001, 400},
                {115002, 1200},
                {115003, 300},
                {115004, 600},
                {115005, 7777},
                {115006, 7777},
                {115007, 7777},
                {115008, 7777},
                {115009, 7777},
                {115010, 7777},
                {115011, 7777},
                {115012, 100},
                {115013, 2000}
            };
            
            var totalPrice = 0;

            for (var i = 0; i < itemIds.Count(x => x != 0); i++)
            {
                switch (itemTypes[i])
                {
                    case RequiredItemType.Item:
                        if (recyclable.TryGetValue(itemIds[i], out var value))
                        {
                            totalPrice += value * itemStack[i];
                        }
                        else
                        {
                            totalPrice += itemMaster.GetData(itemIds[i]).Price * itemStack[i];
                        }

                        break;
                    case RequiredItemType.Category:
                    {
                        var itemList = Il2CppHelper.ToSystemList(itemMaster.GetItemListByItemCategory((ItemCategory)itemIds[i])).Select(x => x.Price).ToList();
                        totalPrice = CalculateTrimmedMean(itemList, 0.15) * itemStack[i];
                    
                        break;
                    }

                    case RequiredItemType.Group:
                    {
                        var itemList = Il2CppHelper.ToSystemList(MasterDataManager.Instance.MissionstuffGroupMaster
                            .GetData(itemIds[i]).RequiredItemIdList).Where(x => x != 0);
                        var prices = itemList.Select(i => recyclable.TryGetValue(i, out var value) ? value : itemMaster.GetData(i).Price)
                            .ToList();
                        totalPrice = CalculateTrimmedMean(prices, 0.15) * itemStack[i];
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            return Math.Clamp((int)Math.Ceiling((totalPrice / (float)rewardData.Price) * _dynamRewardMulti.Value),
                1, int.MaxValue);
        }
        
        /// <summary>
        /// Calculates mean of provided int list. Trims percentage of high and low values.
        /// </summary>
        /// <param name="numbers">List of integers for which the mean is calculated</param>
        /// <param name="trimPercentage">Percentage of list to trim from both ends</param>
        /// <returns>Trimmed/truncated mean</returns>
        private static int CalculateTrimmedMean(List<int> numbers, double trimPercentage)
        {
            if ((numbers == null || numbers.Count == 0) || (trimPercentage < 0 || trimPercentage >= 50)) return 0;
            
            var trimCount = (int)Math.Round(numbers.Count * trimPercentage / 100);

            return trimCount * 2 >= numbers.Count
                ? 0
                : (int)numbers.OrderBy(x => x).Skip(trimCount).Take(numbers.Count - (2 * trimCount)).Average();
        }

        /// <summary>
        /// Applies generated request mission to supplied mission.
        /// </summary>
        /// <param name="mission">Game mission to apply generated mission to</param>
        /// <param name="request">Generated mission that will be applied to game mission</param>
        private static void ApplyRequest(ResidentMissionMasterData mission, MissionInfo request)
        {
            if (_debugLogging.Value)
                Log.LogMessage("ApplyRequest:\n" + $"   MissionId: {mission.Id}\n" + $"{request}");

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
            
            // Set Friendship/Likeability reward
            mission.RewardLikeability = _friendshipReward.Value;

            // Set Text
            var langMan = LanguageManager.Instance;
            if (langMan.CurrentLanguage != Language.en) return;
            
            langMan.GetLocalizeTextData(LocalizeTextTableType.MissionNameText, mission.NameId).Text =
                TextFormatter.FormatName(request.MissionName, 
                    langMan.GetLocalizeTextData(LocalizeTextTableType.CharacterNameText, mission.CharaId).Text);
            langMan.GetLocalizeTextData(LocalizeTextTableType.MissionConditionsText, mission.ConditionsTextId).Text =
                TextFormatter.FormatName(request.MissionCondition, 
                    langMan.GetLocalizeTextData(LocalizeTextTableType.CharacterNameText, mission.CharaId).Text);
            langMan.GetLocalizeTextData(LocalizeTextTableType.MissionCaptionText, mission.CaptionId).Text =
                TextFormatter.FormatName(request.MissionCaption, 
                    langMan.GetLocalizeTextData(LocalizeTextTableType.CharacterNameText, mission.CharaId).Text);
        }
    }

    /// <summary>
    /// Handles quality determination and calculation for current game save
    /// </summary>
    private static class Quality
    {
        /// <summary>
        /// Categories of quality to consider
        /// </summary>
        private enum QualityType
        {
            Milk,
            Clip,
            Egg,
            Crop,
            Forage,
            Fish,
            Honey,
            Mushroom,
            Bug
        }

        private static Dictionary<QualityType, int> QualityDict = new();

        /// <summary>
        /// Updates quality dictionary for each QualityType category based on current save progress
        /// </summary>
        public static void UpdateQuality()
        {
            var coroManager = CoroMissionManager.Instance;
            var forageQuality = coroManager.CRSM.GetData((short)CoroMissionCategory.Harvest,
                (short)coroManager.GetCurrentRankData(CoroMissionCategory.Harvest).Rank).Quality[0];
            var fishQuality = coroManager.CRSM.GetData((short)CoroMissionCategory.Fish,
                (short)coroManager.GetCurrentRankData(CoroMissionCategory.Fish).Rank).Quality[0];
            var honeyQuality = coroManager.CRSM.GetData((short)CoroMissionCategory.Honey,
                (short)coroManager.GetCurrentRankData(CoroMissionCategory.Honey).Rank).Quality[0];
            var mushroomQuality = coroManager.CRSM.GetData((short)CoroMissionCategory.Mushroom,
                (short)coroManager.GetCurrentRankData(CoroMissionCategory.Mushroom).Rank).Quality[0];
            var bugQuality = coroManager.CRSM.GetData((short)CoroMissionCategory.Insect,
                (short)coroManager.GetCurrentRankData(CoroMissionCategory.Insect).Rank).Quality[0];
            var (milkQuality, clipQuality, eggQuality) = GetAnimalProductQuality();
            var cropQuality = GetCropQuality();

            QualityDict[QualityType.Milk] = milkQuality;
            QualityDict[QualityType.Clip] = clipQuality;
            QualityDict[QualityType.Egg] = eggQuality;
            QualityDict[QualityType.Crop] = cropQuality;
            QualityDict[QualityType.Forage] = forageQuality;
            QualityDict[QualityType.Fish] = fishQuality;
            QualityDict[QualityType.Honey] = honeyQuality;
            QualityDict[QualityType.Mushroom] = mushroomQuality;
            QualityDict[QualityType.Bug] = bugQuality;

            if (_debugLogging.Value)
            {
                var output = "UpdateQuality:\n";
                foreach (var (key, value) in QualityDict)
                {
                    output += $"   {key,-8}: {value,-2} | {(value / 2.0f),-3:F1} stars\n";
                }
                Log.LogMessage(output.TrimEnd());
            }
        }

        /// <summary>
        /// Determines the recommended quality for the provided item id
        /// </summary>
        /// <param name="itemId">Item for which quality will be determined</param>
        /// <param name="type">Type of item id being used</param>
        /// <returns>Recommended quality for the supplied item (1-14)</returns>
        /// <exception cref="ArgumentOutOfRangeException">Invalid ItemCategory</exception>
        public static int GetQuality(uint itemId, RequiredItemType type)
        {
            var itemCat = ItemCategory.None;
            int quality;

            switch (type)
            {
                case RequiredItemType.Item:
                {
                    var itemData = MasterDataManager.Instance.ItemMaster.GetData(itemId);
                    itemCat = (ItemCategory)itemData.Category;

                    if (!itemData.HasQuality) return -1;
                    break;
                }
                case RequiredItemType.Category:
                    itemCat = (ItemCategory)itemId;
                    break;
                case RequiredItemType.Group:
                    itemCat = itemId switch
                    {
                        1000165 or 1000177 or 1000178 or 1000179 => ItemCategory.Accessories,
                        1000161 or 1000172 or 1000183 => ItemCategory.AnimalHair,
                        1000174 => ItemCategory.BallOfYarn,
                        1000151 => ItemCategory.Bouquet,
                        1000007 => ItemCategory.Butter,
                        1000008 => ItemCategory.Cheese,
                        1000169 or 1000170 or 1000171 => ItemCategory.Crickets,
                        1000018 or 1000019 or 1000145 or 1000146 or 1000147 or 1000148 or 1000149 or 1000150 or 1000173
                            or 1000180 or 1000181 => ItemCategory.Crop,
                        1000155 or 1000160 or 1000176 => ItemCategory.Dessert,
                        1000162 => ItemCategory.Color,
                        1000003 or 1000004 => ItemCategory.Egg,
                        1000166 => ItemCategory.Flower,
                        1000164 => ItemCategory.Jewelry,
                        1000017 => ItemCategory.Herbs,
                        1000014 => ItemCategory.Honey,
                        1000157 => ItemCategory.HorsdOeuvre,
                        1000001 or 1000011 or 1000012 or 1000015 or 1000020 or 1000021 or 1000022 or 1000023 or 1000024
                            or 1000025 or 1000026 or 1000027 => ItemCategory.Ingredients,
                        1000154 or 1000159 => ItemCategory.MainDish,
                        1000009 => ItemCategory.Mayonnaise,
                        1000005 or 1000006 => ItemCategory.Milk,
                        1000013 => ItemCategory.Mushroom,
                        1000002 => ItemCategory.Oil,
                        1000163 => ItemCategory.Perfume,
                        1000016 or 1000153 => ItemCategory.Pickles,
                        1000028 or 1000029 or 1000030 or 1000031 or 1000032 or 1000144 or 1000167 or 1000168 => ItemCategory.SmallFish,
                        1000158 => ItemCategory.Soup,
                        1000156 => ItemCategory.Tea,
                        1000175 => ItemCategory.TeaCan,
                        1000152 => ItemCategory.TeaLeaf,
                        1000182 => ItemCategory.WildVegetables,
                        1000010 => ItemCategory.Yogurt,
                        _ => ItemCategory.None
                    };
                    break;
            }

            switch (itemCat)
            {
                case ItemCategory.AnimalHair:
                case ItemCategory.BallOfYarn:
                    quality = QualityDict[QualityType.Clip];
                    break;
                case ItemCategory.Egg:
                case ItemCategory.Mayonnaise:
                    quality = QualityDict[QualityType.Egg];
                    break;
                case ItemCategory.Milk:
                case ItemCategory.Cheese:
                case ItemCategory.Butter:
                case ItemCategory.Yogurt:
                    quality = QualityDict[QualityType.Milk];
                    break;
                case ItemCategory.Cicada:
                case ItemCategory.Beetle:
                case ItemCategory.StagBeetle:
                case ItemCategory.Dragonfly:
                case ItemCategory.FireFly:
                case ItemCategory.Frog:
                case ItemCategory.Butterfly:
                case ItemCategory.Locust:
                case ItemCategory.Bagworm:
                case ItemCategory.Bee:
                case ItemCategory.HermitCrab:
                case ItemCategory.Ladybug:
                case ItemCategory.Crickets:
                    quality = QualityDict[QualityType.Bug];
                    break;
                case ItemCategory.PetFood:
                case ItemCategory.CropSeeds:
                case ItemCategory.FruitTreeSeedlings:
                case ItemCategory.FlowerSeeds:
                case ItemCategory.Crop:
                case ItemCategory.FruitTree:
                case ItemCategory.Flower:
                case ItemCategory.Pickles:
                case ItemCategory.TeaCan:
                case ItemCategory.Ingredients:
                case ItemCategory.Bouquet:
                case ItemCategory.Tea:
                case ItemCategory.TeaLeaf:
                    quality = QualityDict[QualityType.Crop];
                    break;
                case ItemCategory.TrainingItems:
                    quality = (int)Math.Round((QualityDict[QualityType.Crop] + QualityDict[QualityType.Fish]) / 2.0f);
                    break;
                case ItemCategory.Salad:
                case ItemCategory.Soup:
                case ItemCategory.MainDish:
                case ItemCategory.HorsdOeuvre:
                case ItemCategory.Dessert:
                case ItemCategory.Juice:
                    quality = (int)Math.Round(Enumerable.Average([
                        QualityDict[QualityType.Crop],
                        QualityDict[QualityType.Milk],
                        QualityDict[QualityType.Egg],
                        QualityDict[QualityType.Fish],
                        QualityDict[QualityType.Honey],
                        QualityDict[QualityType.Mushroom]
                    ]));
                    break;
                case ItemCategory.SmallFish:
                case ItemCategory.SlightlyBiggerFish:
                case ItemCategory.BigFish:
                case ItemCategory.MasterFish:
                    quality = QualityDict[QualityType.Fish];
                    break;
                case ItemCategory.Fertilize:
                case ItemCategory.Accessories:
                case ItemCategory.Stone:
                case ItemCategory.Firewood:
                case ItemCategory.Weed:
                case ItemCategory.WildVegetables:
                case ItemCategory.Herbs:
                case ItemCategory.StoneMaterial:
                case ItemCategory.WoodMaterial:
                case ItemCategory.Ore:
                case ItemCategory.Jewelry:
                case ItemCategory.Wildflowers:
                    quality = QualityDict[QualityType.Forage];
                    break;
                case ItemCategory.Oil:
                case ItemCategory.Perfume:
                    quality = (int)Math.Round((QualityDict[QualityType.Crop] + QualityDict[QualityType.Forage]) / 2.0f);
                    break;
                case ItemCategory.Honey:
                case ItemCategory.HiveHoney:
                    quality = QualityDict[QualityType.Honey];
                    break;
                case ItemCategory.Mushroom:
                case ItemCategory.MushroomStarter:
                    quality = QualityDict[QualityType.Mushroom];
                    break;
                case ItemCategory.None:
                case ItemCategory.Hatchet:
                case ItemCategory.Sickle:
                case ItemCategory.Hoe:
                case ItemCategory.WateringCan:
                case ItemCategory.FishingRod:
                case ItemCategory.MilkingMachine:
                case ItemCategory.Brush:
                case ItemCategory.HairShears:
                case ItemCategory.AnimalFood:
                case ItemCategory.TheTreeThatWasPulledOut:
                case ItemCategory.Seasoning:
                case ItemCategory.Color:
                case ItemCategory.Coin:
                case ItemCategory.FishingBait:
                case ItemCategory.Treasure:
                case ItemCategory.RecycledProducts:
                case ItemCategory.Tent:
                case ItemCategory.Counter:
                case ItemCategory.OrnamentsSmall:
                case ItemCategory.LargeOrnaments:
                case ItemCategory.MementosResident:
                case ItemCategory.MementosFair:
                case ItemCategory.MementosCoro:
                case ItemCategory.MementosMiniGame:
                case ItemCategory.MementosFestival:
                case ItemCategory.ProposalItem:
                case ItemCategory.Nice:
                case ItemCategory.Sun:
                case ItemCategory.Present:
                case ItemCategory.FlyingStone:
                case ItemCategory.FruitOfTheTreeOfPower:
                case ItemCategory.Sprinkler:
                case ItemCategory.StormKit:
                case ItemCategory.HappyStone:
                case ItemCategory.HappyUtensils:
                case ItemCategory.Emote:
                case ItemCategory.CoroBox:
                case ItemCategory.Skill:
                case ItemCategory.Construction:
                case ItemCategory.Expansion:
                case ItemCategory.Other:
                case ItemCategory.SourceFishingBait:
                case ItemCategory.GrassSeeds:
                case ItemCategory.FishBones:
                case ItemCategory.End:
                    quality = -1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            return quality;
        }

        /// <summary>
        /// Determines quality of animal products for the current save. References the product quality of the best animal 
        /// the player has for each category.
        /// </summary>
        /// <returns>Quality of milk, wool, and eggs for current save</returns>
        /// <exception cref="ArgumentOutOfRangeException">Invalid ProductType</exception>
        private static (int milkQuality, int clipQuality, int eggQuality) GetAnimalProductQuality()
        {
            var qSetting = SettingAssetManager.Instance.QualitySetting;
            var farmAnimals = Il2CppHelper
                .ToSystemList(
                    new Il2CppSystem.Collections.Generic.List<AnimalParemeterBase>(AnimalManager.Instance.AllAnimalParams))
                .Where(x => x.ToFarmAnimalParameter != null)
                .Select(x => x.ToFarmAnimalParameter).ToList();
            var milkQuality = 0;
            var clipQuality = 0;
            var eggQuality = 0;
            
            foreach (var animal in farmAnimals)
            {
                switch (animal.GetProductType())
                {
                    case FarmAnimalParameter.ProductType.None:
                        break;
                    case FarmAnimalParameter.ProductType.Milk:
                        milkQuality = qSetting.GetQuality(animal.GetQualityValue()) > milkQuality
                            ? qSetting.GetQuality(animal.GetQualityValue())
                            : milkQuality;
                        break;
                    case FarmAnimalParameter.ProductType.Clip:
                        clipQuality = qSetting.GetQuality(animal.GetQualityValue()) > clipQuality
                            ? qSetting.GetQuality(animal.GetQualityValue())
                            : clipQuality;
                        break;
                    case FarmAnimalParameter.ProductType.Egg:
                        eggQuality = qSetting.GetQuality(animal.GetQualityValue()) > eggQuality
                            ? qSetting.GetQuality(animal.GetQualityValue())
                            : eggQuality;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return (Math.Clamp(milkQuality, 1, 14), 
                Math.Clamp(clipQuality, 1, 14), 
                Math.Clamp(eggQuality, 1, 14));
        }

        /// <summary>
        /// Determines crop quality for the current save. Based on crop show wins and soil level.
        /// </summary>
        /// <returns>Crop quality for current save</returns>
        private static int GetCropQuality()
        {
            var soilLevel = ExpansionManager.Instance.RidgeLevel;
            var festExe = FestivalExecutor.Instance;
            var bronzeTot = 0;
            var silverTot = 0;
            var goldTot = 0;

            bronzeTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Spring,
                FestivalExecutor.CompetitionRank.Bronze);
            silverTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Spring,
                FestivalExecutor.CompetitionRank.Silver);
            goldTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Spring,
                FestivalExecutor.CompetitionRank.Gold);

            bronzeTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Summer,
                FestivalExecutor.CompetitionRank.Bronze);
            silverTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Summer,
                FestivalExecutor.CompetitionRank.Silver);
            goldTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Summer,
                FestivalExecutor.CompetitionRank.Gold);

            bronzeTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn,
                FestivalExecutor.CompetitionRank.Bronze);
            silverTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn,
                FestivalExecutor.CompetitionRank.Silver);
            goldTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Autumn,
                FestivalExecutor.CompetitionRank.Gold);

            bronzeTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Winter,
                FestivalExecutor.CompetitionRank.Bronze);
            silverTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Winter,
                FestivalExecutor.CompetitionRank.Silver);
            goldTot += festExe.GetFestivalWinCount(FestivalExecutor.FestivalCategory.CompetitionCrop_Winter,
                FestivalExecutor.CompetitionRank.Gold);

            return (int)Math.Clamp(
                (soilLevel * 1) + (Math.Floor(bronzeTot * 0.5) * 1) + (silverTot * 1) + (goldTot * 2), 1, 14);
        }
    }
    
    /// <summary>
    /// Helper functions for converting to and from Il2Cpp objects and System objects
    /// </summary>
    private static class Il2CppHelper
    {
        /// <summary>
        /// Converts System List to Il2Cpp List
        /// </summary>
        /// <param name="list">System List</param>
        /// <typeparam name="T">Type of elements in list</typeparam>
        /// <returns>Il2Cpp list</returns>
        public static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(List<T> list)
        {
            var Il2CppList = new Il2CppSystem.Collections.Generic.List<T>();

            foreach (var item in list)
            {
                Il2CppList.Add(item);
            }

            return Il2CppList;
        }

        /// <summary>
        /// Converts Il2Cpp List to System List
        /// </summary>
        /// <param name="list">Il2Cpp list</param>
        /// <typeparam name="T">Type of elements in list</typeparam>
        /// <returns>System list</returns>
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

    /// <summary>
    /// Helper functions for text replacements
    /// </summary>
    private static class TextFormatter
    {
        /// <summary>
        /// Replaces instances of {char} with the proper character's name
        /// </summary>
        /// <param name="input">String with placeholder elements</param>
        /// <param name="charName">Name of the character to replace {char}</param>
        /// <returns>Input string with replacements made</returns>
        public static string FormatName(string input, string charName)
        {
            return input.Replace("{char}", charName);
        }
    }

    /// <summary>
    /// Handles reading, writing, and validation of json files
    /// </summary>
    private static class JsonHandler
    {
        /// <summary>
        /// Writes supplied missionData to file. Used in saving mission information
        /// </summary>
        /// <param name="jsonPath">Path of json file</param>
        /// <param name="missionData">Data to be written to json file</param>
        /// <returns>Writing success</returns>
        public static bool Write(string jsonPath, List<MissionInfo> missionData)
        {
            var json = JsonSerializer.Serialize(missionData, new JsonSerializerOptions { WriteIndented = true });

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
                File.WriteAllText(jsonPath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads in json file
        /// </summary>
        /// <param name="jsonPath">Path of json file</param>
        /// <typeparam name="T">Type of object json file deserialized as</typeparam>
        /// <returns>Deserialized json object</returns>
        public static T Read<T>(string jsonPath) where T : class
        {
            return File.Exists(jsonPath)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(jsonPath))
                : null;
        }
        
        /// <summary>
        /// Confirms that each instance of ItemList contains lists for ItemIds, ItemType, ItemStack, and ItemQuality
        /// of all matching length.
        /// </summary>
        /// <param name="requestGroups">Request groups to be validated</param>
        /// <returns>List of all RequestGroup Ids that do not pass validation test</returns>
        public static List<uint> ValidateItemListLength(List<RequestGroups> requestGroups)
        {
            return (from @group in requestGroups
                let hasInvalidItem =
                    @group.Items.Any(item =>
                        item.ItemIds.Count != item.ItemType.Count || item.ItemIds.Count != item.ItemStack.Count ||
                        item.ItemIds.Count != item.ItemQuality.Count)
                where hasInvalidItem
                select @group.Id).ToList();
        }
        
        /// <summary>
        /// Validates that all RequestGroups have a unique Id value
        /// </summary>
        /// <param name="requestGroups">Request groups to be validated</param>
        /// <returns>List of all RequestGroup Ids that do not pass validation test</returns>
        public static List<uint> ValidateUniqueIds(List<RequestGroups> requestGroups)
        {
            var seenIds = new HashSet<uint>();
            var duplicates = new HashSet<uint>();
    
            foreach (var item in requestGroups.Where(item => !seenIds.Add(item.Id)))
                duplicates.Add(item.Id);

            return duplicates.ToList();
        }
        
        /// <summary>
        /// Validates that all ItemTypes correspond properly to the ItemIds they represent. Based on Id numeric range.
        /// </summary>
        /// <param name="requestGroups">Request groups to be validated</param>
        /// <returns>List of all RequestGroup Ids that do not pass validation test</returns>
        public static List<uint> ValidateItemTypes(List<RequestGroups> requestGroups)
        {
            var invalidRequestGroupIds = new List<uint>();
        
            foreach (var requestGroup in requestGroups.Where(requestGroup => requestGroup.Items != null))
            {
                foreach (var itemGroup in requestGroup.Items.Where(itemGroup => itemGroup.ItemIds != null && itemGroup.ItemType != null))
                {
                    // Check if arrays have the same length
                    if (itemGroup.ItemIds.Count != itemGroup.ItemType.Count)
                    {
                        invalidRequestGroupIds.Add(requestGroup.Id);
                        break;
                    }
                
                    // Check each ItemId against its corresponding ItemType
                    for (var i = 0; i < itemGroup.ItemIds.Count; i++)
                    {
                        var itemId = itemGroup.ItemIds[i];
                        var itemType = itemGroup.ItemType[i];
                    
                        var isValid = itemType switch
                        {
                            RequiredItemType.Item => itemId is >= 10000 and <= 999999,  // Item number
                            RequiredItemType.Category => itemId is >= 0 and <= 999,     // Category number
                            RequiredItemType.Group => itemId > 999999,                  // Mission group number
                            _ => false                                                  // Invalid ItemType value
                        };

                        if (isValid) continue;
                        invalidRequestGroupIds.Add(requestGroup.Id);
                        break;
                    }
                
                    // If we already found this request group to be invalid, move to the next one
                    if (invalidRequestGroupIds.Contains(requestGroup.Id))
                        break;
                }
            }
        
            return invalidRequestGroupIds.Distinct().ToList();
        }
    }

    /// <summary>
    /// Functions useful in troubleshooting or otherwise generating data useful for development
    /// </summary>
    private class Troubleshoot
    {
        /// <summary>
        /// Outputs csv style price list of every item in supplied list of RequestGroups. Based on base item price and quantity.
        /// Does not consider item quality.
        /// </summary>
        /// <param name="requestGroups">RequestGroups for which item prices will be printed</param>
        public static void OutputPriceList(List<RequestGroups> requestGroups)
        {
            Log.LogInfo("ItemGroupId,ItemId,Stack,Price");
            
            foreach (var group in requestGroups)
            {
                foreach (var itemGroup in group.Items)
                {
                    for (var j = 0; j < itemGroup.ItemIds.Count; j++)
                    {
                        Log.LogInfo($"{group.Id},{itemGroup.ItemIds[j]},{itemGroup.ItemStack[j]},{GetPrice(itemGroup.ItemIds[j], itemGroup.ItemType[j], itemGroup.ItemStack[j])}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets price of supplied item. Based on base price and quantity.
        /// For groups and categories, trimmed mean is used as base price.
        /// For items considered recyclable, Treasureland sell price used as base price.
        /// </summary>
        /// <param name="item">Item for which price is determined</param>
        /// <param name="type">Type of item id supplied</param>
        /// <param name="stack">Quantity of item</param>
        /// <returns>Item price</returns>
        /// <exception cref="ArgumentOutOfRangeException">Invalid RequiredItemType</exception>
        private static int GetPrice(uint item, RequiredItemType type, int stack)
        {
            var itemMaster = MasterDataManager.Instance.ItemMaster;
            var recyclable = new Dictionary<uint, int>
            {
                { 115000, 200 },
                { 115001, 400 },
                { 115002, 1200 },
                { 115003, 300 },
                { 115004, 600 },
                { 115005, 7777 },
                { 115006, 7777 },
                { 115007, 7777 },
                { 115008, 7777 },
                { 115009, 7777 },
                { 115010, 7777 },
                { 115011, 7777 },
                { 115012, 100 },
                { 115013, 2000 }
            };

            var totalPrice = 0;
            
            switch (type)
            {
                case RequiredItemType.Item:
                    if (recyclable.TryGetValue(item, out var value))
                    {
                        totalPrice += value * stack;
                    }
                    else
                    {
                        totalPrice += itemMaster.GetData(item).Price * stack;
                    }

                    break;
                case RequiredItemType.Category:
                {
                    var itemList = Il2CppHelper.ToSystemList(itemMaster.GetItemListByItemCategory((ItemCategory)item)).Select(x => x.Price).ToList();
                    totalPrice = CalculateTrimmedMean(itemList, 0.15) * stack;
                    
                    break;
                }

                case RequiredItemType.Group:
                {
                    var itemList = Il2CppHelper.ToSystemList(MasterDataManager.Instance.MissionstuffGroupMaster
                        .GetData(item).RequiredItemIdList).Where(x => x != 0);
                    var prices = itemList.Select(i => recyclable.TryGetValue(i, out var value) ? value : itemMaster.GetData(i).Price)
                        .ToList();
                    totalPrice = CalculateTrimmedMean(prices, 0.15) * stack;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return totalPrice;
        }
        
        /// <summary>
        /// Calculates mean of provided int list. Trims percentage of high and low values.
        /// </summary>
        /// <param name="numbers">List of integers for which the mean is calculated</param>
        /// <param name="trimPercentage">Percentage of list to trim from both ends</param>
        /// <returns>Trimmed/truncated mean</returns>
        private static int CalculateTrimmedMean(List<int> numbers, double trimPercentage)
        {
            if ((numbers == null || numbers.Count == 0) || (trimPercentage < 0 || trimPercentage >= 50)) return 0;
            
            var trimCount = (int)Math.Round(numbers.Count * trimPercentage / 100);

            return trimCount * 2 >= numbers.Count
                ? 0
                : (int)numbers.OrderBy(x => x).Skip(trimCount).Take(numbers.Count - (2 * trimCount)).Average();
        }
    }

    /// <summary>
    /// Data class for generated missions
    /// </summary>
    private class MissionInfo
    {
        public uint? MissionId { get; set; }
        public List<uint> ItemId { get; set; }
        public List<RequiredItemType> ItemType { get; set; }
        public List<int> ItemStack { get; set; }
        public List<int> ItemQuality { get; set; }
        public uint RewardItemId { get; set; }
        public int RewardItemStack { get; set; }
        public int RewardItemQuality { get; set; }
        public string MissionName { get; set; }
        public string MissionCondition { get; set; }
        public string MissionCaption { get; set; }
        
        public override string ToString()
        {
            var sb = new StringBuilder();
    
            if(MissionId != null) sb.AppendLine($"   MissionId: {MissionId.ToString()}");
            sb.AppendLine("   | Required Items                          |");
            
            if (ItemId?.Count > 0)
            {
                sb.AppendLine("   | Ids     | Type     | Quantity | Quality |");
                
                for (var i = 0; i < ItemId.Count(x => x != 0); i++)
                {
                    var id = i < ItemId.Count ? ItemId[i].ToString() : "N/A";
                    var type = i < ItemType?.Count ? ItemType[i].ToString() : "N/A";
                    var quantity = i < ItemStack?.Count ? ItemStack[i].ToString() : "N/A";
                    var quality = i < ItemQuality?.Count ? ItemQuality[i].ToString() : "N/A";
            
                    sb.AppendLine($"   | {id,-7} | {type,-8} | {quantity,-8} | {quality,-7} |");
                }
            }
            else
            {
                sb.AppendLine("   No required items");
            }
    
            sb.AppendLine();
            sb.AppendLine("   | RewardItem                   |");
            
            if (RewardItemId > 0)
            {
                sb.AppendLine("   | Ids     | Quantity | Quality |");
                sb.AppendLine($"   | {RewardItemId,-7} | {RewardItemStack,-8} | {RewardItemQuality,-7} |");
            }
            else
            {
                sb.AppendLine("   No reward item");
            }
    
            sb.AppendLine();
            sb.AppendLine($"   Name:      {MissionName ?? "N/A"}");
            sb.AppendLine($"   Condition: {MissionCondition ?? "N/A"}");
            sb.AppendLine($"   Caption:   {MissionCaption ?? "N/A"}");
    
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Data class for RequestGroup data. Data used to generate missions.
    /// </summary>
    private class RequestGroups
    {
        public uint Id { get; set; }
        public List<ItemList> Items { get; set; }
        public Special Special { get; set; }
        public List<uint> Characters { get; set; }
        public string MissionName { get; set; }
        public string MissionCondition { get; set; }
        public string MissionCaption { get; set; }
        public string DebugInfo { get; set; }
    }

    /// <summary>
    /// Inner data class for RequestGroups. Data for items to be chosen from in request mission generation.
    /// </summary>
    private class ItemList
    {
        public List<uint> ItemIds { get; set; }
        public List<RequiredItemType> ItemType { get; set; }
        public List<int> ItemStack { get; set; }
        public List<int> ItemQuality { get; set; }
        public RequestCategory Category { get; set; }
        public float DifficultChance { get; set; }
        public bool PickOne { get; set; }
    }

    /// <summary>
    /// Data class for reward item generation.
    /// </summary>
    private class RewardGroups
    {
        public uint Id { get; set; }
        public RequestCategory Category { get; set; }
        public List<uint> ItemIds { get; set; }
        public List<int> ItemStack { get; set; }
        public List<int> ItemQuality { get; set; }
    }

    /// <summary>
    /// Category request belongs to
    /// </summary>
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
        Seed,
        Mineral,
        Misc
    }

    /// <summary>
    /// Dictates which special conditions a mission can be tied to.
    /// </summary>
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