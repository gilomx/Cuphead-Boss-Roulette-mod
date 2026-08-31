using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsGiftCatalogEntry
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string ImagePath;
        internal readonly int CoinsPerUnit;

        internal CreatorToolsGiftCatalogEntry(
            string id, string name, string imagePath, int coinsPerUnit)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            ImagePath = imagePath ?? string.Empty;
            CoinsPerUnit = Math.Max(0, coinsPerUnit);
        }
    }

    internal sealed class CreatorToolsPeskyBattleSettings
    {
        private const int CurrentVersion = 1;
        private readonly string path;
        private readonly Action<string> logWarning;

        internal string GiftId = string.Empty;
        internal bool AllowStreamAttacks = true;
        internal readonly HashSet<string> DisabledItems =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CreatorToolsPeskyBattleSettings(
            string path, Action<string> logWarning)
        {
            this.path = path;
            this.logWarning = logWarning;
        }

        internal static CreatorToolsPeskyBattleSettings Load(
            string pluginConfigPath, Action<string> logWarning)
        {
            var directory = Path.GetDirectoryName(
                string.IsNullOrEmpty(pluginConfigPath)
                    ? string.Empty
                    : Path.GetFullPath(pluginConfigPath));
            if (string.IsNullOrEmpty(directory))
                directory = Environment.CurrentDirectory;
            var settings = new CreatorToolsPeskyBattleSettings(
                Path.Combine(directory,
                    "mx.gilomx.cuphead.bossroulette.pesky-battle.json"),
                logWarning);
            if (settings.TryLoad(settings.path))
                return settings;
            if (settings.TryLoad(settings.path + ".bak"))
            {
                settings.Warn(
                    "La configuracion de Batalla Molestosa se recupero " +
                    "desde el respaldo.");
                settings.Save();
                return settings;
            }
            if (File.Exists(settings.path) ||
                File.Exists(settings.path + ".bak"))
                settings.Warn(
                    "La configuracion de Batalla Molestosa no era valida; " +
                    "se usaran valores seguros.");
            return settings;
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
                Warn("No se pudo guardar Batalla Molestosa: " +
                    exception.Message);
            }
        }

        private bool TryLoad(string candidatePath)
        {
            if (!File.Exists(candidatePath))
                return false;
            try
            {
                var json = File.ReadAllText(candidatePath, Encoding.UTF8);
                int version;
                bool allowStreamAttacks;
                string giftId;
                List<string> disabledItems;
                if (!TryReadInt(json, "version", out version) ||
                    version != CurrentVersion ||
                    !TryReadString(json, "giftId", out giftId) ||
                    !TryReadBoolean(json, "allowStreamAttacks",
                        out allowStreamAttacks) ||
                    !TryReadStringArray(json, "disabledItems",
                        out disabledItems))
                    return false;

                GiftId = NormalizeGiftId(giftId);
                AllowStreamAttacks = allowStreamAttacks;
                DisabledItems.Clear();
                for (var i = 0; i < disabledItems.Count; i++)
                    if (IsKnownItem(disabledItems[i]))
                        DisabledItems.Add(disabledItems[i]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string BuildJson()
        {
            var builder = new StringBuilder(512);
            builder.Append("{\n  \"version\": ")
                .Append(CurrentVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"giftId\": \"");
            AppendJson(builder, GiftId);
            builder.Append("\",\n  \"allowStreamAttacks\": ")
                .Append(AllowStreamAttacks ? "true" : "false")
                .Append(",\n  \"disabledItems\": [");
            var first = true;
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var item = CreatorToolsInteractionIds.All[i];
                if (!DisabledItems.Contains(item))
                    continue;
                if (!first)
                    builder.Append(", ");
                builder.Append('"');
                AppendJson(builder, item);
                builder.Append('"');
                first = false;
            }
            builder.Append("]\n}\n");
            return builder.ToString();
        }

        internal static string NormalizeGiftId(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length > 160)
                value = value.Substring(0, 160);
            for (var i = 0; i < value.Length; i++)
                if (!char.IsDigit(value[i]))
                    return string.Empty;
            return value;
        }

        private static bool IsKnownItem(string item)
        {
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
                if (string.Equals(CreatorToolsInteractionIds.All[i], item,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
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

        private static bool TryReadBoolean(
            string json, string property, out bool value)
        {
            value = false;
            var match = Regex.Match(json,
                "\\\"" + Regex.Escape(property) +
                "\\\"\\s*:\\s*(?<value>true|false)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return false;
            value = string.Equals(match.Groups["value"].Value, "true",
                StringComparison.Ordinal);
            return true;
        }

        private static bool TryReadString(
            string json, string property, out string value)
        {
            value = string.Empty;
            var match = Regex.Match(json,
                "\\\"" + Regex.Escape(property) +
                "\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return false;
            value = UnescapeJson(match.Groups["value"].Value);
            return true;
        }

        private static bool TryReadStringArray(
            string json, string property, out List<string> values)
        {
            values = new List<string>();
            var match = Regex.Match(json,
                "\\\"" + Regex.Escape(property) +
                "\\\"\\s*:\\s*\\[(?<value>[\\s\\S]*?)\\]",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return false;
            var entries = Regex.Matches(match.Groups["value"].Value,
                "\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"",
                RegexOptions.CultureInvariant);
            for (var i = 0; i < entries.Count; i++)
                values.Add(UnescapeJson(
                    entries[i].Groups["value"].Value));
            return true;
        }

        private static void AppendJson(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\').Append(character);
                else if (character == '\n') builder.Append("\\n");
                else if (character == '\r') builder.Append("\\r");
                else if (character == '\t') builder.Append("\\t");
                else builder.Append(character);
            }
        }

        private static string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value ?? string.Empty;
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(character);
                    continue;
                }
                character = value[++i];
                if (character == 'n') builder.Append('\n');
                else if (character == 'r') builder.Append('\r');
                else if (character == 't') builder.Append('\t');
                else builder.Append(character);
            }
            return builder.ToString();
        }

        private void Warn(string message)
        {
            if (logWarning != null)
                logWarning(message);
        }
    }
}
