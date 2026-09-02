using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsTapFarmingSettings
    {
        private const int CurrentVersion = 2;
        internal const int DefaultTapsPerConversion = 2;
        internal const int DefaultHealthPointsPerConversion = 1;
        internal const int MinimumConversionValue = 1;
        internal const int MaximumConversionValue = 100000;

        private readonly string path;
        private readonly Action<string> logWarning;

        internal int TapsPerConversion = DefaultTapsPerConversion;
        internal int HealthPointsPerConversion =
            DefaultHealthPointsPerConversion;

        private CreatorToolsTapFarmingSettings(
            string path, Action<string> logWarning)
        {
            this.path = path;
            this.logWarning = logWarning;
        }

        internal static CreatorToolsTapFarmingSettings Load(
            string pluginConfigPath, Action<string> logWarning)
        {
            var directory = Path.GetDirectoryName(
                string.IsNullOrEmpty(pluginConfigPath)
                    ? string.Empty
                    : Path.GetFullPath(pluginConfigPath));
            if (string.IsNullOrEmpty(directory))
                directory = Environment.CurrentDirectory;

            var settings = new CreatorToolsTapFarmingSettings(
                Path.Combine(directory,
                    "mx.gilomx.cuphead.bossroulette.tap-farming.json"),
                logWarning);
            bool migrated;
            if (settings.TryLoad(settings.path, out migrated))
            {
                if (migrated)
                    settings.Save();
                return settings;
            }
            if (settings.TryLoad(settings.path + ".bak", out migrated))
            {
                settings.Warn(
                    "La configuracion de Farmeando taps se recupero " +
                    "desde el respaldo.");
                settings.Save();
                return settings;
            }
            if (File.Exists(settings.path) ||
                File.Exists(settings.path + ".bak"))
                settings.Warn(
                    "La configuracion de Farmeando taps no era valida; " +
                    "se usara cada 2 taps = 1 punto de vida.");
            return settings;
        }

        internal void SetConversion(
            int tapsPerConversion, int healthPointsPerConversion)
        {
            TapsPerConversion = NormalizeConversionValue(
                tapsPerConversion);
            HealthPointsPerConversion = NormalizeConversionValue(
                healthPointsPerConversion);
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
                        File.Replace(temporaryPath, path,
                            path + ".bak", true);
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
                Warn("No se pudo guardar Farmeando taps: " +
                    exception.Message);
            }
        }

        internal static int NormalizeConversionValue(int value)
        {
            return Math.Max(MinimumConversionValue,
                Math.Min(MaximumConversionValue, value));
        }

        private bool TryLoad(string candidatePath, out bool migrated)
        {
            migrated = false;
            if (!File.Exists(candidatePath))
                return false;
            try
            {
                var json = File.ReadAllText(candidatePath, Encoding.UTF8);
                int version;
                if (!TryReadInt(json, "version", out version))
                    return false;

                if (version == CurrentVersion)
                {
                    int tapsPerConversion;
                    int healthPointsPerConversion;
                    if (!TryReadConversionValue(json,
                            "tapsPerConversion",
                            out tapsPerConversion) ||
                        !TryReadConversionValue(json,
                            "healthPointsPerConversion",
                            out healthPointsPerConversion))
                        return false;
                    TapsPerConversion = tapsPerConversion;
                    HealthPointsPerConversion =
                        healthPointsPerConversion;
                    return true;
                }

                // Version 1 expressed the same exact ratio as "N taps =
                // 1 health point". Preserve it losslessly and immediately
                // rewrite the primary file in the canonical v2 shape.
                int legacyTapsPerHealthPoint;
                if (version != 1 ||
                    !TryReadConversionValue(json,
                        "tapsPerHealthPoint",
                        out legacyTapsPerHealthPoint))
                    return false;
                TapsPerConversion = legacyTapsPerHealthPoint;
                HealthPointsPerConversion = 1;
                migrated = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string BuildJson()
        {
            return "{\n  \"version\": " +
                CurrentVersion.ToString(CultureInfo.InvariantCulture) +
                ",\n  \"tapsPerConversion\": " +
                TapsPerConversion.ToString(
                    CultureInfo.InvariantCulture) +
                ",\n  \"healthPointsPerConversion\": " +
                HealthPointsPerConversion.ToString(
                    CultureInfo.InvariantCulture) +
                "\n}\n";
        }

        private static bool TryReadConversionValue(
            string json, string property, out int value)
        {
            return TryReadInt(json, property, out value) &&
                value >= MinimumConversionValue &&
                value <= MaximumConversionValue;
        }

        private static bool TryReadInt(
            string json, string property, out int value)
        {
            value = 0;
            var match = Regex.Match(json,
                "\\\"" + Regex.Escape(property) +
                "\\\"\\s*:\\s*(?<value>\\d+)",
                RegexOptions.CultureInvariant);
            return match.Success && int.TryParse(
                match.Groups["value"].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        private void Warn(string message)
        {
            if (logWarning != null)
                logWarning(message);
        }
    }
}
