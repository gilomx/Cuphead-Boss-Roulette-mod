using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaPichiRuleta.TikFinity.YouTube;

internal static class YouTubeApplication
{
    // Shared public Desktop OAuth client for La Pichi Ruleta.
    internal const string ClientId = "287370493778-ueg1rqb3h9868hbpuh1flp19c4adtbnf.apps.googleusercontent.com";
    internal const string Scope = "https://www.googleapis.com/auth/youtube.readonly";
    // Desktop clients require this application parameter even when PKCE is used.
    // The publisher supplies an ignored local JSON; user tokens are never embedded.
    internal static readonly string ClientSecret = ReadClientSecret(typeof(YouTubeApplication).Assembly
        .GetManifestResourceStream("LaPichiRuleta.YouTube.DesktopClient.json"), ClientId);

    internal static string ReadClientSecret(Stream? stream, string expectedClientId)
    {
        if (stream == null) return "";
        using (stream)
        {
            try
            {
                using var json = JsonDocument.Parse(stream);
                var root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("installed", out var client) ||
                    client.ValueKind != JsonValueKind.Object || !client.TryGetProperty("client_id", out var id) ||
                    id.ValueKind != JsonValueKind.String || id.GetString() != expectedClientId ||
                    !client.TryGetProperty("client_secret", out var secret) || secret.ValueKind != JsonValueKind.String)
                    return "";
                var value = secret.GetString() ?? "";
                return value.Length is > 0 and <= 512 ? value : "";
            }
            catch (JsonException) { return ""; }
        }
    }
}

internal sealed record YouTubeTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt,
    string ChannelId = "", string ChannelName = "")
{
    public override string ToString() => "[YouTube credentials]";
}
internal sealed record YouTubeIdentity(string ChannelId, string ChannelName);
internal sealed record YouTubeLiveChat(string Id, string VideoId);
internal interface IYouTubeCredentialStore
{
    YouTubeTokens? Read();
    void Write(YouTubeTokens tokens);
    void Delete();
}
internal interface IYouTubeApi
{
    bool Configured { get; }
    Task<YouTubeTokens> ExchangeAsync(string code, string verifier, string redirectUri, CancellationToken cancellationToken);
    Task<YouTubeTokens> RefreshAsync(YouTubeTokens tokens, CancellationToken cancellationToken);
    Task<YouTubeIdentity> IdentifyAsync(string accessToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<YouTubeLiveChat>> FindLiveChatsAsync(YouTubeTokens tokens, CancellationToken cancellationToken);
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
internal interface IYouTubeAuthorization
{
    Task<YouTubeTokens> AuthorizeAsync(IYouTubeApi api, Func<string, DateTimeOffset, Task> ready,
        CancellationToken cancellationToken);
}
internal interface IYouTubeEventReceiver
{
    void Reset();
    Task RunAsync(Func<CancellationToken, Task<YouTubeTokens>> currentTokens, Func<bool, Task> connected,
        CancellationToken cancellationToken);
}
internal interface IYouTubeChatTransport
{
    IAsyncEnumerable<Wire.LiveChatMessageListResponse> StreamAsync(Wire.LiveChatMessageListRequest request,
        YouTubeTokens tokens, CancellationToken cancellationToken);
}
internal sealed class YouTubeAuthorizationException : Exception;
internal sealed class YouTubeAuthorizationDeniedException : Exception;
internal sealed class YouTubeApiException(string code) : Exception
{
    internal string Code { get; } = code;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(YouTubeTokens))]
internal sealed partial class YouTubeCredentialJson : JsonSerializerContext;
