using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LaPichiRuleta.TikFinity.YouTube;

internal sealed class YouTubeApi(HttpClient http, string clientId = YouTubeApplication.ClientId,
    string? clientSecret = null) : IYouTubeApi
{
    private readonly string desktopClientSecret = clientSecret ?? YouTubeApplication.ClientSecret;
    public bool Configured => clientId.Length > 0 && desktopClientSecret.Length > 0;

    public Task<YouTubeTokens> ExchangeAsync(string code, string verifier, string redirectUri, CancellationToken cancellationToken) =>
        TokenAsync(new() { ["client_id"] = clientId, ["code"] = code, ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri, ["grant_type"] = "authorization_code" }, null, cancellationToken);

    public Task<YouTubeTokens> RefreshAsync(YouTubeTokens tokens, CancellationToken cancellationToken) =>
        TokenAsync(new() { ["client_id"] = clientId, ["refresh_token"] = tokens.RefreshToken,
            ["grant_type"] = "refresh_token" }, tokens, cancellationToken);

    private async Task<YouTubeTokens> TokenAsync(Dictionary<string, string> form, YouTubeTokens? previous,
        CancellationToken cancellationToken)
    {
        if (!Configured) throw new YouTubeApiException("application_not_configured");
        form["client_secret"] = desktopClientSecret;
        using var body = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", body, cancellationToken);
        using var json = await ReadAsync(response, cancellationToken);
        var root = json.RootElement;
        if (!response.IsSuccessStatusCode)
        {
            if (Text(root, "error") is "invalid_grant" or "access_denied") throw new YouTubeAuthorizationException();
            if (Text(root, "error") is "invalid_client" || Text(root, "error") is "invalid_request" &&
                Text(root, "error_description").Contains("client_secret", StringComparison.OrdinalIgnoreCase))
                throw new YouTubeApiException("oauth_client_error");
            throw new YouTubeApiException("oauth_error");
        }
        var access = Text(root, "access_token");
        var refresh = Text(root, "refresh_token");
        if (refresh.Length == 0) refresh = previous?.RefreshToken ?? "";
        var scope = Text(root, "scope");
        if (access.Length == 0 || refresh.Length == 0 || !root.TryGetProperty("expires_in", out var expiry) ||
            !expiry.TryGetInt32(out var seconds) || seconds <= 0 ||
            scope.Length > 0 && !scope.Split(' ').Contains(YouTubeApplication.Scope, StringComparer.Ordinal))
            throw new YouTubeAuthorizationException();
        return new(access, refresh, DateTimeOffset.UtcNow.AddSeconds(Math.Min(seconds, 86400)),
            previous?.ChannelId ?? "", previous?.ChannelName ?? "");
    }

    public async Task<YouTubeIdentity> IdentifyAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var json = await GetAsync("channels?part=snippet&mine=true&maxResults=1", accessToken, cancellationToken);
        if (!json.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() != 1)
            throw new YouTubeApiException("channel_required");
        var item = items[0];
        var id = Text(item, "id");
        var title = item.TryGetProperty("snippet", out var snippet) ? Text(snippet, "title") : "";
        if (id.Length == 0 || title.Length == 0) throw new YouTubeApiException("channel_required");
        return new(id, title);
    }

    public async Task<IReadOnlyList<YouTubeLiveChat>> FindLiveChatsAsync(YouTubeTokens tokens, CancellationToken cancellationToken)
    {
        var chats = new List<YouTubeLiveChat>();
        // broadcastStatus and mine are mutually exclusive. Verify ownership explicitly.
        using var json = await GetAsync("liveBroadcasts?part=snippet&broadcastStatus=active&broadcastType=all&maxResults=50",
            tokens.AccessToken, cancellationToken);
        if (json.RootElement.TryGetProperty("items", out var items))
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("snippet", out var snippet) && Text(snippet, "channelId") == tokens.ChannelId &&
                    Text(snippet, "liveChatId") is { Length: > 0 } id)
                    chats.Add(new(id, Text(item, "id")));
        return chats;
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
        using var response = await http.PostAsync("https://oauth2.googleapis.com/revoke", body, cancellationToken);
        // Invalid tokens are already revoked.
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
            throw new YouTubeApiException("revocation_unconfirmed");
    }

    private async Task<JsonDocument> GetAsync(string path, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        var json = await ReadAsync(response, cancellationToken);
        if (response.IsSuccessStatusCode) return json;
        var reason = "api_error";
        if (json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) reason = Text(errors[0], "reason");
        json.Dispose();
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new YouTubeAuthorizationException();
        throw new YouTubeApiException(reason switch {
            "quotaExceeded" or "dailyLimitExceeded" => "quota_exceeded",
            "liveStreamingNotEnabled" => "live_not_enabled",
            "accessNotConfigured" => "api_not_enabled",
            "insufficientPermissions" => "permission_required",
            _ => "api_error",
        });
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Error bodies may contain credentials; only stable codes leave this boundary.
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length > 1024 * 1024) throw new YouTubeApiException("api_error");
        try { return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes)); }
        catch (JsonException) { throw new YouTubeApiException("api_error"); }
        finally { Array.Clear(bytes); }
    }
    private static string Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : "";
}
