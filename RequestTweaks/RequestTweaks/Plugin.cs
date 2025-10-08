using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.ResidentMission;
using HarmonyLib;

namespace RequestTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private new static ManualLogSource Log;
    private static ConfigEntry<float> RewardLikeMulti;
    private static ConfigEntry<int> RewardQualityBoost;
    private static ConfigEntry<float> RewardAmountMulti;
    
    private static ConfigEntry<int> RequiredItemQualityBoost;
    private static ConfigEntry<float> RequiredItemAmountMulti;
    
    private static ConfigEntry<float> ActionMissionAmountMulti;
    private static ConfigEntry<int> ActionMissionAmountCap;

    public override void Load()
    {
        // Plugin startup logic
        RewardLikeMulti = Config.Bind("-----01 REWARDS-----", "Reward_Likeability_Multiplier", 1.0f,
            "Multiple the likeability reward for completing requests");
        RewardQualityBoost = Config.Bind("-----01 REWARDS-----", "Reward_Quality_Boost", 0,
            "Change the quality of rewards by the given amount. Every 1 added is half a star increased." +
            "\nPositive numbers increase the quality while negative decreases it.");
        RewardAmountMulti = Config.Bind("-----01 REWARDS-----", "Reward_Amount_Multiplier", 1.0f,
            "Multiply the amount of reward items by the given amount.");
        
        RequiredItemQualityBoost = Config.Bind("-----02 ITEM REQUIREMENT-----", "Required_Item_Quality_Boost", 0,
            "Change the quality of the required items by the given amount. Every 1 added is half a star increased." +
            "\nPositive numbers increase the quality while negative decreases it.");
        RequiredItemAmountMulti = Config.Bind("-----02 ITEM REQUIREMENT-----", "Required_Item_Amount_Multiplier", 1.0f,
            "Multiply the amount of required items by the given amount.");
        
        ActionMissionAmountMulti = Config.Bind("-----03 COUNT REQUIREMENT-----", "Action_Mission_Amount_Multiplier", 1.0f,
            "For missions that require a number of actions to be performed, multiply the amount required by the given amount.");
        ActionMissionAmountCap = Config.Bind("-----03 COUNT REQUIREMENT-----", "Action_Mission_Amount_Cap", 0,
            "For missions that require a number of actions to be performed, cap the amount required by the given amount." +
            "\nSetting to 0 will disable the cap. Keeps requests with high base counts from getting absurd when multiplied.");
        
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(RequestPatch));
    }

    private static class RequestPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void Postfix()
        {
            var toot = MasterDataManager.Instance.ResidentMissionMaster.list;

            foreach (var mission in toot)
            {
                mission.RewardLikeability = Math.Clamp((int)Math.Round(mission.RewardLikeability * RewardLikeMulti.Value), 0, int.MaxValue);
                mission.RewardNum = Math.Clamp((int)Math.Round(mission.RewardNum * RewardAmountMulti.Value), 1, int.MaxValue);
                
                if (mission.RewardCategory == RewardsType.Item)
                {
                    mission.RewardNum = Math.Clamp((int)Math.Round(mission.RewardNum * RewardAmountMulti.Value), 0, int.MaxValue);

                    if (mission.RewardQuality < 1) continue;
                    mission.RewardQuality = Math.Clamp(mission.RewardQuality + RewardQualityBoost.Value, 1, 14);
                }

                switch (mission.CompleteCondition)
                {
                    case CompleteCondition.Delivery:
                    {
                        for (var i = 0; i < mission.RequiredItemStack.Count; i++)
                        {
                            if (mission.RequiredItemStack[i] == 0) break;
                            mission.RequiredItemStack[i] = Math.Clamp((int)Math.Round(mission.RequiredItemStack[i] * RequiredItemAmountMulti.Value), 0, int.MaxValue);
                            Log.LogInfo($"ItemStack: {mission.RequiredItemStack[i]}");
                            if (mission.RequiredItemQuality[i] < 1) continue;
                            mission.RequiredItemQuality[i] = Math.Clamp(mission.RequiredItemQuality[i] + RequiredItemQualityBoost.Value, 1, 14);
                        }

                        break;
                    }
                    case CompleteCondition.Count:
                    {
                        mission.CompleteValue = Math.Clamp((int)Math.Round(mission.CompleteValue * ActionMissionAmountMulti.Value),
                            1, ActionMissionAmountCap.Value <= 0 ? int.MaxValue : ActionMissionAmountCap.Value);
                        break;
                    }
                    case CompleteCondition.Search:
                    case CompleteCondition.End:
                    default:
                        break;
                }
            }
        }
    }
}