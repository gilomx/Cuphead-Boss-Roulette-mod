using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsDashboardConnection
    {
        internal readonly string Id;
        internal readonly string Platform;
        internal readonly string Connector;
        internal readonly string Label;
        internal string Status;
        internal string Account = string.Empty;
        internal string Message = string.Empty;
        internal string MessageCode = string.Empty;
        internal string LastEventAt = string.Empty;
        internal int RetryAttempt;

        internal CreatorToolsDashboardConnection(
            string id,
            string platform,
            string connector,
            string label,
            string status)
        {
            Id = id;
            Platform = platform;
            Connector = connector;
            Label = label;
            Status = status;
        }

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{\"id\":\"");
            CreatorToolsJson.AppendEscaped(builder, Id);
            builder.Append("\",\"platform\":\"");
            CreatorToolsJson.AppendEscaped(builder, Platform);
            builder.Append("\",\"connector\":\"");
            CreatorToolsJson.AppendEscaped(builder, Connector);
            builder.Append("\",\"label\":\"");
            CreatorToolsJson.AppendEscaped(builder, Label);
            builder.Append("\",\"status\":\"");
            CreatorToolsJson.AppendEscaped(builder, Status);
            builder.Append("\",\"account\":\"");
            CreatorToolsJson.AppendEscaped(builder, Account);
            builder.Append("\",\"message\":\"");
            CreatorToolsJson.AppendEscaped(builder, Message);
            builder.Append("\",\"messageCode\":\"");
            CreatorToolsJson.AppendEscaped(builder, MessageCode);
            builder.Append("\",\"retryAttempt\":")
                .Append(RetryAttempt)
                .Append(",\"lastEventAt\":");
            CreatorToolsJson.AppendNullableString(builder, LastEventAt);
            builder.Append('}');
        }
    }

    internal sealed class CreatorToolsDashboardEventRecord
    {
        internal const int SchemaVersion = 2;
        internal long Sequence;
        internal string LocalId = string.Empty;
        internal string StreamSessionId = string.Empty;
        internal CreatorToolsStreamEvent Event;
        internal string Status = "received";
        internal string MessageCode = string.Empty;
        internal string RuleNames = string.Empty;
        internal string InteractionIds = string.Empty;

        internal void AppendJson(StringBuilder builder)
        {
            var entry = Event ?? new CreatorToolsStreamEvent();
            builder.Append("{\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"id\":\"");
            CreatorToolsJson.AppendEscaped(builder, LocalId);
            builder.Append("\",\"eventId\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.EventId);
            builder.Append("\",\"idempotencyKey\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.IdempotencyKey);
            builder.Append("\",\"sequence\":").Append(Sequence)
                .Append(",\"connectionId\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.ConnectionId);
            builder.Append("\",\"streamSessionId\":\"");
            CreatorToolsJson.AppendEscaped(builder, StreamSessionId);
            builder.Append("\",\"platform\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.Platform);
            builder.Append("\",\"connector\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.Connector);
            builder.Append("\",\"type\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.Type);
            builder.Append("\",\"user\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.UserName);
            builder.Append("\",\"userDisplayName\":");
            CreatorToolsJson.AppendNullableString(
                builder, entry.UserDisplayName);
            builder.Append(",\"userAvatarUrl\":");
            CreatorToolsJson.AppendNullableString(
                builder, entry.UserAvatarUrl);
            builder.Append(",\"userId\":");
            CreatorToolsJson.AppendNullableString(builder, entry.UserId);
            builder.Append(",\"itemId\":");
            CreatorToolsJson.AppendNullableString(builder, entry.ItemId);
            builder.Append(",\"itemName\":");
            CreatorToolsJson.AppendNullableString(builder, entry.ItemName);
            builder.Append(",\"itemImageUrl\":");
            CreatorToolsJson.AppendNullableString(
                builder, entry.ItemImageUrl);
            builder.Append(",\"count\":").Append(entry.Count)
                .Append(",\"unitValue\":");
            CreatorToolsJson.AppendDecimal(builder, entry.UnitValue);
            builder.Append(",\"totalValue\":");
            CreatorToolsJson.AppendDecimal(builder, entry.TotalValue);
            // `amount` remains as the v1 compatibility alias used by the
            // current dashboard while clients migrate to totalValue.
            builder.Append(",\"amount\":");
            CreatorToolsJson.AppendDecimal(builder, entry.TotalValue);
            builder.Append(",\"unit\":");
            CreatorToolsJson.AppendNullableString(builder, entry.Unit);
            builder.Append(",\"currency\":");
            CreatorToolsJson.AppendNullableString(builder, entry.Currency);
            builder.Append(",\"streakId\":");
            CreatorToolsJson.AppendNullableString(builder, entry.StreakId);
            builder.Append(",\"streakState\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.StreakState);
            builder.Append("\",\"rawEventType\":");
            CreatorToolsJson.AppendNullableString(builder, entry.RawEventType);
            builder.Append(",\"status\":\"");
            CreatorToolsJson.AppendEscaped(builder, Status);
            builder.Append("\",\"messageCode\":\"");
            CreatorToolsJson.AppendEscaped(builder, MessageCode);
            builder.Append("\",\"receivedAt\":\"");
            CreatorToolsJson.AppendEscaped(builder, entry.ReceivedAt);
            builder.Append("\",\"simulated\":")
                .Append(entry.Simulated ? "true" : "false");
            if (!string.IsNullOrEmpty(RuleNames))
            {
                builder.Append(",\"rule\":\"");
                CreatorToolsJson.AppendEscaped(builder, RuleNames);
                builder.Append('"');
            }
            if (!string.IsNullOrEmpty(InteractionIds))
            {
                builder.Append(",\"action\":\"");
                CreatorToolsJson.AppendEscaped(builder, InteractionIds);
                builder.Append('"');
            }
            builder.Append('}');
        }
    }
}
