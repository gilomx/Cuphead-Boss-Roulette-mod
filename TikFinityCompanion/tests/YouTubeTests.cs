using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.YouTube;
using LaPichiRuleta.TikFinity.YouTube.Wire;
using MessageType = LaPichiRuleta.TikFinity.YouTube.Wire.LiveChatMessageSnippet.Types.TypeWrapper.Types.Type;

namespace LaPichiRuleta.TikFinity.Tests;

internal static partial class Program
{
    private static readonly YouTubeTokens YouTubeTestTokens = new("YT-ACCESS-PRIVATE", "YT-REFRESH-PRIVATE",
        DateTimeOffset.UtcNow.AddHours(1), "channel42", "Canal de prueba");

    private static LiveChatMessage YouTubeMessage(int type, string id = "m1") => new() {
        Id = id, Snippet = new() { Type = (MessageType)type, LiveChatId = "chat42", AuthorChannelId = "viewer1" },
        AuthorDetails = new() { ChannelId = "viewer1", DisplayName = "Viewer" },
    };

    private static void YouTubeEvents()
    {
        var normalizer = new YouTubeEventNormalizer();
        var vote = YouTubeMessage(1); vote.Snippet.TextMessageDetails = new() { MessageText = " 2 " };
        Equal(null, normalizer.Normalize(vote, "channel42", "another-chat", FixedTime));
        var entry = normalizer.Normalize(vote, "channel42", "chat42", FixedTime)!;
        Equal("chat", entry.Type); Equal("2", entry.ChatText); Equal(FixedTime, entry.ReceivedAt);
        Equal("youtube", entry.Platform); Equal("viewer1", entry.UserId);
        Equal(null, normalizer.Normalize(vote, "channel42", "chat42", FixedTime));
        var invalid = YouTubeMessage(1, "invalid"); invalid.Snippet.TextMessageDetails = new() { MessageText = "hola 2" };
        Equal(null, normalizer.Normalize(invalid, "channel42", "chat42", FixedTime));
        var history = YouTubeMessage(1, "history"); history.Snippet.TextMessageDetails = new() { MessageText = "1" };
        Equal(null, normalizer.Normalize(history, "channel42", "chat42", FixedTime, history: true));
        Equal(null, normalizer.Normalize(history, "channel42", "chat42", FixedTime));

        var money = YouTubeMessage(15, "super"); money.Snippet.SuperChatDetails = new() { AmountMicros = 10500000, Currency = "MXN" };
        entry = normalizer.Normalize(money, "channel42", "chat42", FixedTime)!;
        Equal(10.5m, entry.TotalValue); Equal("MXN", entry.Currency); Equal("money", entry.Unit);
        var sticker = YouTubeMessage(16, "sticker"); sticker.Snippet.SuperStickerDetails = new() { AmountMicros = 2500000, Currency = "USD" };
        Equal(2.5m, normalizer.Normalize(sticker, "channel42", "chat42", FixedTime)!.TotalValue);
        var member = YouTubeMessage(7, "member"); member.Snippet.NewSponsorDetails = new() { MemberLevelName = "Gold" };
        Equal("new", normalizer.Normalize(member, "channel42", "chat42", FixedTime)!.Unit);
        member.Id = "upgrade"; member.Snippet.NewSponsorDetails.IsUpgrade = true;
        Equal("upgrade", normalizer.Normalize(member, "channel42", "chat42", FixedTime)!.Unit);
        var gifts = YouTubeMessage(18, "memberships"); gifts.Snippet.MembershipGiftingDetails = new() { GiftMembershipsCount = 5 };
        entry = normalizer.Normalize(gifts, "channel42", "chat42", FixedTime)!;
        Equal("gift", entry.Unit); Equal(5, entry.Count);
        Equal(null, normalizer.Normalize(YouTubeMessage(19, "recipient"), "channel42", "chat42", FixedTime));
        Equal("milestone", normalizer.Normalize(YouTubeMessage(17, "shared"), "channel42", "chat42", FixedTime)!.Unit);
    }

