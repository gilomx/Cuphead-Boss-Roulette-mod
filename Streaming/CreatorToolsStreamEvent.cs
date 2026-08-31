using System;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Provider-neutral event produced by a streaming adapter. The transport
    /// never owns Unity state; instances cross into the main thread before
    /// rule evaluation and gameplay dispatch.
    /// </summary>
    internal sealed class CreatorToolsStreamEvent
    {
        internal string EventId = string.Empty;
        internal string IdempotencyKey = string.Empty;
        internal string ConnectionId = string.Empty;
        internal string Platform = string.Empty;
        internal string Connector = string.Empty;
        internal string Type = string.Empty;
        internal string UserName = string.Empty;
        internal string UserDisplayName = string.Empty;
        internal string UserAvatarUrl = string.Empty;
        internal string UserId = string.Empty;
        internal string ItemId = string.Empty;
        internal string ItemName = string.Empty;
        internal string ItemImageUrl = string.Empty;
        internal int Count = 1;
        internal decimal UnitValue;
        internal decimal TotalValue;
        internal string Unit = string.Empty;
        internal string Currency = string.Empty;
        internal string StreakId = string.Empty;
        internal string StreakState = "none";
        internal string ReceivedAt = string.Empty;
        internal bool Simulated;
        internal string RawEventType = string.Empty;
    }

    internal sealed class CreatorToolsStreamConnectionUpdate
    {
        internal string ConnectionId = "tikfinity-local";
        internal string State = "disconnected";
        internal string Message = string.Empty;
        internal string MessageCode = string.Empty;
        internal string OccurredAt = string.Empty;
        internal int RetryAttempt;
    }

    internal sealed class CreatorToolsStreamMessage
    {
        internal CreatorToolsStreamConnectionUpdate Connection;
        internal CreatorToolsStreamEvent Event;
    }

    internal sealed class CreatorToolsStreamEvaluation
    {
        internal static readonly CreatorToolsStreamEvaluation None =
            new CreatorToolsStreamEvaluation();

        internal int MatchedRules;
        internal int QueuedInteractions;
        internal long DeferredInteractions;
        internal string RuleNames = string.Empty;
        internal string InteractionIds = string.Empty;
        internal string MessageCode = string.Empty;
    }
}
