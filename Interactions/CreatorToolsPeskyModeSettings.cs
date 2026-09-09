using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsPeskyModeSettings
    {
        internal const int MaximumNameLength = 32;
        internal const int MaximumNames = 200;
        internal const float IntervalLowerLimit = 0.35f;
        internal const float IntervalUpperLimit = 300f;
        internal const float DefaultMinimumInterval = 1.25f;
        internal const float DefaultMaximumInterval = 3.25f;
        internal const float DefaultMiniBossCooldownSeconds = 30f;
        internal const float DefaultMiniBossIntervalMultiplier = 2f;
        internal const int DefaultMaximumCompanionsDuringMiniBoss = 1;
        private const int CurrentVersion = 5;
        private static readonly string[] DefaultNames =
        {
            "Claudia",
            "YeiAndPelos",
            "Yerrisito",
            "Malono",
            "Suches",
            "Elver_hijas"
        };

        private readonly string path;
        private readonly Action<string> logWarning;
        private bool needsMigration;

        internal bool Enabled;
        internal float MinimumInterval { get; private set; }
        internal float MaximumInterval { get; private set; }
        internal float MiniBossCooldownSeconds { get; private set; }
        internal float MiniBossIntervalMultiplier { get; private set; }
        internal int MaximumCompanionsDuringMiniBoss { get; private set; }
        internal readonly List<string> Names = new List<string>();
        internal readonly HashSet<string> DisabledItems =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CreatorToolsPeskyModeSettings(
            string path,
            Action<string> logWarning)
        {
            this.path = path;
            this.logWarning = logWarning;
        }

        internal static CreatorToolsPeskyModeSettings Load(
            string pluginConfigPath,
            Action<string> logWarning)
        {
            var directory = Path.GetDirectoryName(
                string.IsNullOrEmpty(pluginConfigPath)
                    ? string.Empty
                    : Path.GetFullPath(pluginConfigPath));
            if (string.IsNullOrEmpty(directory))
                directory = Environment.CurrentDirectory;
            var path = Path.Combine(directory,
                "mx.gilomx.cuphead.bossroulette.pesky-mode.json");
            var settings = new CreatorToolsPeskyModeSettings(
                path, logWarning);
            settings.ResetToDefaults();

            if (settings.TryLoadFile(path))
            {
                if (settings.needsMigration)
                    settings.Save();
                return settings;
            }

            var backupPath = path + ".bak";
            if (settings.TryLoadFile(backupPath))
            {
                settings.Warn(
                    "La configuracion principal de Modo Molestoso no pudo " +
                    "leerse; se recupero el respaldo.");
                settings.Save();
                return settings;
            }

            if (File.Exists(path) || File.Exists(backupPath))
                settings.Warn(
                    "La configuracion de Modo Molestoso no era valida; se " +
                    "usaran valores seguros.");
            return settings;
        }

        internal void SetNames(IEnumerable<string> values)
        {
            Names.Clear();
            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (values == null)
                return;
            foreach (var raw in values)
            {
                if (Names.Count >= MaximumNames)
                    break;
                var value = (raw ?? string.Empty).Trim();
                if (value.Length == 0)
                    continue;
                if (value.Length > MaximumNameLength)
                    value = value.Substring(0, MaximumNameLength);
                if (seen.Add(value))
                    Names.Add(value);
            }
        }

        internal bool TrySetIntervals(string minimum, string maximum)
        {
            float minimumInterval;
            float maximumInterval;
            if (!float.TryParse(minimum, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out minimumInterval) ||
                !float.TryParse(maximum, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out maximumInterval) ||
                float.IsNaN(minimumInterval) ||
                float.IsInfinity(minimumInterval) ||
                float.IsNaN(maximumInterval) ||
                float.IsInfinity(maximumInterval) ||
                minimumInterval < IntervalLowerLimit ||
                maximumInterval > IntervalUpperLimit ||
                minimumInterval > maximumInterval)
                return false;

            MinimumInterval = minimumInterval;
            MaximumInterval = maximumInterval;
            return true;
        }

        internal bool TrySetPacing(string minimum, string maximum,
            string cooldown, string multiplier, string companions)
        {
            float cooldownValue;
            float multiplierValue;
            float companionsValue;
            // Validate the entire request before changing any field. Older
            // clients may still send only the original interval pair.
            if (!TryBalanceNumber(cooldown ?? MiniBossCooldownSeconds.ToString(
                    "R", CultureInfo.InvariantCulture), 0f, 300f, false, out cooldownValue) ||
                !TryBalanceNumber(multiplier ?? MiniBossIntervalMultiplier.ToString(
                    "R", CultureInfo.InvariantCulture), 1f, 10f, false, out multiplierValue) ||
                !TryBalanceNumber(companions ?? MaximumCompanionsDuringMiniBoss.ToString(
                    CultureInfo.InvariantCulture), 0f, 20f, true, out companionsValue) ||
                !TrySetIntervals(minimum, maximum))
                return false;
            MiniBossCooldownSeconds = cooldownValue;
            MiniBossIntervalMultiplier = multiplierValue;
            MaximumCompanionsDuringMiniBoss = (int)companionsValue;
            return true;
        }

        internal void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, BuildJson(),
                    new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporaryPath, path, path + ".bak", true);
                        return;
                    }
                    catch
                    {
                        File.Copy(path, path + ".bak", true);
                        File.Delete(path);
                    }
                }
                File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Warn("No se pudo guardar Modo Molestoso: " +
                    exception.Message);
            }
        }

        private void ResetToDefaults()
        {
            Enabled = false;
            ResetIntervalsToDefaults();
            MiniBossCooldownSeconds = DefaultMiniBossCooldownSeconds;
            MiniBossIntervalMultiplier = DefaultMiniBossIntervalMultiplier;
            MaximumCompanionsDuringMiniBoss = DefaultMaximumCompanionsDuringMiniBoss;
            DisabledItems.Clear();
            SetNames(DefaultNames);
        }

        private void ResetIntervalsToDefaults()
        {
            MinimumInterval = DefaultMinimumInterval;
            MaximumInterval = DefaultMaximumInterval;
        }

        private bool TryLoadFile(string candidatePath)
        {
            if (!File.Exists(candidatePath))
                return false;
            try
            {
                var json = File.ReadAllText(candidatePath, Encoding.UTF8);
                bool enabled;
                List<string> names;
                List<string> disabledItems;
                if (!TryReadBoolean(json, "enabled", out enabled) ||
                    !TryReadStringArray(json, "names", out names) ||
                    !TryReadStringArray(
                        json, "disabledItems", out disabledItems))
                    return false;

                Enabled = enabled;
                SetNames(names);
                DisabledItems.Clear();
                for (var i = 0; i < disabledItems.Count; i++)
                    if (IsKnownItem(disabledItems[i]))
                        DisabledItems.Add(disabledItems[i]);
                if (EnabledItemCount == 0)
                    Enabled = false;

                // Older files have no interval fields. Recover only this pair
                // if a later file has invalid values, retaining names/items.
                ResetIntervalsToDefaults();
                var minimumPosition = FindPropertyValue(
                    json, "minimumInterval");
                var maximumPosition = FindPropertyValue(
                    json, "maximumInterval");
                if ((minimumPosition >= 0 || maximumPosition >= 0) &&
                    !TrySetIntervals(ReadNumberToken(json, minimumPosition),
                        ReadNumberToken(json, maximumPosition)))
                    Warn("Los intervalos de Modo Molestoso no eran validos; " +
                        "se usaran los intervalos predeterminados.");
                needsMigration = false;
                MiniBossCooldownSeconds = ReadBalanceNumber(json,
                    "miniBossCooldownSeconds", DefaultMiniBossCooldownSeconds, 0f, 300f, false);
                MiniBossIntervalMultiplier = ReadBalanceNumber(json,
                    "miniBossIntervalMultiplier", DefaultMiniBossIntervalMultiplier, 1f, 10f, false);
                MaximumCompanionsDuringMiniBoss = (int)ReadBalanceNumber(json,
                    "maximumCompanionsDuringMiniBoss", DefaultMaximumCompanionsDuringMiniBoss,
                    0f, 20f, true);
                // Retire the former shared opt-in. Interactions owns its own file.
                if (FindPropertyValue(json, "applyToInteractions") >= 0)
                    needsMigration = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private float ReadBalanceNumber(string json, string property,
            float fallback, float minimum, float maximum, bool integer)
        {
            var position = FindPropertyValue(json, property);
            if (position < 0)
            {
                needsMigration = true;
                return fallback;
            }
            float value;
            if (TryBalanceNumber(ReadNumberToken(json, position), minimum, maximum, integer, out value))
                return value;
            Warn("Valor no valido para " + property +
                " en Modo Molestoso; se usara el predeterminado.");
            return fallback;
        }

        private static bool TryBalanceNumber(string token, float minimum,
            float maximum, bool integer, out float value)
        {
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= minimum && value <= maximum && (!integer || value == Math.Floor(value));
        }

        private string BuildJson()
        {
            var builder = new StringBuilder(1024);
            builder.Append("{\n  \"version\": ")
                .Append(CurrentVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"enabled\": ")
                .Append(Enabled ? "true" : "false")
                .Append(",\n  \"minimumInterval\": ")
                .Append(MinimumInterval.ToString(
                    "R", CultureInfo.InvariantCulture))
                .Append(",\n  \"maximumInterval\": ")
                .Append(MaximumInterval.ToString(
                    "R", CultureInfo.InvariantCulture))
                .Append(",\n  \"miniBossCooldownSeconds\": ")
                .Append(MiniBossCooldownSeconds.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\n  \"miniBossIntervalMultiplier\": ")
                .Append(MiniBossIntervalMultiplier.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\n  \"maximumCompanionsDuringMiniBoss\": ")
                .Append(MaximumCompanionsDuringMiniBoss.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"names\": [");
            for (var i = 0; i < Names.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendJsonString(builder, Names[i]);
            }
            builder.Append("],\n  \"disabledItems\": [");
            var first = true;
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var item = CreatorToolsInteractionIds.All[i];
                if (!DisabledItems.Contains(item))
                    continue;
                if (!first)
                    builder.Append(", ");
                AppendJsonString(builder, item);
                first = false;
            }
            builder.Append("]\n}\n");
            return builder.ToString();
        }

        internal int EnabledItemCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
                    if (!DisabledItems.Contains(
                        CreatorToolsInteractionIds.All[i]))
                        count++;
                return count;
            }
        }

        internal bool IsItemEnabled(string item)
        {
            return IsKnownItem(item) && !DisabledItems.Contains(item);
        }

        private static bool IsKnownItem(string item)
        {
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
                if (string.Equals(CreatorToolsInteractionIds.All[i], item,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool TryReadBoolean(
            string json, string property, out bool value)
        {
            value = false;
            var position = FindPropertyValue(json, property);
            if (position < 0)
                return false;
            if (StartsWith(json, position, "true"))
            {
                value = true;
                return true;
            }
            if (StartsWith(json, position, "false"))
                return true;
            return false;
        }

        private static string ReadNumberToken(string json, int position)
        {
            if (position < 0 || position >= json.Length)
                return null;
            var end = position;
            while (end < json.Length && json[end] != ',' &&
                json[end] != '}' && !char.IsWhiteSpace(json[end]))
                end++;
            return json.Substring(position, end - position);
        }

        private static bool TryReadStringArray(
            string json, string property, out List<string> values)
        {
            values = new List<string>();
            var position = FindPropertyValue(json, property);
            if (position < 0 || position >= json.Length ||
                json[position] != '[')
                return false;
            position++;
            while (position < json.Length)
            {
                SkipWhitespace(json, ref position);
                if (position < json.Length && json[position] == ']')
                    return true;
                string value;
                if (!TryReadJsonString(json, ref position, out value))
                    return false;
                values.Add(value);
                SkipWhitespace(json, ref position);
                if (position < json.Length && json[position] == ',')
                {
                    position++;
                    continue;
                }
                if (position < json.Length && json[position] == ']')
                    return true;
                return false;
            }
            return false;
        }

        private static int FindPropertyValue(string json, string property)
        {
            var marker = "\"" + property + "\"";
            var position = json.IndexOf(marker, StringComparison.Ordinal);
            if (position < 0)
                return -1;
            position += marker.Length;
            SkipWhitespace(json, ref position);
            if (position >= json.Length || json[position] != ':')
                return -1;
            position++;
            SkipWhitespace(json, ref position);
            return position;
        }

        private static bool TryReadJsonString(
            string json, ref int position, out string value)
        {
            value = null;
            if (position >= json.Length || json[position] != '"')
                return false;
            position++;
            var builder = new StringBuilder();
            while (position < json.Length)
            {
                var character = json[position++];
                if (character == '"')
                {
                    value = builder.ToString();
                    return true;
                }
                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }
                if (position >= json.Length)
                    return false;
                character = json[position++];
                if (character == '"' || character == '\\' ||
                    character == '/')
                    builder.Append(character);
                else if (character == 'n')
                    builder.Append('\n');
                else if (character == 'r')
                    builder.Append('\r');
                else if (character == 't')
                    builder.Append('\t');
                else if (character == 'b')
                    builder.Append('\b');
                else if (character == 'f')
                    builder.Append('\f');
                else if (character == 'u' && position + 4 <= json.Length)
                {
                    int code;
                    if (!int.TryParse(json.Substring(position, 4),
                        NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                        out code))
                        return false;
                    builder.Append((char)code);
                    position += 4;
                }
                else
                    return false;
            }
            return false;
        }

        private static void AppendJsonString(
            StringBuilder builder, string value)
        {
            builder.Append('"');
            for (var i = 0; i < (value ?? string.Empty).Length; i++)
            {
                var character = value[i];
                if (character == '"' || character == '\\')
                    builder.Append('\\').Append(character);
                else if (character == '\n')
                    builder.Append("\\n");
                else if (character == '\r')
                    builder.Append("\\r");
                else if (character == '\t')
                    builder.Append("\\t");
                else if (character < 32)
                    builder.Append("\\u")
                        .Append(((int)character).ToString("x4"));
                else
                    builder.Append(character);
            }
            builder.Append('"');
        }

        private static void SkipWhitespace(string value, ref int position)
        {
            while (position < value.Length &&
                char.IsWhiteSpace(value[position]))
                position++;
        }

        private static bool StartsWith(
            string value, int position, string candidate)
        {
            return position + candidate.Length <= value.Length &&
                string.Compare(value, position, candidate, 0,
                    candidate.Length, StringComparison.Ordinal) == 0;
        }

        private void Warn(string message)
        {
            if (logWarning != null)
                logWarning(message);
        }
    }
}
