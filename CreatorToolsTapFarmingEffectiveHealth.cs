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
            spentDuringAttempt = NonNegative(spentDuringAttempt);

            if (string.Equals(phase, "completed",
                    StringComparison.OrdinalIgnoreCase))
            {
                var completedTotal = NonNegative(
                    nativeTotal + reserve + spentDuringAttempt);
                return new CreatorToolsTapFarmingEffectiveHealth(
                    true, 0d, completedTotal, 0d);
            }

            if (nativeTotal <= 0d ||
                (!string.Equals(phase, "active",
                     StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(phase, "transition",
                     StringComparison.OrdinalIgnoreCase)))
                return new CreatorToolsTapFarmingEffectiveHealth(
                    false, reserve, 0d, 0d);

            var total = NonNegative(
                nativeTotal + reserve + spentDuringAttempt);
            var current = NonNegative(nativeCurrent + reserve);
            if (total <= 0d)
                return new CreatorToolsTapFarmingEffectiveHealth(
                    false, reserve, 0d, 0d);

            current = Math.Min(current, total);
            return new CreatorToolsTapFarmingEffectiveHealth(
                true, current, total,
                Math.Max(0d, Math.Min(1d, current / total)));
        }

        private static double NonNegative(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? 0d
                : Math.Max(0d, value);
        }
    }
}
