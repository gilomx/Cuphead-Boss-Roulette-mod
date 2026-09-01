using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsTapFarmingSettings
    {
        private const int CurrentVersion = 1;
        internal const int DefaultTapsPerHealthPoint = 2;
        internal const int MinimumTapsPerHealthPoint = 1;
        internal const int MaximumTapsPerHealthPoint = 100000;

        private readonly string path;
        private readonly Action<string> logWarning;

        internal int TapsPerHealthPoint = DefaultTapsPerHealthPoint;

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
            if (settings.TryLoad(settings.path))
                return settings;
            if (settings.TryLoad(settings.path + ".bak"))
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

        internal void SetTapsPerHealthPoint(int value)
        {
            TapsPerHealthPoint = NormalizeTapsPerHealthPoint(value);
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

        internal static int NormalizeTapsPerHealthPoint(int value)
        {
            return Math.Max(MinimumTapsPerHealthPoint,
                Math.Min(MaximumTapsPerHealthPoint, value));
        }

        private bool TryLoad(string candidatePath)
        {
            if (!File.Exists(candidatePath))
                return false;
            try
            {
                var json = File.ReadAllText(candidatePath, Encoding.UTF8);
                int version;
                int tapsPerHealthPoint;
                if (!TryReadInt(json, "version", out version) ||
                    version != CurrentVersion ||
                    !TryReadInt(json, "tapsPerHealthPoint",
                        out tapsPerHealthPoint) ||
                    tapsPerHealthPoint < MinimumTapsPerHealthPoint ||
                    tapsPerHealthPoint > MaximumTapsPerHealthPoint)
                    return false;
                TapsPerHealthPoint = tapsPerHealthPoint;
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
                ",\n  \"tapsPerHealthPoint\": " +
                TapsPerHealthPoint.ToString(CultureInfo.InvariantCulture) +
                "\n}\n";
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
