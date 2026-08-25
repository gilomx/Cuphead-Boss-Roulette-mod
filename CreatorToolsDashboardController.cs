using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsDashboardController
    {
        private const int MaximumEventCount = 500;
        private const int MaximumCommandsPerUpdate = 64;
        private const int MaximumTextLength = 80;
        private const int MaximumCount = 1000000;
        private const decimal MaximumAmount = 1000000000m;
        private const int SchemaVersion = 1;

        private readonly DashboardEvent[] events =
            new DashboardEvent[MaximumEventCount];
        private readonly List<DashboardConnection> connections =
            new List<DashboardConnection>();

        private int eventStart;
        private int eventCount;
        private readonly string streamSessionId =
            "simulation-" + Guid.NewGuid().ToString("N");
        private long revision = 1;
        private long eventSequence;
        private long receivedCount;
        // These remain zero until the rule matcher and gameplay dispatcher
        // are connected in the next stage of the streaming engine.
        private long matchedCount = 0;
        private long queuedCount = 0;
        private long ignoredCount;
        private long giftCount;
        private long valuedCount;
        private long likeCount;
        private long followCount;
        private long subscriptionCount;
        private string lastPublishedState;
        private bool stateDirty = true;

        internal CreatorToolsDashboardController()
        {
            connections.Add(new DashboardConnection(
                "tikfinity", "tiktok", "tikfinity",
                "TikTok / TikFinity"));
            connections.Add(new DashboardConnection(
                "twitch", "twitch", "twitch-eventsub",
                "Twitch"));
            connections.Add(new DashboardConnection(
                "youtube", "youtube", "youtube-live-chat",
                "YouTube"));
        }

        internal void Update(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;

            var processed = 0;
            string query;
            while (processed < MaximumCommandsPerUpdate &&
                   server.TryTakeDashboardCommand(out query))
            {
                ProcessSimulation(ParseQuery(query));
                processed++;
            }

            if (!stateDirty)
                return;
            var state = BuildState();
            if (state == lastPublishedState)
            {
                stateDirty = false;
                return;
            }
            lastPublishedState = state;
            server.SetDashboardState(state);
            stateDirty = false;
        }

        internal void InvalidateState()
        {
            lastPublishedState = null;
            stateDirty = true;
        }

        private void ProcessSimulation(
            Dictionary<string, string> values)
        {
            var timestamp = DateTime.UtcNow.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture);
            var platform = NormalizeIdentifier(Value(values, "platform"));
            var type = NormalizeIdentifier(Value(values, "type"));
            var connection = FindConnection(platform);
            var validPlatform = connection != null;
            var validType = IsValidType(type);

            var entry = new DashboardEvent();
            eventSequence++;
            entry.Id = "sim-" + eventSequence.ToString(
                "D10", CultureInfo.InvariantCulture);
            entry.EventId = entry.Id;
            entry.IdempotencyKey =
                streamSessionId + ":" + entry.EventId;
            entry.Sequence = eventSequence;
            entry.StreamSessionId = streamSessionId;
            entry.Platform = validPlatform ? platform :
                NormalizeText(platform, "unknown");
            entry.Connector = validPlatform
                ? connection.Connector
                : "simulator";
            entry.ConnectionId = validPlatform
                ? connection.Id
                : "simulator";
            entry.Type = validType ? type : NormalizeText(type, "unknown");
            entry.User = NormalizeText(
                Value(values, "user"), string.Empty);
            entry.UserId = NormalizeText(
                Value(values, "userId"), string.Empty);
            entry.Amount = ParseAmount(Value(values, "amount"));
            entry.Count = ParseCount(Value(values, "count"));
            entry.ItemName = NormalizeText(
                Value(values, "itemName"), string.Empty);
            entry.Unit = NormalizeIdentifier(Value(values, "unit"));
            if (entry.Unit.Length == 0 && validPlatform && validType)
                entry.Unit = DefaultUnit(platform, type, entry.Amount);
            entry.Currency = NormalizeCurrency(
                Value(values, "currency"));
            entry.ReceivedAt = timestamp;
            entry.Simulated = true;
            receivedCount++;

            if (validPlatform && validType)
            {
                entry.Status = "received";
                entry.MessageCode = "simulation_received";
                connection.LastEventAt = timestamp;
                UpdateCounters(entry);
            }
            else
            {
                entry.Status = "ignored";
                if (!validPlatform && !validType)
                    entry.MessageCode = "unsupported_platform_and_type";
                else if (!validPlatform)
                    entry.MessageCode = "unsupported_platform";
                else
                    entry.MessageCode = "unsupported_event_type";
                ignoredCount++;
            }

            var writeIndex = (eventStart + eventCount) % MaximumEventCount;
            if (eventCount == MaximumEventCount)
            {
                writeIndex = eventStart;
                eventStart = (eventStart + 1) % MaximumEventCount;
            }
            else
                eventCount++;
            events[writeIndex] = entry;
            revision++;
            stateDirty = true;
        }

        private void UpdateCounters(DashboardEvent entry)
        {
            if (entry.Type == "gift")
                giftCount += entry.Count;
            if ((entry.Type == "gift" || entry.Type == "currency") &&
                entry.Amount > 0m)
                valuedCount++;
            if (entry.Type == "like")
                likeCount += entry.Count;
            else if (entry.Type == "follow")
                followCount += entry.Count;
            else if (entry.Type == "subscription")
                subscriptionCount += entry.Count;
        }

        private string BuildState()
        {
            var builder = new StringBuilder(32768);
            builder.Append("{\"ready\":true,\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"revision\":")
                .Append(revision)
                .Append(",\"engineStatus\":\"simulated\",\"connections\":[");
            for (var i = 0; i < connections.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                connections[i].AppendJson(builder);
            }
            builder.Append("],\"counters\":{\"received\":")
                .Append(receivedCount)
                .Append(",\"matched\":").Append(matchedCount)
                .Append(",\"queued\":").Append(queuedCount)
                .Append(",\"ignored\":").Append(ignoredCount)
                .Append(",\"gifts\":").Append(giftCount)
                .Append(",\"valued\":").Append(valuedCount)
                .Append(",\"likes\":").Append(likeCount)
                .Append(",\"follows\":").Append(followCount)
                .Append(",\"subscriptions\":")
                .Append(subscriptionCount)
                .Append("},\"events\":[");

            // The newest events are first so the dashboard can prepend new
            // activity without reordering the feed.
            for (var i = 0; i < eventCount; i++)
            {
                if (i > 0)
                    builder.Append(',');
                var index = (eventStart + eventCount - 1 - i) %
                    MaximumEventCount;
                events[index].AppendJson(builder);
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private DashboardConnection FindConnection(string platform)
        {
            for (var i = 0; i < connections.Count; i++)
                if (connections[i].Platform == platform)
                    return connections[i];
            return null;
        }

        private static bool IsValidType(string value)
        {
            return value == "gift" || value == "currency" ||
                   value == "like" || value == "follow" ||
                   value == "subscription" || value == "redemption";
        }

        private static string DefaultUnit(
            string platform, string type, decimal amount)
        {
            if (amount <= 0m ||
                (type != "gift" && type != "currency"))
                return string.Empty;
            if (platform == "tiktok")
                return "coin";
            if (platform == "twitch")
                return "bit";
            if (platform == "youtube")
                return "money";
            return string.Empty;
        }

        private static string NormalizeCurrency(string value)
        {
            value = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length != 3)
                return string.Empty;
            for (var i = 0; i < value.Length; i++)
                if (value[i] < 'A' || value[i] > 'Z')
                    return string.Empty;
            return value;
        }

        private static string NormalizeIdentifier(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value.Length <= 24 ? value : value.Substring(0, 24);
        }

        private static string NormalizeText(string value, string fallback)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
                value = fallback ?? string.Empty;
            if (value.Length > MaximumTextLength)
                value = value.Substring(0, MaximumTextLength);
            return value;
        }

        private static decimal ParseAmount(string value)
        {
            decimal amount;
            if (!decimal.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out amount))
                return 0m;
            return Math.Max(0m, Math.Min(MaximumAmount, amount));
        }

        private static int ParseCount(string value)
        {
            int count;
            if (!int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out count))
                return 1;
            return Math.Max(1, Math.Min(MaximumCount, count));
        }

        private static string Value(
            Dictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value)
                ? value
                : string.Empty;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
                return values;
            var pairs = query.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var separator = pairs[i].IndexOf('=');
                var key = separator < 0
                    ? pairs[i]
                    : pairs[i].Substring(0, separator);
                var value = separator < 0
                    ? string.Empty
                    : pairs[i].Substring(separator + 1);
                try
                {
                    key = Uri.UnescapeDataString(key.Replace('+', ' '));
                    value = Uri.UnescapeDataString(value.Replace('+', ' '));
                }
                catch
                {
                    continue;
                }
                if (key.Length <= 64)
                    values[key] = value;
            }
            return values;
        }

        private static void AppendJson(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
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
        }

        private sealed class DashboardConnection
        {
            internal readonly string Id;
            internal readonly string Platform;
            internal readonly string Connector;
            internal readonly string Label;
            internal string LastEventAt;

            internal DashboardConnection(
                string id, string platform, string connector, string label)
            {
                Id = id;
                Platform = platform;
                Connector = connector;
                Label = label;
            }

            internal void AppendJson(StringBuilder builder)
            {
                builder.Append("{\"id\":\"");
                CreatorToolsDashboardController.AppendJson(builder, Id);
                builder.Append("\",\"platform\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, Platform);
                builder.Append("\",\"connector\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, Connector);
                builder.Append("\",\"label\":\"");
                CreatorToolsDashboardController.AppendJson(builder, Label);
                builder.Append(
                    "\",\"status\":\"simulated\",\"account\":\"\"," +
                    "\"message\":\"\"," +
                    "\"lastEventAt\":");
                if (string.IsNullOrEmpty(LastEventAt))
                    builder.Append("null");
                else
                {
                    builder.Append('"');
                    CreatorToolsDashboardController.AppendJson(
                        builder, LastEventAt);
                    builder.Append('"');
                }
                builder.Append('}');
            }
        }

        private sealed class DashboardEvent
        {
            internal string Id;
            internal string EventId;
            internal string IdempotencyKey;
            internal long Sequence;
            internal string ConnectionId;
            internal string StreamSessionId;
            internal string Platform;
            internal string Connector;
            internal string Type;
            internal string User;
            internal string UserId;
            internal decimal Amount;
            internal string Unit;
            internal string Currency;
            internal int Count;
            internal string ItemName;
            internal string Status;
            internal string MessageCode;
            internal string ReceivedAt;
            internal bool Simulated;

            internal void AppendJson(StringBuilder builder)
            {
                builder.Append("{\"schemaVersion\":")
                    .Append(SchemaVersion)
                    .Append(",\"id\":\"");
                CreatorToolsDashboardController.AppendJson(builder, Id);
                builder.Append("\",\"eventId\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, EventId);
                builder.Append("\",\"idempotencyKey\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, IdempotencyKey);
                builder.Append("\",\"sequence\":").Append(Sequence)
                    .Append(",\"connectionId\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, ConnectionId);
                builder.Append("\",\"streamSessionId\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, StreamSessionId);
                builder.Append("\",\"platform\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, Platform);
                builder.Append("\",\"connector\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, Connector);
                builder.Append("\",\"type\":\"");
                CreatorToolsDashboardController.AppendJson(builder, Type);
                builder.Append("\",\"user\":\"");
                CreatorToolsDashboardController.AppendJson(builder, User);
                builder.Append("\",\"userId\":");
                AppendNullableString(builder, UserId);
                builder.Append(",\"amount\":")
                    .Append(Amount.ToString(
                        "0.##", CultureInfo.InvariantCulture))
                    .Append(",\"unit\":");
                AppendNullableString(builder, Unit);
                builder.Append(",\"currency\":");
                AppendNullableString(builder, Currency);
                builder.Append(",\"count\":").Append(Count);
                if (!string.IsNullOrEmpty(ItemName))
                {
                    builder.Append(",\"itemName\":\"");
                    CreatorToolsDashboardController.AppendJson(
                        builder, ItemName);
                    builder.Append('"');
                }
                builder.Append(",\"status\":\"");
                CreatorToolsDashboardController.AppendJson(builder, Status);
                builder.Append("\",\"messageCode\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, MessageCode);
                builder.Append("\",\"receivedAt\":\"");
                CreatorToolsDashboardController.AppendJson(
                    builder, ReceivedAt);
                builder.Append("\",\"simulated\":")
                    .Append(Simulated ? "true" : "false")
                    .Append('}');
            }

            private static void AppendNullableString(
                StringBuilder builder, string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    builder.Append("null");
                    return;
                }
                builder.Append('"');
                CreatorToolsDashboardController.AppendJson(builder, value);
                builder.Append('"');
            }
        }
    }
}
