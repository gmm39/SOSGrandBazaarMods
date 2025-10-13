using System.Collections.Generic;
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
        private static Random rnd = new Random();
        
        private static HashSet<uint> finalMissions =
        [
            30104, 30204, 30304, 30404, 30504, 30604, 30704, 30804, 30904, 31004, 31104, 31204, 31304, 31404, 31504,
            31604, 31704, 31804, 31904, 32004, 32104, 32204, 32304, 32404, 32504, 32604, 32704, 32804
        ];
        private static List<MissionManager.OrderData> availableMissions = new List<MissionManager.OrderData>();
        private static List<uint> selectedMissions = new List<uint>();

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
            
            newMission.ConditionParams = new Il2CppSystem.Collections.Generic.List<uint>();
            newMission.CompleteCondition = CompleteCondition.Delivery;
            newMission.CompleteSubCondition = 0;
            newMission.CompleteSubConditionParam = 0;
            newMission.CompleteValue = 0;
            
            selectedMissions.Add(newMission.Id);
            
            foreach(var mission in selectedMissions) Log.LogInfo($"Id: {mission}");
        }
    }
}