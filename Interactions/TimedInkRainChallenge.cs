namespace Gilomx.CupheadBossRoulette
{
    internal interface ITimedInkRainRuntime
    {
        bool HasTemporaryVisuals { get; }
        void StartTemporary();
        void StopTemporaryEmission();
        void BeginTemporaryDefeat();
        void AdvanceTemporary(float seconds);
        void CancelTemporary();
    }

    // Owns only a temporary session. The roulette challenge keeps its own
    // lifecycle, and earned requests keep their reservation through the drain.
    internal sealed class TimedInkRainChallenge
    {
        private readonly ITimedInkRainRuntime runtime;
        internal bool OwnsRuntime { get; private set; }
        internal bool FinishingAfterDefeat { get; private set; }

        internal TimedInkRainChallenge(ITimedInkRainRuntime runtime) { this.runtime = runtime; }

        internal void Sync(CreatorToolsTimedChallenge timer)
        {
            // The gameplay lease ends on death, but its visuals finish behind
            // the defeat menu. Explicit retry/exit still cancels them.
            if (FinishingAfterDefeat) return;
            if (timer.Item != CreatorToolsTimedChallenge.InkRain || !timer.Busy)
            {
                if (OwnsRuntime) runtime.CancelTemporary();
                OwnsRuntime = false;
                return;
            }
            if (timer.CountingDown) return;
            if (!OwnsRuntime)
            {
                runtime.StartTemporary();
                OwnsRuntime = true;
            }
            if (!timer.Active) runtime.StopTemporaryEmission();
        }

        internal void Advance(CreatorToolsTimedChallenge timer, float seconds, bool playing)
        {
            if (!OwnsRuntime || !playing) return;
            runtime.AdvanceTemporary(seconds);
            if (!timer.Active && !runtime.HasTemporaryVisuals) timer.CompleteInkRainDrain();
        }

        internal void PreserveAfterDefeat()
        {
            if (!OwnsRuntime || FinishingAfterDefeat) return;
            FinishingAfterDefeat = true;
            runtime.BeginTemporaryDefeat();
        }

        internal void AdvanceDefeat(float seconds)
        {
            if (!FinishingAfterDefeat) return;
            runtime.AdvanceTemporary(seconds);
            if (!runtime.HasTemporaryVisuals) CancelVisuals();
        }

        internal void CancelVisuals()
        {
            if (OwnsRuntime) runtime.CancelTemporary();
            OwnsRuntime = false;
            FinishingAfterDefeat = false;
        }
    }
}
