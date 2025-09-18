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
    private static ConfigEntry<float> IndoorIntensityOffset;
    
    private static ConfigEntry<float> NightTimeOffsetSpring;
    private static ConfigEntry<float> TransitionLengthSpring;
    
    private static ConfigEntry<float> NightTimeOffsetSummer;
    private static ConfigEntry<float> TransitionLengthSummer;
    
    private static ConfigEntry<float> NightTimeOffsetAutumn;
    private static ConfigEntry<float> TransitionLengthAutumn;
    
    private static ConfigEntry<float> NightTimeOffsetWinter;
    private static ConfigEntry<float> TransitionLengthWinter;

    public override void Load()
    {
        // Plugin startup logic
        DayIntensity = Config.Bind("0. Intensity", "DayIntensity", 1.2f,
            "Lower is darker, higher is brighter. GameDefault: 1.2");
        NightIntensity = Config.Bind("0. Intensity", "NightIntensity", 0.75f,
            "Lower is darker, higher is brighter. GameDefault: 1.2");
        
        DayBloom = Config.Bind("1. Bloom", "DayBloom", 1.0f,
            "The amount of bloom during the day. GameDefault: 1.7");
        NightBloom = Config.Bind("1. Bloom", "NightBloom", 1.7f,
            "The amount of bloom during the night. GameDefault: 1.7");
        
        IndoorIntensityOffset = Config.Bind("2. Miscellaneous", "IndoorIntensityOffset", 0.05f,
            "Offset indoor intensity during night.");
        
        NightTimeOffsetSpring = Config.Bind("3. Spring", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        TransitionLengthSpring = Config.Bind("3. Spring", "TransitionLength", 1.0f,
            "Length of time for the transition between day and night.");
        
        NightTimeOffsetSummer = Config.Bind("4. Summer", "NightTimeOffset", -0.75f,
            "Offset applied to night start time.");
        TransitionLengthSummer = Config.Bind("4. Summer", "TransitionLength", 1.5f,
            "Length of time for the transition between day and night.");
        
        NightTimeOffsetAutumn = Config.Bind("5. Autumn", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        TransitionLengthAutumn = Config.Bind("5. Autumn", "TransitionLength", 1.0f,
            "Length of time for the transition between day and night.");
        
        NightTimeOffsetWinter = Config.Bind("6. Winter", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        TransitionLengthWinter = Config.Bind("6. Winter", "TransitionLength", 1.0f,
            "Length of time for the transition between day and night.");

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
        private static float transitionLength;

        private static float nightIntensity;
        private static float dayIntensity;
        
        private static bool isIndoor;
        private static BokuMonoWeather weather;
        private static BokuMonoSeason season;
        private static State state;

        private static float lastTime = -1.0f;

        private static void SetValues(LightControlManager __instance)
        {
            weather = __instance.currentWeather;
            season = DateManager.Instance.FixedDate.Season;
            var offset = 0.0f;

            switch (season)
            {
                case BokuMonoSeason.Spring:
                    offset = NightTimeOffsetSpring.Value;
                    transitionLength = TransitionLengthSpring.Value;
                    break;
                case BokuMonoSeason.Summer:
                    offset = NightTimeOffsetSummer.Value;
                    transitionLength = TransitionLengthSummer.Value;
                    break;
                case BokuMonoSeason.Autumn:
                    offset = NightTimeOffsetAutumn.Value;
                    transitionLength = TransitionLengthAutumn.Value;
                    break;
                case BokuMonoSeason.Winter:
                    offset = NightTimeOffsetWinter.Value;
                    transitionLength = TransitionLengthWinter.Value;
                    break;
                default:
                    Log.LogError($"Unknown season {season}");
                    break;
            }
            
            currentTime = __instance.CurrentTime;
            nightStart = __instance.SeasonalTimeSetting.nightStart + offset;
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

        private static float ShapeCurve(float t)
        {
            // easeInOutQuad
            return (float)(t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2);
        }

        private static void LogOutput(LightControlManager __instance)
        {
            if ((int)(lastTime * 10) == (int)(Math.Round(currentTime, 1) * 10)) return;
            lastTime = (float)Math.Round(currentTime, 1);
            var msg = string.Format(
                "Time: {0:f1} | Intensity: {1:f3} | Bloom: {2:f3} | NightStart: {3:f1} | NightEnd: {4:f1} |" +
                " State: {5} | Indoor: {6} | Season: {7} | Weather: {8}", Math.Round(currentTime, 1), __instance.directionalLight.intensity,
                __instance.postProcessSetting.bloomIntensity, nightStart, nightEnd, state,
                isIndoor, season, weather);
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
                        Mathf.Lerp(DayIntensity.Value, nightIntensity, ShapeCurve((currentTime - nightStart) / transitionLength));
                    __instance.postProcessSetting.bloomIntensity =
                        Mathf.Lerp(DayBloom.Value, NightBloom.Value, ShapeCurve((currentTime - nightStart) / transitionLength));
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
                    Log.LogError($"Unknown state {state}");
                    break;
            }

            LogOutput(__instance);
        }
    }
}