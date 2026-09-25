using System;

namespace Gilomx.CupheadBossRoulette
{
    // Pure per-player bookkeeping for the temporary HP.1 challenge. Unity
    // lifecycle code owns the actual setters; this object only decides what
    // may be restored after the timer or a cooperative revive.
    internal sealed class TimedHpOneHealthState
    {
        private int proposedHealth = -1;
        private int proposedHealthMax = -1;

        internal bool Captured { get; private set; }
        internal bool Defeated { get; private set; }
        internal bool PendingChaliceShield { get; private set; }
        internal int OriginalHealth { get; private set; }
        internal int OriginalHealthMax { get; private set; }
        internal int RemovedHealth { get; private set; }

        internal void ObserveProposedHealth(int value)
        {
            if (!Captured && value > proposedHealth)
                proposedHealth = value;
        }

        internal void ObserveProposedHealthMax(int value)
        {
            if (!Captured && value > proposedHealthMax)
                proposedHealthMax = value;
        }

        internal void Capture(int currentHealth, int currentHealthMax)
        {
            if (Captured)
                return;

            OriginalHealth = Math.Max(0,
                Math.Max(currentHealth, proposedHealth));
            OriginalHealthMax = Math.Max(1,
                Math.Max(currentHealthMax, proposedHealthMax));
            if (OriginalHealth > OriginalHealthMax)
                OriginalHealthMax = OriginalHealth;
            RemovedHealth = Math.Max(0, OriginalHealth - 1);
            Captured = true;
        }

        internal void MarkDefeated()
        {
            Defeated = true;
        }

        internal void SuspendChaliceShield()
        {
            PendingChaliceShield = true;
        }

        internal bool ShouldRefundCurrentHealth(bool alive)
        {
            return Captured && alive && !Defeated;
        }

        internal int RestoredCurrentHealth(int currentHealth)
        {
            return Math.Min(OriginalHealthMax,
                Math.Max(0, currentHealth) + RemovedHealth);
        }

        internal bool ShouldRestoreChaliceShield(bool alive)
        {
            return PendingChaliceShield && alive && !Defeated;
        }
    }
}
