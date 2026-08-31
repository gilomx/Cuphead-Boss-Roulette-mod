namespace LaPichiRuleta.TikFinity.Protocol;

internal static class CompanionProtocol
{
    internal const int Version = 1;
    internal const string ConnectionId = "tikfinity-local";
    internal const string Platform = "tiktok";
    internal const string Connector = "tikfinity";
}

internal static class CompanionStatusStates
{
    internal const string Starting = "starting";
    internal const string Connecting = "connecting";
    internal const string Connected = "connected";
    internal const string Disconnected = "disconnected";
    internal const string Error = "error";
}

internal static class StreakStates
{
    internal const string None = "none";
    internal const string Progress = "progress";
    internal const string Final = "final";
}

internal sealed class CompanionStatus
{
    public int ProtocolVersion { get; init; } = CompanionProtocol.Version;

    public string Kind { get; init; } = "status";

    public required string State { get; init; }

    public string ConnectionId { get; init; } = CompanionProtocol.ConnectionId;

    public required string Message { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public int RetryAttempt { get; init; }
}

internal sealed class CompanionEvent
{
    public int ProtocolVersion { get; init; } = CompanionProtocol.Version;

    public string Kind { get; init; } = "event";

    public required string EventId { get; init; }

    public required string IdempotencyKey { get; init; }

    public string ConnectionId { get; init; } = CompanionProtocol.ConnectionId;

    public string Platform { get; init; } = CompanionProtocol.Platform;

    public string Connector { get; init; } = CompanionProtocol.Connector;

    public required string Type { get; init; }

    public string? UserName { get; init; }

    public string? UserDisplayName { get; init; }

    public string? UserId { get; init; }

    public string? UserAvatarUrl { get; init; }

    public string? ItemId { get; init; }

    public string? ItemName { get; init; }

    public string? ItemImageUrl { get; init; }

    public int Count { get; init; }

    public decimal? UnitValue { get; init; }

    public decimal? TotalValue { get; init; }

    public string? Unit { get; init; }

    public string? Currency { get; init; }

    public string? StreakId { get; init; }

    public string? StreakState { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }

    public bool Simulated { get; init; }

    public required string RawEventType { get; init; }
}
