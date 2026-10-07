using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.Runtime;
using LaPichiRuleta.TikFinity.Twitch;

namespace LaPichiRuleta.TikFinity.Tests;

internal static partial class Program
{
    private static CompanionEvent? TwitchEvent(string type, string fields, string broadcaster = "42")
    {
        using var json = JsonDocument.Parse("{\"metadata\":{\"message_id\":\"m1\",\"message_timestamp\":\"2026-10-07T12:00:00Z\"}," +
            "\"payload\":{\"subscription\":{\"type\":\"" + type + "\"},\"event\":{\"broadcaster_user_id\":\"" + broadcaster + "\"," + fields + "}}}");
        return TwitchEventNormalizer.Normalize(json.RootElement, "42", FixedTime);
    }

    private static void TwitchBallots()
    {
        var fields = "\"chatter_user_id\":\"123\",\"chatter_user_login\":\"viewer\",\"message\":{\"text\":\" 3 \"}";
        var vote = TwitchEvent("channel.chat.message", fields)!;
        Equal("chat", vote.Type); Equal("3", vote.ChatText); Equal("123", vote.UserId);
        Equal("twitch:42:m1", vote.IdempotencyKey);
        Equal(null, TwitchEvent("channel.chat.message", fields, "43"));
        Equal(null, TwitchEvent("channel.chat.message", "\"message\":{\"text\":\"private conversation\"}"));
        Equal(null, TwitchEvent("channel.chat.message", "\"message\":{\"text\":\"33\"}"));
        Equal(null, TwitchEvent("channel.chat.message", "\"message\":{\"text\":\"7\"}"));
        Equal(true, TwitchEventSub.IsReconnectAddress("wss://eventsub.wss.twitch.tv/ws?reconnect=abc"));
        Equal(false, TwitchEventSub.IsReconnectAddress("wss://eventsub.wss.twitch.tv.example.com/ws"));
        Equal(false, TwitchEventSub.IsReconnectAddress("wss://eventsub.wss.twitch.tv:444/ws"));
    }

    private static void TwitchEventValues()
    {
        var bits = TwitchEvent("channel.cheer", "\"bits\":150")!;
        Equal("currency", bits.Type); Equal("bit", bits.Unit); Equal(150m, bits.TotalValue);
        var reward = TwitchEvent("channel.channel_points_custom_reward_redemption.add",
            "\"reward\":{\"id\":\"reward-1\",\"title\":\"Reto\",\"cost\":500}")!;
        Equal("redemption", reward.Type); Equal("reward-1", reward.ItemId); Equal(500m, reward.TotalValue);
        Equal(5, TwitchEvent("channel.subscription.gift", "\"total\":5")!.Count);
        Equal(null, TwitchEvent("channel.subscribe", "\"is_gift\":true"));
        Equal("subscription", TwitchEvent("channel.subscribe", "\"is_gift\":false")!.Type);
    }

    private static void TwitchLocalReceiptTime()
    {
        foreach (var serverSkew in new[] { -40, -5, 5, 40 })
        {
            var serverTime = FixedTime.AddSeconds(serverSkew).ToString("O");
            using var json = JsonDocument.Parse("{\"metadata\":{\"message_id\":\"vote\",\"message_timestamp\":\"" + serverTime + "\"}," +
                "\"payload\":{\"subscription\":{\"type\":\"channel.chat.message\"},\"event\":{\"broadcaster_user_id\":\"42\"," +
                "\"chatter_user_id\":\"123\",\"message\":{\"text\":\"1\"}}}}");
            var vote = TwitchEventNormalizer.Normalize(json.RootElement, "42", FixedTime)!;
            Equal(FixedTime, vote.ReceivedAt);
            Equal("1", vote.ChatText);

            // A queued message keeps its original receipt time through serialization.
            using var stream = new MemoryStream();
            new NdjsonWriter(stream).WriteEventAsync(vote, default).GetAwaiter().GetResult();
            using var wire = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
            Equal(FixedTime, wire.RootElement.GetProperty("receivedAt").GetDateTimeOffset());
        }
    }

