using System;
using System.Globalization;

namespace Gilomx.CupheadBossRoulette
{
    internal enum TimedChallengePhase { Idle, Countdown, CountdownExit, Active, Exit }
    // Receives gameplay time only. A completed lease cannot cancel its successor.
    internal sealed class CreatorToolsTimedChallenge
    {
        internal const string HalfDamage = "challenge_half_damage";
        internal const int DefaultDuration = 15;
        internal const int MaximumDuration = 120;
        internal const int DefaultCountdown = 3;
        internal const int MaximumCountdown = 30;
        internal const float ExitSeconds = 0.25f;
        internal TimedChallengePhase Phase { get; private set; }
        internal float PhaseElapsed { get; private set; }
        internal float CountdownRemaining { get; private set; }
        internal float Remaining { get; private set; }
        internal bool Active { get { return Phase == TimedChallengePhase.Active; } }
        internal bool Busy { get { return Phase != TimedChallengePhase.Idle; } }
        internal bool CountingDown { get { return Phase == TimedChallengePhase.Countdown || Phase == TimedChallengePhase.CountdownExit; } }
        internal int Revision { get; private set; }

        internal static bool TryDuration(string token, out int duration)
        {
            duration = DefaultDuration;
            return string.IsNullOrEmpty(token) ||
                (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out duration) && duration >= 1 && duration <= MaximumDuration);
        }

        internal static bool TryCountdown(string token, out int seconds)
        {
            seconds = DefaultCountdown;
            return string.IsNullOrEmpty(token) ||
                (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out seconds) && seconds >= 0 && seconds <= MaximumCountdown);
        }

        internal bool Start(int duration, int countdown = DefaultCountdown)
        {
            if (Busy || duration < 1 || duration > MaximumDuration || countdown < 0 || countdown > MaximumCountdown)
                return false;
            Revision++;
            Remaining = duration;
            CountdownRemaining = countdown;
            Enter(countdown > 0 ? TimedChallengePhase.Countdown : TimedChallengePhase.Active);
            return true;
        }

        internal void Advance(float seconds, bool playing)
        {
            if (!playing || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                return;
            // Carry overshoot through every boundary: frame rate never changes
            // warning length, the full effect duration, or either exit animation.
            while (Busy && seconds > 0f)
            {
                var left = Phase == TimedChallengePhase.Countdown ? CountdownRemaining
                    : Active ? Remaining : ExitSeconds - PhaseElapsed;
                var step = Math.Min(seconds, Math.Max(0f, left));
                PhaseElapsed += step;
                seconds -= step;
                if (Phase == TimedChallengePhase.Countdown) CountdownRemaining = Math.Max(0f, CountdownRemaining - step);
                else if (Active) Remaining = Math.Max(0f, Remaining - step);
                if (step < left) break;
                if (Phase == TimedChallengePhase.Countdown) Enter(TimedChallengePhase.CountdownExit);
                else if (Phase == TimedChallengePhase.CountdownExit) Enter(TimedChallengePhase.Active);
                else if (Active) Enter(TimedChallengePhase.Exit);
                else End();
            }
        }

        private void Enter(TimedChallengePhase phase) { Phase = phase; PhaseElapsed = 0f; }
        // Display-only copy: ending the gameplay lease must not erase the
        // time shown on the defeat screen. The HUD never advances this copy.
        internal CreatorToolsTimedChallenge Snapshot() { return (CreatorToolsTimedChallenge)MemberwiseClone(); }
        internal void End() { Remaining = 0f; CountdownRemaining = 0f; Enter(TimedChallengePhase.Idle); }
        internal void End(int revision) { if (revision == Revision) End(); }
    }
}
