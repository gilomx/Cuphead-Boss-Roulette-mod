using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.Twitch;

internal interface ITwitchEventReceiver
{
    Task RunAsync(Func<CancellationToken, Task<TwitchTokens>> getTokens,
        Func<bool, Task> connected, CancellationToken cancellationToken);
}

internal sealed class TwitchEventSub(ITwitchApi api, NdjsonWriter output) : ITwitchEventReceiver
{
    internal static readonly Uri Address = new("wss://eventsub.wss.twitch.tv/ws");

    public async Task RunAsync(Func<CancellationToken, Task<TwitchTokens>> getTokens,
        Func<bool, Task> connected, CancellationToken cancellationToken)
    {
        var tokens = await getTokens(cancellationToken);
        var session = await OpenAsync(Address, cancellationToken);
        var limited = false;
        try
        {
            var user = tokens.UserId;
            var broadcaster = new Dictionary<string, string> { ["broadcaster_user_id"] = user };
            var chat = new Dictionary<string, string>(broadcaster) { ["user_id"] = user };
            if (!await api.SubscribeAsync("channel.chat.message", "1", chat, session.Id, tokens.AccessToken, cancellationToken))
                throw new TwitchAuthorizationException();
            var follows = new Dictionary<string, string>(broadcaster) { ["moderator_user_id"] = user };
            limited |= !await api.SubscribeAsync("channel.follow", "2", follows, session.Id, tokens.AccessToken, cancellationToken);
            foreach (var type in new[] { "channel.subscribe", "channel.subscription.gift", "channel.subscription.message",
                "channel.cheer", "channel.channel_points_custom_reward_redemption.add" })
                limited |= !await api.SubscribeAsync(type, "1", broadcaster, session.Id, tokens.AccessToken, cancellationToken);
            await connected(limited);

            while (!cancellationToken.IsCancellationRequested)
            {
                // The owner validates at startup, hourly, and on reconnect/401.
                tokens = await getTokens(cancellationToken);
                using var message = await ReadAsync(session.Socket, session.TimeoutSeconds, cancellationToken);
                var root = message.RootElement;
                var kind = Kind(root);
                if (kind == "session_reconnect")
                {
                    var address = TwitchApi.Text(root.GetProperty("payload").GetProperty("session"), "reconnect_url");
                    if (!IsReconnectAddress(address)) throw new InvalidDataException("Invalid EventSub handoff address.");
                    // Keep consuming the old connection until the replacement welcomes us.
                    using var handoffCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var opening = OpenAsync(new Uri(address), handoffCancellation.Token);
                    Task<ReceivedMessage>? pending = null;
                    try
                    {
                        while (!opening.IsCompleted)
                        {
                            pending ??= ReadAsync(session.Socket, session.TimeoutSeconds, handoffCancellation.Token);
                            if (await Task.WhenAny(opening, pending) == opening) break;
                            using var oldMessage = await pending;
                            pending = null;
                            await ObserveAsync(oldMessage, user, cancellationToken);
                        }
                        var replacement = await opening;
                        handoffCancellation.Cancel();
                        if (pending != null)
                        {
                            try { using var last = await pending; await ObserveAsync(last, user, cancellationToken); }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                        }
                        session.Socket.Dispose();
                        session = replacement;
                    }
                    catch
                    {
                        handoffCancellation.Cancel();
                        if (opening.IsCompletedSuccessfully) opening.Result.Socket.Dispose();
                        else { try { (await opening).Socket.Dispose(); } catch { } }
                        throw;
                    }
                }
                else await ObserveAsync(message, user, cancellationToken);
            }
        }
        finally { session.Socket.Dispose(); }
    }

    private async Task ObserveAsync(ReceivedMessage message, string user, CancellationToken cancellationToken)
    {
        var root = message.RootElement;
        var kind = Kind(root);
        if (kind == "revocation")
        {
            var status = TwitchApi.Text(root.GetProperty("payload").GetProperty("subscription"), "status");
            if (status is "authorization_revoked" or "user_removed") throw new TwitchAuthorizationException();
            throw new IOException("Twitch event subscription revoked.");
        }
        if (kind != "notification") return;
        var entry = TwitchEventNormalizer.Normalize(root, user, message.ReceivedAt);
        if (entry != null) await output.WriteEventAsync(entry, cancellationToken);
    }

    internal static bool IsReconnectAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == "wss" &&
        uri.Host == Address.Host && uri.Port == 443 && uri.AbsolutePath == "/ws" && uri.UserInfo.Length == 0;

    private static string Kind(JsonElement root) => TwitchApi.Text(root.GetProperty("metadata"), "message_type");

    private static async Task<Session> OpenAsync(Uri address, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await socket.ConnectAsync(address, timeout.Token);
            using var welcome = await ReadAsync(socket, 20, timeout.Token);
            if (Kind(welcome.RootElement) != "session_welcome") throw new InvalidDataException("Missing EventSub welcome.");
            var session = welcome.RootElement.GetProperty("payload").GetProperty("session");
            var id = TwitchApi.Text(session, "id");
            if (id.Length == 0) throw new InvalidDataException("Missing EventSub session.");
            var keepalive = session.TryGetProperty("keepalive_timeout_seconds", out var seconds) &&
                seconds.ValueKind == JsonValueKind.Number ? Math.Clamp(seconds.GetInt32(), 5, 600) : 10;
            return new(socket, id, keepalive + 5);
        }
        catch { socket.Dispose(); throw; }
    }

    private static async Task<ReceivedMessage> ReadAsync(ClientWebSocket socket, int seconds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(chunk), timeout.Token);
            if (result.MessageType != WebSocketMessageType.Text) throw new IOException("EventSub connection closed.");
            if (buffer.Length + result.Count > 1024 * 1024) throw new InvalidDataException("EventSub message too large.");
            buffer.Write(chunk, 0, result.Count);
        } while (!result.EndOfMessage);
        // Capture arrival before parsing or awaiting a reconnect handoff. A message
        // waiting for processing must retain its original round-fencing timestamp.
        var receivedAt = DateTimeOffset.UtcNow;
        return new(JsonDocument.Parse(Encoding.UTF8.GetString(buffer.ToArray())), receivedAt);
    }

    private sealed record ReceivedMessage(JsonDocument Document, DateTimeOffset ReceivedAt) : IDisposable
    {
        internal JsonElement RootElement => Document.RootElement;
        public void Dispose() => Document.Dispose();
    }

    private sealed record Session(ClientWebSocket Socket, string Id, int TimeoutSeconds);
}
