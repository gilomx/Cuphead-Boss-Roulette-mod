using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const float UpsideDownEntryDelay = 0.25f;
        private const float UpsideDownEntryDuration = 0.45f;
        private const float UpsideDownDefeatReturnDelay = 1f;
        private const float UpsideDownVictoryReturnDelay = 1f;
        private const float UpsideDownTurnSoundVolume = 1f;

        private readonly List<FlatRotationRenderEffect>
            upsideDownRenderEffects =
                new List<FlatRotationRenderEffect>();
        private float upsideDownBlend;
        private float upsideDownTransitionStartedAt = -1f;
        private float upsideDownTransitionFrom;
        private float upsideDownTransitionTo;
        private float upsideDownTransitionDelay;
        private float upsideDownTransitionDuration;
        private int upsideDownLevelInstanceId = -1;
        private Levels upsideDownCurrentLevel;
        private bool upsideDownHasCurrentLevel;
        private bool upsideDownFadeOutStarted;
        private float nextUpsideDownEffectScanAt;
        private bool upsideDownRenderFailureLogged;
        private bool upsideDownTurnSoundPending;

        private void UpdateUpsideDownTransition()
        {
            if (!ExperimentalFeatures.EnableUpsideDownChallenge)
            {
                ResetUpsideDownChallenge();
                return;
            }

            var challengeSelected =
                activeChallenge == ModifierId.UpsideDown;
            var activeFight = false;
            var levelInstanceId = -1;
            var currentLevel = default(Levels);

            if (challengeSelected && !SceneLoader.CurrentlyLoading)
            {
                try
                {
                    var level = Level.Current;
                    activeFight = level != null &&
                                  level.LevelType == Level.Type.Battle &&
                                  ActiveChallengeMatches(level);
                    if (activeFight)
                    {
                        levelInstanceId = level.GetInstanceID();
                        currentLevel = level.CurrentLevel;
                    }
                }
                catch
                {
                    activeFight = false;
                }
            }

            if (activeFight &&
                upsideDownLevelInstanceId != levelInstanceId)
            {
                var continuesDiceChain =
                    IsActiveDicePalaceChallenge() &&
                    upsideDownHasCurrentLevel &&
                    upsideDownCurrentLevel != currentLevel &&
                    upsideDownBlend >= 0.999f;

                DisposeUpsideDownRenderEffects();
                upsideDownLevelInstanceId = levelInstanceId;
                upsideDownCurrentLevel = currentLevel;
                upsideDownHasCurrentLevel = true;
                upsideDownFadeOutStarted = false;

                if (continuesDiceChain)
                {
                    // Internal Dice Palace scenes are one attempt. Preserve
                    // the completed rotation and attach it to the new camera
                    // without replaying the opening animation.
                    upsideDownBlend = 1f;
                    upsideDownTransitionStartedAt = -1f;
                }
                else
                {
                    // First entry and retries begin with the normal frame.
                    upsideDownBlend = 0f;
                    BeginUpsideDownTransition(
                        1f, UpsideDownEntryDelay,
                        UpsideDownEntryDuration);
                }
            }
            else if (!activeFight &&
                     !(challengeSelected && SceneLoader.CurrentlyLoading))
            {
                upsideDownLevelInstanceId = -1;
                upsideDownHasCurrentLevel = false;
                var fadingIn = upsideDownTransitionStartedAt >= 0f &&
                               upsideDownTransitionTo > 0.001f;
                if (!upsideDownFadeOutStarted &&
                    (upsideDownBlend > 0.001f || fadingIn))
                {
                    upsideDownFadeOutStarted = true;
                    BeginUpsideDownTransition(
                        0f, 0f, BlackAndWhiteFadeOutDuration);
                }
            }

            AdvanceUpsideDownTransition();
        }

        private void UpdateUpsideDownRenderEffects()
        {
            var shouldRun = upsideDownLevelInstanceId >= 0 ||
                            upsideDownBlend > 0.001f ||
                            upsideDownTransitionStartedAt >= 0f;

            for (var i = upsideDownRenderEffects.Count - 1; i >= 0; i--)
            {
                var effect = upsideDownRenderEffects[i];
                if (!effect.IsValid)
                {
                    effect.Dispose();
                    upsideDownRenderEffects.RemoveAt(i);
                    continue;
                }
                effect.SetAngle(upsideDownBlend * 180f);
            }

            if (!shouldRun)
            {
                DisposeUpsideDownRenderEffects();
                return;
            }
            if (Time.realtimeSinceStartup < nextUpsideDownEffectScanAt)
                return;

            nextUpsideDownEffectScanAt =
                Time.realtimeSinceStartup + 0.2f;
            var blurEffects = FindObjectsOfType<BlurGamma>();
            for (var i = 0; i < blurEffects.Length; i++)
            {
                var blurEffect = blurEffects[i];
                if (blurEffect == null ||
                    HasUpsideDownEffect(blurEffect))
                    continue;

                FlatRotationRenderEffect effect;
                string error;
                if (!FlatRotationRenderEffect.TryCreate(
                    blurEffect, blackAndWhiteTransitionShader,
                    out effect, out error))
                {
                    if (!string.IsNullOrEmpty(error) &&
                        !upsideDownRenderFailureLogged)
                    {
                        upsideDownRenderFailureLogged = true;
                        Logger.LogWarning(
                            "Upside-down render bridge is waiting: " +
                            error);
                    }
                    continue;
                }

                effect.SetAngle(upsideDownBlend * 180f);
                upsideDownRenderEffects.Add(effect);
                upsideDownRenderFailureLogged = false;
                Logger.LogInfo(
                    "Attached flat rotation to camera " +
                    blurEffect.gameObject.name + ".");
            }
        }

        private bool HasUpsideDownEffect(BlurGamma blurEffect)
        {
            for (var i = 0; i < upsideDownRenderEffects.Count; i++)
            {
                if (upsideDownRenderEffects[i].Matches(blurEffect))
                    return true;
            }
            return false;
        }

        private void BeginUpsideDownTransition(
            float target, float delay, float duration)
        {
            upsideDownTransitionStartedAt = Time.realtimeSinceStartup;
            upsideDownTransitionFrom = upsideDownBlend;
            upsideDownTransitionTo = Mathf.Clamp01(target);
            upsideDownTransitionDelay = Mathf.Max(0f, delay);
            upsideDownTransitionDuration = Mathf.Max(0.001f, duration);
            upsideDownTurnSoundPending =
                Mathf.Abs(upsideDownTransitionTo -
                          upsideDownTransitionFrom) > 0.001f;
        }

        private void BeginUpsideDownVictoryReturn()
        {
            if (activeChallenge != ModifierId.UpsideDown ||
                upsideDownBlend <= 0.001f)
                return;

            // _OnPreWin clears the active challenge immediately afterward.
            // Mark the ordinary lifecycle exit as already owned so it cannot
            // overwrite this delayed, fast K.O. return on the next Update().
            upsideDownFadeOutStarted = true;
            BeginUpsideDownTransition(
                0f, UpsideDownVictoryReturnDelay,
                UpsideDownEntryDuration);
        }

        private void AdvanceUpsideDownTransition()
        {
            if (upsideDownTransitionStartedAt < 0f)
                return;

            if (upsideDownTransitionTo > 0.999f &&
                upsideDownRenderEffects.Count == 0 &&
                Time.realtimeSinceStartup -
                    upsideDownTransitionStartedAt >
                    upsideDownTransitionDelay)
            {
                upsideDownTransitionStartedAt =
                    Time.realtimeSinceStartup -
                    upsideDownTransitionDelay;
                upsideDownBlend = upsideDownTransitionFrom;
                return;
            }

            var elapsed = Time.realtimeSinceStartup -
                          upsideDownTransitionStartedAt -
                          upsideDownTransitionDelay;
            if (elapsed <= 0f)
            {
                upsideDownBlend = upsideDownTransitionFrom;
                return;
            }

            if (upsideDownTurnSoundPending)
            {
                upsideDownTurnSoundPending = false;
                PlayOneShot(
                    upsideDownTurnClip,
                    UpsideDownTurnSoundVolume);
            }

            var progress = Mathf.Clamp01(
                elapsed / upsideDownTransitionDuration);
            var smoothProgress = progress * progress *
                                 (3f - 2f * progress);
            upsideDownBlend = Mathf.Lerp(
                upsideDownTransitionFrom,
                upsideDownTransitionTo,
                smoothProgress);
            if (progress < 1f)
                return;

            upsideDownBlend = upsideDownTransitionTo;
            upsideDownTransitionStartedAt = -1f;
            if (upsideDownBlend <= 0.001f)
                DisposeUpsideDownRenderEffects();
        }

        private void DisposeUpsideDownRenderEffects()
        {
            for (var i = upsideDownRenderEffects.Count - 1; i >= 0; i--)
                upsideDownRenderEffects[i].Dispose();
            upsideDownRenderEffects.Clear();
            nextUpsideDownEffectScanAt = 0f;
        }

        private void ResetUpsideDownChallenge()
        {
            DisposeUpsideDownRenderEffects();
            upsideDownBlend = 0f;
            upsideDownTransitionStartedAt = -1f;
            upsideDownTransitionFrom = 0f;
            upsideDownTransitionTo = 0f;
            upsideDownTransitionDelay = 0f;
            upsideDownTransitionDuration = 0f;
            upsideDownLevelInstanceId = -1;
            upsideDownHasCurrentLevel = false;
            upsideDownFadeOutStarted = false;
            upsideDownRenderFailureLogged = false;
            upsideDownTurnSoundPending = false;
        }
    }
}
