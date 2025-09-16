using System;
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
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LightControlManager), "Update")]
        public static void Postfix(LightControlManager __instance)
        {
            var currentTime = __instance.CurrentTime;
            var nightStart = __instance.SeasonalTimeSetting.nightStart + NightTimeOffset.Value;
            var nightEnd = __instance.SeasonalTimeSetting.nightEnd;
            var isNight = currentTime >= nightStart || currentTime <= nightEnd;
            var isIndoor = ManagedSingletonMonoBehaviour<FieldManager>.Instance.CurrentFieldMasterData.IsInDoor;

            // Applies IndoorIntensityOffset if isIndoor
            var nightIntensity = isIndoor ?
                Math.Clamp(NightIntensity.Value + IndoorIntensityOffset.Value, NightIntensity.Value, DayIntensity.Value) :
                NightIntensity.Value;

            switch (isNight)
            {
                // Ramp down
                // Math: StartVal - (StartVal - EndVal) * Clamp(time - startTime, 0.0f, lengthHours)
                case true when currentTime < nightStart + 1 && currentTime > nightEnd:
                    __instance.directionalLight.intensity =
                        DayIntensity.Value - (DayIntensity.Value - nightIntensity) *
                        Math.Clamp(currentTime - nightStart, 0.0f, 1.0f);

                    __instance.postProcessSetting.bloomIntensity =
                        DayBloom.Value - (DayBloom.Value - NightBloom.Value) *
                        Math.Clamp(currentTime - nightStart, 0.0f, 1.0f);
                    break;
                // Sustain
                case true:
                    __instance.directionalLight.intensity = nightIntensity;
                    __instance.postProcessSetting.bloomIntensity = NightBloom.Value;
                    break;
                // Ramp up (only seeing rarely right before you pass out)
                case false when currentTime <= 5.0f:
                    __instance.directionalLight.intensity =
                        nightIntensity - (nightIntensity - DayIntensity.Value) *
                        Math.Clamp(currentTime - nightEnd, 0.0f, 1.0f);

                    __instance.postProcessSetting.bloomIntensity =
                        NightBloom.Value - (NightBloom.Value - DayBloom.Value) *
                        Math.Clamp(currentTime - nightEnd, 0.0f, 1.0f);
                    break;
                // Daytime
                default:
                    __instance.directionalLight.intensity = DayIntensity.Value;
                    __instance.postProcessSetting.bloomIntensity = DayBloom.Value;
                    break;
            }
            /*
            Log.LogInfo("Intensity: " + __instance.directionalLight.intensity + " Bloom: " +
                        __instance.postProcessSetting.bloomIntensity +
                        " CurrentTime: " + currentTime + " NightStart: " + nightStart + " NightEnd: " + nightEnd);

            Log.LogInfo("Night: " + isNight + " Indoor: " +
                        ManagedSingletonMonoBehaviour<FieldManager>.Instance.CurrentFieldMasterData.IsInDoor);
            */
        }
    }
}