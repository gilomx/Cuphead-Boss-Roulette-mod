namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Bounds panel snapshot work independently of the gameplay frame rate.
    /// Call only on the owning thread, before building the expensive JSON.
    /// Controls may bypass the interval so acknowledgements remain immediate.
    /// </summary>
    internal sealed class CreatorToolsStatePublicationPacing
    {
        internal const double IntervalSeconds = 0.1;
        private bool started;
        private double lastStartedAt;

        internal bool TryBegin(double now, bool urgent)
        {
            if (!urgent && started && now >= lastStartedAt &&
                now - lastStartedAt < IntervalSeconds)
                return false;
            started = true;
            lastStartedAt = now;
            return true;
        }
    }
}
