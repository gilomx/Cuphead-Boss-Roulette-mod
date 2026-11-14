using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LaPichiRuleta.TikFinity.Twitch;

internal sealed class TwitchApi(HttpClient http) : ITwitchApi
{
    private const string RewardsEndpoint = "https://api.twitch.tv/helix/channel_points/custom_rewards";

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

    public async Task<TwitchReward[]> ListRewardsAsync(TwitchTokens tokens, CancellationToken cancellationToken)
    {
        var address = RewardsEndpoint + "?broadcaster_id=" + Uri.EscapeDataString(tokens.UserId);
        if (!tokens.CanManageRewards)
            return await SendRewardsAsync(tokens, HttpMethod.Get, address, null, cancellationToken);

        var all = SendRewardsAsync(tokens, HttpMethod.Get, address, null, cancellationToken);
        var manageable = SendRewardsAsync(tokens, HttpMethod.Get, address + "&only_manageable_rewards=true", null, cancellationToken);
        await Task.WhenAll(all, manageable);
        var ids = manageable.Result.Select(reward => reward.Id).ToHashSet(StringComparer.Ordinal);
        return all.Result.Select(reward => reward with { Manageable = ids.Contains(reward.Id) }).ToArray();
    }

    public async Task<TwitchReward> ChangeRewardAsync(TwitchTokens tokens, TwitchRewardCommand command,
        CancellationToken cancellationToken)
    {
        var address = RewardsEndpoint + "?broadcaster_id=" + Uri.EscapeDataString(tokens.UserId);
        var create = command.Operation == "create";
        // Helix checks Client-Id ownership for PATCH, including when title/cost match a manually created reward.
        if (!create) address += "&id=" + Uri.EscapeDataString(command.RewardId);
        var payload = command.Operation switch
        {
            "create" or "update" => RewardWritePayload(command),
            "sync" => command.Enabled == true ? "{\"is_enabled\":true}" : "{\"is_enabled\":false}",
            _ => throw new TwitchRewardException("invalid_request"),
        };
        var rewards = await SendRewardsAsync(tokens, create ? HttpMethod.Post : HttpMethod.Patch, address, payload, cancellationToken);
        if (rewards.Length != 1 || (!create && rewards[0].Id != command.RewardId))
            throw new InvalidDataException("Unexpected Twitch reward response.");
        return rewards[0] with { Manageable = true };
    }

    private static string RewardWritePayload(TwitchRewardCommand command)
    {
        var payload = new StringBuilder("{\"cost\":").Append(command.Cost.ToString(CultureInfo.InvariantCulture));
        if (command.Operation == "create")
            payload.Append(",\"title\":").Append(Quote(command.Title)).Append(",\"is_enabled\":false");
        AppendLimit(payload, "is_global_cooldown_enabled", "global_cooldown_seconds", command.CooldownSeconds);
        AppendLimit(payload, "is_max_per_stream_enabled", "max_per_stream", command.MaxPerStream);
        AppendLimit(payload, "is_max_per_user_per_stream_enabled", "max_per_user_per_stream", command.MaxPerUserPerStream);
        return payload.Append('}').ToString();
    }

    private static void AppendLimit(StringBuilder payload, string enabledKey, string valueKey, int? limit)
    {
        if (!limit.HasValue) return;
        // Helix treats the flag and its value together, including when clearing a limit.
        payload.Append(",\"").Append(enabledKey).Append("\":").Append(limit.Value > 0 ? "true" : "false")
            .Append(",\"").Append(valueKey).Append("\":").Append(limit.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static int ReadLimit(JsonElement reward, string settingKey, string valueKey, int maximum = int.MaxValue)
    {
        if (!reward.TryGetProperty(settingKey, out var setting) || setting.ValueKind != JsonValueKind.Object ||
            !setting.TryGetProperty("is_enabled", out var enabled) || enabled.ValueKind != JsonValueKind.True ||
            !setting.TryGetProperty(valueKey, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var amount)) return 0;
        return (int)Math.Clamp(amount, 0L, maximum);
    }

    private async Task<TwitchReward[]> SendRewardsAsync(TwitchTokens tokens, HttpMethod method, string address,
        string? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, address);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Headers.Add("Client-Id", TwitchApplication.ClientId);
        if (payload != null) request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new TwitchRewardException(await RewardErrorAsync(response, method, cancellationToken));

        // A full list can include 50 reward definitions, each with multiple image URLs.
        using var json = await ReadAsync(response, cancellationToken, maximumLength: 262144);
        var data = json.RootElement.GetProperty("data");
        if (data.GetArrayLength() > 50) throw new InvalidDataException("Twitch reward list too large.");
        var rewards = new List<TwitchReward>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            var id = Text(item, "id");
            var title = Text(item, "title");
            var cost = item.GetProperty("cost").GetInt64();
            var enabled = item.GetProperty("is_enabled").GetBoolean();
            if (Text(item, "broadcaster_id") != tokens.UserId || !TwitchRewardCommand.ValidId(id) ||
                title.Length is < 1 or > 90 || !TwitchRewardCommand.ValidCost(cost))
                throw new InvalidDataException("Unexpected Twitch reward data.");
            rewards.Add(new(id, title, cost, enabled, false, tokens.UserId,
                ReadLimit(item, "global_cooldown_setting", "global_cooldown_seconds", 604800),
                ReadLimit(item, "max_per_stream_setting", "max_per_stream"),
                ReadLimit(item, "max_per_user_per_stream_setting", "max_per_user_per_stream")));
        }
        return rewards.ToArray();
    }

    private static async Task<string> RewardErrorAsync(HttpResponseMessage response, HttpMethod method,
        CancellationToken cancellationToken)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized: return "authorization_required";
            case HttpStatusCode.Forbidden: return method == HttpMethod.Patch ? "reward_not_manageable" : "channel_points_unavailable";
            case HttpStatusCode.NotFound: return "reward_not_found";
            case HttpStatusCode.TooManyRequests: return "rate_limited";
            case HttpStatusCode.BadRequest:
                if (method == HttpMethod.Post)
                {
                    using var json = await ReadAsync(response, cancellationToken);
                    var message = Text(json.RootElement, "message");
                    if (message.Contains("maximum number of rewards", StringComparison.OrdinalIgnoreCase)) return "reward_limit";
                    if (message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("already exists", StringComparison.OrdinalIgnoreCase)) return "reward_name_conflict";
                }
                return "twitch_rejected";
            default: return "twitch_unavailable";
        }
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

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken,
        int maximumLength = 65536)
    {
        // OAuth tokens stay exclusively in this process. Never log response bodies.
        await response.Content.LoadIntoBufferAsync(maximumLength, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (text.Length > maximumLength) throw new InvalidDataException("Twitch response too large.");
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