    private static void TwitchPublicApi()
    {
        var handler = new TwitchHttpHandler();
        using var http = new HttpClient(handler);
        var api = new TwitchApi(http);
        handler.Response = "{\"device_code\":\"PRIVATE-DEVICE\",\"user_code\":\"PUBLIC\",\"verification_uri\":\"https://www.twitch.tv/activate\",\"expires_in\":1800,\"interval\":5}";
        var code = api.StartAuthorizationAsync(default).GetAwaiter().GetResult();
        Equal("PUBLIC", code.UserCode);
        Equal(true, handler.Body.Contains("client_id=" + TwitchApplication.ClientId));
        Equal(true, handler.Body.Contains("user%3Aread%3Achat"));
        Equal(false, handler.Body.Contains("client_secret"));
        handler.Status = HttpStatusCode.BadRequest; handler.Response = "{\"message\":\"authorization_pending\"}";
        Equal(null, api.PollAuthorizationAsync(code.DeviceCode, default).GetAwaiter().GetResult());
        handler.Status = HttpStatusCode.OK; handler.Response = "{\"access_token\":\"NEW-ACCESS\",\"refresh_token\":\"NEW-REFRESH\"}";
        var renewed = api.RefreshAsync(TestTokens, default).GetAwaiter().GetResult();
        Equal("NEW-REFRESH", renewed.RefreshToken); Equal(false, handler.Body.Contains("client_secret"));
        handler.Response = "{\"device_code\":\"PRIVATE\",\"user_code\":\"PUBLIC\",\"verification_uri\":\"https://evil.invalid/activate\",\"expires_in\":1800,\"interval\":5}";
        try { api.StartAuthorizationAsync(default).GetAwaiter().GetResult(); throw new Exception("Unsafe URL accepted"); }
        catch (InvalidDataException) { }
    }

    private static readonly TwitchTokens TestTokens = new("ACCESS-PRIVATE", "REFRESH-PRIVATE", "42", "channel");

    private static async Task TwitchCommandPipeAsync()
    {
        foreach (var preamble in new[] { Array.Empty<byte>(), Encoding.UTF8.GetPreamble() })
        {
            var store = new FakeTwitchStore();
            await using var harness = new TwitchHarness(new FakeTwitchApi(), store);
            await harness.WaitAsync("not_connected");
            using var pipe = new MemoryStream(preamble.Concat(Encoding.ASCII.GetBytes("twitch:connect:1\r\n")).ToArray());
            await CompanionHost.ReadCommandsAsync(pipe, harness.Service, default);
            var status = await harness.WaitAsync("authorization_pending");
            Equal(1L, status.ControlRevision); Equal("PUBLIC-CODE", status.UserCode);
            Equal(0, store.Writes); // The command requests authorization without authorizing an account.
        }
    }

    private static async Task TwitchRestoreAsync()
    {
        var api = new FakeTwitchApi { ExpiredAccess = true };
        var store = new FakeTwitchStore { Value = TestTokens };
        await using var harness = new TwitchHarness(api, store);
        var connected = await harness.WaitAsync("connected");
        Equal(true, connected.Authorized); Equal("channel", connected.Account);
        Equal("ROTATED-REFRESH", store.Value!.RefreshToken); Equal(1, store.Writes);
        Equal(true, api.ValidationCount >= 2); Equal(1, harness.Receiver.Starts);
        harness.Service.TryCommand("cancel:1"); await harness.WaitAsync("connected");
        harness.Service.TryCommand("disconnect:1"); // Old revisions must not stop the active socket.
        harness.Service.TryCommand("cancel:2"); await harness.WaitAsync("connected");
        Equal(3, harness.Receiver.Starts);
    }

    private static async Task TwitchCancelAsync()
    {
        var api = new FakeTwitchApi { DelayedPoll = true };
        var store = new FakeTwitchStore();
        await using var harness = new TwitchHarness(api, store);
        await harness.WaitAsync("not_connected"); harness.Service.TryCommand("connect:1");
        var pending = await harness.WaitAsync("authorization_pending");
        Equal("PUBLIC-CODE", pending.UserCode); Equal(false, pending.Authorized);
        await api.PollStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Service.TryCommand("cancel:2");
        await api.PollCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        api.PollResult.SetResult(TestTokens); // Simulates an HTTP response arriving after cancellation.
        await harness.WaitAsync("authorization_cancelled");
        Equal(0, store.Writes); Equal(null, store.Value); Equal(0, harness.Receiver.Starts);
    }

    private static async Task TwitchDisconnectAsync()
    {
        var api = new FakeTwitchApi { FailRevoke = true };
        var store = new FakeTwitchStore { Value = TestTokens, FailDelete = true };
        await using var harness = new TwitchHarness(api, store);
        await harness.WaitAsync("connected"); harness.Service.TryCommand("disconnect:1");
        var failure = await harness.WaitAsync("storage_delete_error");
        Equal(false, failure.Authorized); Equal(TestTokens, store.Value); Equal(1, harness.Receiver.Stops);
        store.FailDelete = false; harness.Service.TryCommand("disconnect:2");
        await harness.WaitAsync("revocation_unconfirmed");
        Equal(null, store.Value); Equal(1, api.Revokes); Equal("ACCESS-PRIVATE", api.RevokedAccess);
    }

    private static async Task TwitchStartupErrorsAsync()
    {
        await using (var vault = new TwitchHarness(new FakeTwitchApi(), new FakeTwitchStore { FailRead = true }))
        {
            Equal(false, (await vault.WaitAsync("storage_error")).Authorized);
            Equal(0, vault.Receiver.Starts);
        }
        var store = new FakeTwitchStore { Value = TestTokens };
        await using var wrong = new TwitchHarness(new FakeTwitchApi { WrongIdentity = true }, store);
        await wrong.WaitAsync("authorization_required");
        Equal(null, store.Value); Equal(0, wrong.Receiver.Starts);
    }

