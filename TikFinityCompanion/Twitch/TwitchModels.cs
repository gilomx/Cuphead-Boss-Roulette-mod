using System.Text.Json.Serialization;

namespace LaPichiRuleta.TikFinity.Twitch;

internal static class TwitchApplication
{
    internal const string ClientId = "mvkigdcw0qjbj4j7o149qed3wu2b0z";
    internal static readonly string[] Scopes = [
        "user:read:chat", "moderator:read:followers", "channel:read:subscriptions",
        "bits:read", "channel:read:redemptions",
    ];
}

internal sealed record TwitchTokens(string AccessToken, string RefreshToken, string UserId, string Login)
{
    public override string ToString() => "[Twitch credentials]";
}
internal sealed record TwitchIdentity(string ClientId, string UserId, string Login, string[] Scopes);
internal sealed record TwitchDeviceCode(string DeviceCode, string UserCode, string VerificationUri, int ExpiresIn, int Interval);

internal interface ITwitchCredentialStore
{
    TwitchTokens? Read();
    void Write(TwitchTokens tokens);
    void Delete();
}

internal interface ITwitchApi
{
    Task<TwitchDeviceCode> StartAuthorizationAsync(CancellationToken cancellationToken);
    Task<TwitchTokens?> PollAuthorizationAsync(string deviceCode, CancellationToken cancellationToken);
    Task<TwitchTokens> RefreshAsync(TwitchTokens tokens, CancellationToken cancellationToken);
    Task<TwitchIdentity> ValidateAsync(string accessToken, CancellationToken cancellationToken);
    Task RevokeAsync(string accessToken, CancellationToken cancellationToken);
    Task<bool> SubscribeAsync(string type, string version, Dictionary<string, string> condition,
        string sessionId, string accessToken, CancellationToken cancellationToken);
}

internal sealed class TwitchAuthorizationException : Exception;
internal sealed class TwitchSlowDownException : Exception;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TwitchTokens))]
internal sealed partial class TwitchCredentialJson : JsonSerializerContext;
