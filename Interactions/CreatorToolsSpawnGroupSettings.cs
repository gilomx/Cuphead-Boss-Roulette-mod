using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    // Shared validation and serialization, but each mode owns its own instance.
    internal sealed class CreatorToolsSpawnGroupSettings
    {
        internal const float DefaultMiniBossInterval = 30f;
        internal const int DefaultBatch = 1;
        internal float MiniBossMinimumInterval { get; private set; } = DefaultMiniBossInterval;
        internal float MiniBossMaximumInterval { get; private set; } = DefaultMiniBossInterval;
        internal int LightMinimumBatch { get; private set; } = DefaultBatch;
        internal int LightMaximumBatch { get; private set; } = DefaultBatch;
        internal int StrongMinimumBatch { get; private set; } = DefaultBatch;
        internal int StrongMaximumBatch { get; private set; } = DefaultBatch;

        internal CreatorToolsSpawnGroupSettings(
            float miniMinimum = DefaultMiniBossInterval,
            float miniMaximum = DefaultMiniBossInterval,
            int lightMinimum = DefaultBatch, int lightMaximum = DefaultBatch,
            int strongMinimum = DefaultBatch, int strongMaximum = DefaultBatch)
        {
            MiniBossMinimumInterval = miniMinimum;
            MiniBossMaximumInterval = miniMaximum;
            LightMinimumBatch = lightMinimum;
            LightMaximumBatch = lightMaximum;
            StrongMinimumBatch = strongMinimum;
            StrongMaximumBatch = strongMaximum;
        }

        internal static readonly string[] PropertyNames =
        {
            "miniBossMinimumInterval", "miniBossMaximumInterval",
            "lightMinimumBatch", "lightMaximumBatch",
            "strongMinimumBatch", "strongMaximumBatch"
        };

        internal bool TryUpdate(Dictionary<string, string> values, string prefix,
            out CreatorToolsSpawnGroupSettings updated)
        {
            updated = null;
            if (values == null) return false;
            float miniMinimum = MiniBossMinimumInterval, miniMaximum = MiniBossMaximumInterval;
            float lightMinimum = LightMinimumBatch, lightMaximum = LightMaximumBatch;
            float strongMinimum = StrongMinimumBatch, strongMaximum = StrongMaximumBatch;
            string legacyToken;
            if (values.TryGetValue(prefix + "miniBossCooldownSeconds", out legacyToken))
            {
                float legacy;
                // Validate every supplied field, including a superseded alias.
                if (!TryNumber(legacyToken, 0f, 300f, false, out legacy)) return false;
                if (!values.ContainsKey(prefix + PropertyNames[0]) &&
                    !values.ContainsKey(prefix + PropertyNames[1]))
                    miniMinimum = miniMaximum = legacy;
            }
            if (!TryPair(values, prefix, 0, 0f, 300f, false, ref miniMinimum, ref miniMaximum) ||
                !TryPair(values, prefix, 2, 1f, 20f, true, ref lightMinimum, ref lightMaximum) ||
                !TryPair(values, prefix, 4, 1f, 20f, true, ref strongMinimum, ref strongMaximum))
                return false;
            updated = new CreatorToolsSpawnGroupSettings
            {
                MiniBossMinimumInterval = miniMinimum,
                MiniBossMaximumInterval = miniMaximum,
                LightMinimumBatch = (int)lightMinimum,
                LightMaximumBatch = (int)lightMaximum,
                StrongMinimumBatch = (int)strongMinimum,
                StrongMaximumBatch = (int)strongMaximum
            };
            return true;
        }

        private static bool TryPair(Dictionary<string, string> values, string prefix, int index,
            float lowerLimit, float upperLimit, bool integer, ref float minimum, ref float maximum)
        {
            var minimumKey = prefix + PropertyNames[index];
            var maximumKey = prefix + PropertyNames[index + 1];
            if (!values.ContainsKey(minimumKey) && !values.ContainsKey(maximumKey)) return true;
            return TryNumber(CreatorToolsFlatJson.Value(values, minimumKey), lowerLimit, upperLimit, integer, out minimum) &&
                TryNumber(CreatorToolsFlatJson.Value(values, maximumKey), lowerLimit, upperLimit, integer, out maximum) &&
                minimum <= maximum;
        }

        internal static bool TryNumber(string token, float minimum, float maximum, bool integer, out float value)
        {
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= minimum && value <= maximum && (!integer || value == Math.Floor(value));
        }

        // Recover a bad stored pair independently so names, selections and other pairs survive.
        internal static CreatorToolsSpawnGroupSettings Load(Dictionary<string, string> values,
            Action<string> warning, out bool needsMigration,
            CreatorToolsSpawnGroupSettings defaults = null)
        {
            needsMigration = false;
            var result = defaults ?? new CreatorToolsSpawnGroupSettings();
            for (var index = 0; index < PropertyNames.Length; index += 2)
            {
                var pair = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var offset = 0; offset < 2; offset++)
                {
                    var key = PropertyNames[index + offset];
                    string token;
                    if (values.TryGetValue(key, out token)) pair[key] = token;
                    else needsMigration = true;
                }
                if (index == 0 && pair.Count == 0 && values.ContainsKey("miniBossCooldownSeconds"))
                    pair["miniBossCooldownSeconds"] = values["miniBossCooldownSeconds"];
                CreatorToolsSpawnGroupSettings updated;
                if (result.TryUpdate(pair, "", out updated)) result = updated;
                else
                {
                    needsMigration = true;
                    if (warning != null) warning("Los ajustes de " +
                        (index == 0 ? "minijefes" : index == 2 ? "molestias leves" : "molestias intensas") +
                        " no eran validos; se usaran los valores predeterminados para ese grupo.");
                }
            }
            return result;
        }

        internal void AppendJson(StringBuilder builder, bool defaultKeys = false)
        {
            var values = new[]
            {
                MiniBossMinimumInterval, MiniBossMaximumInterval,
                LightMinimumBatch, LightMaximumBatch,
                StrongMinimumBatch, StrongMaximumBatch
            };
            for (var i = 0; i < PropertyNames.Length; i++)
            {
                var key = PropertyNames[i];
                if (defaultKeys) key = "default" + char.ToUpperInvariant(key[0]) + key.Substring(1);
                builder.Append(",\"").Append(key).Append("\":")
                    .Append(values[i].ToString("R", CultureInfo.InvariantCulture));
            }
        }
    }
}
