using System.Threading.Channels;
using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.Twitch;

internal sealed class TwitchConnectionService(
    ITwitchApi api, ITwitchCredentialStore store, ITwitchEventReceiver receiver,
    Func<CompanionStatus, CancellationToken, Task> publish, ITwitchEventReceiver? testReceiver = null,
    Func<TwitchRewardResult, CancellationToken, Task>? publishReward = null)
{
    private sealed record RewardSession(CancellationToken Cancellation, TwitchTokens Tokens, long ControlRevision);

    private readonly Channel<(string Action, long Revision)> commands = Channel.CreateBounded<(string, long)>(16);
    private readonly Channel<(TwitchRewardCommand Command, RewardSession Session)> rewardCommands =
        Channel.CreateBounded<(TwitchRewardCommand, RewardSession)>(16);
    private RewardSession? rewardSession;
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

    internal Task QueueRewardCommandAsync(string line, CancellationToken cancellationToken)
    {
        if (publishReward == null || TwitchRewardCommand.Parse(line) is not { } command) return Task.CompletedTask;
        var session = Volatile.Read(ref rewardSession);
        var error = !command.Valid ? "invalid_request" : session == null ? "not_connected" : "";
        if (error.Length > 0) return publishReward(RewardFailure(command, session, error), cancellationToken);
        return rewardCommands.Writer.TryWrite((command, session!)) ? Task.CompletedTask
            : publishReward(RewardFailure(command, session, "busy"), cancellationToken);
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var rewardCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var rewardActivity = publishReward == null ? Task.CompletedTask : RunRewardsAsync(rewardCancellation.Token);
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
        finally
        {
            rewardCancellation.Cancel();
            await StopActivityAsync();
            await rewardActivity;
        }
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
        Volatile.Write(ref rewardSession, null);
        activityCancellation?.Cancel();
        if (activity != null)
        {
            try { await activity; }
            catch (OperationCanceledException) { }
        }
        Volatile.Write(ref rewardSession, null);
        activityCancellation?.Dispose(); activityCancellation = null; activity = null;
    }

    private async Task RunRewardsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var entry in rewardCommands.Reader.ReadAllAsync(cancellationToken))
            {
                var result = await ExecuteRewardAsync(entry.Command, entry.Session, cancellationToken);
                await publishReward!(result, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task<TwitchRewardResult> ExecuteRewardAsync(TwitchRewardCommand command, RewardSession accepted,
        CancellationToken cancellationToken)
    {
        var session = Volatile.Read(ref rewardSession);
        if (session == null || session.Cancellation != accepted.Cancellation)
            return RewardFailure(command, accepted, "session_changed");
        var tokens = session.Tokens;
        if (command.BroadcasterId.Length > 0 && command.BroadcasterId != tokens.UserId)
            return RewardFailure(command, session, "wrong_broadcaster");
        if (command.Operation == "list" ? !tokens.CanReadRewards : !tokens.CanManageRewards)
            return RewardFailure(command, session, command.Operation == "list" ? "read_permission_required" : "manage_permission_required");

        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Cancellation);
        request.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            request.Token.ThrowIfCancellationRequested();
            var result = new TwitchRewardResult(command.RequestId, command.Operation, true, tokens.UserId,
                tokens.CanManageRewards, session.ControlRevision);
            if (command.Operation == "list")
            {
                var rewards = await api.ListRewardsAsync(tokens, request.Token);
                result = result with { RewardsJson = JsonSerializer.Serialize(rewards, CompanionJsonContext.Default.TwitchRewardArray) };
            }
            else
            {
                var reward = await api.ChangeRewardAsync(tokens, command, request.Token);
                result = result with { RewardJson = JsonSerializer.Serialize(reward, CompanionJsonContext.Default.TwitchReward) };
            }
            request.Token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref rewardSession)?.Cancellation != session.Cancellation)
                return RewardFailure(command, session, "session_changed");
            return result;
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested)
        { return RewardFailure(command, session, "session_changed"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return RewardFailure(command, session, "twitch_unavailable"); }
        catch (TwitchRewardException exception)
        { return RewardFailure(command, session, exception.Reason); }
        catch (Exception) when (!request.IsCancellationRequested)
        { return RewardFailure(command, session, "twitch_unavailable"); }
    }

    private TwitchRewardResult RewardFailure(TwitchRewardCommand command, RewardSession? session, string error) =>
        new(command.RequestId, command.Operation, false, session?.Tokens.UserId ?? "",
            session?.Tokens.CanManageRewards ?? false, session?.ControlRevision ?? Interlocked.Read(ref controlRevision), error);

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
                    cancellationToken.ThrowIfCancellationRequested();
                    Volatile.Write(ref rewardSession, new RewardSession(cancellationToken, tokens!, controlRevision));
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
            Volatile.Write(ref rewardSession, null);
            tokens = null;
            try { store.Delete(); }
            catch { await StatusAsync("error", "storage_delete_error", cancellationToken); return; }
            await StatusAsync("disconnected", "authorization_required", cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref rewardSession, null);
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
        cancellationToken.ThrowIfCancellationRequested();
        tokens = validated;
        if (Volatile.Read(ref rewardSession) is { } session && session.Cancellation == cancellationToken)
            Interlocked.CompareExchange(ref rewardSession, session with { Tokens = validated }, session);
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
        var manage = identity.Scopes.Contains("channel:manage:redemptions", StringComparer.Ordinal);
        return candidate with {
            UserId = identity.UserId, Login = identity.Login, CanManageRewards = manage,
            CanReadRewards = manage || identity.Scopes.Contains("channel:read:redemptions", StringComparer.Ordinal),
        };
    }

    private Task StatusAsync(string state, string messageCode, CancellationToken cancellationToken) => publish(new CompanionStatus {
        ConnectionId = "twitch", State = state, Message = "", MessageCode = messageCode,
        Account = testing ? "twitch_cli" : tokens?.Login ?? "", Authorized = !testing && tokens != null,
        BroadcasterId = !testing ? Volatile.Read(ref rewardSession)?.Tokens.UserId ?? "" : "",
        CanManageRewards = !testing && Volatile.Read(ref rewardSession)?.Tokens.CanManageRewards == true,
        TestMode = testing, OccurredAt = DateTimeOffset.UtcNow,
        ControlRevision = controlRevision,
    }, cancellationToken);
}
