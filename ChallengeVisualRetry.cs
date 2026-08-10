using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const float ChallengeDefeatVisualResetDuration = 0.35f;

        private bool challengeVisualRetryResetPending;
        private int challengeVisualRetryPreviousLevelInstanceId = -1;
        private bool challengeVisualDefeatUnwindActive;
        private bool challengeVisualPauseRestartWaitingForBlack;

        private static void BeginChallengeVisualDefeatUnwindPrefix()
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.BeginChallengeVisualDefeatUnwind();
        }

        private void BeginChallengeVisualDefeatUnwind()
        {
            if (!ShouldShowActiveChallenge())
                return;

            switch (activeChallenge)
            {
                case ModifierId.RgbShift:
                    BeginRgbShiftTransition(
                        0f, 0f, ChallengeDefeatVisualResetDuration);
                    break;
                case ModifierId.UpsideDown:
                    BeginUpsideDownTransition(
                        0f, UpsideDownDefeatReturnDelay,
                        UpsideDownEntryDuration);
                    break;
                case ModifierId.BlackAndWhite:
                    BeginBlackAndWhiteTransition(
                        0f, 0f, ChallengeDefeatVisualResetDuration);
                    break;
                default:
                    return;
            }

            challengeVisualDefeatUnwindActive = true;
            Logger.LogInfo(
                "Challenge render effect is returning to normal after defeat.");
        }

        private void UpdateChallengeVisualDefeatUnwind()
        {
            // Do not run the normal active-fight lifecycle here: Level remains
            // a matching battle until Retry/Exit, which would immediately
            // start the challenge again. Advance only the already-started
            // return transitions and their render bridges.
            AdvanceRgbShiftTransition();
            AdvanceUpsideDownTransition();
            UpdateUpsideDownRenderEffects();
            AdvanceBlackAndWhiteTransition();
            UpdateBlackAndWhiteRenderEffects();
        }

        private static void PrepareChallengeVisualsForRetryPrefix()
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.PrepareChallengeVisualsForRetry();
        }

        private static void PrepareChallengeVisualsForPauseRestartPrefix()
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.PrepareChallengeVisualsForPauseRestart();
        }

        private void PrepareChallengeVisualsForRetry()
        {
            if (activeChallenge == ModifierId.None)
                return;

            CaptureChallengeVisualRestartLevel();
            ResetChallengeVisualsForReload();
        }

        private void PrepareChallengeVisualsForPauseRestart()
        {
            if (activeChallenge == ModifierId.None)
                return;
            try
            {
                // Tower of Power opens a confirmation path instead of
                // reloading from this method; do not arm a future fade for it.
                if (Level.IsTowerOfPower)
                    return;
            }
            catch
            {
            }

            CaptureChallengeVisualRestartLevel();
            challengeVisualPauseRestartWaitingForBlack = true;
            Logger.LogInfo(
                "Holding challenge render effect through pause restart fade.");
        }

        private void CaptureChallengeVisualRestartLevel()
        {

            challengeVisualRetryPreviousLevelInstanceId = -1;
            try
            {
                var level = Level.Current;
                if (level != null)
                {
                    challengeVisualRetryPreviousLevelInstanceId =
                        level.GetInstanceID();
                }
            }
            catch
            {
            }
        }

        private void CompleteChallengeVisualPauseRestartOnFadeInEnd()
        {
            if (!challengeVisualPauseRestartWaitingForBlack)
                return;

            challengeVisualPauseRestartWaitingForBlack = false;
            ResetChallengeVisualsForReload();
            Logger.LogInfo(
                "Cleared challenge render effects behind opaque restart fade.");
        }

        private void ResetChallengeVisualsForReload()
        {

            // Clear every camera-wide challenge before ReloadLevel() starts.
            // Defeat Retry calls this before ReloadLevel(). Pause Restart calls
            // it only after Cuphead's fade has reached full black.
            ResetRgbShiftChallenge();
            ResetUpsideDownChallenge();
            ResetBlackAndWhiteChallengeForRetry();
            challengeVisualDefeatUnwindActive = false;
            challengeVisualRetryResetPending = true;
            Logger.LogInfo(
                "Cleared challenge render effects before restart reload.");
        }

        private bool ShouldHoldChallengeVisualsForRetry()
        {
            if (!challengeVisualRetryResetPending)
                return false;
            if (SceneLoader.CurrentlyLoading)
                return true;

            try
            {
                var level = Level.Current;
                if (level == null ||
                    level.GetInstanceID() ==
                        challengeVisualRetryPreviousLevelInstanceId)
                    return true;

                challengeVisualRetryResetPending = false;
                challengeVisualRetryPreviousLevelInstanceId = -1;
                return false;
            }
            catch
            {
                return true;
            }
        }

        private void ResetBlackAndWhiteChallengeForRetry()
        {
            ResetBlackAndWhiteRenderEffects();
            blackAndWhiteBlend = 0f;
            blackAndWhiteTransitionStartedAt = -1f;
            blackAndWhiteTransitionFrom = 0f;
            blackAndWhiteTransitionTo = 0f;
            blackAndWhiteTransitionDelay = 0f;
            blackAndWhiteTransitionDuration = 0f;
            blackAndWhiteLevelInstanceId = -1;
            blackAndWhiteFadeOutStarted = false;
            blackAndWhiteRenderFailureLogged = false;
        }

        private void ClearChallengeVisualRetryGate()
        {
            challengeVisualDefeatUnwindActive = false;
            challengeVisualPauseRestartWaitingForBlack = false;
            challengeVisualRetryResetPending = false;
            challengeVisualRetryPreviousLevelInstanceId = -1;
        }
    }
}
