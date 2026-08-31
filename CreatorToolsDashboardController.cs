using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsDashboardController
    {
        private const int MaximumEventCount = 30;
        private const int MaximumCommandsPerUpdate = 64;
        private const int MaximumScheduledSimulations = 1024;
        private const double MaximumDelaySeconds = 3600d;
        private const int MaximumTextLength = 160;
        private const int MaximumCount = 1000000;
        private const int MaximumSimulationCount = 1000;
        private const decimal MaximumAmount = 1000000000m;
        private const int SchemaVersion = 2;

        private readonly CreatorToolsDashboardEventRecord[] events =
            new CreatorToolsDashboardEventRecord[MaximumEventCount];
        private readonly List<CreatorToolsDashboardConnection> connections =
            new List<CreatorToolsDashboardConnection>();
        private readonly CreatorToolsStreamDeduplicator deduplicator =
            new CreatorToolsStreamDeduplicator();
        private readonly object simulationLock = new object();
        private readonly List<ScheduledSimulation> scheduledSimulations =
            new List<ScheduledSimulation>();
        private readonly Func<CreatorToolsStreamEvent, bool>
            resolveSimulationGift;
        private readonly string streamSessionId =
            "stream-" + Guid.NewGuid().ToString("N");

        private int eventStart;
        private int eventCount;
        private long revision = 1;
        private long eventSequence;
        private long receivedCount;
        private long matchedCount;
        private long queuedCount;
        private long ignoredCount;
        private long giftCount;
        private long valuedCount;
        private long likeCount;
        private long followCount;
        private long subscriptionCount;
        private decimal coinCount;
        private decimal bitCount;
        private string lastPublishedState;
        private bool stateDirty = true;
        private long nextSimulationSequence;

        internal CreatorToolsDashboardController(
            Func<CreatorToolsStreamEvent, bool> resolveSimulationGift)
        {
            this.resolveSimulationGift = resolveSimulationGift;
            connections.Add(new CreatorToolsDashboardConnection(
                "tikfinity-local", "tiktok", "tikfinity",
                "TikTok / TikFinity", "connecting"));
            connections.Add(new CreatorToolsDashboardConnection(
                "twitch", "twitch", "twitch-eventsub",
                "Twitch", "pending"));
            connections.Add(new CreatorToolsDashboardConnection(
                "youtube", "youtube", "youtube-live-chat",
                "YouTube", "pending"));
        }

        internal void Update(
            CreatorToolsServer server,
            Func<CreatorToolsStreamEvent, CreatorToolsStreamEvaluation>
                evaluate)
        {
            ProcessDueSimulations(evaluate);

            if (server == null || !server.IsRunning)
                return;
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

        /// <summary>
        /// Runs on the local HTTP worker. It only validates and schedules
        /// immutable event data; Unity evaluation remains on Update.
        /// Returns an empty string when accepted or a stable API error code.
        /// </summary>
        internal string ScheduleSimulation(string query)
        {
            var acceptedAt = Stopwatch.GetTimestamp();
            var values = ParseQuery(query);
            CreatorToolsStreamEvent entry;
            string error;
            if (!TryBuildSimulation(values, out entry, out error))
                return error;

            var delaySeconds = ParseDelaySeconds(
                Value(values, "delaySeconds"));
            var delayTicks = (long)Math.Round(
                delaySeconds * Stopwatch.Frequency,
                MidpointRounding.AwayFromZero);
            var dueAt = acceptedAt > long.MaxValue - delayTicks
                ? long.MaxValue
                : acceptedAt + delayTicks;

            lock (simulationLock)
            {
                if (scheduledSimulations.Count >=
                    MaximumScheduledSimulations)
                    return "simulation_queue_full";

                var scheduled = new ScheduledSimulation
                {
                    DueAt = dueAt,
                    Sequence = ++nextSimulationSequence,
                    Event = entry
                };
                var index = scheduledSimulations.Count;
                while (index > 0 && ComesBefore(
                    scheduled, scheduledSimulations[index - 1]))
                    index--;
                scheduledSimulations.Insert(index, scheduled);
            }
            return string.Empty;
        }

        internal void ApplyConnectionUpdate(
            CreatorToolsStreamConnectionUpdate update)
        {
            if (update == null)
                return;
            var connection = FindConnectionById(update.ConnectionId) ??
                FindConnectionByPlatform("tiktok");
            if (connection == null)
                return;
            var status = NormalizeConnectionStatus(update.State);
            var message = NormalizeText(
                update.Message, string.Empty, MaximumTextLength);
            var messageCode = NormalizeIdentifier(
                update.MessageCode, 64);
            var retryAttempt = Math.Max(0, update.RetryAttempt);
            var changed = connection.Status != status ||
                connection.Message != message ||
                connection.MessageCode != messageCode ||
                connection.RetryAttempt != retryAttempt;
            connection.Status = status;
            connection.Message = message;
            connection.MessageCode = messageCode;
            connection.RetryAttempt = retryAttempt;
            if (changed)
            {
                revision++;
                stateDirty = true;
            }
        }

        /// <summary>
        /// Single normalized boundary for real and simulated events. Dedupe
        /// and streak-finalization happen before counters, rules or gameplay.
        /// </summary>
        internal void ProcessEvent(
            CreatorToolsStreamEvent streamEvent,
            Func<CreatorToolsStreamEvent, CreatorToolsStreamEvaluation>
                evaluate)
        {
            if (streamEvent == null)
                return;
            NormalizeEvent(streamEvent);

            var record = new CreatorToolsDashboardEventRecord
            {
                Event = streamEvent,
                Sequence = ++eventSequence,
                LocalId = "evt-" + eventSequence.ToString(
                    "D10", CultureInfo.InvariantCulture),
                StreamSessionId = streamSessionId
            };

            var connection = FindConnectionById(streamEvent.ConnectionId);
            var validPlatform = IsValidPlatform(streamEvent.Platform);
            var validType = IsValidType(streamEvent.Type);
            if (!deduplicator.TryAccept(streamEvent))
            {
                record.Status = "ignored";
                record.MessageCode = "duplicate_event";
                ignoredCount++;
            }
            else
            {
                receivedCount++;
                if (streamEvent.StreakState == "progress")
                {
                    record.Status = "ignored";
                    record.MessageCode = "streak_progress";
                    ignoredCount++;
                }
                else if (!validPlatform || !validType)
                {
                    record.Status = "ignored";
                    record.MessageCode = !validPlatform && !validType
                        ? "unsupported_platform_and_type"
                        : !validPlatform
                            ? "unsupported_platform"
                            : "unsupported_event_type";
                    ignoredCount++;
                }
                else
                {
                    var result = evaluate == null
                        ? CreatorToolsStreamEvaluation.None
                        : evaluate(streamEvent) ??
                            CreatorToolsStreamEvaluation.None;
                    record.RuleNames = result.RuleNames;
                    record.InteractionIds = result.InteractionIds;
                    record.MessageCode = result.MessageCode;
                    if (result.MessageCode == "interactions_disabled")
                    {
                        record.Status = "ignored";
                        ignoredCount++;
                    }
                    else if (result.MatchedRules > 0)
                    {
                        matchedCount++;
                        // The stream backlog is itself a reliable queue. Mark
                        // accepted deferred work as queued immediately; the
                        // global counter still advances only as gameplay-queue
                        // slots are actually assigned.
                        record.Status = result.QueuedInteractions > 0 ||
                            result.DeferredInteractions > 0
                            ? "queued"
                            : "matched";
                    }
                    else
                        record.Status = "received";
                    queuedCount += Math.Max(0, result.QueuedInteractions);
                    UpdateCounters(streamEvent);
                }
            }

            if (connection != null)
                connection.LastEventAt = streamEvent.ReceivedAt;
            AddRecord(record);
            revision++;
            stateDirty = true;
        }

        internal void InvalidateState()
        {
            lastPublishedState = null;
            stateDirty = true;
        }

        internal void AddQueuedInteractions(int count)
        {
            if (count <= 0)
                return;
            queuedCount += count;
            revision++;
            stateDirty = true;
        }

        private void ProcessDueSimulations(
            Func<CreatorToolsStreamEvent, CreatorToolsStreamEvaluation>
                evaluate)
        {
            var ready = new List<ScheduledSimulation>();
            var now = Stopwatch.GetTimestamp();
            lock (simulationLock)
            {
                while (ready.Count < MaximumCommandsPerUpdate &&
                       scheduledSimulations.Count > 0 &&
                       scheduledSimulations[0].DueAt <= now)
                {
                    ready.Add(scheduledSimulations[0]);
                    scheduledSimulations.RemoveAt(0);
                }
            }

            for (var i = 0; i < ready.Count; i++)
                ProcessSimulation(ready[i].Event, evaluate);
        }

        private bool TryBuildSimulation(
            Dictionary<string, string> values,
            out CreatorToolsStreamEvent entry,
            out string error)
        {
            var platform = NormalizeIdentifier(
                Value(values, "platform"), 24);
            var type = NormalizeIdentifier(Value(values, "type"), 24);
            var count = Math.Min(
                MaximumSimulationCount,
                ParseCount(Value(values, "count")));
            var totalValue = ParseAmount(Value(values, "amount"));
            var giftId = NormalizeText(
                Value(values, "giftId"), string.Empty, 160);
            if (giftId.Length == 0)
                giftId = NormalizeText(
                    Value(values, "itemId"), string.Empty, 160);

            entry = new CreatorToolsStreamEvent
            {
                Platform = platform,
                Type = type,
                UserName = NormalizeText(
                    Value(values, "user"), string.Empty, 80),
                UserDisplayName = NormalizeText(
                    Value(values, "user"), string.Empty, 80),
                UserAvatarUrl = string.Empty,
                UserId = NormalizeText(
                    Value(values, "userId"), string.Empty, 160),
                ItemId = giftId,
                Count = count,
                UnitValue = count > 0 ? totalValue / count : totalValue,
                TotalValue = totalValue,
                Unit = NormalizeIdentifier(Value(values, "unit"), 24),
                Currency = NormalizeCurrency(Value(values, "currency")),
                StreakState = "none",
                Simulated = true,
                RawEventType = "dashboard_simulation"
            };

            if (platform == "tiktok" && type == "gift")
            {
                if (resolveSimulationGift == null ||
                    !resolveSimulationGift(entry))
                {
                    error = "unknown_gift";
                    entry = null;
                    return false;
                }
            }
            else if (entry.Unit.Length == 0)
                entry.Unit = DefaultUnit(platform, type, totalValue);

            error = string.Empty;
            return true;
        }

        private void ProcessSimulation(
            CreatorToolsStreamEvent entry,
            Func<CreatorToolsStreamEvent, CreatorToolsStreamEvaluation>
                evaluate)
        {
            var sourceConnection = FindConnectionByPlatform(entry.Platform);
            var simulationId = Guid.NewGuid().ToString("N");
            entry.EventId = "sim-" + simulationId;
            entry.IdempotencyKey = streamSessionId + ":sim:" + simulationId;
            entry.ConnectionId = sourceConnection == null
                ? "simulator"
                : "simulator-" + entry.Platform;
            entry.Connector = "simulator";
            entry.ReceivedAt = UtcTimestamp();
            ProcessEvent(entry, evaluate);
        }

        private void AddRecord(CreatorToolsDashboardEventRecord record)
        {
            var writeIndex = (eventStart + eventCount) % MaximumEventCount;
            if (eventCount == MaximumEventCount)
            {
                writeIndex = eventStart;
                eventStart = (eventStart + 1) % MaximumEventCount;
            }
            else eventCount++;
            events[writeIndex] = record;
        }

        private void UpdateCounters(CreatorToolsStreamEvent entry)
        {
            if (entry.Type == "gift")
                giftCount += entry.Count;
            if (entry.Platform == "tiktok" && entry.Type == "gift" &&
                entry.TotalValue > 0m)
                coinCount += entry.TotalValue;
            if (entry.Platform == "twitch" &&
                (entry.Type == "gift" || entry.Type == "currency") &&
                entry.TotalValue > 0m)
                bitCount += entry.TotalValue;
            if ((entry.Type == "gift" || entry.Type == "currency") &&
                entry.TotalValue > 0m)
                valuedCount++;
            if (entry.Type == "like") likeCount += entry.Count;
            else if (entry.Type == "follow") followCount += entry.Count;
            else if (entry.Type == "subscription")
                subscriptionCount += entry.Count;
        }

        private string BuildState()
        {
            var builder = new StringBuilder(65536);
            builder.Append("{\"ready\":true,\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"revision\":")
                .Append(revision)
                .Append(",\"engineStatus\":\"");
            CreatorToolsJson.AppendEscaped(builder, EngineStatus());
            builder.Append("\",\"streamSessionId\":\"");
            CreatorToolsJson.AppendEscaped(builder, streamSessionId);
            builder.Append("\",\"connections\":[");
            for (var i = 0; i < connections.Count; i++)
            {
                if (i > 0) builder.Append(',');
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
                .Append(",\"coins\":");
            CreatorToolsJson.AppendDecimal(builder, coinCount);
            builder.Append(",\"bits\":");
            CreatorToolsJson.AppendDecimal(builder, bitCount);
            builder
                .Append("},\"events\":[");
            for (var i = 0; i < eventCount; i++)
            {
                if (i > 0) builder.Append(',');
                var index = (eventStart + eventCount - 1 - i) %
                    MaximumEventCount;
                events[index].AppendJson(builder);
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private string EngineStatus()
        {
            var tikfinity = FindConnectionByPlatform("tiktok");
            if (tikfinity == null) return "idle";
            if (tikfinity.Status == "connected" ||
                tikfinity.Status == "live")
                return "running";
            if (tikfinity.Status == "connecting" ||
                tikfinity.Status == "reconnecting")
                return "connecting";
            if (tikfinity.Status == "error") return "degraded";
            return "idle";
        }

        private CreatorToolsDashboardConnection FindConnectionById(string id)
        {
            for (var i = 0; i < connections.Count; i++)
                if (connections[i].Id == id)
                    return connections[i];
            return null;
        }

        private CreatorToolsDashboardConnection FindConnectionByPlatform(
            string platform)
        {
            for (var i = 0; i < connections.Count; i++)
                if (connections[i].Platform == platform)
                    return connections[i];
            return null;
        }

        private static void NormalizeEvent(CreatorToolsStreamEvent entry)
        {
            entry.EventId = NormalizeText(
                entry.EventId, Guid.NewGuid().ToString("N"), 160);
            entry.ConnectionId = NormalizeText(
                entry.ConnectionId, "unknown", 80);
            entry.IdempotencyKey = NormalizeText(entry.IdempotencyKey,
                entry.ConnectionId + ":" + entry.EventId, 240);
            entry.Platform = NormalizeIdentifier(entry.Platform, 24);
            entry.Connector = NormalizeIdentifier(entry.Connector, 64);
            entry.Type = NormalizeIdentifier(entry.Type, 24);
            entry.UserName = NormalizeText(
                entry.UserName, string.Empty, 80);
            entry.UserDisplayName = NormalizeText(
                entry.UserDisplayName, entry.UserName, 160);
            entry.UserAvatarUrl = NormalizeText(
                entry.UserAvatarUrl, string.Empty, 2048);
            entry.UserId = NormalizeText(entry.UserId, string.Empty, 160);
            entry.ItemId = NormalizeText(entry.ItemId, string.Empty, 160);
            entry.ItemName = NormalizeText(entry.ItemName, string.Empty, 160);
            entry.ItemImageUrl = NormalizeText(
                entry.ItemImageUrl, string.Empty, 2048);
            entry.Count = Math.Max(1, Math.Min(MaximumCount, entry.Count));
            entry.UnitValue = Math.Max(
                0m, Math.Min(MaximumAmount, entry.UnitValue));
            entry.TotalValue = Math.Max(
                0m, Math.Min(MaximumAmount, entry.TotalValue));
            entry.Unit = NormalizeIdentifier(entry.Unit, 24);
            entry.Currency = NormalizeCurrency(entry.Currency);
            entry.StreakId = NormalizeText(
                entry.StreakId, string.Empty, 160);
            if (entry.StreakState != "progress" &&
                entry.StreakState != "final")
                entry.StreakState = "none";
            if (string.IsNullOrEmpty(entry.ReceivedAt))
                entry.ReceivedAt = UtcTimestamp();
        }

        private static string NormalizeConnectionStatus(string value)
        {
            value = NormalizeIdentifier(value, 24);
            if (value == "starting") return "connecting";
            if (value == "connecting" || value == "connected" ||
                value == "live" || value == "waiting" ||
                value == "reconnecting" || value == "disconnected" ||
                value == "error")
                return value;
            return "error";
        }

        private static bool IsValidPlatform(string value)
        {
            return value == "tiktok" || value == "twitch" ||
                value == "youtube";
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
            if (amount <= 0m || (type != "gift" && type != "currency"))
                return string.Empty;
            if (platform == "tiktok") return "coin";
            if (platform == "twitch") return "bit";
            if (platform == "youtube") return "money";
            return string.Empty;
        }

        private static string NormalizeCurrency(string value)
        {
            value = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length != 3) return string.Empty;
            for (var i = 0; i < value.Length; i++)
                if (value[i] < 'A' || value[i] > 'Z')
                    return string.Empty;
            return value;
        }

        private static string NormalizeIdentifier(string value, int maximum)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value.Length <= maximum
                ? value
                : value.Substring(0, maximum);
        }

        private static string NormalizeText(
            string value, string fallback, int maximum)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0) value = fallback ?? string.Empty;
            return value.Length <= maximum
                ? value
                : value.Substring(0, maximum);
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

        private static double ParseDelaySeconds(string value)
        {
            double delay;
            if (!double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out delay) ||
                double.IsNaN(delay) || double.IsInfinity(delay) ||
                delay <= 0d)
                return 0d;
            return Math.Min(MaximumDelaySeconds, delay);
        }

        private static bool ComesBefore(
            ScheduledSimulation left,
            ScheduledSimulation right)
        {
            return left.DueAt < right.DueAt ||
                (left.DueAt == right.DueAt &&
                 left.Sequence < right.Sequence);
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
            if (string.IsNullOrEmpty(query)) return values;
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
                catch { continue; }
                if (key.Length <= 64 && value.Length <= 2048)
                    values[key] = value;
            }
            return values;
        }

        private static string UtcTimestamp()
        {
            return DateTime.UtcNow.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture);
        }

        private sealed class ScheduledSimulation
        {
            internal long DueAt;
            internal long Sequence;
            internal CreatorToolsStreamEvent Event;
        }
    }
}
