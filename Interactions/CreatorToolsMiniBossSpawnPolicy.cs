using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    // Accepts a snapshot of every live mini-boss, including native actors.
    // All mini-boss types share one slot, regardless of their source queue.
    internal static class CreatorToolsMiniBossSpawnPolicy
    {
        internal const int MaximumActive = 1;

        internal static int ClampMaximum(int value)
        {
            // Normalize settings from the former configurable implementation.
            return MaximumActive;
        }

        internal static bool CanSpawn(
            string item, IEnumerable<string> activeItems, int maximum)
        {
            if (string.IsNullOrEmpty(item))
                return false;
            // Keep the legacy parameter for existing callers, but no saved
            // setting or old panel may admit a second mini-boss.
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
