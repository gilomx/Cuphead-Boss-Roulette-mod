namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private TimedInkRainChallenge timedInkRain;
        private bool OwnsTimedInkRain { get { return timedInkRain != null && timedInkRain.OwnsRuntime; } }

        private bool IsTimedInkRainAvailable()
        {
            return ExperimentalFeatures.EnableInkRainChallenge && inkRainRuntime != null &&
                inkRainRuntime.PrepareTemporary();
        }

        private void SyncTimedChallengeEffects(CreatorToolsTimedChallenge timer)
        {
            timedPlaneWeapons.Update(timer);
            SyncTimedHpOneEffect(timer);
            SyncTimedUpsideDownEffect(timer);
            if (timedInkRain != null) timedInkRain.Sync(timer);
        }

        private void AdvanceTimedChallengeEffects(CreatorToolsTimedChallenge timer, float seconds, bool playing)
        {
            if (timedInkRain != null) timedInkRain.Advance(timer, seconds, playing);
        }

        private void UpdateTimedInkRainDefeat()
        {
            if (timedInkRain == null || !timedInkRain.FinishingAfterDefeat) return;
            if (SceneLoader.CurrentlyLoading || Level.Current == null ||
                Level.Current.GetInstanceID() != creatorToolsInteractionLevelInstanceId)
            {
                timedInkRain.CancelVisuals();
                return;
            }
            if (creatorToolsApplicationFocused)
                timedInkRain.AdvanceDefeat(UnityEngine.Time.unscaledDeltaTime);
        }
    }
}