    private static void YouTubeJewels()
    {
        var normalizer = new YouTubeEventNormalizer();
        var gift = YouTubeMessage(21);
        gift.Snippet.GiftDetails = new() { GiftName = "Rose", JewelsAmount = 10, ComboCount = 2, GiftUrl = "https://example.invalid/rose.png" };
        var entry = normalizer.Normalize(gift, "channel42", "chat42", FixedTime)!;
        Equal(2, entry.Count); Equal(20m, entry.TotalValue); Equal("jewel", entry.Unit);
        Equal(null, normalizer.Normalize(gift, "channel42", "chat42", FixedTime));
        gift.Snippet.GiftDetails.ComboCount = 5;
        var delta = normalizer.Normalize(gift, "channel42", "chat42", FixedTime)!;
        Equal(3, delta.Count); Equal(30m, delta.TotalValue); Equal(false, entry.IdempotencyKey == delta.IdempotencyKey);
        gift.Snippet.GiftDetails.ComboCount = 3;
        Equal(null, normalizer.Normalize(gift, "channel42", "chat42", FixedTime));
        gift.Id = "old-combo"; gift.Snippet.GiftDetails.ComboCount = 4;
        Equal(null, normalizer.Normalize(gift, "channel42", "chat42", FixedTime, history: true));
        gift.Snippet.GiftDetails.ComboCount = 5; gift.Snippet.GiftDetails.GiftUrl = "file:///private.png";
        delta = normalizer.Normalize(gift, "channel42", "chat42", FixedTime)!;
        Equal(1, delta.Count); Equal("", delta.ItemImageUrl);
    }

    private static async Task YouTubeLoopbackAsync()
    {
        var api = new FakeYouTubeApi();
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var auth = new YouTubeAuthorization("public-client.apps.googleusercontent.com", _ => { });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var flow = auth.AuthorizeAsync(api, (url, _) => { ready.SetResult(url); return Task.CompletedTask; }, cancellation.Token);
        var authorization = new Uri(await ready.Task);
        var query = ParseTestQuery(authorization.Query);
        Equal("S256", query["code_challenge_method"]); Equal("offline", query["access_type"]);
        Equal(YouTubeApplication.Scope, query["scope"]);
        Equal(false, authorization.Query.Contains("client_secret")); Equal(false, authorization.Query.Contains("code_verifier"));
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
        var redirect = query["redirect_uri"];
        Equal(HttpStatusCode.BadRequest, (await client.GetAsync(redirect + "?state=wrong&code=stolen")).StatusCode);
        Equal(0, api.Exchanges);
        Equal(HttpStatusCode.OK, (await client.GetAsync(redirect + "?state=" + query["state"] + "&code=accepted-code")).StatusCode);
        Equal(YouTubeTestTokens, await flow);
        Equal(1, api.Exchanges); Equal("accepted-code", api.Code);
        Equal(query["code_challenge"], YouTubeAuthorization.Challenge(api.Verifier));
        Equal(redirect, api.RedirectUri);
    }

    private static Dictionary<string, string> ParseTestQuery(string query) => query.TrimStart('?').Split('&')
        .Select(pair => pair.Split('=', 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

    private static async Task YouTubeLifecycleAsync()
    {
        var api = new FakeYouTubeApi();
        var store = new FakeYouTubeStore { Value = YouTubeTestTokens with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) } };
        await using var harness = new YouTubeHarness(api, store);
        var ready = await harness.WaitAsync("waiting_live");
        Equal(true, ready.Authorized); Equal("Canal de prueba", ready.Account);
        Equal(1, store.Writes); Equal("YT-ROTATED-ACCESS", store.Value!.AccessToken);
        store.FailDelete = true; harness.Service.TryCommand("disconnect:1");
        await harness.WaitAsync("storage_delete_error"); Equal(1, harness.Receiver.Stops);
        store.FailDelete = false; api.FailRevoke = true; harness.Service.TryCommand("disconnect:2");
        await harness.WaitAsync("revocation_unconfirmed");
        Equal(null, store.Value); Equal("YT-REFRESH-PRIVATE", api.RevokedToken);
    }

