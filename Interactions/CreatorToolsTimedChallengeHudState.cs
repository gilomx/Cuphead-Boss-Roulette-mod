using System;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsTimedChallengeHudState
    {
        internal CreatorToolsTimedChallenge DefeatSnapshot { get; private set; }
        internal string DefeatDonor { get; private set; }
        internal bool WaitingForAttempt { get; private set; }
        private int warningCueRevision = -1;
        private int countdownCueRevision = -1;
        private int countdownCueNumber = -1;

        internal void HoldAfterDefeat(CreatorToolsTimedChallenge timer, string donor)
        {
            if (WaitingForAttempt || DefeatSnapshot != null || timer == null || !timer.HudVisible) return;
            DefeatSnapshot = timer.Snapshot();
            DefeatDonor = donor ?? string.Empty;
        }

        internal void Reset(bool waitingForAttempt)
        {
            DefeatSnapshot = null;
            DefeatDonor = string.Empty;
            WaitingForAttempt = waitingForAttempt;
            warningCueRevision = -1;
            countdownCueRevision = countdownCueNumber = -1;
        }

        internal bool TakeWarningCue(
            CreatorToolsTimedChallenge timer, bool playing)
        {
            if (!playing || WaitingForAttempt || DefeatSnapshot != null || timer == null ||
                timer.Phase != TimedChallengePhase.Countdown) return false;
            if (warningCueRevision == timer.Revision) return false;
            warningCueRevision = timer.Revision;
            // The native warning is a continuous sting, played once per
            // challenge alongside the individual countdown clicks.
            return true;
        }

        internal bool TakeCountdownCue(
            CreatorToolsTimedChallenge timer, bool playing)
        {
            if (!playing || WaitingForAttempt || DefeatSnapshot != null ||
                timer == null ||
                timer.Phase != TimedChallengePhase.Countdown)
                return false;
            var number = Math.Max(
                1, (int)Math.Ceiling(timer.CountdownRemaining));
            if (countdownCueRevision == timer.Revision &&
                countdownCueNumber == number)
                return false;
            countdownCueRevision = timer.Revision;
            countdownCueNumber = number;
            return true;
        }
    }
}
