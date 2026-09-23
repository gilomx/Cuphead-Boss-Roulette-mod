using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Gilomx.CupheadBossRoulette
{
    // Unity texture creation must stay on its thread. Bound each loading-screen
    // slice instead of decoding an entire animation in a gameplay availability check.
    internal sealed class IncrementalAssetPreparation : IDisposable
    {
        private IEnumerator<Action> steps;
        private readonly Action<Exception> failed;
        internal bool Settled { get; private set; }
        internal bool Failed { get; private set; }
        internal int CompletedSteps { get; private set; }
        internal double MaximumStepMilliseconds { get; private set; }

        internal IncrementalAssetPreparation(IEnumerator<Action> steps, Action<Exception> failed)
        {
            this.steps = steps;
            this.failed = failed;
        }

        internal void Advance(bool loadingScreen)
        {
            if (!loadingScreen || Settled) return;
            var started = Stopwatch.GetTimestamp();
            try
            {
                for (var count = 0; count < 4; count++)
                {
                    var stepStarted = Stopwatch.GetTimestamp();
                    if (!steps.MoveNext()) { Dispose(); return; }
                    steps.Current();
                    CompletedSteps++;
                    var ended = Stopwatch.GetTimestamp();
                    MaximumStepMilliseconds = Math.Max(MaximumStepMilliseconds,
                        (ended - stepStarted) * 1000.0 / Stopwatch.Frequency);
                    if ((ended - started) * 1000.0 / Stopwatch.Frequency >= 2.0) return;
                }
            }
            catch (Exception exception)
            {
                Failed = true;
                Dispose();
                failed(exception);
            }
        }

        public void Dispose()
        {
            Settled = true;
            if (steps != null) steps.Dispose();
            steps = null;
        }
    }
}
