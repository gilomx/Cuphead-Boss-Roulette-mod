using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LaPichiRuleta.TikFinity.Twitch;

internal sealed class TwitchApi(HttpClient http) : ITwitchApi
{
    public async Task<TwitchDeviceCode> StartAuthorizationAsync(CancellationToken cancellationToken)
    {
        using var document = await PostFormAsync("device", new() {
            ["client_id"] = TwitchApplication.ClientId,
            ["scopes"] = string.Join(' ', TwitchApplication.Scopes),
        }, cancellationToken);
        var root = document.RootElement;
        var uri = Text(root, "verification_uri");
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme != "https" ||
            parsed.Host != "www.twitch.tv" || parsed.Port != 443 || parsed.UserInfo.Length != 0 || parsed.AbsolutePath != "/activate")
            throw new InvalidDataException("Invalid Twitch authorization address.");
        return new(Text(root, "device_code"), Text(root, "user_code"), uri,
            Math.Clamp(root.GetProperty("expires_in").GetInt32(), 1, 3600),
            Math.Clamp(root.GetProperty("interval").GetInt32(), 1, 60));
    }

    public async Task<TwitchTokens?> PollAuthorizationAsync(string deviceCode, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token") {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["client_id"] = TwitchApplication.ClientId,
                ["scopes"] = string.Join(' ', TwitchApplication.Scopes),
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            }),
        };
        using var response = await http.SendAsync(request, cancellationToken);
        using var document = await ReadAsync(response, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = Text(document.RootElement, "message");
            if (message == "authorization_pending") return null;
            if (message == "slow_down") throw new TwitchSlowDownException();
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                throw new TwitchAuthorizationException();
            throw new HttpRequestException("Twitch authorization unavailable.");
        }
        return Tokens(document.RootElement, "", "");
    }

    public async Task<TwitchTokens> RefreshAsync(TwitchTokens tokens, CancellationToken cancellationToken)
    {
        using var document = await PostFormAsync("token", new() {
            ["client_id"] = TwitchApplication.ClientId, ["refresh_token"] = tokens.RefreshToken,
            ["grant_type"] = "refresh_token",
        }, cancellationToken);
        return Tokens(document.RootElement, tokens.UserId, tokens.Login);
    }

    public async Task<TwitchIdentity> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new TwitchAuthorizationException();
        response.EnsureSuccessStatusCode();
        using var document = await ReadAsync(response, cancellationToken);
        var root = document.RootElement;
        return new(Text(root, "client_id"), Text(root, "user_id"), Text(root, "login"),
            root.GetProperty("scopes").EnumerateArray().Select(item => item.GetString() ?? "").ToArray());
    }

    public async Task RevokeAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync("https://id.twitch.tv/oauth2/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["client_id"] = TwitchApplication.ClientId, ["token"] = accessToken }), cancellationToken);
        // An already invalid token no longer grants access.
        if (response.StatusCode != HttpStatusCode.BadRequest) response.EnsureSuccessStatusCode();
    }

    public async Task<bool> SubscribeAsync(string type, string version, Dictionary<string, string> condition,
        string sessionId, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions") {
            Content = new StringContent(SubscriptionPayload(type, version, condition, sessionId), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", TwitchApplication.ClientId);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new TwitchAuthorizationException();
        if (response.StatusCode == HttpStatusCode.Forbidden) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    internal static string SubscriptionPayload(string type, string version, Dictionary<string, string> condition, string sessionId)
    {
        // Build from fixed event types and authenticated identity, never from browser input.
        var payload = new StringBuilder("{\"type\":").Append(Quote(type))
            .Append(",\"version\":").Append(Quote(version)).Append(",\"condition\":{");
        var first = true;
        foreach (var pair in condition)
        {
            if (!first) payload.Append(','); first = false;
            payload.Append(Quote(pair.Key)).Append(':').Append(Quote(pair.Value));
        }
        payload.Append("},\"transport\":{\"method\":\"websocket\",\"session_id\":")
            .Append(Quote(sessionId)).Append("}}");
        return payload.ToString();
    }

    private async Task<JsonDocument> PostFormAsync(string endpoint, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync("https://id.twitch.tv/oauth2/" + endpoint,
            new FormUrlEncodedContent(fields), cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            throw new TwitchAuthorizationException();
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response, cancellationToken);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // OAuth tokens stay exclusively in this process. Never log response bodies.
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (text.Length > 65536) throw new InvalidDataException("Twitch response too large.");
        return JsonDocument.Parse(text);
    }

    private static TwitchTokens Tokens(JsonElement root, string userId, string login)
    {
        var access = Text(root, "access_token"); var refresh = Text(root, "refresh_token");
        if (access.Length == 0 || refresh.Length == 0) throw new TwitchAuthorizationException();
        return new(access, refresh, userId, login);
    }

    internal static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? "" : "";

    private static string Quote(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}
