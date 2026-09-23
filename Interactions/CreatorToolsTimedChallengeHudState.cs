using System;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsTimedChallengeHudState
    {
        internal CreatorToolsTimedChallenge DefeatSnapshot { get; private set; }
        internal string DefeatDonor { get; private set; }
        internal bool WaitingForAttempt { get; private set; }
        private int cueRevision = -1;
        private int cueNumber = -1;

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
            cueRevision = cueNumber = -1;
        }

        internal bool TakeCountdownCue(CreatorToolsTimedChallenge timer, bool playing)
        {
            if (!playing || WaitingForAttempt || DefeatSnapshot != null || timer == null ||
                timer.Phase != TimedChallengePhase.Countdown) return false;
            var number = Math.Max(1, (int)Math.Ceiling(timer.CountdownRemaining));
            if (cueRevision == timer.Revision && cueNumber == number) return false;
            cueRevision = timer.Revision;
            cueNumber = number;
            // One cue for the currently visible number. Slow frames never
            // replay skipped numbers or produce a burst on resume.
            return true;
        }
    }
}
