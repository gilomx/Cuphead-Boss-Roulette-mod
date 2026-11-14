using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.Twitch;

internal sealed record TwitchReward(string Id, string Title, long Cost, bool Enabled, bool Manageable, string BroadcasterId,
    int CooldownSeconds = 0, int MaxPerStream = 0, int MaxPerUserPerStream = 0);

internal sealed record TwitchRewardCommand(string RequestId, string Operation, string BroadcasterId,
    string RewardId, string Title, long Cost, bool? Enabled, int? CooldownSeconds = null,
    int? MaxPerStream = null, int? MaxPerUserPerStream = null)
{
    private bool HasLimits => CooldownSeconds.HasValue || MaxPerStream.HasValue || MaxPerUserPerStream.HasValue;
    private bool ValidLimits => CooldownSeconds is null or 0 or >= 60 and <= 604800 &&
        MaxPerStream is null or >= 0 && MaxPerUserPerStream is null or >= 0;

    internal bool Valid =>
        (Operation == "list" && BroadcasterId.Length == 0 || ValidBroadcasterId(BroadcasterId)) &&
        (Operation switch
        {
            "list" => !HasLimits,
            "create" => Title.Length is > 0 and <= 45 && !Title.Any(char.IsControl) && ValidCost(Cost) && ValidLimits,
            "update" => ValidId(RewardId) && ValidCost(Cost) && ValidLimits,
            "sync" => ValidId(RewardId) && Enabled.HasValue && !HasLimits,
            _ => false,
        });

    internal static TwitchRewardCommand? Parse(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || TwitchApi.Text(root, "kind") != "twitch_reward_command")
                return null;
            var id = TwitchApi.Text(root, "requestId");
            if (!ValidId(id)) return null;
            var operation = TwitchApi.Text(root, "operation");
            if (operation is not ("list" or "create" or "update" or "sync")) operation = "";
            var cost = root.TryGetProperty("cost", out var costNode) && costNode.ValueKind == JsonValueKind.Number &&
                costNode.TryGetInt64(out var points) ? points : 0L;
            bool? enabled = root.TryGetProperty("enabled", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? flag.GetBoolean() : null;
            var broadcasterId = TwitchApi.Text(root, "broadcasterId");
            if (root.TryGetProperty("broadcasterId", out var broadcaster) && broadcaster.ValueKind != JsonValueKind.String)
                broadcasterId = "?";
            return new(id, operation, broadcasterId, TwitchApi.Text(root, "rewardId"),
                TwitchApi.Text(root, "title").Trim(), cost, enabled,
                OptionalLimit(root, "cooldownSeconds"), OptionalLimit(root, "maxPerStream"),
                OptionalLimit(root, "maxPerUserPerStream"));
        }
        catch (JsonException) { return null; }
    }

    private static int? OptionalLimit(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var limit) ? limit : -1;
    }

    internal static bool ValidId(string value) => value.Length is > 0 and <= 96 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    internal static bool ValidBroadcasterId(string value) => value.Length is > 0 and <= 32 && value.All(char.IsAsciiDigit);
    internal static bool ValidCost(long value) => value is >= 1 and <= 1000000000;
}

internal sealed record TwitchRewardResult(string RequestId, string Operation, bool Ok, string BroadcasterId,
    bool CanManage, long ControlRevision, string Error = "", string RewardsJson = "", string RewardJson = "")
{
    public int ProtocolVersion => CompanionProtocol.Version;
    public string Kind => "twitch_reward_result";
}

internal sealed class TwitchRewardException(string reason) : Exception
{
    internal string Reason { get; } = reason;
}
