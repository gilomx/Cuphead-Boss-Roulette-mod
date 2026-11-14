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
            "\"broadcaster_user_name\":\"Canal\",\"reward\":{\"id\":\"reward-1\",\"title\":\"Reto\",\"cost\":500}")!;
        Equal("redemption", reward.Type); Equal("reward-1", reward.ItemId); Equal(500m, reward.TotalValue);
        Equal("42", reward.BroadcasterId); Equal("Canal", reward.ChannelName);
        using (var wire = JsonDocument.Parse(NdjsonWriter.Serialize(reward)))
        {
            Equal("42", wire.RootElement.GetProperty("broadcasterId").GetString());
            Equal("Canal", wire.RootElement.GetProperty("channelName").GetString());
        }
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
        Equal(true, handler.Body.Contains("channel%3Amanage%3Aredemptions"));
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

    private static async Task TwitchRewardApiAsync()
    {
        using var handler = new TwitchHttpHandler();
        using var http = new HttpClient(handler);
        var api = new TwitchApi(http);
        const string managedReward = """{"id":"managed-1","title":"Zanahoria","cost":2000,"is_enabled":false,"broadcaster_id":"42"}""";
        const string manualReward = """{"id":"manual-1","title":"Otro","cost":500,"is_enabled":true,"broadcaster_id":"42"}""";
        handler.SelectResponse = address => address.Contains("&only_manageable_rewards=true", StringComparison.Ordinal)
            ? "{\"data\":[" + managedReward + "]}" : "{\"data\":[" + managedReward + "," + manualReward + "]}";
        var readOnly = await api.ListRewardsAsync(TestTokens with { CanReadRewards = true }, default);
        Equal(2, readOnly.Length); Equal(false, readOnly[0].Manageable);
        Equal(false, handler.Address.Contains("only_manageable_rewards", StringComparison.Ordinal));

        var tokens = TestTokens with { CanReadRewards = true, CanManageRewards = true };
        var rewards = await api.ListRewardsAsync(tokens, default);
        Equal(2, rewards.Length); Equal(true, rewards[0].Manageable); Equal(false, rewards[1].Manageable);
        Equal("Bearer " + TestTokens.AccessToken, handler.Authorization);
        Equal(TwitchApplication.ClientId, handler.ClientId);
        handler.SelectResponse = null;
        handler.Response = "{\"data\":[" + managedReward + "]}";
        var create = new TwitchRewardCommand("create-1", "create", "42", "", "Zanahoria", 2000, true);
        var created = await api.ChangeRewardAsync(tokens, create, default);
        Equal("managed-1", created.Id); Equal(true, created.Manageable); Equal("POST", handler.Method);
        using (var body = JsonDocument.Parse(handler.Body))
        {
            Equal("Zanahoria", body.RootElement.GetProperty("title").GetString());
            Equal(2000L, body.RootElement.GetProperty("cost").GetInt64());
            Equal(false, body.RootElement.GetProperty("is_enabled").GetBoolean());
        }

        await api.ChangeRewardAsync(tokens, create with { Operation = "sync", RewardId = created.Id }, default);
        Equal("PATCH", handler.Method); Equal("{\"is_enabled\":true}", handler.Body);
        Equal("https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id=42&id=managed-1", handler.Address);
        await api.ChangeRewardAsync(tokens, create with { Operation = "update", RewardId = created.Id, Cost = 2500 }, default);
        Equal("{\"cost\":2500}", handler.Body);

        handler.Status = HttpStatusCode.Forbidden;
        try
        {
            await api.ChangeRewardAsync(tokens, create with { Operation = "sync", RewardId = "manual-1" }, default);
            throw new Exception("Twitch ownership rejection must propagate");
        }
        catch (TwitchRewardException exception) { Equal("reward_not_manageable", exception.Reason); }
    }

    private static async Task TwitchRewardCommandsAsync()
    {
        var api = new FakeTwitchApi { Scopes = ["user:read:chat", "channel:read:redemptions"] };
        await using var harness = new TwitchHarness(api, new FakeTwitchStore { Value = TestTokens });
        var connected = await harness.WaitAsync("connected");
        Equal("42", connected.BroadcasterId);
        Equal(false, connected.CanManageRewards);
        var commands = """
            {"kind":"twitch_reward_command","operation":"list","requestId":"abc"}
            {"kind":"twitch_reward_command","operation":"create","requestId":"wrong","broadcasterId":"99","title":"Zanahoria","cost":2000}
            {"kind":"twitch_reward_command","operation":"create","requestId":"scope","broadcasterId":"42","title":"Zanahoria","cost":2000}
            {"kind":"twitch_reward_command","operation":"create","requestId":"type","broadcasterId":"42","title":"Zanahoria","cost":"2000"}
            {"kind":"twitch_reward_command","operation":"create","requestId":"missing_channel","title":"Zanahoria","cost":2000}

            """;
        using var pipe = new MemoryStream(Encoding.UTF8.GetBytes(commands));
        await CompanionHost.ReadCommandsAsync(pipe, harness.Service, default);
        var replies = new Dictionary<string, TwitchRewardResult>();
        for (var i = 0; i < 5; i++)
        {
            var reply = await harness.NextRewardAsync();
            replies.Add(reply.RequestId, reply);
        }
        Equal(true, replies["abc"].Ok); Equal(false, replies["abc"].CanManage);
        Equal("wrong_broadcaster", replies["wrong"].Error);
        Equal("manage_permission_required", replies["scope"].Error);
        Equal("invalid_request", replies["type"].Error);
        Equal("invalid_request", replies["missing_channel"].Error);
        await using var stream = new MemoryStream();
        await using var writer = new NdjsonWriter(stream);
        await writer.WriteTwitchRewardAsync(replies["abc"], default);
        var wire = Encoding.UTF8.GetString(stream.ToArray());
        Equal(false, wire.Contains(TestTokens.AccessToken)); Equal(false, wire.Contains(TestTokens.RefreshToken));
        using var root = JsonDocument.Parse(wire);
        Equal(1, root.RootElement.GetProperty("protocolVersion").GetInt32());
        Equal("twitch_reward_result", root.RootElement.GetProperty("kind").GetString());
        using var list = JsonDocument.Parse(root.RootElement.GetProperty("rewardsJson").GetString()!);
        Equal("reward-1", list.RootElement[0].GetProperty("id").GetString());
        Equal("Canje", list.RootElement[0].GetProperty("title").GetString());
    }

    private static async Task TwitchRewardLimitsAsync()
    {
        static TwitchRewardCommand Parse(string limits, string operation = "update") =>
            TwitchRewardCommand.Parse(
                "{\"kind\":\"twitch_reward_command\",\"operation\":\"" + operation +
                "\",\"requestId\":\"limits\",\"broadcasterId\":\"42\",\"rewardId\":\"reward-1\",\"title\":\"Canje\",\"cost\":500,\"enabled\":true" +
                limits + "}")!;

        var omitted = Parse("");
        Equal(true, omitted.Valid);
        Equal(null, omitted.CooldownSeconds);
        Equal(null, omitted.MaxPerStream);
        Equal(null, omitted.MaxPerUserPerStream);
        foreach (var invalid in new[] { ",\"cooldownSeconds\":59", ",\"cooldownSeconds\":604801", ",\"maxPerStream\":\"4\"", ",\"maxPerStream\":2147483648",
            ",\"maxPerUserPerStream\":null", ",\"maxPerUserPerStream\":1.5", ",\"maxPerUserPerStream\":-1" })
            Equal(false, Parse(invalid).Valid);
        Equal(false, Parse(",\"maxPerStream\":2", "sync").Valid);
        Equal(false, Parse(",\"cooldownSeconds\":0", "list").Valid);
        var command = Parse(",\"cooldownSeconds\":60,\"maxPerStream\":2147483647,\"maxPerUserPerStream\":0", "create");
        Equal(true, command.Valid);

        using var handler = new TwitchHttpHandler
        {
            Response = """
                {"data":[{"id":"reward-1","title":"Canje","cost":500,"is_enabled":false,"broadcaster_id":"42",
                  "global_cooldown_setting":{"is_enabled":true,"global_cooldown_seconds":30},
                  "max_per_stream_setting":{"is_enabled":false,"max_per_stream":20},
                  "max_per_user_per_stream_setting":{"is_enabled":true,"max_per_user_per_stream":3}}]}
                """,
        };
        using var http = new HttpClient(handler);
        var api = new TwitchApi(http);
        var reward = await api.ChangeRewardAsync(TestTokens, command, default);
        Equal(30, reward.CooldownSeconds); Equal(0, reward.MaxPerStream); Equal(3, reward.MaxPerUserPerStream);
        using (var response = JsonDocument.Parse(JsonSerializer.Serialize(reward, CompanionJsonContext.Default.TwitchReward)))
        {
            Equal(30, response.RootElement.GetProperty("cooldownSeconds").GetInt32());
            Equal(0, response.RootElement.GetProperty("maxPerStream").GetInt32());
            Equal(3, response.RootElement.GetProperty("maxPerUserPerStream").GetInt32());
        }
        using (var body = JsonDocument.Parse(handler.Body))
        {
            var payload = body.RootElement;
            Equal(false, payload.GetProperty("is_enabled").GetBoolean());
            Equal(true, payload.GetProperty("is_global_cooldown_enabled").GetBoolean());
            Equal(60, payload.GetProperty("global_cooldown_seconds").GetInt32());
            Equal(true, payload.GetProperty("is_max_per_stream_enabled").GetBoolean());
            Equal(int.MaxValue, payload.GetProperty("max_per_stream").GetInt32());
            Equal(false, payload.GetProperty("is_max_per_user_per_stream_enabled").GetBoolean());
            Equal(0, payload.GetProperty("max_per_user_per_stream").GetInt32());
        }
        await api.ChangeRewardAsync(TestTokens, omitted with { CooldownSeconds = 0 }, default);
        Equal("{\"cost\":500,\"is_global_cooldown_enabled\":false,\"global_cooldown_seconds\":0}", handler.Body);
    }

    private static async Task TwitchRewardDisconnectAsync()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeTwitchApi {
            ChangeReward = async (command, token) => {
                started.TrySetResult();
                using var registration = token.Register(() => canceled.TrySetResult());
                await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
                return new("slow", command.Title, command.Cost, false, true, command.BroadcasterId);
            },
        };
        await using var harness = new TwitchHarness(api, new FakeTwitchStore { Value = TestTokens });
        Equal(true, (await harness.WaitAsync("connected")).CanManageRewards);
        await harness.Service.QueueRewardCommandAsync(
            """{"kind":"twitch_reward_command","operation":"create","requestId":"slow","broadcasterId":"42","title":"Zanahoria","cost":2000}""", default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Service.TryCommand("disconnect:1");
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.WaitAsync("not_connected");
        release.TrySetResult();
        var result = await harness.NextRewardAsync();
        Equal(false, result.Ok);
        Equal("session_changed", result.Error);
        Equal("", result.RewardJson);
    }

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

    private static async Task TwitchLocalModeAsync()
    {
        var api = new FakeTwitchApi();
        var store = new FakeTwitchStore { Value = TestTokens };
        var local = new FakeTwitchReceiver();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var statuses = Channel.CreateUnbounded<CompanionStatus>();
        var service = new TwitchConnectionService(api, store, new FakeTwitchReceiver(),
            (status, token) => statuses.Writer.WriteAsync(status, token).AsTask(), local);
        var running = service.RunAsync(cancellation.Token);
        async Task<CompanionStatus> Wait(string code) {
            while (true) { var next = await statuses.Reader.ReadAsync(cancellation.Token); if (next.MessageCode == code) return next; }
        }
        try {
            await Wait("connected");
            Equal(true, service.TryCommand("test:1"));
            var connected = await Wait("test_connected");
            Equal(true, connected.TestMode); Equal(false, connected.Authorized);
            Equal("twitch_cli", connected.Account); Equal(1, local.Starts);
            Equal(TestTokens, store.Value); Equal(0, store.Writes); Equal(0, api.Revokes);
            service.TryCommand("cancel:2");
            var restored = await Wait("connected");
            Equal(false, restored.TestMode); Equal(true, restored.Authorized);
            Equal(TestTokens, store.Value); Equal(0, store.Writes); Equal(0, api.Revokes);
            Equal(true, TwitchEventSub.IsReconnectAddress("ws://127.0.0.1:8080/ws?reconnect=abc", true));
            Equal(false, TwitchEventSub.IsReconnectAddress("ws://evil.invalid:8080/ws", true));
            Equal(false, TwitchEventSub.IsReconnectAddress("ws://127.0.0.1:8080/ws"));
            var handler = new TwitchHttpHandler(); using var http = new HttpClient(handler);
            await new TwitchLocalTestApi(http).SubscribeAsync("channel.follow", "2", new() { ["broadcaster_user_id"] = "42" }, "session", "PRIVATE", default);
            Equal(false, handler.Body.Contains("PRIVATE"));
            Equal(false, handler.Body.Contains(TwitchApplication.ClientId));
            Equal("Bearer local-test", handler.Authorization);
            Equal("pichi-cli-tests", handler.ClientId);
            Equal("http://127.0.0.1:8080/eventsub/subscriptions", handler.Address);
        }
        finally { cancellation.Cancel(); await running; }
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
        internal string Authorization = "", ClientId = "", Address = "", Method = "";
        internal Func<string, string>? SelectResponse;
        internal HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString() ?? "";
            ClientId = request.Headers.TryGetValues("Client-Id", out var ids) ? ids.Single() : "";
            var address = request.RequestUri?.ToString() ?? "";
            Address = address;
            Method = request.Method.Method;
            Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(Status) { Content = new StringContent(SelectResponse?.Invoke(address) ?? Response) };
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
        internal string[] Scopes = TwitchApplication.Scopes;
        internal Func<TwitchRewardCommand, CancellationToken, Task<TwitchReward>>? ChangeReward;
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
            return Task.FromResult(new TwitchIdentity(TwitchApplication.ClientId, WrongIdentity ? "99" : "42", "channel", Scopes));
        }
        public Task RevokeAsync(string access, CancellationToken token)
        {
            Revokes++; RevokedAccess = access;
            return FailRevoke ? Task.FromException(new IOException()) : Task.CompletedTask;
        }
        public Task<bool> SubscribeAsync(string type, string version, Dictionary<string, string> condition, string session, string access, CancellationToken token) => Task.FromResult(true);
        public Task<TwitchReward[]> ListRewardsAsync(TwitchTokens tokens, CancellationToken token) =>
            Task.FromResult<TwitchReward[]>([new("reward-1", "Canje", 500, true, false, tokens.UserId)]);
        public Task<TwitchReward> ChangeRewardAsync(TwitchTokens tokens, TwitchRewardCommand command, CancellationToken token) =>
            ChangeReward?.Invoke(command, token) ?? throw new InvalidOperationException("Unexpected Twitch mutation");
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
        private readonly Channel<TwitchRewardResult> rewards = Channel.CreateUnbounded<TwitchRewardResult>();
        private readonly Task running;
        internal TwitchHarness(FakeTwitchApi api, FakeTwitchStore store)
        {
            Service = new(api, store, Receiver, (status, token) => statuses.Writer.WriteAsync(status, token).AsTask(),
                publishReward: (reward, token) => rewards.Writer.WriteAsync(reward, token).AsTask());
            running = Service.RunAsync(cancellation.Token);
        }
        internal async Task<TwitchRewardResult> NextRewardAsync() =>
            await rewards.Reader.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        internal async Task<CompanionStatus> WaitAsync(string code)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true) { var status = await statuses.Reader.ReadAsync(timeout.Token); if (status.MessageCode == code) return status; }
        }
        public async ValueTask DisposeAsync() { cancellation.Cancel(); await running; cancellation.Dispose(); }
    }
}
