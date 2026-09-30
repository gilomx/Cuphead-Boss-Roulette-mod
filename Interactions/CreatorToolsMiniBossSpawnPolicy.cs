using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    // Accepts a snapshot of every live mini-boss, including native actors.
    // The shared one-at-a-time guard may be disabled explicitly; queue and
    // arena admission rules remain authoritative in either mode.
    internal static class CreatorToolsMiniBossSpawnPolicy
    {
        internal const int Unlimited = 0;
        internal const int MaximumActive = 1;

        internal static int ClampMaximum(int value)
        {
            // Zero is the explicit opt-out. Every legacy value, including the
            // former capacity of two, migrates to the safe default of one.
            return value == Unlimited ? Unlimited : MaximumActive;
        }

        internal static bool CanSpawn(
            string item, IEnumerable<string> activeItems, int maximum)
        {
            if (string.IsNullOrEmpty(item))
                return false;
            if (ClampMaximum(maximum) == Unlimited)
                return true;
            if (activeItems != null)
                foreach (var activeItem in activeItems)
                {
                    if (!string.IsNullOrEmpty(activeItem))
                        return false;
                }
            return true;
        }
    }
}
