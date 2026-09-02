using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsOverlayComposerComponent
    {
        internal string Id;
        internal int X;
        internal int Y;
        internal int Width;
        internal int Height;
        internal bool Enabled;
        internal bool Locked;
        internal int Layer;
        internal string Variant;
        internal bool ShowTitle;
        internal bool ShowDetails;
        internal bool Motion;
        internal string LiquidColor;
        internal string CollectingColor;
        internal string TextColor;
        internal string OutlineColor;

        internal CreatorToolsOverlayComposerComponent Clone()
        {
            return (CreatorToolsOverlayComposerComponent)MemberwiseClone();
        }
    }

    internal sealed class CreatorToolsOverlayComposerProfile
    {
        internal string Id;
        internal int CanvasWidth;
        internal int CanvasHeight;
        internal readonly List<CreatorToolsOverlayComposerComponent>
            Components =
                new List<CreatorToolsOverlayComposerComponent>();

        internal CreatorToolsOverlayComposerProfile Clone()
        {
            var clone = new CreatorToolsOverlayComposerProfile
            {
                Id = Id,
                CanvasWidth = CanvasWidth,
                CanvasHeight = CanvasHeight
            };
            for (var i = 0; i < Components.Count; i++)
                clone.Components.Add(Components[i].Clone());
            return clone;
        }

        internal CreatorToolsOverlayComposerComponent FindComponent(
            string componentId)
        {
            for (var i = 0; i < Components.Count; i++)
                if (string.Equals(Components[i].Id, componentId,
                        StringComparison.OrdinalIgnoreCase))
                    return Components[i];
            return null;
        }
    }

    internal sealed class CreatorToolsOverlayComposerSettings
    {
        internal const int SchemaVersion = 1;
        internal const string VerticalProfileId = "vertical";
        internal const string HorizontalProfileId = "horizontal";
        internal const string TapFarmingComponentId = "tap_farming";
        internal const string PeskyBattleComponentId = "pesky_battle";
        internal const string DefaultLiquidColor = "#ff4f92";
        internal const string DefaultCollectingColor = "#f4c95d";
        internal const string DefaultTextColor = "#ffffff";
        internal const string DefaultOutlineColor = "#f5f5f7";

        private const int MaximumLayer = 100;
        private readonly string path;
        private readonly Action<string> logWarning;

        internal int Revision;
        internal readonly List<CreatorToolsOverlayComposerProfile> Profiles =
            new List<CreatorToolsOverlayComposerProfile>();

        private CreatorToolsOverlayComposerSettings(
            string path, Action<string> logWarning)
        {
            this.path = path;
            this.logWarning = logWarning;
        }

        internal static CreatorToolsOverlayComposerSettings Load(
            string pluginConfigPath, Action<string> logWarning)
        {
            var directory = Path.GetDirectoryName(
                string.IsNullOrEmpty(pluginConfigPath)
                    ? string.Empty
                    : Path.GetFullPath(pluginConfigPath));
            if (string.IsNullOrEmpty(directory))
                directory = Environment.CurrentDirectory;
            var path = Path.Combine(directory,
                "mx.gilomx.cuphead.bossroulette.overlay-composer.json");
            var defaults = CreateDefaults(path, logWarning);

            CreatorToolsOverlayComposerSettings loaded;
            if (TryLoadFile(path, logWarning, out loaded))
                return loaded;
            if (TryLoadFile(path + ".bak", logWarning, out loaded))
            {
                Warn(logWarning,
                    "La configuracion del compositor se recupero desde " +
                    "el respaldo.");
                loaded.TryRestorePrimaryFromBackup(path + ".bak");
                return loaded;
            }
            if (File.Exists(path) || File.Exists(path + ".bak"))
                Warn(logWarning,
                    "La configuracion del compositor no era valida; se " +
                    "usaran perfiles seguros.");
            return defaults;
        }

        internal static CreatorToolsOverlayComposerSettings CreateDefaults(
            string path, Action<string> logWarning)
        {
            var settings = new CreatorToolsOverlayComposerSettings(
                path, logWarning);
            settings.Profiles.Add(CreateDefaultProfile(VerticalProfileId));
            settings.Profiles.Add(CreateDefaultProfile(HorizontalProfileId));
            return settings;
        }

        internal CreatorToolsOverlayComposerSettings Clone()
        {
            var clone = new CreatorToolsOverlayComposerSettings(
                path, logWarning) { Revision = Revision };
            for (var i = 0; i < Profiles.Count; i++)
                clone.Profiles.Add(Profiles[i].Clone());
            return clone;
        }

        internal CreatorToolsOverlayComposerProfile FindProfile(string id)
        {
            id = NormalizeProfileId(id);
            for (var i = 0; i < Profiles.Count; i++)
                if (Profiles[i].Id == id)
                    return Profiles[i];
            return null;
        }

        internal bool ResetProfile(string profileId, string componentId)
        {
            var current = FindProfile(profileId);
            if (current == null)
                return false;
            var defaults = CreateDefaultProfile(current.Id);
            if (string.IsNullOrEmpty(componentId))
            {
                current.Components.Clear();
                for (var i = 0; i < defaults.Components.Count; i++)
                    current.Components.Add(defaults.Components[i].Clone());
                return true;
            }
            var target = current.FindComponent(componentId);
            var source = defaults.FindComponent(componentId);
            if (target == null || source == null)
                return false;
            CopyComponentValues(source, target, 1d, 1d);
            return true;
        }

        internal bool CopyProfile(
            string sourceProfileId,
            string destinationProfileId,
            string componentId)
        {
            var source = FindProfile(sourceProfileId);
            var destination = FindProfile(destinationProfileId);
            if (source == null || destination == null ||
                ReferenceEquals(source, destination))
                return false;
            var scaleX = destination.CanvasWidth /
                (double)Math.Max(1, source.CanvasWidth);
            var scaleY = destination.CanvasHeight /
                (double)Math.Max(1, source.CanvasHeight);
            if (!string.IsNullOrEmpty(componentId))
            {
                var from = source.FindComponent(componentId);
                var to = destination.FindComponent(componentId);
                if (from == null || to == null)
                    return false;
                CopyComponentValues(from, to, scaleX, scaleY);
                NormalizeComponent(destination, to);
                return true;
            }
            for (var i = 0; i < destination.Components.Count; i++)
            {
                var to = destination.Components[i];
                var from = source.FindComponent(to.Id);
                if (from == null)
                    return false;
                CopyComponentValues(from, to, scaleX, scaleY);
                NormalizeComponent(destination, to);
            }
            return true;
        }

        internal void Normalize()
        {
            for (var i = 0; i < Profiles.Count; i++)
                for (var j = 0; j < Profiles[i].Components.Count; j++)
                    NormalizeComponent(
                        Profiles[i], Profiles[i].Components[j]);
        }

        internal bool TrySave()
        {
            var temporaryPath = path + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(temporaryPath, BuildFileJson(),
                    new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path,
                        path + ".bak", true);
                    return true;
                }
                File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "No se pudo guardar el compositor: " +
                    exception.Message);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }

        private void TryRestorePrimaryFromBackup(string backupPath)
        {
            var temporaryPath = path + ".restore.tmp";
            try
            {
                File.Copy(backupPath, temporaryPath, true);
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null, true);
                else
                    File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "No se pudo restaurar el archivo principal del " +
                    "compositor; el respaldo sigue disponible: " +
                    exception.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }

        internal string BuildStateJson(string feedback, bool error)
        {
            var builder = new StringBuilder(2048);
            builder.Append("{\"ready\":true,\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"revision\":").Append(Revision)
                .Append(",\"profiles\":");
            AppendProfilesJson(builder);
            builder.Append(",\"feedback\":\"");
            CreatorToolsJson.AppendEscaped(
                builder, feedback ?? string.Empty);
            builder.Append("\",\"error\":")
                .Append(error ? "true" : "false").Append('}');
            return builder.ToString();
        }

        internal static string BuildProfileJson(
            CreatorToolsOverlayComposerProfile profile)
        {
            if (profile == null)
                return "null";
            var builder = new StringBuilder(1024);
            AppendProfileJson(builder, profile);
            return builder.ToString();
        }

        internal static bool TryParseProfileJson(
            string json,
            string expectedProfileId,
            out CreatorToolsOverlayComposerProfile profile)
        {
            profile = null;
            expectedProfileId = NormalizeProfileId(expectedProfileId);
            if (expectedProfileId.Length == 0 || string.IsNullOrEmpty(json) ||
                json.Length > 8192)
                return false;

            JsonValue root;
            if (!JsonParser.TryParse(json, out root) ||
                root == null || root.ObjectValue == null ||
                NormalizeProfileId(root.String("id")) != expectedProfileId)
                return false;

            var candidate = CreateDefaultProfile(expectedProfileId);
            var canvas = root.Property("canvas");
            var components = root.Property("components");
            if (canvas == null || canvas.ObjectValue == null ||
                components == null || components.ArrayValue == null ||
                canvas.Integer("width", -1) != candidate.CanvasWidth ||
                canvas.Integer("height", -1) != candidate.CanvasHeight ||
                components.ArrayValue.Count != candidate.Components.Count)
                return false;

            var seenComponents = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (var index = 0;
                 index < components.ArrayValue.Count;
                 index++)
            {
                var componentNode = components.ArrayValue[index];
                var componentId = NormalizeComponentId(
                    componentNode.String("id"));
                var component = candidate.FindComponent(componentId);
                if (component == null ||
                    !seenComponents.Add(componentId) ||
                    !TryLoadComponent(componentNode, component))
                    return false;
                NormalizeComponent(candidate, component);
            }
            if (seenComponents.Count != candidate.Components.Count)
                return false;

            profile = candidate;
            return true;
        }

        internal static string NormalizeProfileId(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value == VerticalProfileId ||
                value == HorizontalProfileId ? value : string.Empty;
        }

        internal static string NormalizeComponentId(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value == TapFarmingComponentId ||
                value == PeskyBattleComponentId ? value : string.Empty;
        }

        internal static string NormalizeVariant(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value == "default" || value == "compact" ||
                value == "minimal" ? "default" : string.Empty;
        }

        internal static string NormalizeColor(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length != 7 || value[0] != '#')
                return string.Empty;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f') ||
                      (character >= 'A' && character <= 'F')))
                    return string.Empty;
            }
            return value.ToLowerInvariant();
        }

        internal static void NormalizeComponent(
            CreatorToolsOverlayComposerProfile profile,
            CreatorToolsOverlayComposerComponent component)
        {
            var minimumWidth = component.Id == PeskyBattleComponentId
                ? 320 : 220;
            var minimumHeight = component.Id == PeskyBattleComponentId
                ? 180 : 220;
            component.Width = Math.Max(minimumWidth,
                Math.Min(profile.CanvasWidth, component.Width));
            component.Height = Math.Max(minimumHeight,
                Math.Min(profile.CanvasHeight, component.Height));
            component.X = Math.Max(0, Math.Min(
                profile.CanvasWidth - component.Width, component.X));
            component.Y = Math.Max(0, Math.Min(
                profile.CanvasHeight - component.Height, component.Y));
            component.Layer = Math.Max(0,
                Math.Min(MaximumLayer, component.Layer));
            component.Variant = "default";
            var liquidColor = NormalizeColor(component.LiquidColor);
            component.LiquidColor = liquidColor.Length == 0
                ? DefaultLiquidColor : liquidColor;
            var collectingColor = NormalizeColor(component.CollectingColor);
            component.CollectingColor = collectingColor.Length == 0
                ? DefaultCollectingColor : collectingColor;
            var textColor = NormalizeColor(component.TextColor);
            component.TextColor = textColor.Length == 0
                ? DefaultTextColor : textColor;
            var outlineColor = NormalizeColor(component.OutlineColor);
            component.OutlineColor = outlineColor.Length == 0
                ? DefaultOutlineColor : outlineColor;
        }

        private string BuildFileJson()
        {
            var builder = new StringBuilder(2048);
            builder.Append("{\n  \"schemaVersion\": ")
                .Append(SchemaVersion)
                .Append(",\n  \"revision\": ").Append(Revision)
                .Append(",\n  \"profiles\": ");
            AppendProfilesJson(builder);
            builder.Append("\n}\n");
            return builder.ToString();
        }

        private void AppendProfilesJson(StringBuilder builder)
        {
            builder.Append('[');
            for (var i = 0; i < Profiles.Count; i++)
            {
                if (i > 0) builder.Append(',');
                AppendProfileJson(builder, Profiles[i]);
            }
            builder.Append(']');
        }

        private static void AppendProfileJson(
            StringBuilder builder,
            CreatorToolsOverlayComposerProfile profile)
        {
            builder.Append("{\"id\":\"");
            CreatorToolsJson.AppendEscaped(builder, profile.Id);
            builder.Append("\",\"canvas\":{\"width\":")
                .Append(profile.CanvasWidth)
                .Append(",\"height\":")
                .Append(profile.CanvasHeight)
                .Append("},\"components\":[");
            for (var index = 0; index < profile.Components.Count; index++)
            {
                if (index > 0) builder.Append(',');
                var component = profile.Components[index];
                builder.Append("{\"id\":\"");
                CreatorToolsJson.AppendEscaped(builder, component.Id);
                builder.Append("\",\"x\":").Append(component.X)
                    .Append(",\"y\":").Append(component.Y)
                    .Append(",\"width\":").Append(component.Width)
                    .Append(",\"height\":").Append(component.Height)
                    .Append(",\"enabled\":")
                    .Append(component.Enabled ? "true" : "false")
                    .Append(",\"locked\":")
                    .Append(component.Locked ? "true" : "false")
                    .Append(",\"layer\":").Append(component.Layer)
                    .Append(",\"variant\":\"");
                CreatorToolsJson.AppendEscaped(builder, component.Variant);
                builder.Append("\",\"showTitle\":")
                    .Append(component.ShowTitle ? "true" : "false")
                    .Append(",\"showDetails\":")
                    .Append(component.ShowDetails ? "true" : "false")
                    .Append(",\"motion\":")
                    .Append(component.Motion ? "true" : "false")
                    .Append(",\"liquidColor\":\"");
                CreatorToolsJson.AppendEscaped(
                    builder, component.LiquidColor);
                builder.Append("\",\"collectingColor\":\"");
                CreatorToolsJson.AppendEscaped(
                    builder, component.CollectingColor);
                builder.Append("\",\"textColor\":\"");
                CreatorToolsJson.AppendEscaped(
                    builder, component.TextColor);
                builder.Append("\",\"outlineColor\":\"");
                CreatorToolsJson.AppendEscaped(
                    builder, component.OutlineColor);
                builder.Append("\"}");
            }
            builder.Append("]}");
        }

        private static CreatorToolsOverlayComposerProfile
            CreateDefaultProfile(string id)
        {
            var vertical = id == VerticalProfileId;
            var profile = new CreatorToolsOverlayComposerProfile
            {
                Id = id,
                CanvasWidth = vertical ? 1080 : 1920,
                CanvasHeight = vertical ? 1920 : 1080
            };
            profile.Components.Add(
                new CreatorToolsOverlayComposerComponent
                {
                    Id = TapFarmingComponentId,
                    X = vertical ? 220 : 1290,
                    Y = vertical ? 1010 : 430,
                    Width = vertical ? 640 : 570,
                    Height = vertical ? 720 : 570,
                    Enabled = true,
                    Locked = false,
                    Layer = 20,
                    Variant = "default",
                    ShowTitle = false,
                    ShowDetails = false,
                    Motion = true,
                    LiquidColor = DefaultLiquidColor,
                    CollectingColor = DefaultCollectingColor,
                    TextColor = DefaultTextColor,
                    OutlineColor = DefaultOutlineColor
                });
            profile.Components.Add(
                new CreatorToolsOverlayComposerComponent
                {
                    Id = PeskyBattleComponentId,
                    X = vertical ? 60 : 80,
                    Y = vertical ? 1260 : 720,
                    Width = vertical ? 960 : 1760,
                    Height = vertical ? 560 : 300,
                    Enabled = true,
                    Locked = false,
                    Layer = 10,
                    Variant = "default",
                    ShowTitle = true,
                    ShowDetails = true,
                    Motion = true,
                    LiquidColor = DefaultLiquidColor,
                    CollectingColor = DefaultCollectingColor,
                    TextColor = DefaultTextColor,
                    OutlineColor = DefaultOutlineColor
                });
            return profile;
        }

        private static void CopyComponentValues(
            CreatorToolsOverlayComposerComponent source,
            CreatorToolsOverlayComposerComponent destination,
            double scaleX,
            double scaleY)
        {
            destination.X = (int)Math.Round(source.X * scaleX);
            destination.Y = (int)Math.Round(source.Y * scaleY);
            destination.Width = (int)Math.Round(source.Width * scaleX);
            destination.Height = (int)Math.Round(source.Height * scaleY);
            destination.Enabled = source.Enabled;
            destination.Locked = source.Locked;
            destination.Layer = source.Layer;
            destination.Variant = source.Variant;
            destination.ShowTitle = source.ShowTitle;
            destination.ShowDetails = source.ShowDetails;
            destination.Motion = source.Motion;
            destination.LiquidColor = source.LiquidColor;
            destination.TextColor = source.TextColor;
            destination.OutlineColor = source.OutlineColor;
        }

        private static bool TryLoadFile(
            string candidatePath,
            Action<string> logWarning,
            out CreatorToolsOverlayComposerSettings loaded)
        {
            loaded = null;
            if (!File.Exists(candidatePath))
                return false;
            try
            {
                var json = File.ReadAllText(candidatePath, Encoding.UTF8);
                JsonValue root;
                if (!JsonParser.TryParse(json, out root) ||
                    root.ObjectValue == null)
                    return false;
                var version = root.Integer("schemaVersion",
                    root.Integer("version", -1));
                var revision = root.Integer("revision", -1);
                var profiles = root.Property("profiles");
                if (version != SchemaVersion || revision < 0 ||
                    profiles == null || profiles.ArrayValue == null)
                    return false;

                var path = candidatePath.EndsWith(".bak",
                    StringComparison.OrdinalIgnoreCase)
                    ? candidatePath.Substring(0,
                        candidatePath.Length - ".bak".Length)
                    : candidatePath;
                var candidate = CreateDefaults(path, logWarning);
                candidate.Revision = revision;
                var seenProfiles = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < profiles.ArrayValue.Count; i++)
                {
                    var profileNode = profiles.ArrayValue[i];
                    var id = NormalizeProfileId(profileNode.String("id"));
                    if (id.Length == 0 || !seenProfiles.Add(id))
                        return false;
                    var target = candidate.FindProfile(id);
                    var canvas = profileNode.Property("canvas");
                    var components = profileNode.Property("components");
                    if (target == null || canvas == null ||
                        canvas.ObjectValue == null || components == null ||
                        components.ArrayValue == null ||
                        canvas.Integer("width", -1) != target.CanvasWidth ||
                        canvas.Integer("height", -1) != target.CanvasHeight)
                        return false;
                    var seenComponents = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                    for (var j = 0; j < components.ArrayValue.Count; j++)
                    {
                        var componentNode = components.ArrayValue[j];
                        var componentId = NormalizeComponentId(
                            componentNode.String("id"));
                        var component = target.FindComponent(componentId);
                        if (component == null ||
                            !seenComponents.Add(componentId) ||
                            !TryLoadComponent(componentNode, component))
                            return false;
                        NormalizeComponent(target, component);
                    }
                    if (seenComponents.Count != target.Components.Count)
                        return false;
                }
                if (seenProfiles.Count != candidate.Profiles.Count)
                    return false;
                loaded = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLoadComponent(
            JsonValue node,
            CreatorToolsOverlayComposerComponent component)
        {
            int x, y, width, height, layer;
            bool enabled, locked, showTitle, showDetails, motion;
            var variant = NormalizeVariant(node.String("variant"));
            if (node.ObjectValue == null ||
                !node.TryInteger("x", out x) ||
                !node.TryInteger("y", out y) ||
                !node.TryInteger("width", out width) ||
                !node.TryInteger("height", out height) ||
                !node.TryBoolean("enabled", out enabled) ||
                !node.TryBoolean("locked", out locked) ||
                !node.TryInteger("layer", out layer) ||
                variant.Length == 0 ||
                !node.TryBoolean("showTitle", out showTitle) ||
                !node.TryBoolean("showDetails", out showDetails) ||
                !node.TryBoolean("motion", out motion))
                return false;
            component.X = x;
            component.Y = y;
            component.Width = width;
            component.Height = height;
            component.Enabled = enabled;
            component.Locked = locked;
            component.Layer = layer;
            component.Variant = variant;
            component.ShowTitle = showTitle;
            component.ShowDetails = showDetails;
            component.Motion = motion;
            var colorNode = node.Property("liquidColor");
            var legacyTapPresentation =
                component.Id == TapFarmingComponentId &&
                colorNode == null &&
                node.Property("textColor") == null &&
                node.Property("outlineColor") == null &&
                component.Variant == "default" &&
                component.ShowTitle && component.ShowDetails;
            if (colorNode != null)
            {
                var color = NormalizeColor(node.String("liquidColor"));
                if (color.Length == 0) return false;
                component.LiquidColor = color;
            }
            colorNode = node.Property("collectingColor");
            if (colorNode != null)
            {
                var color = NormalizeColor(node.String("collectingColor"));
                if (color.Length == 0) return false;
                component.CollectingColor = color;
            }
            colorNode = node.Property("textColor");
            if (colorNode != null)
            {
                var color = NormalizeColor(node.String("textColor"));
                if (color.Length == 0) return false;
                component.TextColor = color;
            }
            colorNode = node.Property("outlineColor");
            if (colorNode != null)
            {
                var color = NormalizeColor(node.String("outlineColor"));
                if (color.Length == 0) return false;
                component.OutlineColor = color;
            }
            if (legacyTapPresentation)
            {
                component.Variant = "default";
                component.ShowTitle = false;
                component.ShowDetails = false;
            }
            return true;
        }

        private static void Warn(Action<string> warning, string message)
        {
            if (warning != null)
                warning(message);
        }

        private sealed class JsonValue
        {
            internal Dictionary<string, JsonValue> ObjectValue;
            internal List<JsonValue> ArrayValue;
            internal string StringValue;
            internal decimal? NumberValue;
            internal bool? BooleanValue;

            internal JsonValue Property(string name)
            {
                JsonValue value;
                return ObjectValue != null &&
                    ObjectValue.TryGetValue(name, out value) ? value : null;
            }

            internal string String(string name)
            {
                var value = Property(name);
                return value == null ? string.Empty :
                    value.StringValue ?? string.Empty;
            }

            internal int Integer(string name, int fallback)
            {
                int value;
                return TryInteger(name, out value) ? value : fallback;
            }

            internal bool TryInteger(string name, out int result)
            {
                result = 0;
                var value = Property(name);
                if (value == null || !value.NumberValue.HasValue ||
                    value.NumberValue.Value !=
                        decimal.Truncate(value.NumberValue.Value) ||
                    value.NumberValue.Value < int.MinValue ||
                    value.NumberValue.Value > int.MaxValue)
                    return false;
                result = decimal.ToInt32(value.NumberValue.Value);
                return true;
            }

            internal bool TryBoolean(string name, out bool result)
            {
                result = false;
                var value = Property(name);
                if (value == null || !value.BooleanValue.HasValue)
                    return false;
                result = value.BooleanValue.Value;
                return true;
            }
        }

        private sealed class JsonParser
        {
            private readonly string json;
            private int position;

            private JsonParser(string json)
            {
                this.json = json;
            }

            internal static bool TryParse(string json, out JsonValue value)
            {
                value = null;
                if (string.IsNullOrEmpty(json) || json.Length > 65536)
                    return false;
                try
                {
                    var parser = new JsonParser(json);
                    value = parser.ReadValue(0);
                    parser.SkipWhitespace();
                    return value != null && parser.position == json.Length;
                }
                catch
                {
                    value = null;
                    return false;
                }
            }

            private JsonValue ReadValue(int depth)
            {
                if (depth > 16)
                    throw new FormatException();
                SkipWhitespace();
                if (position >= json.Length)
                    throw new FormatException();
                if (json[position] == '{') return ReadObject(depth + 1);
                if (json[position] == '[') return ReadArray(depth + 1);
                if (json[position] == '"')
                    return new JsonValue { StringValue = ReadString() };
                if (Match("true"))
                    return new JsonValue { BooleanValue = true };
                if (Match("false"))
                    return new JsonValue { BooleanValue = false };
                if (Match("null")) return new JsonValue();
                return ReadNumber();
            }

            private JsonValue ReadObject(int depth)
            {
                position++;
                var values = new Dictionary<string, JsonValue>(
                    StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}'))
                    return new JsonValue { ObjectValue = values };
                while (values.Count < 128)
                {
                    SkipWhitespace();
                    var key = ReadString();
                    SkipWhitespace();
                    if (!Consume(':') || values.ContainsKey(key))
                        throw new FormatException();
                    values[key] = ReadValue(depth);
                    SkipWhitespace();
                    if (Consume('}'))
                        return new JsonValue { ObjectValue = values };
                    if (!Consume(',')) throw new FormatException();
                }
                throw new FormatException();
            }

            private JsonValue ReadArray(int depth)
            {
                position++;
                var values = new List<JsonValue>();
                SkipWhitespace();
                if (Consume(']'))
                    return new JsonValue { ArrayValue = values };
                while (values.Count < 128)
                {
                    values.Add(ReadValue(depth));
                    SkipWhitespace();
                    if (Consume(']'))
                        return new JsonValue { ArrayValue = values };
                    if (!Consume(',')) throw new FormatException();
                }
                throw new FormatException();
            }

            private JsonValue ReadNumber()
            {
                var start = position;
                while (position < json.Length &&
                    "-+0123456789.eE".IndexOf(json[position]) >= 0)
                    position++;
                decimal number;
                if (position == start || !decimal.TryParse(
                        json.Substring(start, position - start),
                        NumberStyles.Float, CultureInfo.InvariantCulture,
                        out number))
                    throw new FormatException();
                return new JsonValue { NumberValue = number };
            }

            private string ReadString()
            {
                if (!Consume('"')) throw new FormatException();
                var builder = new StringBuilder();
                while (position < json.Length && builder.Length <= 8192)
                {
                    var character = json[position++];
                    if (character == '"') return builder.ToString();
                    if (character < 32) throw new FormatException();
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }
                    if (position >= json.Length) throw new FormatException();
                    character = json[position++];
                    if (character == '"' || character == '\\' ||
                        character == '/') builder.Append(character);
                    else if (character == 'b') builder.Append('\b');
                    else if (character == 'f') builder.Append('\f');
                    else if (character == 'n') builder.Append('\n');
                    else if (character == 'r') builder.Append('\r');
                    else if (character == 't') builder.Append('\t');
                    else if (character == 'u')
                    {
                        if (position + 4 > json.Length)
                            throw new FormatException();
                        int code;
                        if (!int.TryParse(json.Substring(position, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out code))
                            throw new FormatException();
                        builder.Append((char)code);
                        position += 4;
                    }
                    else throw new FormatException();
                }
                throw new FormatException();
            }

            private bool Match(string value)
            {
                if (position + value.Length > json.Length ||
                    string.CompareOrdinal(json, position, value, 0,
                        value.Length) != 0)
                    return false;
                position += value.Length;
                return true;
            }

            private bool Consume(char value)
            {
                if (position >= json.Length || json[position] != value)
                    return false;
                position++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (position < json.Length &&
                    char.IsWhiteSpace(json[position])) position++;
            }
        }
    }
}