    private static async Task YouTubeCancelAsync()
    {
        var auth = new FakeYouTubeAuthorization { Delayed = true };
        var store = new FakeYouTubeStore();
        await using var harness = new YouTubeHarness(new FakeYouTubeApi(), store, auth);
        await harness.WaitAsync("not_connected"); harness.Service.TryCommand("connect:1");
        var pending = await harness.WaitAsync("authorization_pending"); Equal(false, pending.Authorized);
        harness.Service.TryCommand("cancel:2");
        await auth.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        auth.Result.SetResult(YouTubeTestTokens);
        await harness.WaitAsync("authorization_cancelled");
        Equal(0, store.Writes); Equal(0, harness.Receiver.Starts);
    }

    private static async Task YouTubeErrorsAsync()
    {
        await using (var unconfigured = new YouTubeHarness(new FakeYouTubeApi { Configured = false }, new FakeYouTubeStore()))
        {
            await unconfigured.WaitAsync("application_not_configured");
            Equal(0, unconfigured.Receiver.Starts);
            unconfigured.Service.TryCommand("connect:1"); await unconfigured.WaitAsync("application_not_configured");
        }
        await using (var failed = new YouTubeHarness(new FakeYouTubeApi(), new FakeYouTubeStore { FailRead = true }))
        { await failed.WaitAsync("storage_error"); Equal(0, failed.Receiver.Starts); }
        var store = new FakeYouTubeStore { Value = YouTubeTestTokens };
        await using var wrong = new YouTubeHarness(new FakeYouTubeApi { WrongIdentity = true }, store);
        await wrong.WaitAsync("authorization_required"); Equal(null, store.Value); Equal(0, wrong.Receiver.Starts);
    }

