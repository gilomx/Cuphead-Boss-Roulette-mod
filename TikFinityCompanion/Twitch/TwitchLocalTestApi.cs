using System.Text;

namespace LaPichiRuleta.TikFinity.Twitch;

// Fixed loopback endpoints and synthetic identity. OAuth and credentials never
// enter the CLI test server, including when a real account is saved.
internal sealed class TwitchLocalTestApi(HttpClient http) : ITwitchApi
{
    internal static readonly Uri WebSocketAddress = new("ws://127.0.0.1:8080/ws");
    internal static readonly TwitchTokens Identity = new("", "", "42", "twitch_cli");

    public async Task<bool> SubscribeAsync(string type, string version, Dictionary<string, string> condition,
        string sessionId, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8080/eventsub/subscriptions") {
            Content = new StringContent(TwitchApi.SubscriptionPayload(type, version, condition, sessionId), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Client-Id", "pichi-cli-tests");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer local-test");
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new IOException("Twitch CLI rejected " + type + ": " + detail[..Math.Min(detail.Length, 256)]);
        }
        response.EnsureSuccessStatusCode();
        return true;
    }

    public Task<TwitchDeviceCode> StartAuthorizationAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<TwitchTokens?> PollAuthorizationAsync(string deviceCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<TwitchTokens> RefreshAsync(TwitchTokens tokens, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<TwitchIdentity> ValidateAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RevokeAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
}
