using System.Text.Json.Serialization;

namespace LaPichiRuleta.TikFinity.Protocol;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(CompanionStatus))]
[JsonSerializable(typeof(CompanionEvent))]
internal sealed partial class CompanionJsonContext : JsonSerializerContext;
