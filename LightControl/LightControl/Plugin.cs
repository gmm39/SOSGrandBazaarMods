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
    
    private static ConfigEntry<float> BaseDayIntensity;
    private static ConfigEntry<float> BaseNightIntensity;
    private static ConfigEntry<float> DayBloom;
    private static ConfigEntry<float> NightBloom;
    private static ConfigEntry<float> NightTimeOffset;
    
    private static ConfigEntry<float> IndoorIntensityOffsetDay;
    private static ConfigEntry<float> IndoorIntensityOffsetNight;
    
    private static ConfigEntry<float> NightTimeOffsetSpring;
    private static ConfigEntry<float> TransitionLengthSpring;
    private static ConfigEntry<float> NightIntensityOffsetSpring;
    
    private static ConfigEntry<float> NightTimeOffsetSummer;
    private static ConfigEntry<float> TransitionLengthSummer;
    private static ConfigEntry<float> NightIntensityOffsetSummer;
    
    private static ConfigEntry<float> NightTimeOffsetAutumn;
    private static ConfigEntry<float> TransitionLengthAutumn;
    private static ConfigEntry<float> NightIntensityOffsetAutumn;
    
    private static ConfigEntry<float> NightTimeOffsetWinter;
    private static ConfigEntry<float> TransitionLengthWinter;
    private static ConfigEntry<float> NightIntensityOffsetWinter;
    
    private static ConfigEntry<float> DayIntensityOffsetSunny;
    private static ConfigEntry<float> DayIntensityOffsetRainy;
    private static ConfigEntry<float> DayIntensityOffsetSnowy;
    private static ConfigEntry<float> DayIntensityOffsetTyphoon;
    private static ConfigEntry<float> DayIntensityOffsetHeavySnow;
    private static ConfigEntry<float> DayIntensityOffsetCloudy;
    private static ConfigEntry<float> DayIntensityOffsetHeavyRain;
    private static ConfigEntry<float> DayIntensityOffsetMax;

    public override void Load()
    {
        // Plugin startup logic
        BaseDayIntensity = Config.Bind("0. Intensity", "DayIntensity", 1.2f,
            "Lower is darker, higher is brighter. GameDefault: 1.2");
        BaseNightIntensity = Config.Bind("0. Intensity", "NightIntensity", 0.75f,
            "Lower is darker, higher is brighter. GameDefault: 1.2");
        
        DayBloom = Config.Bind("1. Bloom", "DayBloom", 1.0f,
            "The amount of bloom during the day. GameDefault: 1.7");
        NightBloom = Config.Bind("1. Bloom", "NightBloom", 1.7f,
            "The amount of bloom during the night. GameDefault: 1.7");
        
        IndoorIntensityOffsetDay = Config.Bind("2. Indoor", "IndoorIntensityOffsetDay", 0.0f,
            "Offset indoor intensity during day.");
        IndoorIntensityOffsetNight = Config.Bind("2. Indoor", "IndoorIntensityOffsetNight", 0.05f,
            "Offset indoor intensity during night.");
        
        NightTimeOffsetSpring = Config.Bind("3. Spring", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        TransitionLengthSpring = Config.Bind("3. Spring", "TransitionLength", 1.0f,
            "Length of time for the transition between day and night.");
        NightIntensityOffsetSpring = Config.Bind("3. Spring", "NightIntensityOffset", 0.0f,
            "Offset applied to base night intensity.");
        
        NightTimeOffsetSummer = Config.Bind("4. Summer", "NightTimeOffset", -0.75f,
            "Offset applied to night start time.");
        TransitionLengthSummer = Config.Bind("4. Summer", "TransitionLength", 1.5f,
            "Length of time for the transition between day and night.");
        NightIntensityOffsetSummer = Config.Bind("4. Summer", "NightIntensityOffset", 0.0f,
            "Offset applied to base night intensity.");
        
        NightTimeOffsetAutumn = Config.Bind("5. Autumn", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        TransitionLengthAutumn = Config.Bind("5. Autumn", "TransitionLength", 1.0f,
            "Length of time for the transition between day and night.");
        NightIntensityOffsetAutumn = Config.Bind("5. Autumn", "NightIntensityOffset", 0.0f,
            "Offset applied to base night intensity.");
        
        NightTimeOffsetWinter = Config.Bind("6. Winter", "NightTimeOffset", -0.25f,
            "Offset applied to night start time.");
        TransitionLengthWinter = Config.Bind("6. Winter", "TransitionLength", 1.0f,
            "Length of time for the transition between day and night.");
        NightIntensityOffsetWinter = Config.Bind("6. Winter", "NightIntensityOffset", -0.15f,
            "Offset applied to base night intensity.");
        
        DayIntensityOffsetSunny = Config.Bind("7. Weather", "DayIntensityOffsetSunny", 0.0f,
            "Offset applied to day intensity during sunny weather.");
        DayIntensityOffsetRainy = Config.Bind("7. Weather", "DayIntensityOffsetRainy", -0.066f,
            "Offset applied to day intensity during rainy weather.");
        DayIntensityOffsetSnowy = Config.Bind("7. Weather", "DayIntensityOffsetSnowy", -0.066f,
            "Offset applied to day intensity during snowy weather.");
        DayIntensityOffsetTyphoon = Config.Bind("7. Weather", "DayIntensityOffsetTyphoon", -0.175f,
            "Offset applied to day intensity during typhoon weather.");
        DayIntensityOffsetHeavySnow = Config.Bind("7. Weather", "DayIntensityOffsetHeavySnow", -0.10f,
            "Offset applied to day intensity during heavy snow weather.");
        DayIntensityOffsetCloudy = Config.Bind("7. Weather", "DayIntensityOffsetCloudy", -0.033f,
            "Offset applied to day intensity during cloudy weather.");
        DayIntensityOffsetHeavyRain = Config.Bind("7. Weather", "DayIntensityOffsetHeavyRain", -0.10f,
            "Offset applied to day intensity during heavy rain weather.");
        DayIntensityOffsetMax = Config.Bind("7. Weather", "DayIntensityOffsetMax", 0.0f,
            "Offset applied to day intensity during max weather. Max weather appears in the code as a weather" +
            "option but I have not seen it used.");

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
            isIndoor = FieldManager.Instance.CurrentFieldMasterData.IsInDoor;
            
            nightIntensity = BaseNightIntensity.Value;
            dayIntensity = BaseDayIntensity.Value;
            var offset = 0.0f;

            switch (season)
            {
                case BokuMonoSeason.Spring:
                    offset = NightTimeOffsetSpring.Value;
                    transitionLength = TransitionLengthSpring.Value;
                    nightIntensity += NightIntensityOffsetSpring.Value;
                    break;
                case BokuMonoSeason.Summer:
                    offset = NightTimeOffsetSummer.Value;
                    transitionLength = TransitionLengthSummer.Value;
                    nightIntensity += NightIntensityOffsetSummer.Value;
                    break;
                case BokuMonoSeason.Autumn:
                    offset = NightTimeOffsetAutumn.Value;
                    transitionLength = TransitionLengthAutumn.Value;
                    nightIntensity += NightIntensityOffsetAutumn.Value;
                    break;
                case BokuMonoSeason.Winter:
                    offset = NightTimeOffsetWinter.Value;
                    transitionLength = TransitionLengthWinter.Value;
                    nightIntensity += NightIntensityOffsetWinter.Value;
                    break;
                default:
                    Log.LogError($"Unknown season {season}");
                    break;
            }
            
            currentTime = __instance.CurrentTime;
            nightStart = __instance.SeasonalTimeSetting.nightStart + offset;
            nightEnd = __instance.SeasonalTimeSetting.nightEnd;

            switch (weather)
            {
                case BokuMonoWeather.Sunny:
                    dayIntensity += DayIntensityOffsetSunny.Value;
                    break;
                case BokuMonoWeather.Rainy:
                    dayIntensity += DayIntensityOffsetRainy.Value;
                    break;
                case BokuMonoWeather.Snowy:
                    dayIntensity += DayIntensityOffsetSnowy.Value;
                    break;
                case BokuMonoWeather.Typhoon:
                    dayIntensity += DayIntensityOffsetTyphoon.Value;
                    break;
                case BokuMonoWeather.HeavySnow:
                    dayIntensity += DayIntensityOffsetHeavySnow.Value;
                    break;
                case BokuMonoWeather.Cloudy:
                    dayIntensity += DayIntensityOffsetCloudy.Value;
                    break;
                case BokuMonoWeather.HeavyRain:
                    dayIntensity += DayIntensityOffsetHeavyRain.Value;
                    break;
                case BokuMonoWeather.Max:
                    dayIntensity += DayIntensityOffsetMax.Value;
                    break;
                default:
                    Log.LogError($"Unknown weather {weather}");
                    break;
            }
            
            if (isIndoor) {
                dayIntensity = Math.Clamp(dayIntensity + IndoorIntensityOffsetDay.Value,
                    BaseNightIntensity.Value, BaseDayIntensity.Value);
                nightIntensity = Math.Clamp(BaseNightIntensity.Value + IndoorIntensityOffsetNight.Value, 
                    BaseNightIntensity.Value, BaseDayIntensity.Value);
            }
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

            switch (state)
            {
                case State.Day:
                    __instance.directionalLight.intensity = dayIntensity;
                    __instance.postProcessSetting.bloomIntensity = DayBloom.Value;
                    break;
                case State.DayToNight:
                    __instance.directionalLight.intensity = 
                        Mathf.Lerp(dayIntensity, nightIntensity, ShapeCurve((currentTime - nightStart) / transitionLength));
                    __instance.postProcessSetting.bloomIntensity =
                        Mathf.Lerp(DayBloom.Value, NightBloom.Value, ShapeCurve((currentTime - nightStart) / transitionLength));
                    break;
                case State.Night:
                    __instance.directionalLight.intensity = nightIntensity;
                    __instance.postProcessSetting.bloomIntensity = NightBloom.Value;
                    break;
                case State.NightToDay:
                    __instance.directionalLight.intensity = 
                        Mathf.Lerp(nightIntensity, dayIntensity, currentTime - nightEnd);
                    __instance.postProcessSetting.bloomIntensity = 
                        Mathf.Lerp(NightBloom.Value, DayBloom.Value, currentTime - nightEnd);
                    break;
                default:
                    Log.LogError($"Unknown state {state}");
                    break;
            }

            //LogOutput(__instance);
        }
    }
}