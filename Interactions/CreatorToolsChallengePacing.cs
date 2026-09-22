using System;
using System.Globalization;

namespace Gilomx.CupheadBossRoulette
{
    // Challenges have their own rest clock, independent of actor intervals,
    // mini-boss reservations and the warning/effect clocks.
    internal sealed class CreatorToolsChallengePacing
    {
        internal const int DefaultWait = 5;
        internal const int MaximumWait = 300;
        internal float Remaining { get; private set; } = DefaultWait;
        private bool wasBusy;

        internal bool Ready { get { return Remaining <= 0f && !wasBusy; } }

        internal void Reset(int wait)
        {
            Remaining = wait;
            wasBusy = false;
        }

        internal void Advance(float seconds, bool playing, bool busy, int wait)
        {
            if (busy || wasBusy)
            {
                Remaining = wait;
                wasBusy = busy;
                return;
            }
            if (playing && seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds))
                Remaining = Math.Max(0f, Remaining - seconds);
        }

        internal static bool TryWait(string value, out int seconds)
        {
            seconds = DefaultWait;
            return string.IsNullOrEmpty(value) ||
                (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) &&
                 seconds >= 0 && seconds <= MaximumWait);
        }
    }
}
