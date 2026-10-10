using System.Threading.Channels;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.YouTube;

internal sealed class YouTubeConnectionService(IYouTubeApi api, IYouTubeCredentialStore store,
    IYouTubeAuthorization authorization, IYouTubeEventReceiver receiver,
    Func<CompanionStatus, CancellationToken, Task> publish)
{
    private readonly Channel<(string Action, long Revision)> commands = Channel.CreateBounded<(string, long)>(16);
    private long revision;
    private YouTubeTokens? tokens;
    private CancellationTokenSource? activityCancellation;
    private Task? activity;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private string lastStatus = "";

    internal bool TryCommand(string command)
    {
        var parts = command.Split(':');
        return parts.Length == 2 && parts[0] is "connect" or "disconnect" or "cancel" &&
            long.TryParse(parts[1], out var value) && value > 0 && commands.Writer.TryWrite((parts[0], value));
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (api.Configured)
            {
                var storageFailed = false;
                try { tokens = store.Read(); }
                catch { storageFailed = true; await StatusAsync("error", "storage_error", cancellationToken); }
                if (tokens != null) Start(false, cancellationToken);
                else if (!storageFailed) await StatusAsync("disconnected", "not_connected", cancellationToken);
            }
            else await StatusAsync("disconnected", "application_not_configured", cancellationToken);
            await foreach (var command in commands.Reader.ReadAllAsync(cancellationToken))
            {
                if (command.Revision <= revision) continue;
                await StopAsync(); revision = command.Revision;
                if (command.Action == "disconnect") await ForgetAsync(cancellationToken);
                else if (command.Action == "cancel")
                {
                    if (tokens != null) Start(false, cancellationToken);
                    else await StatusAsync("disconnected", "authorization_cancelled", cancellationToken);
                }
                else if (!api.Configured) await StatusAsync("disconnected", "application_not_configured", cancellationToken);
                else if (tokens != null) Start(false, cancellationToken);
                else { receiver.Reset(); Start(true, cancellationToken); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { await StopAsync(); }
    }

    private void Start(bool authorize, CancellationToken cancellationToken)
    {
        activityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activity = ActivityAsync(authorize, activityCancellation.Token);
    }

    private async Task StopAsync()
    {
        activityCancellation?.Cancel();
        if (activity != null) try { await activity; } catch (OperationCanceledException) { }
        activityCancellation?.Dispose(); activityCancellation = null; activity = null;
    }

    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        receiver.Reset();
        var former = tokens;
        try { store.Delete(); }
        catch { await StatusAsync("error", "storage_delete_error", cancellationToken); return; }
        tokens = null;
        await StatusAsync("disconnected", "not_connected", cancellationToken);
        if (former == null) return;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await api.RevokeAsync(former.RefreshToken, deadline.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { await StatusAsync("disconnected", "revocation_unconfirmed", cancellationToken); }
    }

    private async Task ActivityAsync(bool authorize, CancellationToken cancellationToken)
    {
        try
        {
            if (authorize)
            {
                await StatusAsync("connecting", "requesting_authorization", cancellationToken);
                YouTubeTokens candidate;
                try
                {
                    candidate = await authorization.AuthorizeAsync(api, (url, expires) => publish(new CompanionStatus {
                        ConnectionId = "youtube", State = "connecting", Message = "", MessageCode = "authorization_pending",
                        VerificationUri = url, ExpiresAt = expires.ToString("O"), ControlRevision = revision,
                        OccurredAt = DateTimeOffset.UtcNow,
                    }, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { await StatusAsync("disconnected", "authorization_expired", cancellationToken); return; }
                cancellationToken.ThrowIfCancellationRequested();
                var identity = await api.IdentifyAsync(candidate.AccessToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var identified = candidate with { ChannelId = identity.ChannelId, ChannelName = identity.ChannelName };
                store.Write(identified); tokens = identified;
            }

            var attempts = 0;
            while (!cancellationToken.IsCancellationRequested && tokens != null)
            {
                try
                {
                    var current = await CurrentTokensAsync(cancellationToken);
                    var identity = await api.IdentifyAsync(current.AccessToken, cancellationToken);
                    if (identity.ChannelId != current.ChannelId) throw new YouTubeAuthorizationException();
                    await StatusAsync("connecting", "connecting", cancellationToken);
                    await receiver.RunAsync(CurrentTokensAsync, async live => {
                        attempts = 0;
                        await StatusAsync("connected", live ? "chat_live" : "waiting_live", cancellationToken);
                    }, cancellationToken);
                }
                catch (YouTubeApiException ex)
                {
                    await StatusAsync("error", ex.Code, cancellationToken);
                    await Task.Delay(TimeSpan.FromMinutes(ex.Code == "quota_exceeded" ? 15 : 1), cancellationToken);
                }
                catch (YouTubeAuthorizationException) { throw; }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    await StatusAsync("reconnecting", "network_retry", cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(++attempts, 5)))), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (YouTubeAuthorizationDeniedException)
        { await StatusAsync("disconnected", "authorization_denied", cancellationToken); }
        catch (YouTubeAuthorizationException)
        {
            tokens = null;
            try { store.Delete(); }
            catch { await StatusAsync("error", "storage_delete_error", cancellationToken); return; }
            await StatusAsync("disconnected", "authorization_required", cancellationToken);
        }
        catch (YouTubeApiException ex) { await StatusAsync("error", ex.Code, cancellationToken); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { await StatusAsync("error", "connection_error", cancellationToken); }
    }

    private async Task<YouTubeTokens> CurrentTokensAsync(CancellationToken cancellationToken)
    {
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            var current = tokens ?? throw new YouTubeAuthorizationException();
            if (current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return current;
            var renewed = await api.RefreshAsync(current, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            store.Write(renewed); tokens = renewed;
            return renewed;
        }
        finally { refreshGate.Release(); }
    }

    private Task StatusAsync(string state, string code, CancellationToken cancellationToken)
    {
        lock (refreshGate)
        {
            var key = state + ":" + code + ":" + revision + ":" + tokens?.ChannelName;
            if (key == lastStatus) return Task.CompletedTask;
            lastStatus = key;
            return publish(new CompanionStatus {
                ConnectionId = "youtube", State = state, Message = "", MessageCode = code,
                Account = tokens?.ChannelName ?? "", Authorized = tokens != null, ControlRevision = revision,
                OccurredAt = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }
    }
}
