using System;

namespace Gilomx.CupheadBossRoulette
{
    // Pure attempt ledger. The controller owns synchronization and native
    // phase detection; neither deposits nor damage can reopen a past phase.
    internal sealed class CreatorToolsTapFarmingPhaseReserve
    {
        internal const double MaximumReserveHealth = 1000000000000d;
        private double[] weights = new double[0];
        private double[] balances = new double[0];
        private int currentPhaseIndex = -1;
        private double remaining;

        internal bool IsInitialized { get { return weights.Length > 0; } }
        internal int PhaseCount { get { return weights.Length; } }
        internal int CurrentPhaseIndex { get { return currentPhaseIndex; } }
        internal double Remaining { get { return remaining; } }
        internal double CurrentRemaining
        {
            get { return GetPhaseRemaining(currentPhaseIndex); }
        }

        internal double GetPhaseRemaining(int index)
        {
            return index < 0 || index >= balances.Length ? 0d : balances[index];
        }

        internal void Clear()
        {
            weights = new double[0];
            balances = new double[0];
            currentPhaseIndex = -1;
            remaining = 0d;
        }

        // A retry supplies the remaining session reserve, rather than the
        // original deposit. Original native phase HP is a fixed weight for
        // this attempt, independent of damage already dealt inside a phase.
        internal void BeginAttempt(double[] nativePhaseHealth, double reserve,
            int phaseIndex = 0)
        {
            var count = nativePhaseHealth == null ? 0 : nativePhaseHealth.Length;
            if (count == 0)
                throw new ArgumentException("Native phase weights are required.",
                    "nativePhaseHealth");

            var nextWeights = new double[count];
            var maximumWeight = 0d;
            for (var i = 0; i < count; i++)
            {
                nextWeights[i] = PositiveFinite(nativePhaseHealth[i]);
                maximumWeight = Math.Max(maximumWeight, nextWeights[i]);
            }
            // Normalize before summing so even extreme valid caller weights
            // cannot overflow the proportional-allocation denominator.
            if (maximumWeight > 0d)
                for (var i = 0; i < count; i++) nextWeights[i] /= maximumWeight;

            weights = nextWeights;
            balances = new double[count];
            currentPhaseIndex = Math.Max(0, Math.Min(count, phaseIndex));
            remaining = Math.Min(MaximumReserveHealth, PositiveFinite(reserve));
            Allocate(remaining);
        }

        // Only the current phase and future phases receive new farmed HP.
        // Return the accepted amount so a caller can share the same cap.
        internal double AddReserve(double amount)
        {
            amount = Math.Min(PositiveFinite(amount),
                Math.Max(0d, MaximumReserveHealth - remaining));
            if (amount <= 0d)
                return 0d;
            remaining += amount;
            Allocate(amount);
            return amount;
        }

        internal double Absorb(double incomingDamage)
        {
            incomingDamage = PositiveFinite(incomingDamage);
            if (currentPhaseIndex < 0 || currentPhaseIndex >= balances.Length ||
                incomingDamage <= 0d)
                return 0d;
            var absorbed = Math.Min(balances[currentPhaseIndex], incomingDamage);
            balances[currentPhaseIndex] = Math.Max(0d,
                balances[currentPhaseIndex] - absorbed);
            remaining = Math.Max(0d, remaining - absorbed);
            return absorbed;
        }

        // Scripted advancement can skip damage gates. Move an unused balance
        // forward without minting/spending HP or making earlier phases live.
        // After the final phase it remains available for a legitimate retry.
        internal bool AdvanceTo(int phaseIndex)
        {
            if (!IsInitialized)
                return false;
            var next = Math.Max(0, Math.Min(weights.Length, phaseIndex));
            if (next <= currentPhaseIndex)
                return false;
            var carry = 0d;
            for (var i = currentPhaseIndex; i < next; i++)
            {
                carry += balances[i];
                balances[i] = 0d;
            }
            currentPhaseIndex = next;
            Allocate(carry);
            return true;
        }

        private void Allocate(double amount)
        {
            if (amount <= 0d)
                return;
            if (currentPhaseIndex < 0 || currentPhaseIndex >= weights.Length)
            {
                // Remaining is the session balance even when no live phase
                // can currently use it. BeginAttempt will assign it on retry.
                return;
            }
            var sum = 0d;
            var last = -1;
            for (var i = currentPhaseIndex; i < weights.Length; i++)
            {
                sum += weights[i];
                if (weights[i] > 0d) last = i;
            }
            var equal = sum <= 0d;
            if (equal)
            {
                sum = weights.Length - currentPhaseIndex;
                last = weights.Length - 1;
            }
            var left = amount;
            for (var i = currentPhaseIndex; i <= last; i++)
            {
                var weight = equal ? 1d : weights[i];
                if (weight <= 0d)
                    continue;
                var share = i == last ? left : Math.Min(left,
                    amount * (weight / sum));
                balances[i] += share;
                left = Math.Max(0d, left - share);
            }
        }

        private static double PositiveFinite(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value <= 0d
                ? 0d : value;
        }
    }
}
