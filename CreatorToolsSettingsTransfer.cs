using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Gilomx.CupheadBossRoulette
{
    // Only this explicit list belongs to a portable configuration. Account state,
    // Credential Manager, launcher data, saves and runtime queues are never read.
    internal sealed class CreatorToolsSettingsTransfer
    {
        internal const int MaximumBundleBytes = 1024 * 1024;
        private const int MaximumFileBytes = 256 * 1024;
        private const string Prefix = "mx.gilomx.cuphead.bossroulette";
        private const string Format = "la-pichi-ruleta-settings";
        private static readonly string[] Suffixes = {
            ".cfg", ".stream-rules.json", ".pesky-mode.json",
            ".interaction-pacing.json", ".pesky-battle.json",
            ".tap-farming.json", ".overlay-composer.json", ".community-gift.json"
        };
        private readonly object sync = new object();
        private readonly string configPath;
        private readonly string directory;
        private readonly string pendingPath;
        private readonly string defaultsConfigPath;
        private readonly Action<string> warning;
        private readonly Dictionary<string, string> defaults;

        internal CreatorToolsSettingsTransfer(string pluginConfigPath, Action<string> warning)
        {
            configPath = Path.GetFullPath(pluginConfigPath);
            directory = Path.GetDirectoryName(configPath);
            pendingPath = Path.Combine(directory, Prefix + ".pending-import.json");
            this.warning = warning;
            // These paths never exist. Loaders therefore construct their own defaults
            // without reading or saving the current installation's settings.
            var emptyPath = Path.Combine(Path.Combine(directory, ".defaults-" + Guid.NewGuid().ToString("N")), Prefix + ".cfg");
            defaultsConfigPath = emptyPath;
            var pacing = new StringBuilder();
            CreatorToolsInteractionPacingSettings.Load(emptyPath, null).AppendJson(pacing);
            defaults = new Dictionary<string, string>(StringComparer.Ordinal) {
                { ".cfg", "" },
                { ".stream-rules.json", "{\"version\":6,\"nextId\":1,\"rules\":[]}" },
                { ".pesky-mode.json", CreatorToolsPeskyModeSettings.Load(emptyPath, null).BuildJson() },
                { ".interaction-pacing.json", pacing.ToString() },
                { ".pesky-battle.json", CreatorToolsPeskyBattleSettings.Load(emptyPath, null).BuildJson() },
                { ".tap-farming.json", CreatorToolsTapFarmingSettings.Load(emptyPath, null).BuildJson() },
                { ".overlay-composer.json", CreatorToolsOverlayComposerSettings.Load(emptyPath, null).BuildFileJson() },
                { ".community-gift.json", "{\"version\":1,\"giftId\":\"\",\"previousGiftId\":\"\",\"name\":\"Community Gift\",\"imagePath\":\"/assets/creator-tools/gifts/images/0.webp\",\"coinsPerUnit\":0,\"observedAt\":\"\"}" }
            };
        }

        internal string GetStateJson()
        {
            lock (sync) return "{\"ready\":true,\"pendingImport\":" + (File.Exists(pendingPath) ? "true" : "false") + "}";
        }

        internal string Export()
        {
            lock (sync)
            {
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var suffix in Suffixes)
                {
                    var path = PathFor(suffix);
                    var text = File.Exists(path) ? ReadBounded(path) :
                        File.Exists(path + ".bak") ? ReadBounded(path + ".bak") : defaults[suffix];
                    if (suffix == ".pesky-mode.json" && !File.Exists(path) && !File.Exists(path + ".bak"))
                    {
                        // Match the running loader's first-use migration from the
                        // legacy interaction limit when Pesky has no saved file yet.
                        var legacyLimit = CreatorToolsPeskyModeSettings.DefaultMaximumActive;
                        var match = Regex.Match(files[Prefix + ".cfg"], @"(?m)^InteraccionesMaximasEnPantalla = ([0-9]+)$");
                        if (match.Success) legacyLimit = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        text = CreatorToolsPeskyModeSettings.Load(defaultsConfigPath, null, legacyLimit).BuildJson();
                    }
                    if (suffix == ".cfg") text = PortableConfig(text, false);
                    else text = PortableJson(text, suffix, false);
                    files[Prefix + suffix] = text;
                }
                return BuildBundle(files, "es");
            }
        }

        internal string StageImport(string body)
        {
            lock (sync)
            {
                Dictionary<string, string> files;
                string locale;
                if (!TryBundle(body, out files, out locale)) return "invalid_configuration";
                try
                {
                    Directory.CreateDirectory(directory);
                    AtomicWrite(pendingPath, Encoding.UTF8.GetBytes(BuildBundle(files, locale)));
                    return string.Empty;
                }
                catch (Exception exception)
                {
                    Warn("No se pudo preparar la importacion: " + exception.Message);
                    return "save_failed";
                }
            }
        }

        internal string CancelImport()
        {
            lock (sync)
            {
                try { if (File.Exists(pendingPath)) File.Delete(pendingPath); return string.Empty; }
                catch (Exception exception) { Warn(exception.Message); return "save_failed"; }
            }
        }

        // Run before any Config.Bind or controller construction. The running game
        // never replaces loaded files, and account connections keep their lifecycle.
        internal bool ApplyPending()
        {
            lock (sync)
            {
                if (!File.Exists(pendingPath)) return false;
                var originals = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var changed = new List<string>();
                try
                {
                    Dictionary<string, string> files;
                    string locale;
                    if (!TryBundle(ReadBounded(pendingPath, MaximumBundleBytes), out files, out locale))
                        throw new FormatException("Invalid pending configuration.");
                    var backupDirectory = Path.Combine(Path.Combine(directory, "pichi-settings-backups"),
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(backupDirectory);
                    foreach (var suffix in Suffixes)
                    {
                        var path = PathFor(suffix);
                        foreach (var previous in new[] { path, path + ".bak" })
                        {
                            originals[previous] = File.Exists(previous) ? File.ReadAllBytes(previous) : null;
                            if (originals[previous] != null)
                                File.WriteAllBytes(Path.Combine(backupDirectory, Path.GetFileName(previous)), originals[previous]);
                        }
                    }
                    foreach (var suffix in Suffixes)
                    {
                        var path = PathFor(suffix);
                        AtomicWrite(path, Encoding.UTF8.GetBytes(files[Prefix + suffix]));
                        changed.Add(path);
                        // Old automatic recovery must not undo the imported defaults.
                        // Both the old primary and old .bak survive in the backup above.
                        AtomicWrite(path + ".bak", Encoding.UTF8.GetBytes(files[Prefix + suffix]));
                        changed.Add(path + ".bak");
                    }
                    File.Delete(pendingPath);
                    return true;
                }
                catch (Exception exception)
                {
                    for (var index = changed.Count - 1; index >= 0; index--)
                    {
                        var path = changed[index];
                        try
                        {
                            if (originals[path] == null) File.Delete(path);
                            else AtomicWrite(path, originals[path]);
                        }
                        catch (Exception rollback) { Warn("No se pudo restaurar " + Path.GetFileName(path) + ": " + rollback.Message); }
                    }
                    Warn("No se importo la configuracion; se conserva el respaldo anterior: " + exception.Message);
                    return false;
                }
            }
        }

        private bool TryBundle(string body, out Dictionary<string, string> files, out string locale)
        {
            files = new Dictionary<string, string>(StringComparer.Ordinal);
            locale = "es";
            try
            {
                CreatorToolsJsonValue root;
                if (body == null || Encoding.UTF8.GetByteCount(body) > MaximumBundleBytes ||
                    !CreatorToolsJsonParser.TryParse(body, out root, MaximumBundleBytes, MaximumFileBytes, 256) ||
                    root.ObjectValue == null || root.ObjectValue.Count != 4 || root.String("format") != Format ||
                    root.Integer("schemaVersion", -1) != 1 || (root.String("panelLocale") != "es" && root.String("panelLocale") != "en")) return false;
                locale = root.String("panelLocale");
                var node = root.Property("files");
                if (node == null || node.ObjectValue == null || node.ObjectValue.Count != Suffixes.Length) return false;
                foreach (var suffix in Suffixes)
                {
                    var value = node.Property(Prefix + suffix);
                    if (value == null || value.StringValue == null || Encoding.UTF8.GetByteCount(value.StringValue) > MaximumFileBytes) return false;
                    files[Prefix + suffix] = suffix == ".cfg" ? PortableConfig(value.StringValue, true) : PortableJson(value.StringValue, suffix, true);
                }
                return true;
            }
            catch { return false; }
        }

        private string PortableJson(string text, string suffix, bool importing)
        {
            CreatorToolsJsonValue node, template;
            if (!CreatorToolsJsonParser.TryParse(text, out node, MaximumFileBytes, 8192, 256) || node.ObjectValue == null ||
                !CreatorToolsJsonParser.TryParse(defaults[suffix], out template, MaximumFileBytes, 8192, 256)) throw new FormatException("Invalid settings JSON.");
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            CollectKeys(template, allowed);
            if (suffix == ".stream-rules.json")
                foreach (var key in new[] { "id", "name", "enabled", "platform", "eventType", "giftId", "giftName", "rewardName", "every", "interaction", "quantity", "userCooldownSeconds", "globalCooldownSeconds", "durationSeconds", "countdownSeconds" }) allowed.Add(key);
            FilterKeys(node, allowed, importing, suffix == ".stream-rules.json");
            foreach (var key in template.ObjectValue.Keys)
                if (node.Property(key) == null) throw new FormatException("Incomplete settings.");
            var versionKey = suffix == ".overlay-composer.json" ? "schemaVersion" : "version";
            if (template.Property(versionKey) != null && node.Integer(versionKey, -1) != template.Integer(versionKey, -2))
                throw new FormatException("Unsupported settings version.");
            ValidateShape(node, template);
            if (suffix == ".stream-rules.json") { ValidateRules(node); return SerializeRuleSettings(node); }
            ValidateValues(node, suffix);
            return Serialize(node);
        }

        private void ValidateValues(CreatorToolsJsonValue node, string suffix)
        {
            var emptyPath = Path.Combine(Path.Combine(directory, ".validation-" + Guid.NewGuid().ToString("N")), Prefix + ".cfg");
            if (suffix == ".interaction-pacing.json" || suffix == ".pesky-mode.json")
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in node.ObjectValue)
                    if (pair.Value.ObjectValue == null && pair.Value.ArrayValue == null) values[pair.Key] = pair.Value.StringValue ?? Serialize(pair.Value);
                var valid = suffix == ".interaction-pacing.json"
                    ? CreatorToolsInteractionPacingSettings.Load(emptyPath, null).TrySet(values, "")
                    : CreatorToolsPeskyModeSettings.Load(emptyPath, null).TrySetPacing(values);
                if (!valid) throw new FormatException("Invalid pacing settings.");
                if (suffix == ".pesky-mode.json")
                {
                    RequireRange(node, "challengeDurationSeconds", 1, 120);
                    RequireRange(node, "challengeCountdownSeconds", 0, 30);
                    RequireRange(node, "challengeWaitSeconds", 0, 300);
                    if (node.Property("names").ArrayValue.Count > CreatorToolsPeskyModeSettings.MaximumNames) throw new FormatException("Too many names.");
                }
            }
            else if (suffix == ".tap-farming.json")
            {
                RequireRange(node, "tapsPerConversion", CreatorToolsTapFarmingSettings.MinimumConversionValue, CreatorToolsTapFarmingSettings.MaximumConversionValue);
                RequireRange(node, "healthPointsPerConversion", CreatorToolsTapFarmingSettings.MinimumConversionValue, CreatorToolsTapFarmingSettings.MaximumConversionValue);
            }
            else if (suffix == ".pesky-battle.json") RequireRange(node, "capacity", 2, 5);
            else if (suffix == ".overlay-composer.json")
            {
                foreach (var profile in node.Property("profiles").ArrayValue)
                {
                    CreatorToolsOverlayComposerProfile validated;
                    if (!CreatorToolsOverlayComposerSettings.TryParseProfileJson(Serialize(profile), profile.String("id"), out validated)) throw new FormatException("Invalid overlay profile.");
                }
            }
        }

        private static void RequireRange(CreatorToolsJsonValue node, string key, int minimum, int maximum)
        {
            int value;
            if (!node.TryInteger(key, out value) || value < minimum || value > maximum) throw new FormatException("Invalid settings range.");
        }

        private static void CollectKeys(CreatorToolsJsonValue node, HashSet<string> keys)
        {
            if (node.ObjectValue != null) foreach (var pair in node.ObjectValue) { keys.Add(pair.Key); CollectKeys(pair.Value, keys); }
            if (node.ArrayValue != null) foreach (var entry in node.ArrayValue) CollectKeys(entry, keys);
        }

        private static void FilterKeys(CreatorToolsJsonValue node, HashSet<string> allowed, bool importing, bool rules)
        {
            if (node.ObjectValue != null)
            {
                foreach (var key in new List<string>(node.ObjectValue.Keys))
                {
                    // Rules target the platform, never a saved account/connection.
                    if (!importing && rules && key == "connectionId") { node.ObjectValue.Remove(key); continue; }
                    if (!allowed.Contains(key)) throw new FormatException("Nonportable settings field.");
                    FilterKeys(node.ObjectValue[key], allowed, importing, rules);
                }
            }
            if (node.ArrayValue != null) foreach (var entry in node.ArrayValue) FilterKeys(entry, allowed, importing, rules);
        }

        private static void ValidateShape(CreatorToolsJsonValue node, CreatorToolsJsonValue template)
        {
            if ((node.ObjectValue != null) != (template.ObjectValue != null) ||
                (node.ArrayValue != null) != (template.ArrayValue != null) ||
                (node.StringValue != null) != (template.StringValue != null) ||
                node.NumberValue.HasValue != template.NumberValue.HasValue ||
                node.BooleanValue.HasValue != template.BooleanValue.HasValue) throw new FormatException("Invalid settings type.");
            if (template.ObjectValue != null) foreach (var pair in template.ObjectValue)
            {
                var value = node.Property(pair.Key);
                if (value == null) throw new FormatException("Missing settings field.");
                ValidateShape(value, pair.Value);
            }
            if (template.ArrayValue != null)
            {
                if (template.ArrayValue.Count > 0 && template.ArrayValue[0].ObjectValue != null)
                {
                    if (node.ArrayValue.Count != template.ArrayValue.Count) throw new FormatException("Incomplete settings list.");
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var value in node.ArrayValue)
                    {
                        var id = value.String("id");
                        if (!seen.Add(id)) throw new FormatException("Duplicate settings ID.");
                        CreatorToolsJsonValue match = null;
                        foreach (var candidate in template.ArrayValue) if (candidate.String("id") == id) match = candidate;
                        if (match == null) throw new FormatException("Unknown settings ID.");
                        ValidateShape(value, match);
                    }
                }
                else foreach (var value in node.ArrayValue)
                    if (template.ArrayValue.Count > 0 ? value.StringValue == null : value.ObjectValue == null && value.StringValue == null) throw new FormatException("Invalid settings list.");
            }
        }

        private static void ValidateRules(CreatorToolsJsonValue node)
        {
            var rules = node.Property("rules").ArrayValue;
            if (rules.Count > 100) throw new FormatException("Too many rules.");
            var ids = new HashSet<decimal>();
            var nextId = node.Property("nextId").NumberValue;
            if (!nextId.HasValue || nextId.Value < 1 || nextId.Value > long.MaxValue || nextId.Value != decimal.Truncate(nextId.Value)) throw new FormatException("Invalid next rule ID.");
            foreach (var rule in rules)
            {
                var id = rule.Property("id");
                if (rule.ObjectValue == null || id == null || !id.NumberValue.HasValue || id.NumberValue.Value < 1 || id.NumberValue.Value > long.MaxValue || id.NumberValue.Value != decimal.Truncate(id.NumberValue.Value) || !ids.Add(id.NumberValue.Value) ||
                    (rule.String("platform") != "tiktok" && rule.String("platform") != "twitch") ||
                    Array.IndexOf(CreatorToolsInteractionIds.All, rule.String("interaction")) < 0) throw new FormatException("Invalid stream rule.");
                foreach (var key in new[] { "name", "platform", "eventType", "giftId", "giftName", "rewardName", "interaction" })
                    if (rule.Property(key) == null || rule.Property(key).StringValue == null) throw new FormatException("Invalid rule text.");
                bool enabled;
                if (!rule.TryBoolean("enabled", out enabled) || rule.String("name").Length > 64 || rule.String("rewardName").Length > 64 || !Regex.IsMatch(rule.String("giftId"), @"^\d*$", RegexOptions.CultureInvariant)) throw new FormatException("Invalid rule settings.");
                var eventType = rule.String("eventType");
                var allowed = rule.String("platform") == "tiktok" ? new[] { "gift", "like", "follow" } : new[] { "follow", "currency", "subscription", "subscription_gift", "resubscription", "redemption" };
                if (Array.IndexOf(allowed, eventType) < 0 || (eventType == "redemption" && rule.String("rewardName").Trim().Length == 0) || (eventType == "follow" && rule.Integer("every", 0) != 1)) throw new FormatException("Invalid rule trigger.");
                RequireRange(rule, "every", 1, 1000000);
                RequireRange(rule, "quantity", 1, 50);
                RequireRange(rule, "userCooldownSeconds", 0, 3600);
                RequireRange(rule, "globalCooldownSeconds", 0, 3600);
                RequireRange(rule, "durationSeconds", 1, 120);
                RequireRange(rule, "countdownSeconds", 0, 30);
            }
        }

        private static string SerializeRuleSettings(CreatorToolsJsonValue node)
        {
            // The legacy settings reader uses a fixed field order. Canonicalize
            // independently of the uploaded JSON's ordering, without account IDs.
            var builder = new StringBuilder("{\"version\":6,\"nextId\":");
            AppendValue(builder, node.Property("nextId")); builder.Append(",\"rules\":[");
            var rules = node.Property("rules").ArrayValue;
            var fields = new[] { "id", "name", "enabled", "platform", "eventType", "giftId", "giftName", "rewardName", "every", "interaction", "quantity", "userCooldownSeconds", "globalCooldownSeconds", "durationSeconds", "countdownSeconds" };
            for (var index = 0; index < rules.Count; index++)
            {
                if (index > 0) builder.Append(','); builder.Append('{');
                for (var field = 0; field < fields.Length; field++)
                { if (field > 0) builder.Append(','); AppendString(builder, fields[field]); builder.Append(':'); AppendValue(builder, rules[index].Property(fields[field])); }
                builder.Append('}');
            }
            return builder.Append("]}").ToString();
        }

        private static string PortableConfig(string text, bool importing)
        {
            var section = string.Empty;
            var builder = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim().TrimStart('\ufeff');
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2);
                    if (importing && section != "Controles" && section != "Juego" && section != "Creator Tools" && section != "El chat elige") throw new FormatException("Nonportable config section.");
                    continue;
                }
                var separator = line.IndexOf('=');
                if (separator < 1) { if (importing) throw new FormatException("Invalid config entry."); continue; }
                var key = line.Substring(0, separator).Trim();
                var identity = section + "/" + key;
                if (!IsPortableEntry(identity)) { if (importing) throw new FormatException("Nonportable config entry."); continue; }
                if (!seen.Add(identity)) throw new FormatException("Duplicate config entry.");
                var value = line.Substring(separator + 1).Trim();
                ValidateConfigValue(identity, value);
                builder.Append('[').Append(section).Append("]\n").Append(key).Append(" = ").Append(value).Append('\n');
            }
            return builder.ToString();
        }

        private static bool IsPortableEntry(string entry)
        {
            foreach (var allowed in new[] {
                "Controles/AbrirCerrar", "Controles/Girar", "Juego/CargarAutomaticamente", "Juego/Dificultad", "Juego/Reto", "Juego/RetosDesactivados", "Juego/DemoraAntesDeCargar",
                "Juego/RetoEquipadoPartida1", "Juego/RetoEquipadoPartida2", "Juego/RetoEquipadoPartida3", "El chat elige/Con reto",
                "Creator Tools/Activado", "Creator Tools/Tamano", "Creator Tools/Orden", "Creator Tools/Alineacion", "Creator Tools/Opacidad", "Creator Tools/VistaPrevia", "Creator Tools/MostrarNombre", "Creator Tools/AlReintentar",
                "Creator Tools/InteraccionesMaximasEnPantalla", "Creator Tools/InteraccionesSinLimiteEnPantalla", "Creator Tools/MiniJefesMaximosEnPantalla", "Creator Tools/InteraccionesActivadas"
            }) if (entry == allowed) return true;
            return false;
        }

        private static void ValidateConfigValue(string entry, string value)
        {
            var key = entry.Substring(entry.IndexOf('/') + 1);
            if (key == "Dificultad")
            { if (value != "Easy" && value != "Normal" && value != "Hard") throw new FormatException("Invalid difficulty."); }
            else if (key == "Orden")
            { if (value != "IconsAbove" && value != "TextAbove") throw new FormatException("Invalid order."); }
            else if (key == "Alineacion")
            { if (value != "Left" && value != "Center" && value != "Right") throw new FormatException("Invalid alignment."); }
            else if (key == "AlReintentar")
            { if (value != "Keep" && value != "Reappear") throw new FormatException("Invalid retry setting."); }
            else if (key == "RetosDesactivados" || key.StartsWith("RetoEquipadoPartida", StringComparison.Ordinal))
            { if (value.Length > 8192 || !Regex.IsMatch(value, @"^[A-Za-z0-9_,\s]*$", RegexOptions.CultureInvariant)) throw new FormatException("Invalid challenges."); }
            else if (key == "AbrirCerrar" || key == "Girar")
            { if (value.Length > 256 || !Regex.IsMatch(value, @"^[A-Za-z0-9_ +]+$", RegexOptions.CultureInvariant)) throw new FormatException("Invalid shortcut."); }
            else if (key == "Tamano" || key == "DemoraAntesDeCargar")
            {
                float number;
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || float.IsNaN(number) || float.IsInfinity(number) || number < 0) throw new FormatException("Invalid numeric setting.");
            }
            else if (key == "Opacidad" || key == "InteraccionesMaximasEnPantalla" || key == "MiniJefesMaximosEnPantalla")
            {
                int number;
                var maximum = key == "Opacidad" ? 100 : key == "MiniJefesMaximosEnPantalla" ? 1 : 30;
                var minimum = key == "InteraccionesMaximasEnPantalla" ? 1 : 0;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < minimum || number > maximum) throw new FormatException("Invalid limit.");
            }
            else { bool enabled; if (!bool.TryParse(value, out enabled)) throw new FormatException("Invalid boolean setting."); }
        }

        private static string BuildBundle(Dictionary<string, string> files, string locale)
        {
            var builder = new StringBuilder("{\"format\":\"" + Format + "\",\"schemaVersion\":1,\"panelLocale\":\"" + locale + "\",\"files\":{");
            var first = true;
            foreach (var suffix in Suffixes)
            {
                if (!first) builder.Append(','); first = false;
                AppendString(builder, Prefix + suffix); builder.Append(':'); AppendString(builder, files[Prefix + suffix]);
            }
            var result = builder.Append("}}").ToString();
            if (Encoding.UTF8.GetByteCount(result) > MaximumBundleBytes) throw new FormatException("Configuration too large.");
            return result;
        }

        private static string Serialize(CreatorToolsJsonValue node)
        {
            var builder = new StringBuilder(); AppendValue(builder, node); return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, CreatorToolsJsonValue node)
        {
            if (node.ObjectValue != null)
            {
                builder.Append('{'); var first = true;
                foreach (var pair in node.ObjectValue) { if (!first) builder.Append(','); first = false; AppendString(builder, pair.Key); builder.Append(':'); AppendValue(builder, pair.Value); }
                builder.Append('}');
            }
            else if (node.ArrayValue != null)
            {
                builder.Append('['); for (var i = 0; i < node.ArrayValue.Count; i++) { if (i > 0) builder.Append(','); AppendValue(builder, node.ArrayValue[i]); } builder.Append(']');
            }
            else if (node.StringValue != null) AppendString(builder, node.StringValue);
            else if (node.NumberValue.HasValue) builder.Append(node.NumberValue.Value.ToString(CultureInfo.InvariantCulture));
            else if (node.BooleanValue.HasValue) builder.Append(node.BooleanValue.Value ? "true" : "false");
            else builder.Append("null");
        }

        private static void AppendString(StringBuilder builder, string text)
        { builder.Append('"'); CreatorToolsJson.AppendEscaped(builder, text); builder.Append('"'); }
        private string PathFor(string suffix) { return suffix == ".cfg" ? configPath : Path.Combine(directory, Prefix + suffix); }
        private static string ReadBounded(string path, int limit = MaximumFileBytes)
        {
            if (new FileInfo(path).Length > limit) throw new FormatException("Configuration too large.");
            return File.ReadAllText(path, Encoding.UTF8);
        }
        private static void AtomicWrite(string path, byte[] bytes)
        {
            var temporary = path + ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, bytes); if (File.Exists(path)) File.Replace(temporary, path, null, true); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void Warn(string message) { if (warning != null) warning(message); }
    }
}