    private static void YouTubeDesktopConfiguration()
    {
        const string id = "public.apps.googleusercontent.com";
        string Read(string json) => YouTubeApplication.ReadClientSecret(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)), id);
        Equal("", YouTubeApplication.ReadClientSecret(null, id));
        Equal("", Read("invalid JSON")); Equal("", Read("[]"));
        Equal("", Read("{\"web\":{\"client_id\":\"public.apps.googleusercontent.com\",\"client_secret\":\"APP-PRIVATE\"}}"));
        Equal("", Read("{\"installed\":{\"client_id\":\"wrong-client\",\"client_secret\":\"APP-PRIVATE\"}}"));
        Equal("", Read("{\"installed\":{\"client_id\":\"public.apps.googleusercontent.com\"}}"));
        Equal("APP-PRIVATE", Read("{\"installed\":{\"client_id\":\"public.apps.googleusercontent.com\",\"client_secret\":\"APP-PRIVATE\"}}"));
        using var handler = new TwitchHttpHandler(); using var http = new HttpClient(handler);
        var unavailable = new YouTubeApi(http, id, ""); Equal(false, unavailable.Configured);
        try { unavailable.ExchangeAsync("CODE", "VERIFIER", "http://127.0.0.1:1234/oauth2/callback", default).GetAwaiter().GetResult(); throw new Exception("Unconfigured client should fail"); }
        catch (YouTubeApiException ex) { Equal("application_not_configured", ex.Code); }
        Equal("", handler.Body);
    }

    private static void YouTubeHttpApi()
    {
        using var handler = new TwitchHttpHandler { Response = "{\"access_token\":\"ACCESS\",\"refresh_token\":\"REFRESH\",\"expires_in\":3600}" };
        using var http = new HttpClient(handler);
        var api = new YouTubeApi(http, "public.apps.googleusercontent.com", "YT-APP-PRIVATE");
        Equal(true, api.Configured);
        var tokens = api.ExchangeAsync("CODE", "VERIFIER", "http://127.0.0.1:1234/oauth2/callback", default).GetAwaiter().GetResult();
        Equal("ACCESS", tokens.AccessToken); Equal("REFRESH", tokens.RefreshToken);
        Equal(true, handler.Body.Contains("client_secret=YT-APP-PRIVATE")); Equal(true, handler.Body.Contains("code_verifier=VERIFIER"));
        Equal(false, handler.Address.Contains("YT-APP-PRIVATE"));
        handler.Response = "{\"access_token\":\"ROTATED-ACCESS\",\"expires_in\":3600}";
        tokens = api.RefreshAsync(tokens, default).GetAwaiter().GetResult();
        Equal("ROTATED-ACCESS", tokens.AccessToken); Equal("REFRESH", tokens.RefreshToken);
        Equal(true, handler.Body.Contains("client_secret=YT-APP-PRIVATE")); Equal(true, handler.Body.Contains("refresh_token=REFRESH"));
        Equal(false, handler.Body.Contains("code_verifier"));
        handler.Response = "{\"items\":[{\"id\":\"video\",\"snippet\":{\"channelId\":\"channel42\",\"liveChatId\":\"chat42\"}},{\"id\":\"foreign\",\"snippet\":{\"channelId\":\"other\",\"liveChatId\":\"foreign-chat\"}}]}";
        var chats = api.FindLiveChatsAsync(YouTubeTestTokens, default).GetAwaiter().GetResult();
        Equal(1, chats.Count); Equal("chat42", chats[0].Id);
        Equal("Bearer YT-ACCESS-PRIVATE", handler.Authorization);
        Equal(false, handler.Address.Contains("ACCESS")); Equal(false, handler.Address.Contains("mine="));
        handler.Response = "{\"error\":{\"errors\":[{\"reason\":\"quotaExceeded\"}]}}"; handler.Status = HttpStatusCode.Forbidden;
        try { api.FindLiveChatsAsync(YouTubeTestTokens, default).GetAwaiter().GetResult(); throw new Exception("quota should fail"); }
        catch (YouTubeApiException ex) { Equal("quota_exceeded", ex.Code); }
        handler.Response = "{\"error\":\"invalid_request\",\"error_description\":\"client_secret is missing. YT-APP-PRIVATE\"}";
        handler.Status = HttpStatusCode.BadRequest;
        try { api.ExchangeAsync("CODE", "VERIFIER", "http://127.0.0.1:1234/oauth2/callback", default).GetAwaiter().GetResult(); throw new Exception("Desktop credential rejection should fail"); }
        catch (YouTubeApiException ex) { Equal("oauth_client_error", ex.Code); Equal(false, ex.Message.Contains("YT-APP-PRIVATE")); }
        handler.Response = "{\"error\":\"invalid_client\"}";
        try { api.RefreshAsync(tokens, default).GetAwaiter().GetResult(); throw new Exception("Invalid client should fail"); }
        catch (YouTubeApiException ex) { Equal("oauth_client_error", ex.Code); }
    }

    private static async Task YouTubeCursorAsync()
    {
        using var stream = new MemoryStream();
        await using var writer = new NdjsonWriter(stream);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var source = new FakeYouTubeTransport(cancellation);
        var receiver = new YouTubeChatReceiver(new FakeYouTubeApi { HasLive = true }, writer, source);
        Task RunAsync() => receiver.RunAsync(_ => Task.FromResult(YouTubeTestTokens), _ => Task.CompletedTask, cancellation.Token);
        try { await RunAsync(); throw new Exception("The transport should interrupt"); }
        catch (IOException) { }
        Equal("", source.Cursors[0]);
        try { await RunAsync(); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        Equal("cursor-2", source.Cursors[1]);
        var events = System.Text.Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Equal(2, events.Length); // History suppressed; resumed duplicate suppressed; fresh votes retained.
        using var first = JsonDocument.Parse(events[0]); using var second = JsonDocument.Parse(events[1]);
        Equal("live-vote", first.RootElement.GetProperty("eventId").GetString());
        Equal("resumed-vote", second.RootElement.GetProperty("eventId").GetString());
        receiver.Reset();
        using var freshCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        source.Cancellation = freshCancellation;
        try { await receiver.RunAsync(_ => Task.FromResult(YouTubeTestTokens), _ => Task.CompletedTask, freshCancellation.Token); }
        catch (OperationCanceledException) { }
        Equal("", source.Cursors[2]);
    }

    private sealed class FakeYouTubeTransport(CancellationTokenSource cancellation) : IYouTubeChatTransport
    {
        internal readonly List<string> Cursors = [];
        internal CancellationTokenSource Cancellation = cancellation;
        public async IAsyncEnumerable<LiveChatMessageListResponse> StreamAsync(LiveChatMessageListRequest request,
            YouTubeTokens tokens, [EnumeratorCancellation] CancellationToken token)
        {
            Cursors.Add(request.PageToken);
            await Task.Yield();
            LiveChatMessage Vote(string id) { var vote = YouTubeMessage(1, id); vote.Snippet.TextMessageDetails = new() { MessageText = "1" }; return vote; }
            if (Cursors.Count == 1)
            {
                var history = new LiveChatMessageListResponse { NextPageToken = "cursor-1" };
                history.Items.Add(Vote("history")); yield return history;
                var live = new LiveChatMessageListResponse { NextPageToken = "cursor-2" };
                live.Items.Add(Vote("live-vote")); yield return live;
                throw new IOException("Fake network interruption");
            }
            var resumed = new LiveChatMessageListResponse { NextPageToken = "cursor-3" };
            resumed.Items.Add(Vote("live-vote")); resumed.Items.Add(Vote("resumed-vote")); yield return resumed;
            Cancellation.Cancel(); token.ThrowIfCancellationRequested();
        }
    }

    private sealed class FakeYouTubeStore : IYouTubeCredentialStore
    {
        internal YouTubeTokens? Value;
        internal bool FailRead, FailDelete;
        internal int Writes;
        public YouTubeTokens? Read() => FailRead ? throw new IOException() : Value;
        public void Write(YouTubeTokens tokens) { Value = tokens; Writes++; }
        public void Delete() { if (FailDelete) throw new IOException(); Value = null; }
    }
    private sealed class FakeYouTubeApi : IYouTubeApi
    {
        public bool Configured { get; set; } = true;
        internal bool FailRevoke, WrongIdentity, HasLive;
        internal int Exchanges;
        internal string Code = "", Verifier = "", RedirectUri = "", RevokedToken = "";
        public Task<YouTubeTokens> ExchangeAsync(string code, string verifier, string redirectUri, CancellationToken token)
        { Exchanges++; Code = code; Verifier = verifier; RedirectUri = redirectUri; return Task.FromResult(YouTubeTestTokens); }
        public Task<YouTubeTokens> RefreshAsync(YouTubeTokens tokens, CancellationToken token) =>
            Task.FromResult(tokens with { AccessToken = "YT-ROTATED-ACCESS", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        public Task<YouTubeIdentity> IdentifyAsync(string access, CancellationToken token) =>
            Task.FromResult(new YouTubeIdentity(WrongIdentity ? "wrong" : "channel42", "Canal de prueba"));
        public Task<IReadOnlyList<YouTubeLiveChat>> FindLiveChatsAsync(YouTubeTokens tokens, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<YouTubeLiveChat>>(HasLive ? [new("chat42", "video42")] : []);
        public Task RevokeAsync(string token, CancellationToken cancellation)
        { RevokedToken = token; return FailRevoke ? Task.FromException(new IOException()) : Task.CompletedTask; }
    }
    private sealed class FakeYouTubeAuthorization : IYouTubeAuthorization
    {
        internal bool Delayed;
        internal readonly TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<YouTubeTokens> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<YouTubeTokens> AuthorizeAsync(IYouTubeApi api, Func<string, DateTimeOffset, Task> ready, CancellationToken token)
        {
            await ready("https://accounts.google.com/o/oauth2/v2/auth?state=PUBLIC", DateTimeOffset.UtcNow.AddMinutes(5));
            if (!Delayed) return YouTubeTestTokens;
            using var registration = token.Register(() => Cancelled.TrySetResult());
            return await Result.Task;
        }
    }
    private sealed class FakeYouTubeReceiver : IYouTubeEventReceiver
    {
        public void Reset() { }
        internal int Starts, Stops;
        public async Task RunAsync(Func<CancellationToken, Task<YouTubeTokens>> tokens, Func<bool, Task> connected, CancellationToken token)
        {
            await tokens(token); Starts++;
            try { await connected(false); await Task.Delay(Timeout.Infinite, token); }
            finally { Stops++; }
        }
    }
    private sealed class YouTubeHarness : IAsyncDisposable
    {
        internal readonly YouTubeConnectionService Service;
        internal readonly FakeYouTubeReceiver Receiver = new();
        private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
        private readonly Channel<CompanionStatus> statuses = Channel.CreateUnbounded<CompanionStatus>();
        private readonly Task running;
        internal YouTubeHarness(FakeYouTubeApi api, FakeYouTubeStore store, FakeYouTubeAuthorization? authorization = null)
        {
            Service = new(api, store, authorization ?? new(), Receiver, (status, token) => statuses.Writer.WriteAsync(status, token).AsTask());
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
