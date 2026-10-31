using System;
using System.Collections.Generic;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal static class ControllerTriggerLabels
    {
        private const int MaximumCachedLabels = 256;
        private const int Left = 1;
        private const int Right = 2;
        private static readonly Dictionary<string, int> labels =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal static bool IsLeft(string label) { return (Classify(label) & Left) != 0; }
        internal static bool IsRight(string label) { return (Classify(label) & Right) != 0; }

        private static int Classify(string label)
        {
            if (string.IsNullOrEmpty(label)) return 0;
            int result;
            if (labels.TryGetValue(label, out result)) return result;
            // Rewired's element names repeat every frame. Normalize each
            // distinct label once, preserving the existing matching rules.
            var builder = new StringBuilder(label.Length);
            foreach (var character in label.ToLowerInvariant())
                if (char.IsLetterOrDigit(character)) builder.Append(character);
            var normalized = builder.ToString();
            result = 0;
            if (normalized.Contains("lefttrigger") || normalized.Contains("triggerleft") ||
                normalized == "l2" || normalized.EndsWith("l2") ||
                normalized == "zl" || normalized.EndsWith("zl")) result |= Left;
            if (normalized.Contains("righttrigger") || normalized.Contains("triggerright") ||
                normalized == "r2" || normalized.EndsWith("r2") ||
                normalized == "zr" || normalized.EndsWith("zr")) result |= Right;
            if (labels.Count < MaximumCachedLabels) labels.Add(label, result);
            return result;
        }
    }
}
