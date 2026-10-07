using System.Threading.Channels;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.Twitch;

internal sealed class TwitchConnectionService(
    ITwitchApi api, ITwitchCredentialStore store, ITwitchEventReceiver receiver,
    Func<CompanionStatus, CancellationToken, Task> publish, ITwitchEventReceiver? testReceiver = null)
{
    private readonly Channel<(string Action, long Revision)> commands = Channel.CreateBounded<(string, long)>(16);
    private long controlRevision;
    private TwitchTokens? tokens;
    private TwitchTokens? pendingRevocation;
    private DateTimeOffset nextValidation;
    private CancellationTokenSource? activityCancellation;
    private Task? activity;
    private bool testing;

    internal bool TryCommand(string command)
    {
        var parts = command.Split(':');
        return parts.Length == 2 && (parts[0] is "connect" or "disconnect" or "cancel" || parts[0] == "test" && testReceiver != null) &&
            long.TryParse(parts[1], out var revision) && revision > 0 &&
            commands.Writer.TryWrite((parts[0], revision));
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storageFailed = false;
            try { tokens = store.Read(); }
            catch { storageFailed = true; await StatusAsync("error", "storage_error", cancellationToken); }
            if (tokens != null) StartActivity(false, cancellationToken);
            else if (!storageFailed) await StatusAsync("disconnected", "not_connected", cancellationToken);
            await foreach (var command in commands.Reader.ReadAllAsync(cancellationToken))
            {
                if (command.Revision <= controlRevision) continue;
                await StopActivityAsync();
                controlRevision = command.Revision;
                var wasTesting = testing;
                testing = command.Action == "test";
                if (testing)
                {
                    activityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    activity = TestActivityAsync(activityCancellation.Token);
                    continue;
                }
                // Leaving local tests restores the saved account without revoking it.
                if (wasTesting)
                {
                    if (tokens != null) StartActivity(false, cancellationToken);
                    else await StatusAsync("disconnected", "not_connected", cancellationToken);
                    continue;
                }
                if (command.Action == "connect")
                {
                    // Switching is explicit: forget the former account before authorizing another.
                    if (!await ForgetAsync(cancellationToken)) continue;
                    StartActivity(true, cancellationToken);
                }
                else if (command.Action == "disconnect") await ForgetAsync(cancellationToken);
                else if (tokens != null) StartActivity(false, cancellationToken);
                else await StatusAsync("disconnected", "authorization_cancelled", cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { await StopActivityAsync(); }
    }

    private void StartActivity(bool authorize, CancellationToken cancellationToken)
    {
        activityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activity = ActivityAsync(authorize, activityCancellation.Token);
    }

    private async Task TestActivityAsync(CancellationToken cancellationToken)
    {
        var attempts = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await StatusAsync("connecting", "test_connecting", cancellationToken);
                    await testReceiver!.RunAsync(_ => Task.FromResult(TwitchLocalTestApi.Identity), async _ => {
                        attempts = 0;
                        await StatusAsync("connected", "test_connected", cancellationToken);
                    }, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    await StatusAsync("reconnecting", "test_retry", cancellationToken);
                    attempts++;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempts, 5)))), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task StopActivityAsync()
    {
        activityCancellation?.Cancel();
        if (activity != null)
        {
            try { await activity; }
            catch (OperationCanceledException) { }
        }
        activityCancellation?.Dispose(); activityCancellation = null; activity = null;
    }

    private async Task<bool> ForgetAsync(CancellationToken cancellationToken)
    {
        var former = tokens ?? pendingRevocation;
        pendingRevocation = former;
        tokens = null;
        try { store.Delete(); }
        catch { await StatusAsync("error", "storage_delete_error", cancellationToken); return false; }
        await StatusAsync("disconnected", "not_connected", cancellationToken);
        if (former == null) return true;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await api.RevokeAsync(former.AccessToken, deadline.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await StatusAsync("disconnected", "revocation_unconfirmed", cancellationToken);
        }
        pendingRevocation = null;
        return true;
    }

    private async Task ActivityAsync(bool authorize, CancellationToken cancellationToken)
    {
        try
        {
            if (authorize)
            {
                await StatusAsync("connecting", "requesting_code", cancellationToken);
                var code = await api.StartAuthorizationAsync(cancellationToken);
                var expires = DateTimeOffset.UtcNow.AddSeconds(code.ExpiresIn);
                await publish(new CompanionStatus {
                    ConnectionId = "twitch", State = "connecting", Message = "", MessageCode = "authorization_pending",
                    UserCode = code.UserCode, VerificationUri = code.VerificationUri, ExpiresAt = expires.ToString("O"),
                    ControlRevision = controlRevision,
                    OccurredAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
                var interval = code.Interval;
                TwitchTokens? result = null;
                while (result == null && DateTimeOffset.UtcNow < expires)
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
                    if (DateTimeOffset.UtcNow >= expires) break;
                    try { result = await api.PollAuthorizationAsync(code.DeviceCode, cancellationToken); }
                    catch (TwitchSlowDownException) { interval = Math.Min(120, interval + 5); }
                }
                if (result == null)
                {
                    await StatusAsync("disconnected", "authorization_expired", cancellationToken); return;
                }
                cancellationToken.ThrowIfCancellationRequested();
                tokens = await IdentifyAsync(result, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                store.Write(tokens);
                nextValidation = DateTimeOffset.UtcNow.AddHours(1);
            }
            else nextValidation = DateTimeOffset.MinValue;

            var attempts = 0;
            while (!cancellationToken.IsCancellationRequested && tokens != null)
            {
                try
                {
                    // Every new socket must validate, even if the last check was recent.
                    nextValidation = DateTimeOffset.MinValue;
                    await CurrentTokensAsync(cancellationToken);
                    await StatusAsync("connecting", "connecting", cancellationToken);
                    await receiver.RunAsync(CurrentTokensAsync, async limited => {
                        attempts = 0;
                        await StatusAsync("connected", limited ? "connected_limited" : "connected", cancellationToken);
                    }, cancellationToken);
                }
                catch (TwitchAuthorizationException) { throw; }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    attempts++;
                    await StatusAsync("reconnecting", "network_retry", cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempts, 5)))), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (TwitchAuthorizationException)
        {
            tokens = null;
            try { store.Delete(); }
            catch { await StatusAsync("error", "storage_delete_error", cancellationToken); return; }
            await StatusAsync("disconnected", "authorization_required", cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            tokens = null;
            await StatusAsync("error", "connection_error", cancellationToken);
        }
    }

    private async Task<TwitchTokens> CurrentTokensAsync(CancellationToken cancellationToken)
    {
        var current = tokens ?? throw new TwitchAuthorizationException();
        if (DateTimeOffset.UtcNow < nextValidation) return current;
        TwitchTokens validated;
        try { validated = await IdentifyAsync(current, cancellationToken); }
        catch (TwitchAuthorizationException)
        {
            var renewed = await api.RefreshAsync(current, cancellationToken);
            validated = await IdentifyAsync(renewed, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Public-client refresh tokens rotate; persist the replacement before continuing.
            store.Write(validated);
        }
        tokens = validated;
        nextValidation = DateTimeOffset.UtcNow.AddHours(1);
        return validated;
    }

    private async Task<TwitchTokens> IdentifyAsync(TwitchTokens candidate, CancellationToken cancellationToken)
    {
        var identity = await api.ValidateAsync(candidate.AccessToken, cancellationToken);
        if (identity.ClientId != TwitchApplication.ClientId || identity.UserId.Length == 0 || identity.Login.Length == 0 ||
            !identity.Scopes.Contains("user:read:chat", StringComparer.Ordinal) ||
            (candidate.UserId.Length > 0 && candidate.UserId != identity.UserId))
            throw new TwitchAuthorizationException();
        return candidate with { UserId = identity.UserId, Login = identity.Login };
    }

    private Task StatusAsync(string state, string messageCode, CancellationToken cancellationToken) => publish(new CompanionStatus {
        ConnectionId = "twitch", State = state, Message = "", MessageCode = messageCode,
        Account = testing ? "twitch_cli" : tokens?.Login ?? "", Authorized = !testing && tokens != null,
        TestMode = testing, OccurredAt = DateTimeOffset.UtcNow,
        ControlRevision = controlRevision,
    }, cancellationToken);
}