    private static void TwitchPublicProtocol()
    {
        using var stream = new MemoryStream();
        var writer = new NdjsonWriter(stream);
        writer.WriteStatusAsync(new CompanionStatus { State = "connected", Message = "", ConnectionId = "twitch", Account = "channel", Authorized = true }, default).GetAwaiter().GetResult();
        var output = Encoding.UTF8.GetString(stream.ToArray());
        Equal(false, output.Contains(TestTokens.AccessToken)); Equal(false, output.Contains(TestTokens.RefreshToken));
        Equal(false, output.Contains("deviceCode")); Equal(true, output.Contains("\"account\":\"channel\""));
        Equal("[Twitch credentials]", TestTokens.ToString());
    }

    private sealed class TwitchHttpHandler : HttpMessageHandler
    {
        internal string Body = "", Response = "";
        internal HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(Status) { Content = new StringContent(Response) };
        }
    }

    private sealed class FakeTwitchStore : ITwitchCredentialStore
    {
        internal TwitchTokens? Value;
        internal bool FailRead, FailDelete;
        internal int Writes;
        public TwitchTokens? Read() => FailRead ? throw new IOException() : Value;
        public void Write(TwitchTokens tokens) { Value = tokens; Writes++; }
        public void Delete() { if (FailDelete) throw new IOException(); Value = null; }
    }

    private sealed class FakeTwitchApi : ITwitchApi
    {
        internal bool ExpiredAccess, WrongIdentity, DelayedPoll, FailRevoke;
        internal int ValidationCount, Revokes;
        internal string RevokedAccess = "";
        internal readonly TaskCompletionSource PollStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource PollCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<TwitchTokens?> PollResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<TwitchDeviceCode> StartAuthorizationAsync(CancellationToken token) =>
            Task.FromResult(new TwitchDeviceCode("PRIVATE-DEVICE", "PUBLIC-CODE", "https://www.twitch.tv/activate", 300, 1));
        public async Task<TwitchTokens?> PollAuthorizationAsync(string code, CancellationToken token)
        {
            PollStarted.TrySetResult();
            if (!DelayedPoll) return TestTokens;
            using var registration = token.Register(() => PollCancelled.TrySetResult());
            return await PollResult.Task;
        }
        public Task<TwitchTokens> RefreshAsync(TwitchTokens tokens, CancellationToken token) =>
            Task.FromResult(tokens with { AccessToken = "ROTATED-ACCESS", RefreshToken = "ROTATED-REFRESH" });
        public Task<TwitchIdentity> ValidateAsync(string access, CancellationToken token)
        {
            ValidationCount++;
            if (ExpiredAccess && access == TestTokens.AccessToken) throw new TwitchAuthorizationException();
            return Task.FromResult(new TwitchIdentity(TwitchApplication.ClientId, WrongIdentity ? "99" : "42", "channel", TwitchApplication.Scopes));
        }
        public Task RevokeAsync(string access, CancellationToken token)
        {
            Revokes++; RevokedAccess = access;
            return FailRevoke ? Task.FromException(new IOException()) : Task.CompletedTask;
        }
        public Task<bool> SubscribeAsync(string type, string version, Dictionary<string, string> condition, string session, string access, CancellationToken token) => Task.FromResult(true);
    }

    private sealed class FakeTwitchReceiver : ITwitchEventReceiver
    {
        internal int Starts, Stops;
        public async Task RunAsync(Func<CancellationToken, Task<TwitchTokens>> tokens, Func<bool, Task> connected, CancellationToken token)
        {
            await tokens(token); Starts++;
            try { await connected(false); await Task.Delay(Timeout.Infinite, token); }
            finally { Stops++; }
        }
    }

    private sealed class TwitchHarness : IAsyncDisposable
    {
        internal readonly TwitchConnectionService Service;
        internal readonly FakeTwitchReceiver Receiver = new();
        private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
        private readonly Channel<CompanionStatus> statuses = Channel.CreateUnbounded<CompanionStatus>();
        private readonly Task running;
        internal TwitchHarness(FakeTwitchApi api, FakeTwitchStore store)
        {
            Service = new(api, store, Receiver, (status, token) => statuses.Writer.WriteAsync(status, token).AsTask());
            running = Service.RunAsync(cancellation.Token);
        }
        internal async Task<CompanionStatus> WaitAsync(string code)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true) { var status = await statuses.Reader.ReadAsync(timeout.Token); if (status.MessageCode == code) return status; }
        }
        public async ValueTask DisposeAsync() { cancellation.Cancel(); await running; cancellation.Dispose(); }
    }
}
