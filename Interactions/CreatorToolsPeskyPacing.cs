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
        internal bool IntervalScheduled { get; private set; }

        internal bool IntervalReady { get { return IntervalRemaining <= 0f; } }
        internal bool MiniBossReady { get { return !MiniBossPresent && CooldownRemaining <= 0f; } }

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
            int activeCompanions, int maximumCompanions, bool miniBossReserved = false)
        {
            // Common admissions spend only the common clock. A mini-boss
            // never waits for that clock or restarts it on admission.
            return !applyPacing || ((miniBoss || (IntervalReady && !miniBossReserved)) &&
                CanSelect(miniBoss, activeCompanions, maximumCompanions));
        }

        internal void ScheduleInterval(float seconds)
        {
            IntervalRemaining = seconds;
            IntervalScheduled = true;
        }

        internal void ResetInterval()
        {
            IntervalRemaining = 0f;
            IntervalScheduled = false;
        }

        internal void ScaleInterval(float factor)
        {
            IntervalRemaining *= factor;
        }

        internal void Reset()
        {
            MiniBossPresent = false;
            CooldownRemaining = 0f;
            ResetInterval();
        }
    }
}
