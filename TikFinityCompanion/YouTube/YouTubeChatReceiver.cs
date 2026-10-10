using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.YouTube.Wire;

namespace LaPichiRuleta.TikFinity.YouTube;

internal sealed class YouTubeChatReceiver(IYouTubeApi api, NdjsonWriter output, IYouTubeChatTransport? transport = null) : IYouTubeEventReceiver
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> cursors = new(StringComparer.Ordinal);
    private YouTubeEventNormalizer normalizer = new();
    public void Reset() { cursors.Clear(); normalizer = new(); }

    public async Task RunAsync(Func<CancellationToken, Task<YouTubeTokens>> currentTokens, Func<bool, Task> connected,
        CancellationToken cancellationToken)
    {
        using var grpc = transport == null ? new YouTubeGrpcTransport() : null;
        var source = transport ?? grpc!;
        while (!cancellationToken.IsCancellationRequested)
        {
            var tokens = await currentTokens(cancellationToken);
            var chats = await api.FindLiveChatsAsync(tokens, cancellationToken);
            if (chats.Count == 0)
            {
                await connected(false);
                // Discovery costs one quota unit. Chat delivery uses a persistent stream.
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                continue;
            }
            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = chats.Select(chat => StreamAsync(chat)).ToArray();
            try { await await Task.WhenAny(tasks); }
            finally
            {
                cycle.Cancel();
                try { await Task.WhenAll(tasks); }
                catch (OperationCanceledException) { }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            async Task StreamAsync(YouTubeLiveChat chat)
            {
                var current = await currentTokens(cycle.Token);
                var key = current.ChannelId + ":" + chat.Id;
                cursors.TryGetValue(key, out var cursor);
                var history = string.IsNullOrEmpty(cursor);
                var request = new LiveChatMessageListRequest {
                    LiveChatId = chat.Id, PageToken = cursor ?? "", ProfileImageSize = 88,
                };
                request.Part.Add(new[] { "id", "snippet", "authorDetails" });
                await foreach (var response in source.StreamAsync(request, current, cycle.Token))
                {
                    await connected(true);
                    foreach (var message in response.Items)
                    {
                        var normalized = normalizer.Normalize(message, current.ChannelId, chat.Id, DateTimeOffset.UtcNow, history);
                        if (normalized != null) await output.WriteEventAsync(normalized, cycle.Token);
                    }
                    history = false;
                    if (response.NextPageToken.Length > 0) cursors[key] = response.NextPageToken;
                    if (response.OfflineAt.Length > 0) { cursors.TryRemove(key, out _); return; }
                }
            }
        }
    }
}
