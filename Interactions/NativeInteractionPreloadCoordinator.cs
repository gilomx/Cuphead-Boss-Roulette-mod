namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Serializes additive scene loads used only to retain native interaction
    /// prefabs. Unity's async scene queue can stall when more than one load is
    /// held below activation at the same time.
    /// </summary>
    internal static class NativeInteractionPreloadCoordinator
    {
        private static object owner;

        internal static bool TryAcquire(object candidate)
        {
            if (candidate == null)
                return false;
            if (owner == null)
                owner = candidate;
            return ReferenceEquals(owner, candidate);
        }

        internal static void Release(object candidate)
        {
            if (ReferenceEquals(owner, candidate))
                owner = null;
        }
    }
}
