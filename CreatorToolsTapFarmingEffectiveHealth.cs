using System;

namespace Gilomx.CupheadBossRoulette
{
    internal struct CreatorToolsTapFarmingEffectiveHealth
    {
        internal readonly bool Available;
        internal readonly double Current;
        internal readonly double Total;
        internal readonly double Ratio;

        internal CreatorToolsTapFarmingEffectiveHealth(
            bool available, double current, double total, double ratio)
        {
            Available = available;
            Current = current;
            Total = total;
            Ratio = ratio;
        }

        internal static CreatorToolsTapFarmingEffectiveHealth Calculate(
            string phase,
            double nativeCurrent,
            double nativeTotal,
            double reserve,
            double spentDuringAttempt)
        {
            nativeCurrent = NonNegative(nativeCurrent);
            nativeTotal = NonNegative(nativeTotal);
            reserve = NonNegative(reserve);

            if (string.Equals(phase, "completed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CreatorToolsTapFarmingEffectiveHealth(
                    true, 0d, nativeTotal, 0d);
            }

            if (nativeTotal <= 0d ||
                (!string.Equals(phase, "active",
                     StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(phase, "transition",
                     StringComparison.OrdinalIgnoreCase)))
                return new CreatorToolsTapFarmingEffectiveHealth(
                    false, reserve, 0d, 0d);

            // The original native HP remains the reference for 100%.
            // Deposits and absorbed hits only change the numerator.
            var total = nativeTotal;
            var current = NonNegative(nativeCurrent + reserve);
            if (total <= 0d)
                return new CreatorToolsTapFarmingEffectiveHealth(
                    false, reserve, 0d, 0d);

            return new CreatorToolsTapFarmingEffectiveHealth(
                true, current, total, Math.Max(0d, current / total));
        }

        private static double NonNegative(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? 0d
                : Math.Max(0d, value);
        }
    }
}
