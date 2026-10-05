namespace Gilomx.CupheadBossRoulette
{
    // Only the first presentation is automatic. Closing the card after this
    // wait leaves reopening to the normal roulette shortcut.
    internal sealed class ChatChoosesCardEntrance
    {
        internal const double DelaySeconds = 1.5;
        private double focusedSince = -1;
        internal bool Pending { get; private set; }

        internal void Begin()
        {
            Pending = true;
            Suspend();
        }

        internal void Suspend() { focusedSince = -1; }

        internal void Cancel()
        {
            Pending = false;
            Suspend();
        }

        internal bool ShouldOpen(bool focused, bool mapAvailable, double now)
        {
            if (!Pending) return false;
            if (!focused || !mapAvailable)
            {
                Suspend();
                return false;
            }
            if (focusedSince < 0) focusedSince = now;
            if (now - focusedSince < DelaySeconds) return false;
            Pending = false;
            return true;
        }
    }
}
