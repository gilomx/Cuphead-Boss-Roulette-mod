using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal static class TikFinityCompanionProtocol
    {
        internal const int ProtocolVersion = 1;
        private const int MaximumTextLength = 256;
        private const decimal MaximumValue = 1000000000m;

        internal static bool TryParse(
            string json, out CreatorToolsStreamMessage message)
        {
            message = null;
            Dictionary<string, string> values;
            if (!CreatorToolsFlatJson.TryParse(json, out values) ||
                CreatorToolsFlatJson.Integer(values, "protocolVersion",
                    0, 0, 100) != ProtocolVersion)
                return false;

            var kind = Identifier(CreatorToolsFlatJson.Value(values, "kind"));
            if (kind == "status")
                return TryParseStatus(values, out message);
            if (kind == "event")
                return TryParseEvent(values, out message);
            return false;
        }

        private static bool TryParseStatus(
            Dictionary<string, string> values,
            out CreatorToolsStreamMessage message)
        {
            message = null;
            var state = Identifier(CreatorToolsFlatJson.Value(values, "state"));
            if (state != "starting" && state != "connecting" &&
                state != "connected" && state != "waiting" &&
                state != "reconnecting" && state != "disconnected" &&
                state != "error")
                return false;
            message = new CreatorToolsStreamMessage
            {
                Connection = new CreatorToolsStreamConnectionUpdate
                {
                    ConnectionId = Text(CreatorToolsFlatJson.Value(
                        values, "connectionId"), "tikfinity-local", 80),
                    State = state,
                    Message = Text(CreatorToolsFlatJson.Value(
                        values, "message"), string.Empty, MaximumTextLength),
                    MessageCode = Identifier(CreatorToolsFlatJson.Value(
                        values, "messageCode")),
                    OccurredAt = Timestamp(CreatorToolsFlatJson.Value(
                        values, "occurredAt")),
                    RetryAttempt = CreatorToolsFlatJson.Integer(
                        values, "retryAttempt", 0, 0, 1000000)
                }
            };
            return true;
        }

        private static bool TryParseEvent(
            Dictionary<string, string> values,
            out CreatorToolsStreamMessage message)
        {
            message = null;
            var type = Identifier(CreatorToolsFlatJson.Value(values, "type"));
            if (type != "gift" && type != "currency" &&
                type != "like" && type != "follow" &&
                type != "subscription" && type != "redemption")
                return false;
            var eventId = Text(CreatorToolsFlatJson.Value(
                values, "eventId"), string.Empty, 160);
            if (eventId.Length == 0)
                return false;
            var connectionId = Text(CreatorToolsFlatJson.Value(
                values, "connectionId"), "tikfinity-local", 80);
            var idempotencyKey = Text(CreatorToolsFlatJson.Value(
                values, "idempotencyKey"), string.Empty, 240);
            if (idempotencyKey.Length == 0)
                idempotencyKey = connectionId + ":" + eventId;
            var streakState = Identifier(CreatorToolsFlatJson.Value(
                values, "streakState"));
            if (streakState != "progress" && streakState != "final")
                streakState = "none";

            message = new CreatorToolsStreamMessage
            {
                Event = new CreatorToolsStreamEvent
                {
                    EventId = eventId,
                    IdempotencyKey = idempotencyKey,
                    ConnectionId = connectionId,
                    Platform = IdentifierOr(CreatorToolsFlatJson.Value(
                        values, "platform"), "tiktok"),
                    Connector = IdentifierOr(CreatorToolsFlatJson.Value(
                        values, "connector"), "tikfinity"),
                    Type = type,
                    UserName = Text(CreatorToolsFlatJson.Value(
                        values, "userName"), string.Empty, 80),
                    UserId = Text(CreatorToolsFlatJson.Value(
                        values, "userId"), string.Empty, 160),
                    ItemId = Text(CreatorToolsFlatJson.Value(
                        values, "itemId"), string.Empty, 160),
                    ItemName = Text(CreatorToolsFlatJson.Value(
                        values, "itemName"), string.Empty, 160),
                    ItemImageUrl = Text(CreatorToolsFlatJson.Value(
                        values, "itemImageUrl"), string.Empty, 2048),
                    Count = CreatorToolsFlatJson.Integer(
                        values, "count", 1, 1, 1000000),
                    UnitValue = CreatorToolsFlatJson.Decimal(
                        values, "unitValue", 0m, MaximumValue),
                    TotalValue = CreatorToolsFlatJson.Decimal(
                        values, "totalValue", 0m, MaximumValue),
                    Unit = Identifier(CreatorToolsFlatJson.Value(
                        values, "unit")),
                    Currency = Currency(CreatorToolsFlatJson.Value(
                        values, "currency")),
                    StreakId = Text(CreatorToolsFlatJson.Value(
                        values, "streakId"), string.Empty, 160),
                    StreakState = streakState,
                    ReceivedAt = Timestamp(CreatorToolsFlatJson.Value(
                        values, "receivedAt")),
                    Simulated = CreatorToolsFlatJson.Boolean(
                        values, "simulated"),
                    RawEventType = Identifier(CreatorToolsFlatJson.Value(
                        values, "rawEventType"))
                }
            };
            if (message.Event.TotalValue <= 0m &&
                message.Event.UnitValue > 0m)
                message.Event.TotalValue = message.Event.UnitValue *
                    message.Event.Count;
            return true;
        }

        private static string IdentifierOr(string value, string fallback)
        {
            value = Identifier(value);
            return value.Length == 0 ? fallback : value;
        }

        private static string Identifier(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length > 64)
                value = value.Substring(0, 64);
            return value;
        }

        private static string Text(
            string value, string fallback, int maximumLength)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
                value = fallback ?? string.Empty;
            if (value.Length > maximumLength)
                value = value.Substring(0, maximumLength);
            return value;
        }

        private static string Currency(string value)
        {
            value = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length != 3)
                return string.Empty;
            for (var i = 0; i < value.Length; i++)
                if (value[i] < 'A' || value[i] > 'Z')
                    return string.Empty;
            return value;
        }

        private static string Timestamp(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out parsed))
                return parsed.ToUniversalTime().ToString(
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    System.Globalization.CultureInfo.InvariantCulture);
            return DateTime.UtcNow.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
