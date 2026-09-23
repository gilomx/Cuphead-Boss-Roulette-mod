using System;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private ChromaticAberrationFilmGrain timedRgbEffect;
        private BlurGamma timedRgbBlur;
        private float timedRgbNextSearch;
        private bool timedRgbPatchesInstalled;

        private bool IsTimedRgbRendering
        {
            get { return timedChallengeInteractions != null && timedChallengeInteractions.Presentation.RendersRgbShift; }
        }

        private bool OwnsBaseRgbEffect
        {
            get { return rgbShiftLevelInstanceId >= 0 || rgbShiftBlend > 0.001f ||
                (rgbShiftTransitionStartedAt >= 0f && rgbShiftTransitionTo > 0.001f); }
        }

        private void InstallTimedRgbShiftPatches(Harmony harmony)
        {
            var color = AccessTools.Method(typeof(ChromaticAberrationFilmGrain), "OnRenderImage");
            var blur = AccessTools.Method(typeof(BlurGamma), "OnRenderImage");
            if (color == null || blur == null)
            {
                Logger.LogWarning("Timed RGB is unavailable: native image effects were not found.");
                return;
            }
            harmony.Patch(color,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TimedRgbColorPrefix")),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TimedRgbColorFinalizer")));
            harmony.Patch(blur,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TimedRgbBlurPrefix")),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TimedRgbBlurFinalizer")));
            timedRgbPatchesInstalled = true;
        }

        private bool IsTimedRgbAvailable()
        {
            if (!timedRgbPatchesInstalled || !ExperimentalFeatures.EnableRgbShiftChallenge || SceneLoader.CurrentlyLoading) return false;
            if (timedRgbEffect != null && timedRgbBlur != null &&
                timedRgbEffect.isActiveAndEnabled && timedRgbBlur.isActiveAndEnabled) return true;
            if (Time.realtimeSinceStartup < timedRgbNextSearch) return false;
            timedRgbNextSearch = Time.realtimeSinceStartup + 0.2f;
            timedRgbEffect = UnityEngine.Object.FindObjectOfType<ChromaticAberrationFilmGrain>();
            timedRgbBlur = timedRgbEffect == null ? null : timedRgbEffect.GetComponent<BlurGamma>();
            return timedRgbEffect != null && timedRgbBlur != null &&
                timedRgbEffect.isActiveAndEnabled && timedRgbBlur.isActiveAndEnabled;
        }

        private static void TimedRgbColorPrefix(ChromaticAberrationFilmGrain __instance, out TimedRgbShiftRenderFrame.ColorState __state)
        {
            __state = default(TimedRgbShiftRenderFrame.ColorState);
            var plugin = activeInstance;
            if (plugin == null || !plugin.IsTimedRgbRendering || plugin.OwnsBaseRgbEffect || __instance != plugin.timedRgbEffect) return;
            var timer = plugin.timedChallengeInteractions.Presentation;
            __state = TimedRgbShiftRenderFrame.Apply(__instance, timer.RgbShiftBlend, timer.EffectElapsed);
        }

        private static Exception TimedRgbColorFinalizer(ChromaticAberrationFilmGrain __instance, TimedRgbShiftRenderFrame.ColorState __state, Exception __exception)
        {
            TimedRgbShiftRenderFrame.Restore(__instance, __state);
            return __exception;
        }

        private static void TimedRgbBlurPrefix(BlurGamma __instance, out TimedRgbShiftRenderFrame.BlurState __state)
        {
            __state = default(TimedRgbShiftRenderFrame.BlurState);
            var plugin = activeInstance;
            if (plugin == null || !plugin.IsTimedRgbRendering || plugin.OwnsBaseRgbEffect || __instance != plugin.timedRgbBlur) return;
            var timer = plugin.timedChallengeInteractions.Presentation;
            __state = TimedRgbShiftRenderFrame.Apply(__instance, timer.RgbShiftBlend, timer.EffectElapsed);
        }

        private static Exception TimedRgbBlurFinalizer(BlurGamma __instance, TimedRgbShiftRenderFrame.BlurState __state, Exception __exception)
        {
            TimedRgbShiftRenderFrame.Restore(__instance, __state);
            return __exception;
        }
    }
}
