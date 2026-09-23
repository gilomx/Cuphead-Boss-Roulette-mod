using System;
using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private bool timedVisualPatchesPrepared;

        private void InstallTimedVisualPreparationPatch()
        {
            var load = AccessTools.Method(typeof(SceneLoader), "load_cr");
            if (load == null)
            {
                Logger.LogWarning("Timed visual assets unavailable: native loading hook was not found.");
                return;
            }
            harmony.Patch(load, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(Plugin), "TimedVisualPreparationPostfix")));
        }

        private static void TimedVisualPreparationPostfix(ref IEnumerator __result, ref bool ___doneLoadingSceneAsync,
            UnityEngine.UI.Image ___icon)
        {
            var plugin = activeInstance;
            if (plugin == null) return;
            plugin.assetLoadingClock = ___icon;
            if (!(SceneLoader.SceneName ?? string.Empty).StartsWith("scene_level_", StringComparison.OrdinalIgnoreCase)) return;
            // load_cr starts after the native fade covers the screen. Do not
            // let the previous load's completion flag reveal this scene early.
            ___doneLoadingSceneAsync = false;
            __result = plugin.PrepareTimedVisualsBeforeLevel(__result);
        }

        private IEnumerator PrepareTimedVisualsBeforeLevel(IEnumerator nativeLoad)
        {
            var started = Stopwatch.StartNew();
            // Manual equipment activates in LevelInit, after this loading
            // barrier, so read its saved selection as well as roulette state.
            var includeSquid = NeedsEquippedInkIntro();
            try
            {
                preparingTimedVisualAssets = !timedVisualPatchesPrepared ||
                    (inkRainRuntime != null && inkRainRuntime.NeedsAssetPreparation(includeSquid));
                if (!timedVisualPatchesPrepared)
                {
                    timedVisualPatchesPrepared = true;
                    // Installing these optional image hooks also has a one-time
                    // cost. Pay it under the level fade, not at game startup.
                    try { InstallTimedRgbShiftPatches(harmony); }
                    catch (Exception exception)
                    {
                        Logger.LogWarning("Timed RGB hooks could not be prepared: " + exception.Message);
                    }
                    Logger.LogInfo("Timed visual hooks prepared under loading fade in " + started.ElapsedMilliseconds + " ms.");
                    yield return null;
                }
                if (inkRainRuntime != null)
                {
                    while (inkRainRuntime.NeedsAssetPreparation(includeSquid) && SceneLoader.CurrentlyLoading)
                    {
                        inkRainRuntime.PrepareAssetsDuringLoading(includeSquid);
                        yield return null;
                    }
                }
                preparingTimedVisualAssets = false;
                while (nativeLoad.MoveNext()) yield return nativeLoad.Current;
            }
            finally
            {
                preparingTimedVisualAssets = false;
                var disposable = nativeLoad as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
        }

        private bool NeedsEquippedInkIntro()
        {
            // A roulette result takes precedence, even when its challenge is
            // disabled. A stale manual session must not override a new choice.
            if (loanedLoadoutsActive || (activeChallenge != ModifierId.None && !activeChallengeFromManualEquipment))
                return activeChallenge == ModifierId.InkRain;
            return GetEquippedManualChallenge() == ModifierId.InkRain;
        }
    }
}
