using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsOverlayComposerResponse
    {
        internal readonly int StatusCode;
        internal readonly string StatusText;
        internal readonly string Json;

        internal CreatorToolsOverlayComposerResponse(
            int statusCode, string statusText, string json)
        {
            StatusCode = statusCode;
            StatusText = statusText ?? string.Empty;
            Json = json ?? "{}";
        }
    }

    internal sealed class CreatorToolsOverlayComposerController
    {
        private const double MaximumSyntheticValue = 1000000000000d;
        private static readonly TimeSpan PreviewLifetime =
            TimeSpan.FromMinutes(2d);
        private const int MaximumPreviewCancellations = 128;
        private readonly object stateLock = new object();
        private readonly Func<DateTime> utcNow;
        private CreatorToolsOverlayComposerSettings settings;
        private readonly Dictionary<string, PreviewSlot> previews =
            new Dictionary<string, PreviewSlot>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> previewCancellations =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);

        internal CreatorToolsOverlayComposerController(
            string pluginConfigPath, Action<string> logWarning)
            : this(pluginConfigPath, logWarning,
                delegate { return DateTime.UtcNow; })
        {
        }

        internal CreatorToolsOverlayComposerController(
            string pluginConfigPath,
            Action<string> logWarning,
            Func<DateTime> utcNow)
        {
            settings = CreatorToolsOverlayComposerSettings.Load(
                pluginConfigPath, logWarning);
            this.utcNow = utcNow ?? delegate { return DateTime.UtcNow; };
            previews[CreatorToolsOverlayComposerSettings.VerticalProfileId] =
                new PreviewSlot(
                    CreatorToolsOverlayComposerSettings.VerticalProfileId);
            previews[CreatorToolsOverlayComposerSettings.HorizontalProfileId] =
                new PreviewSlot(
                    CreatorToolsOverlayComposerSettings.HorizontalProfileId);
        }

        internal string GetConfigState()
        {
            lock (stateLock)
                return settings.BuildStateJson("ready", false);
        }

        internal CreatorToolsOverlayComposerResponse ProcessConfigCommand(
            string body)
        {
            Dictionary<string, string> values;
            if (!CreatorToolsFlatJson.TryParse(body, out values))
                return ConfigFailure(400, "Bad Request", "invalid_json");
            if (!HasSupportedSchema(values))
                return ConfigFailure(400, "Bad Request",
                    "unsupported_schema");
            var operation = Value(values, "operation")
                .Trim().ToLowerInvariant();
            if (operation.Length == 0)
                operation = "update";
            int expectedRevision;
            if (!TryReadInt(values, "expectedRevision",
                    out expectedRevision) || expectedRevision < 0)
                return ConfigFailure(400, "Bad Request",
                    "invalid_expected_revision");
            var profileId =
                CreatorToolsOverlayComposerSettings.NormalizeProfileId(
                    Value(values, "profileId"));
            if (profileId.Length == 0)
                return ConfigFailure(400, "Bad Request",
                    "invalid_profile");

            lock (stateLock)
            {
                if (expectedRevision != settings.Revision)
                    return new CreatorToolsOverlayComposerResponse(
                        409, "Conflict",
                        settings.BuildStateJson(
                            "revision_conflict", true));

                var candidate = settings.Clone();
                var feedback = string.Empty;
                if (operation == "update")
                {
                    if (!ApplyComponentUpdate(candidate, values, profileId))
                        return new CreatorToolsOverlayComposerResponse(
                            400, "Bad Request",
                            settings.BuildStateJson(
                                "invalid_component_update", true));
                    feedback = "updated";
                }
                else if (operation == "reset")
                {
                    var componentId = OptionalComponentId(values);
                    if ((values.ContainsKey("componentId") &&
                        componentId.Length == 0) ||
                        !candidate.ResetProfile(profileId, componentId))
                        return new CreatorToolsOverlayComposerResponse(
                            400, "Bad Request",
                            settings.BuildStateJson(
                                "invalid_reset", true));
                    feedback = "reset";
                }
                else if (operation == "copy")
                {
                    var sourceProfileId =
                        CreatorToolsOverlayComposerSettings
                            .NormalizeProfileId(
                                Value(values, "sourceProfileId"));
                    var componentId = OptionalComponentId(values);
                    if (sourceProfileId.Length == 0 ||
                        (values.ContainsKey("componentId") &&
                        componentId.Length == 0) ||
                        !candidate.CopyProfile(
                            sourceProfileId, profileId, componentId))
                        return new CreatorToolsOverlayComposerResponse(
                            400, "Bad Request",
                            settings.BuildStateJson(
                                "invalid_copy", true));
                    feedback = "copied";
                }
                else
                    return new CreatorToolsOverlayComposerResponse(
                        400, "Bad Request",
                        settings.BuildStateJson("invalid_operation", true));

                candidate.Normalize();
                candidate.Revision = settings.Revision == int.MaxValue
                    ? int.MaxValue
                    : settings.Revision + 1;
                if (!candidate.TrySave())
                    return new CreatorToolsOverlayComposerResponse(
                        500, "Internal Server Error",
                        settings.BuildStateJson("save_failed", true));
                settings = candidate;
                return new CreatorToolsOverlayComposerResponse(
                    200, "OK", settings.BuildStateJson(feedback, false));
            }
        }

        internal CreatorToolsOverlayComposerResponse GetPreviewState(
            string profileId)
        {
            profileId =
                CreatorToolsOverlayComposerSettings.NormalizeProfileId(
                    profileId);
            if (profileId.Length == 0)
                return PreviewFailure(400, "Bad Request",
                    string.Empty, "invalid_profile");
            lock (stateLock)
            {
                var slot = previews[profileId];
                ExpirePreviewLocked(slot);
                return new CreatorToolsOverlayComposerResponse(
                    200, "OK", BuildPreviewJson(slot));
            }
        }

        internal CreatorToolsOverlayComposerResponse ProcessPreviewCommand(
            string body)
        {
            Dictionary<string, string> values;
            if (!CreatorToolsFlatJson.TryParse(body, out values))
                return PreviewFailure(400, "Bad Request",
                    string.Empty, "invalid_json");
            if (!HasSupportedSchema(values))
                return PreviewFailure(400, "Bad Request",
                    string.Empty, "unsupported_schema");
            var operation = Value(values, "operation")
                .Trim().ToLowerInvariant();
            var profileId =
                CreatorToolsOverlayComposerSettings.NormalizeProfileId(
                    Value(values, "profileId"));
            if (profileId.Length == 0)
                return PreviewFailure(400, "Bad Request",
                    string.Empty, "invalid_profile");
            if (operation != "start" && operation != "update" &&
                operation != "stop")
                return PreviewFailure(400, "Bad Request",
                    profileId, "invalid_operation");
            var sessionId = NormalizeIdentifier(
                Value(values, "sessionId"), 96);
            if (sessionId.Length == 0)
                return PreviewFailure(400, "Bad Request",
                    profileId, "invalid_preview_session");

            lock (stateLock)
            {
                var slot = previews[profileId];
                ExpirePreviewLocked(slot);
                var cancellationKey = PreviewCancellationKey(
                    profileId, sessionId);
                PrunePreviewCancellationsLocked();
                if (operation == "start" &&
                    previewCancellations.ContainsKey(cancellationKey))
                    return new CreatorToolsOverlayComposerResponse(
                        409, "Conflict", BuildPreviewJson(
                            slot, "preview_session_cancelled", true));
                if (operation != "start" && !string.Equals(
                    sessionId, slot.SessionId,
                    StringComparison.Ordinal))
                {
                    RecordPreviewCancellationLocked(cancellationKey);
                    return new CreatorToolsOverlayComposerResponse(
                        409, "Conflict", BuildPreviewJson(
                            slot, "preview_session_conflict", true));
                }
                int expectedRevision;
                if (values.ContainsKey("expectedRevision") &&
                    (!TryReadInt(values, "expectedRevision",
                        out expectedRevision) ||
                     expectedRevision != slot.Revision))
                    return new CreatorToolsOverlayComposerResponse(
                        409, "Conflict", BuildPreviewJson(
                            slot, "revision_conflict", true));

                if (operation == "stop")
                {
                    StopPreviewLocked(slot, "stopped");
                    RecordPreviewCancellationLocked(cancellationKey);
                    return new CreatorToolsOverlayComposerResponse(
                        200, "OK", BuildPreviewJson(slot));
                }
                if (operation == "update" && !slot.Active)
                    return new CreatorToolsOverlayComposerResponse(
                        409, "Conflict", BuildPreviewJson(
                            slot, "preview_not_active", true));

                var candidate = operation == "start"
                    ? CreatePreviewDefaults(profileId, values)
                    : slot.Clone();
                if (candidate == null ||
                    !ApplyPreviewValues(candidate, values))
                    return new CreatorToolsOverlayComposerResponse(
                        400, "Bad Request", BuildPreviewJson(
                            slot, "invalid_preview", true));
                candidate.Active = true;
                if (operation == "start")
                {
                    candidate.SessionId = sessionId;
                    candidate.RunId = slot.RunId == int.MaxValue
                        ? int.MaxValue : slot.RunId + 1;
                }
                candidate.Revision = slot.Revision == int.MaxValue
                    ? int.MaxValue : slot.Revision + 1;
                candidate.ExpiresAtUtc = utcNow().ToUniversalTime()
                    .Add(PreviewLifetime);
                candidate.Feedback = operation == "start"
                    ? "started" : "updated";
                previews[profileId] = candidate;
                return new CreatorToolsOverlayComposerResponse(
                    200, "OK", BuildPreviewJson(candidate));
            }
        }

        private CreatorToolsOverlayComposerResponse ConfigFailure(
            int code, string status, string feedback)
        {
            lock (stateLock)
                return new CreatorToolsOverlayComposerResponse(
                    code, status,
                    settings.BuildStateJson(feedback, true));
        }

        private CreatorToolsOverlayComposerResponse PreviewFailure(
            int code, string status, string profileId, string feedback)
        {
            lock (stateLock)
            {
                PreviewSlot slot;
                if (!previews.TryGetValue(profileId ?? string.Empty, out slot))
                    slot = new PreviewSlot(profileId ?? string.Empty);
                return new CreatorToolsOverlayComposerResponse(
                    code, status, BuildPreviewJson(slot, feedback, true));
            }
        }

        private static bool ApplyComponentUpdate(
            CreatorToolsOverlayComposerSettings candidate,
            Dictionary<string, string> values,
            string profileId)
        {
            var componentId =
                CreatorToolsOverlayComposerSettings.NormalizeComponentId(
                    Value(values, "componentId"));
            var profile = candidate.FindProfile(profileId);
            var component = profile == null ? null :
                profile.FindComponent(componentId);
            if (component == null)
                return false;
            var changed = false;
            int number;
            bool boolean;
            if (values.ContainsKey("x"))
            {
                if (!TryReadInt(values, "x", out number)) return false;
                component.X = number; changed = true;
            }
            if (values.ContainsKey("y"))
            {
                if (!TryReadInt(values, "y", out number)) return false;
                component.Y = number; changed = true;
            }
            if (values.ContainsKey("width"))
            {
                if (!TryReadInt(values, "width", out number)) return false;
                component.Width = number; changed = true;
            }
            if (values.ContainsKey("height"))
            {
                if (!TryReadInt(values, "height", out number)) return false;
                component.Height = number; changed = true;
            }
            if (values.ContainsKey("layer"))
            {
                if (!TryReadInt(values, "layer", out number)) return false;
                component.Layer = number; changed = true;
            }
            if (values.ContainsKey("enabled"))
            {
                if (!TryReadBoolean(values, "enabled", out boolean))
                    return false;
                component.Enabled = boolean; changed = true;
            }
            if (values.ContainsKey("locked"))
            {
                if (!TryReadBoolean(values, "locked", out boolean))
                    return false;
                component.Locked = boolean; changed = true;
            }
            if (values.ContainsKey("showTitle"))
            {
                if (!TryReadBoolean(values, "showTitle", out boolean))
                    return false;
                component.ShowTitle = boolean; changed = true;
            }
            if (values.ContainsKey("showDetails"))
            {
                if (!TryReadBoolean(values, "showDetails", out boolean))
                    return false;
                component.ShowDetails = boolean; changed = true;
            }
            if (values.ContainsKey("motion"))
            {
                if (!TryReadBoolean(values, "motion", out boolean))
                    return false;
                component.Motion = boolean; changed = true;
            }
            if (values.ContainsKey("variant"))
            {
                var variant =
                    CreatorToolsOverlayComposerSettings.NormalizeVariant(
                        Value(values, "variant"));
                if (variant.Length == 0) return false;
                component.Variant = variant; changed = true;
            }
            if (values.ContainsKey("liquidColor"))
            {
                var color =
                    CreatorToolsOverlayComposerSettings.NormalizeColor(
                        Value(values, "liquidColor"));
                if (color.Length == 0) return false;
                component.LiquidColor = color; changed = true;
            }
            if (values.ContainsKey("collectingColor"))
            {
                var color =
                    CreatorToolsOverlayComposerSettings.NormalizeColor(
                        Value(values, "collectingColor"));
                if (color.Length == 0) return false;
                component.CollectingColor = color; changed = true;
            }
            if (values.ContainsKey("textColor"))
            {
                var color =
                    CreatorToolsOverlayComposerSettings.NormalizeColor(
                        Value(values, "textColor"));
                if (color.Length == 0) return false;
                component.TextColor = color; changed = true;
            }
            if (values.ContainsKey("outlineColor"))
            {
                var color =
                    CreatorToolsOverlayComposerSettings.NormalizeColor(
                        Value(values, "outlineColor"));
                if (color.Length == 0) return false;
                component.OutlineColor = color; changed = true;
            }
            if (!changed) return false;
            CreatorToolsOverlayComposerSettings.NormalizeComponent(
                profile, component);
            return true;
        }

        private PreviewSlot CreatePreviewDefaults(
            string profileId, Dictionary<string, string> values)
        {
            var componentId =
                CreatorToolsOverlayComposerSettings.NormalizeComponentId(
                    Value(values, "componentId"));
            if (componentId.Length == 0)
                componentId =
                    CreatorToolsOverlayComposerSettings
                        .TapFarmingComponentId;
            var slot = new PreviewSlot(profileId)
            {
                ComponentId = componentId,
                SimulationActive = true,
                Scenario = "active",
                PhaseIndex = 2,
                PhaseCount = 4,
                Attempt = 1,
                Capacity = 5,
                Layout = settings.FindProfile(profileId).Clone()
            };
            if (componentId ==
                CreatorToolsOverlayComposerSettings.TapFarmingComponentId)
            {
                slot.TotalTaps = 25680L;
                slot.ReserveHealth = 3840d;
                slot.SpentHealth = 9000d;
                slot.CurrentHealth = 1810d;
                slot.TotalHealth = 3000d;
                slot.OverallProgress = 0.43d;
            }
            else
                slot.ParticipantCount = 5;
            return slot;
        }

        private static bool ApplyPreviewValues(
            PreviewSlot slot, Dictionary<string, string> values)
        {
            bool boolean;
            if (values.ContainsKey("simulationActive"))
            {
                if (!TryReadBoolean(values, "simulationActive", out boolean))
                    return false;
                slot.SimulationActive = boolean;
            }
            if (values.ContainsKey("layoutJson"))
            {
                CreatorToolsOverlayComposerProfile layout;
                if (!CreatorToolsOverlayComposerSettings.TryParseProfileJson(
                        Value(values, "layoutJson"),
                        slot.ProfileId,
                        out layout))
                    return false;
                slot.Layout = layout;
            }
            if (values.ContainsKey("componentId"))
            {
                var componentId =
                    CreatorToolsOverlayComposerSettings.NormalizeComponentId(
                        Value(values, "componentId"));
                if (componentId.Length == 0) return false;
                slot.ComponentId = componentId;
            }
            if (values.ContainsKey("scenario"))
            {
                var scenario = NormalizeIdentifier(
                    Value(values, "scenario"), 32);
                if (scenario.Length == 0) return false;
                slot.Scenario = scenario;
            }
            long integer64;
            double number;
            if (values.ContainsKey("totalTaps"))
            {
                if (!TryReadLong(values, "totalTaps", out integer64))
                    return false;
                slot.TotalTaps = ClampLong(integer64, 0L,
                    1000000000000L);
            }
            if (values.ContainsKey("tapDelta"))
            {
                if (!TryReadLong(values, "tapDelta", out integer64))
                    return false;
                slot.TapDelta = ClampLong(integer64, 0L, 1000000000L);
            }
            if (values.ContainsKey("damageDelta"))
            {
                if (!TryReadDouble(values, "damageDelta", out number))
                    return false;
                slot.DamageDelta = Clamp(number, 0d,
                    MaximumSyntheticValue);
            }
            if (!ApplyOptionalDouble(values, "reserveHealth",
                    ref slot.ReserveHealth) ||
                !ApplyOptionalDouble(values, "spentHealth",
                    ref slot.SpentHealth) ||
                !ApplyOptionalDouble(values, "currentHealth",
                    ref slot.CurrentHealth) ||
                !ApplyOptionalDouble(values, "totalHealth",
                    ref slot.TotalHealth))
                return false;
            if (values.ContainsKey("overallProgress"))
            {
                if (!TryReadDouble(values, "overallProgress", out number))
                    return false;
                slot.OverallProgress = Clamp(number, 0d, 1d);
            }
            if (!ApplyOptionalInt(values, "phaseIndex",
                    ref slot.PhaseIndex, 0, 64) ||
                !ApplyOptionalInt(values, "phaseCount",
                    ref slot.PhaseCount, 0, 64) ||
                !ApplyOptionalInt(values, "attempt",
                    ref slot.Attempt, 0, 1000000) ||
                !ApplyOptionalInt(values, "participantCount",
                    ref slot.ParticipantCount, 0, 100) ||
                !ApplyOptionalInt(values, "capacity",
                    ref slot.Capacity, 0, 100))
                return false;
            if (slot.TotalHealth > 0d)
                slot.CurrentHealth = Math.Min(
                    slot.CurrentHealth, slot.TotalHealth);
            slot.PhaseIndex = Math.Min(slot.PhaseIndex, slot.PhaseCount);
            slot.ParticipantCount = Math.Min(
                slot.ParticipantCount, slot.Capacity);
            return true;
        }

        private static bool ApplyOptionalDouble(
            Dictionary<string, string> values,
            string key,
            ref double target)
        {
            if (!values.ContainsKey(key)) return true;
            double value;
            if (!TryReadDouble(values, key, out value)) return false;
            target = Clamp(value, 0d, MaximumSyntheticValue);
            return true;
        }

        private static bool ApplyOptionalInt(
            Dictionary<string, string> values,
            string key,
            ref int target,
            int minimum,
            int maximum)
        {
            if (!values.ContainsKey(key)) return true;
            int value;
            if (!TryReadInt(values, key, out value)) return false;
            target = Math.Max(minimum, Math.Min(maximum, value));
            return true;
        }

        private void ExpirePreviewLocked(PreviewSlot slot)
        {
            if (!slot.Active || slot.ExpiresAtUtc >
                    utcNow().ToUniversalTime())
                return;
            StopPreviewLocked(slot, "expired");
        }

        private static string PreviewCancellationKey(
            string profileId, string sessionId)
        {
            return (profileId ?? string.Empty) + "\n" +
                (sessionId ?? string.Empty);
        }

        private void RecordPreviewCancellationLocked(string key)
        {
            PrunePreviewCancellationsLocked();
            if (!previewCancellations.ContainsKey(key) &&
                previewCancellations.Count >= MaximumPreviewCancellations)
            {
                var oldestKey = string.Empty;
                var oldestExpiry = DateTime.MaxValue;
                foreach (var item in previewCancellations)
                    if (item.Value < oldestExpiry)
                    {
                        oldestKey = item.Key;
                        oldestExpiry = item.Value;
                    }
                if (oldestKey.Length > 0)
                    previewCancellations.Remove(oldestKey);
            }
            previewCancellations[key] = utcNow().ToUniversalTime()
                .Add(PreviewLifetime);
        }

        private void PrunePreviewCancellationsLocked()
        {
            if (previewCancellations.Count == 0) return;
            var now = utcNow().ToUniversalTime();
            var expired = new List<string>();
            foreach (var item in previewCancellations)
                if (item.Value <= now)
                    expired.Add(item.Key);
            for (var index = 0; index < expired.Count; index++)
                previewCancellations.Remove(expired[index]);
        }

        private static void StopPreviewLocked(
            PreviewSlot slot, string feedback)
        {
            slot.Active = false;
            slot.SimulationActive = false;
            slot.Layout = null;
            slot.ExpiresAtUtc = DateTime.MinValue;
            slot.TapDelta = 0L;
            slot.DamageDelta = 0d;
            slot.Revision = slot.Revision == int.MaxValue
                ? int.MaxValue : slot.Revision + 1;
            slot.Feedback = feedback;
        }

        private static string BuildPreviewJson(PreviewSlot slot)
        {
            return BuildPreviewJson(slot, slot.Feedback, false);
        }

        private static string BuildPreviewJson(
            PreviewSlot slot, string feedback, bool error)
        {
            var builder = new StringBuilder(2048);
            builder.Append("{\"ready\":true,\"schemaVersion\":1")
                .Append(",\"revision\":").Append(slot.Revision)
                .Append(",\"runId\":").Append(slot.RunId)
                .Append(",\"active\":")
                .Append(slot.Active ? "true" : "false")
                .Append(",\"simulationActive\":")
                .Append(slot.Active && slot.SimulationActive
                    ? "true" : "false")
                .Append(",\"layout\":")
                .Append(slot.Active && slot.Layout != null
                    ? CreatorToolsOverlayComposerSettings.BuildProfileJson(
                        slot.Layout)
                    : "null")
                .Append(",\"profileId\":\"");
            CreatorToolsJson.AppendEscaped(builder, slot.ProfileId);
            builder.Append("\",\"sessionId\":\"");
            CreatorToolsJson.AppendEscaped(builder, slot.SessionId);
            builder.Append("\",\"componentId\":\"");
            CreatorToolsJson.AppendEscaped(builder, slot.ComponentId);
            builder.Append("\",\"scenario\":\"");
            CreatorToolsJson.AppendEscaped(builder, slot.Scenario);
            builder.Append("\",\"expiresAtUtc\":");
            if (!slot.Active || slot.ExpiresAtUtc == DateTime.MinValue)
                builder.Append("null");
            else
            {
                builder.Append('"').Append(slot.ExpiresAtUtc.ToString(
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture)).Append('"');
            }
            builder.Append(",\"totalTaps\":").Append(slot.TotalTaps)
                .Append(",\"tapDelta\":").Append(slot.TapDelta)
                .Append(",\"damageDelta\":")
                .Append(Format(slot.DamageDelta))
                .Append(",\"reserveHealth\":")
                .Append(Format(slot.ReserveHealth))
                .Append(",\"spentHealth\":")
                .Append(Format(slot.SpentHealth))
                .Append(",\"currentHealth\":")
                .Append(Format(slot.CurrentHealth))
                .Append(",\"totalHealth\":")
                .Append(Format(slot.TotalHealth))
                .Append(",\"overallProgress\":")
                .Append(Format(slot.OverallProgress))
                .Append(",\"phaseIndex\":").Append(slot.PhaseIndex)
                .Append(",\"phaseCount\":").Append(slot.PhaseCount)
                .Append(",\"attempt\":").Append(slot.Attempt)
                .Append(",\"participantCount\":")
                .Append(slot.ParticipantCount)
                .Append(",\"capacity\":").Append(slot.Capacity)
                .Append(",\"feedback\":\"");
            CreatorToolsJson.AppendEscaped(builder, feedback ?? string.Empty);
            builder.Append("\",\"error\":")
                .Append(error ? "true" : "false").Append('}');
            return builder.ToString();
        }

        private static string OptionalComponentId(
            Dictionary<string, string> values)
        {
            return values.ContainsKey("componentId")
                ? CreatorToolsOverlayComposerSettings.NormalizeComponentId(
                    Value(values, "componentId"))
                : string.Empty;
        }

        private static string NormalizeIdentifier(string value, int maximum)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0 || value.Length > maximum)
                return string.Empty;
            for (var i = 0; i < value.Length; i++)
                if (!char.IsLetterOrDigit(value[i]) &&
                    value[i] != '_' && value[i] != '-')
                    return string.Empty;
            return value;
        }

        private static string Value(
            Dictionary<string, string> values, string key)
        {
            return CreatorToolsFlatJson.Value(values, key);
        }

        private static bool TryReadBoolean(
            Dictionary<string, string> values, string key, out bool value)
        {
            var raw = Value(values, key);
            if (raw == "true") { value = true; return true; }
            if (raw == "false") { value = false; return true; }
            value = false;
            return false;
        }

        private static bool HasSupportedSchema(
            Dictionary<string, string> values)
        {
            if (!values.ContainsKey("schemaVersion"))
                return true;
            int schemaVersion;
            return TryReadInt(values, "schemaVersion", out schemaVersion) &&
                schemaVersion ==
                    CreatorToolsOverlayComposerSettings.SchemaVersion;
        }

        private static bool TryReadInt(
            Dictionary<string, string> values, string key, out int value)
        {
            return int.TryParse(Value(values, key), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadLong(
            Dictionary<string, string> values, string key, out long value)
        {
            return long.TryParse(Value(values, key), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadDouble(
            Dictionary<string, string> values, string key, out double value)
        {
            return double.TryParse(Value(values, key), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) &&
                !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double minimum,
            double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static long ClampLong(long value, long minimum, long maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string Format(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private sealed class PreviewSlot
        {
            internal readonly string ProfileId;
            internal int Revision;
            internal int RunId;
            internal bool Active;
            internal bool SimulationActive;
            internal CreatorToolsOverlayComposerProfile Layout;
            internal string SessionId = string.Empty;
            internal string ComponentId = string.Empty;
            internal string Scenario = string.Empty;
            internal DateTime ExpiresAtUtc = DateTime.MinValue;
            internal long TotalTaps;
            internal long TapDelta;
            internal double DamageDelta;
            internal double ReserveHealth;
            internal double SpentHealth;
            internal double CurrentHealth;
            internal double TotalHealth;
            internal double OverallProgress;
            internal int PhaseIndex;
            internal int PhaseCount;
            internal int Attempt;
            internal int ParticipantCount;
            internal int Capacity;
            internal string Feedback = "ready";

            internal PreviewSlot(string profileId)
            {
                ProfileId = profileId ?? string.Empty;
            }

            internal PreviewSlot Clone()
            {
                return (PreviewSlot)MemberwiseClone();
            }
        }
    }
}
