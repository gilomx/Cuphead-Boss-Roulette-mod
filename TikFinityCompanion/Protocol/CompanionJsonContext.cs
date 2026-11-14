using System.Text.Json.Serialization;
using LaPichiRuleta.TikFinity.Twitch;

namespace LaPichiRuleta.TikFinity.Protocol;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(CompanionStatus))]
[JsonSerializable(typeof(CompanionEvent))]
[JsonSerializable(typeof(TwitchRewardResult))]
[JsonSerializable(typeof(TwitchReward))]
[JsonSerializable(typeof(TwitchReward[]))]
internal sealed partial class CompanionJsonContext : JsonSerializerContext;
