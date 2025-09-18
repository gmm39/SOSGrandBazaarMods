using System;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using UnityEngine;

namespace LightControl;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private static new ManualLogSource Log;
    private static ConfigEntry<float> DayIntensity;
    private static ConfigEntry<float> NightIntensity;
    private static ConfigEntry<float> DayBloom;
    private static ConfigEntry<float> NightBloom;
    private static ConfigEntry<float> NightTimeOffset;
    //private static ConfigEntry<float> NightTimeOffsetSpring // -0.25f
    //private static ConfigEntry<float> NightTimeOffsetSummer // -0.5f
    private static ConfigEntry<float> IndoorIntensityOffset;

    public override void Load()
    {
        // Plugin startup logic
        DayIntensity = Config.Bind("Intensity", "DayIntensity", 1.2f,
            "Lower is darker, higher is brighter. GameDefault: 1.2");
        NightIntensity = Config.Bind("Intensity", "NightIntensity", 0.7f,
            "Lower is darker, higher is brighter. GameDefault: 1.2");
        DayBloom = Config.Bind("Bloom", "DayBloom", 1.7f,
            "The amount of bloom during the day. GameDefault: 1.7");
        NightBloom = Config.Bind("Bloom", "NightBloom", 3.0f,
            "The amount of bloom during the night. GameDefault: 1.7");
        NightTimeOffset = Config.Bind("Miscellaneous", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        IndoorIntensityOffset = Config.Bind("Miscellaneous", "IndoorIntensityOffset", 0.2f,
            "Offset indoor intensity during night.");

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(LightPatch));
    }

    [HarmonyPatch]
    private class LightPatch
    {
        private enum State
        {
            Day,
            DayToNight,
            Night,
            NightToDay
        }

        private static float currentTime;
        private static float nightStart;
        private static float nightEnd;

        private static float nightIntensity;
        private static float dayIntensity;
        
        private static bool isIndoor;
        private static State state;

        private static float lastTime = -1.0f;
        private static float transitionLength = 1.0f;

        private static void SetValues(LightControlManager __instance)
        {
            currentTime = __instance.CurrentTime;
            nightStart = __instance.SeasonalTimeSetting.nightStart + NightTimeOffset.Value;
            nightEnd = __instance.SeasonalTimeSetting.nightEnd;
            
            isIndoor = FieldManager.Instance.CurrentFieldMasterData.IsInDoor;
        }

        private static void SetState()
        { 
            if (currentTime >= 5.0f && currentTime < nightStart)
            {
                state = State.Day;
            }
            else if (currentTime > 5.0f && currentTime <= nightStart + transitionLength)
            {
                state = State.DayToNight;
            }
            else if (currentTime >= nightStart || currentTime <= nightEnd)
            {
                state = State.Night;
            }
            else
            {
                state = State.NightToDay;
            }
        }

        private static void CalculateIntensity()
        {
            nightIntensity = isIndoor ?
                Math.Clamp(NightIntensity.Value + IndoorIntensityOffset.Value, NightIntensity.Value, DayIntensity.Value) :
                NightIntensity.Value;
        }

        private static void LogOutput(LightControlManager __instance)
        {
            if ((int)(lastTime * 10) == (int)(Math.Round(currentTime, 1) * 10)) return;
            lastTime = (float)Math.Round(currentTime, 1);
            var msg = string.Format(
                "Time: {0:f1} Intensity: {1:f3} Bloom: {2:f3} NightStart: {3:f1} NightEnd: {4:f1}" +
                " State: {5} Indoor: {6}", Math.Round(currentTime, 1), __instance.directionalLight.intensity,
                __instance.postProcessSetting.bloomIntensity, nightStart, nightEnd, state,
                FieldManager.Instance.CurrentFieldMasterData.IsInDoor);
            Log.LogInfo(msg);
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LightControlManager), "Update")]
        public static void Postfix(LightControlManager __instance)
        {
            SetValues(__instance);
            SetState();

            CalculateIntensity();

            switch (state)
            {
                case State.Day:
                    __instance.directionalLight.intensity = DayIntensity.Value;
                    __instance.postProcessSetting.bloomIntensity = DayBloom.Value;
                    break;
                
                case State.DayToNight:
                    __instance.directionalLight.intensity = 
                        Mathf.Lerp(DayIntensity.Value, nightIntensity, (currentTime - nightStart) / transitionLength);
                    __instance.postProcessSetting.bloomIntensity =
                        Mathf.Lerp(DayBloom.Value, NightBloom.Value, (currentTime - nightStart) / transitionLength);
                    break;
                
                case State.Night:
                    __instance.directionalLight.intensity = nightIntensity;
                    __instance.postProcessSetting.bloomIntensity = NightBloom.Value;
                    break;
                
                case State.NightToDay:
                    __instance.directionalLight.intensity = 
                        Mathf.Lerp(nightIntensity, DayIntensity.Value, currentTime - nightEnd);
                    __instance.postProcessSetting.bloomIntensity = 
                        Mathf.Lerp(NightBloom.Value, DayBloom.Value, currentTime - nightEnd);
                    break;
                
                default:
                    Log.LogInfo("Improper state!");
                    break;
            }

            LogOutput(__instance);
        }
    }
}