using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const float RgbShiftAmount = 32f;
        private const float RgbShiftSpeed = 10f;
        private const float RgbShiftHorizontalSpeedRatio = 0.73f;
        private const float RgbShiftHorizontalScale = 0.7f;
        private const float RgbShiftRedScale = 1.2f;
        private const float RgbShiftGreenScale = 0.6f;
        private const float RgbShiftBlueScale = 0.9f;
        private const float RgbBlurStrength = 0.7f;
        private const float RgbBlurInitialOffset = 1f;
        private const float RgbBlurRiseDuration = 0.6f;
        private const float RgbBlurPeakOffset = 1.6f;
        private const float RgbBlurPulseDuration = 2.2f;

        private ChromaticAberrationFilmGrain rgbShiftEffect;
        private BlurGamma rgbShiftBlur;
        private Vector2 rgbShiftOriginalRed;
        private Vector2 rgbShiftOriginalGreen;
        private Vector2 rgbShiftOriginalBlue;
        private float rgbShiftOriginalBlurSize;
        private bool rgbShiftOriginalCaptured;
        private bool rgbShiftBlurOriginalCaptured;
        private int rgbShiftLevelInstanceId = -1;
        private bool rgbShiftFadeOutStarted;
        private float rgbShiftBlend;
        private float rgbShiftPhaseTime;
        private float rgbShiftBlurPulseTime;
        private float rgbShiftTransitionStartedAt = -1f;
        private float rgbShiftTransitionFrom;
        private float rgbShiftTransitionTo;
        private float rgbShiftTransitionDelay;
        private float rgbShiftTransitionDuration;

        private void UpdateRgbShiftTransition()
        {
            if (!ExperimentalFeatures.EnableRgbShiftChallenge)
            {
                ResetRgbShiftChallenge();
                return;
            }

            var challengeSelected = activeChallenge == ModifierId.RgbShift;
            var activeFight = false;
            var levelInstanceId = -1;

            if (challengeSelected && !SceneLoader.CurrentlyLoading)
            {
                try
                {
                    var level = Level.Current;
                    activeFight = level != null &&
                                  level.LevelType == Level.Type.Battle &&
                                  ActiveChallengeMatches(level);
                    if (activeFight)
                        levelInstanceId = level.GetInstanceID();
                }
                catch
                {
                    activeFight = false;
                }
            }

            if (activeFight)
            {
                if (rgbShiftLevelInstanceId != levelInstanceId)
                {
                    RestoreRgbShiftOriginalValues();
                    rgbShiftLevelInstanceId = levelInstanceId;
                    rgbShiftFadeOutStarted = false;
                    rgbShiftBlend = 0f;
                    rgbShiftPhaseTime = 0f;
                    rgbShiftBlurPulseTime = 0f;
                    TryAcquireRgbShiftEffect();
                    BeginRgbShiftTransition(
                        1f, BlackAndWhiteEntryDelay,
                        BlackAndWhiteFadeInDuration);
                }
            }
            else if (!(challengeSelected && SceneLoader.CurrentlyLoading))
            {
                rgbShiftLevelInstanceId = -1;
                var fadingIn = rgbShiftTransitionStartedAt >= 0f &&
                               rgbShiftTransitionTo > 0.001f;
                if (!rgbShiftFadeOutStarted &&
                    (rgbShiftBlend > 0.001f || fadingIn))
                {
                    rgbShiftFadeOutStarted = true;
                    BeginRgbShiftTransition(
                        0f, 0f, BlackAndWhiteFadeOutDuration);
                }
            }

            AdvanceRgbShiftTransition();
        }

        private void ApplyRgbShiftEffectLate()
        {
            var entering = rgbShiftTransitionStartedAt >= 0f &&
                           rgbShiftTransitionTo > 0.001f;
            var shouldOwnEffect = rgbShiftLevelInstanceId >= 0 ||
                                  rgbShiftBlend > 0.001f || entering;
            if (!shouldOwnEffect)
            {
                RestoreRgbShiftOriginalValues();
                return;
            }

            if (!TryAcquireRgbShiftEffect() || rgbShiftEffect == null)
                return;

            // During the accepted 1.5-second normal opening, leave Cuphead's
            // native camera values untouched. The transition starts at zero.
            if (rgbShiftBlend <= 0.001f)
                return;

            rgbShiftPhaseTime += Time.deltaTime;
            rgbShiftBlurPulseTime = Mathf.Repeat(
                rgbShiftBlurPulseTime + Time.deltaTime,
                RgbBlurPulseDuration);
            var verticalPhase = Mathf.Sin(
                rgbShiftPhaseTime * RgbShiftSpeed);
            var horizontalPhase = Mathf.Sin(
                rgbShiftPhaseTime * RgbShiftSpeed *
                RgbShiftHorizontalSpeedRatio + Mathf.PI * 0.5f);
            var baseOffset = new Vector2(
                horizontalPhase * RgbShiftAmount *
                RgbShiftHorizontalScale,
                verticalPhase * RgbShiftAmount);
            var red = baseOffset * RgbShiftRedScale;
            var green = baseOffset * RgbShiftGreenScale;
            var blue = -baseOffset * RgbShiftBlueScale;
            var blend = Mathf.Clamp01(rgbShiftBlend);

            // LateUpdate runs after Cuphead's pollen coroutine, so Cagney's
            // own hit effect cannot make the permanent challenge flicker.
            rgbShiftEffect.r = Vector2.Lerp(rgbShiftOriginalRed, red, blend);
            rgbShiftEffect.g = Vector2.Lerp(rgbShiftOriginalGreen, green, blend);
            rgbShiftEffect.b = Vector2.Lerp(rgbShiftOriginalBlue, blue, blend);

            if (rgbShiftBlur != null && rgbShiftBlurOriginalCaptured)
            {
                var blurOffset = NativeCagneyBlurOffset(
                    rgbShiftBlurPulseTime);
                rgbShiftBlur.blurSize = Mathf.Lerp(
                    rgbShiftOriginalBlurSize,
                    rgbShiftOriginalBlurSize +
                    blurOffset * RgbBlurStrength,
                    blend);
            }
        }

        private static float NativeCagneyBlurOffset(float pulseTime)
        {
            // TouchFuzzy immediately adds 1, rises at one unit per second for
            // 0.6 seconds, then falls at the same rate back to the baseline.
            if (pulseTime < RgbBlurRiseDuration)
                return RgbBlurInitialOffset + pulseTime;

            return Mathf.Max(0f, RgbBlurPeakOffset -
                (pulseTime - RgbBlurRiseDuration));
        }

        private static bool SuppressCagneyFuzzyDuringRgbPrefix()
        {
            var plugin = activeInstance;
            return plugin == null ||
                   !plugin.ShouldSuppressCagneyFuzzyDuringRgb();
        }

        private bool ShouldSuppressCagneyFuzzyDuringRgb()
        {
            if (activeChallenge != ModifierId.RgbShift ||
                activeChallengeBoss < 0 ||
                activeChallengeBoss >= RouletteData.Bosses.Length ||
                RouletteData.Bosses[activeChallengeBoss].Level !=
                    Levels.Flower)
                return false;

            return ShouldShowActiveChallenge();
        }

        private bool TryAcquireRgbShiftEffect()
        {
            if (rgbShiftEffect == null)
            {
                rgbShiftOriginalCaptured = false;
                rgbShiftEffect = UnityEngine.Object.FindObjectOfType<
                    ChromaticAberrationFilmGrain>();
            }

            if (rgbShiftEffect == null)
                return false;

            if (!rgbShiftOriginalCaptured)
            {
                rgbShiftOriginalRed = rgbShiftEffect.r;
                rgbShiftOriginalGreen = rgbShiftEffect.g;
                rgbShiftOriginalBlue = rgbShiftEffect.b;
                rgbShiftOriginalCaptured = true;
            }

            if (rgbShiftBlur == null)
            {
                rgbShiftBlurOriginalCaptured = false;
                rgbShiftBlur = rgbShiftEffect.GetComponent<BlurGamma>();
                if (rgbShiftBlur == null)
                    rgbShiftBlur = UnityEngine.Object.FindObjectOfType<
                        BlurGamma>();
            }

            if (rgbShiftBlur != null && !rgbShiftBlurOriginalCaptured)
            {
                rgbShiftOriginalBlurSize = rgbShiftBlur.blurSize;
                rgbShiftBlurOriginalCaptured = true;
            }

            return true;
        }

        private void BeginRgbShiftTransition(
            float target, float delay, float duration)
        {
            rgbShiftTransitionStartedAt = Time.realtimeSinceStartup;
            rgbShiftTransitionFrom = rgbShiftBlend;
            rgbShiftTransitionTo = Mathf.Clamp01(target);
            rgbShiftTransitionDelay = Mathf.Max(0f, delay);
            rgbShiftTransitionDuration = Mathf.Max(0.001f, duration);
        }

        private void AdvanceRgbShiftTransition()
        {
            if (rgbShiftTransitionStartedAt < 0f)
                return;

            var elapsed = Time.realtimeSinceStartup -
                          rgbShiftTransitionStartedAt -
                          rgbShiftTransitionDelay;
            if (elapsed <= 0f)
            {
                rgbShiftBlend = rgbShiftTransitionFrom;
                return;
            }

            var progress = Mathf.Clamp01(
                elapsed / rgbShiftTransitionDuration);
            var smoothProgress = progress * progress *
                                 (3f - 2f * progress);
            rgbShiftBlend = Mathf.Lerp(
                rgbShiftTransitionFrom,
                rgbShiftTransitionTo,
                smoothProgress);
            if (progress < 1f)
                return;

            rgbShiftBlend = rgbShiftTransitionTo;
            rgbShiftTransitionStartedAt = -1f;
            if (rgbShiftBlend <= 0.001f)
                RestoreRgbShiftOriginalValues();
        }

        private void RestoreRgbShiftOriginalValues()
        {
            if (rgbShiftEffect != null && rgbShiftOriginalCaptured)
            {
                rgbShiftEffect.r = rgbShiftOriginalRed;
                rgbShiftEffect.g = rgbShiftOriginalGreen;
                rgbShiftEffect.b = rgbShiftOriginalBlue;
            }

            if (rgbShiftBlur != null && rgbShiftBlurOriginalCaptured)
                rgbShiftBlur.blurSize = rgbShiftOriginalBlurSize;

            rgbShiftEffect = null;
            rgbShiftBlur = null;
            rgbShiftOriginalCaptured = false;
            rgbShiftBlurOriginalCaptured = false;
        }

        private void ResetRgbShiftChallenge()
        {
            RestoreRgbShiftOriginalValues();
            rgbShiftLevelInstanceId = -1;
            rgbShiftFadeOutStarted = false;
            rgbShiftBlend = 0f;
            rgbShiftPhaseTime = 0f;
            rgbShiftBlurPulseTime = 0f;
            rgbShiftTransitionStartedAt = -1f;
            rgbShiftTransitionFrom = 0f;
            rgbShiftTransitionTo = 0f;
            rgbShiftTransitionDelay = 0f;
            rgbShiftTransitionDuration = 0f;
        }
    }
}
