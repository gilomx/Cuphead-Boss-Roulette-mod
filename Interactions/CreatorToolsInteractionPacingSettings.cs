using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    // Separate persistence and values for Manual/Stream; never loads Pesky settings.
    internal sealed class CreatorToolsInteractionPacingSettings
    {
        internal const string FileName = "mx.gilomx.cuphead.bossroulette.interaction-pacing.json";
        private readonly string path;
        private readonly Action<string> warning;
        internal bool Enabled { get; private set; }
        internal float MinimumInterval { get; private set; } = 1.25f;
        internal float MaximumInterval { get; private set; } = 3.25f;
        internal float MiniBossCooldownSeconds { get; private set; } = 30f;
        internal float MiniBossIntervalMultiplier { get; private set; } = 2f;
        internal int MaximumCompanionsDuringMiniBoss { get; private set; } = 1;

        private CreatorToolsInteractionPacingSettings(string path, Action<string> warning)
        {
            this.path = path;
            this.warning = warning;
        }

        internal static CreatorToolsInteractionPacingSettings Load(string pluginConfigPath, Action<string> warning)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(pluginConfigPath));
            var settings = new CreatorToolsInteractionPacingSettings(Path.Combine(directory, FileName), warning);
            if (settings.TryLoad(settings.path)) return settings;
            if (settings.TryLoad(settings.path + ".bak"))
            {
                settings.Warn("Se recupero el respaldo del balance de Interacciones.");
                settings.Save();
            }
            else if (File.Exists(settings.path) || File.Exists(settings.path + ".bak"))
                settings.Warn("Balance de Interacciones no valido; se usaran sus propios valores predeterminados.");
            return settings;
        }

        private bool TryLoad(string candidate)
        {
            try
            {
                Dictionary<string, string> values;
                return File.Exists(candidate) &&
                    CreatorToolsFlatJson.TryParse(File.ReadAllText(candidate), out values) &&
                    TrySet(values, "");
            }
            catch { return false; }
        }

        // A complete balance request is validated before committing any value.
        internal bool TrySet(Dictionary<string, string> values, string prefix)
        {
            bool enabled;
            var enabledToken = CreatorToolsFlatJson.Value(values, prefix + "enabled");
            if (enabledToken == "1") enabled = true;
            else if (enabledToken == "0") enabled = false;
            else if (!bool.TryParse(enabledToken, out enabled)) return false;
            float minimum, maximum, cooldown, multiplier, companions;
            if (!Number(values, prefix + "minimumInterval", 0.35f, 300f, out minimum) ||
                !Number(values, prefix + "maximumInterval", 0.35f, 300f, out maximum) || minimum > maximum ||
                !Number(values, prefix + "miniBossCooldownSeconds", 0f, 300f, out cooldown) ||
                !Number(values, prefix + "miniBossIntervalMultiplier", 1f, 10f, out multiplier) ||
                !Number(values, prefix + "maximumCompanionsDuringMiniBoss", 0f, 20f, out companions) ||
                companions != Math.Floor(companions)) return false;
            Enabled = enabled;
            MinimumInterval = minimum;
            MaximumInterval = maximum;
            MiniBossCooldownSeconds = cooldown;
            MiniBossIntervalMultiplier = multiplier;
            MaximumCompanionsDuringMiniBoss = (int)companions;
            return true;
        }

        private static bool Number(Dictionary<string, string> values, string key, float min, float max, out float result)
        {
            return float.TryParse(CreatorToolsFlatJson.Value(values, key), NumberStyles.Float,
                CultureInfo.InvariantCulture, out result) && !float.IsNaN(result) &&
                !float.IsInfinity(result) && result >= min && result <= max;
        }

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{\"enabled\":").Append(Enabled ? "true" : "false")
                .Append(",\"minimumInterval\":").Append(MinimumInterval.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"maximumInterval\":").Append(MaximumInterval.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"miniBossCooldownSeconds\":").Append(MiniBossCooldownSeconds.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"miniBossIntervalMultiplier\":").Append(MiniBossIntervalMultiplier.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"maximumCompanionsDuringMiniBoss\":").Append(MaximumCompanionsDuringMiniBoss)
                .Append('}');
        }

        internal static void AppendDefaultsJson(StringBuilder builder)
        {
            new CreatorToolsInteractionPacingSettings(null, null).AppendJson(builder);
        }

        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var builder = new StringBuilder();
                AppendJson(builder);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, builder.ToString(), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
                else File.Move(temporary, path);
            }
            catch (Exception exception) { Warn("No se pudo guardar el balance de Interacciones: " + exception.Message); }
        }

        private void Warn(string message) { if (warning != null) warning(message); }
    }
}
