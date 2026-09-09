using System;

namespace Gilomx.CupheadBossRoulette
{
    // Receives gameplay time only: pause/loading cannot spend the recovery gap.
    // Automatic Pesky entries always use the mini-boss policy. Regular earned
    // entries opt in explicitly; their caller retains them while they wait.
    internal sealed class CreatorToolsPeskyPacing
    {
        internal bool MiniBossPresent { get; private set; }
        internal float CooldownRemaining { get; private set; }
        internal float IntervalRemaining { get; private set; }

        internal bool IntervalReady { get { return IntervalRemaining <= 0f; } }

        internal bool Advance(float gameplaySeconds, bool miniBossPresent,
            float cooldownSeconds)
        {
            CooldownRemaining = Math.Max(0f, CooldownRemaining - gameplaySeconds);
            IntervalRemaining = Math.Max(0f, IntervalRemaining - gameplaySeconds);
            var changed = MiniBossPresent != miniBossPresent;
            if (MiniBossPresent && !miniBossPresent)
                CooldownRemaining = cooldownSeconds;
            MiniBossPresent = miniBossPresent;
            return changed;
        }

        internal bool CanSelect(bool miniBoss, int activeCompanions, int maximumCompanions)
        {
            if (miniBoss)
                return !MiniBossPresent && CooldownRemaining <= 0f &&
                    activeCompanions <= maximumCompanions;
            return !MiniBossPresent || activeCompanions < maximumCompanions;
        }

        internal bool CanDispatchInteraction(bool applyPacing, bool miniBoss,
            int activeCompanions, int maximumCompanions)
        {
            return !applyPacing || (IntervalReady &&
                CanSelect(miniBoss, activeCompanions, maximumCompanions));
        }

        internal void ScheduleInterval(float seconds)
        {
            IntervalRemaining = seconds;
        }

        internal void Reset()
        {
            MiniBossPresent = false;
            CooldownRemaining = 0f;
            IntervalRemaining = 0f;
        }
    }
}
