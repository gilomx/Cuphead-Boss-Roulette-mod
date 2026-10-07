using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.TikFinity;
using LaPichiRuleta.TikFinity.Twitch;

namespace LaPichiRuleta.TikFinity.Runtime;

internal sealed class CompanionHost
{
    private readonly NdjsonWriter output;
    private readonly ParentProcessLifetime parentLifetime;
    private readonly bool twitchControl;

    internal CompanionHost(
        NdjsonWriter output,
        ParentProcessLifetime parentLifetime, bool twitchControl = false)
    {
        this.output = output;
        this.parentLifetime = parentLifetime;
        this.twitchControl = twitchControl;
    }

    internal async Task<int> RunAsync()
    {
        await output.WriteStatusAsync(
            CompanionStatusStates.Starting,
            "TikFinity companion is starting.",
            retryAttempt: 0,
            CancellationToken.None).ConfigureAwait(false);

        using var lifetimeCancellation = new CancellationTokenSource();
        var connector = new TikFinityWebSocketClient(
            output,
            new TikFinityEventNormalizer());
        var connectorTask = connector.RunAsync(lifetimeCancellation.Token);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var twitchApi = new TwitchApi(http);
        var twitch = new TwitchConnectionService(twitchApi, new WindowsTwitchCredentialStore(),
            new TwitchEventSub(twitchApi, output), output.WriteStatusAsync);
        var twitchTask = twitchControl ? twitch.RunAsync(lifetimeCancellation.Token)
            : Task.Delay(Timeout.Infinite, lifetimeCancellation.Token);
        var inputTask = twitchControl ? ReadCommandsAsync(twitch, lifetimeCancellation.Token)
            : Task.Delay(Timeout.Infinite, lifetimeCancellation.Token);
        var parentExitTask = parentLifetime.WaitForExitAsync(
            lifetimeCancellation.Token);

        var completedTask = await Task.WhenAny(
            connectorTask,
            parentExitTask, twitchTask, inputTask).ConfigureAwait(false);

        try { await completedTask.ConfigureAwait(false); }
        finally
        {
            // Never wait on a console pipe while shutting down its owning process.
            lifetimeCancellation.Cancel();
            try { await Task.WhenAll(connectorTask, twitchTask, parentExitTask).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        return completedTask == parentExitTask || completedTask == inputTask
            ? ExitCodes.Success : ExitCodes.FatalError;
    }

    private static async Task ReadCommandsAsync(TwitchConnectionService twitch, CancellationToken cancellationToken)
    {
        using var input = Console.OpenStandardInput();
        var line = new System.Text.StringBuilder();
        var buffer = new byte[256];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) return;
            for (var index = 0; index < read; index++)
            {
                var value = buffer[index];
                if (value == '\n')
                {
                    var command = line.ToString().Trim(); line.Clear();
                    if (command.StartsWith("twitch:", StringComparison.Ordinal)) twitch.TryCommand(command[7..]);
                }
                else if (value != '\r')
                {
                    if (line.Length >= 128) throw new InvalidDataException("Companion command too long.");
                    line.Append((char)value);
                }
            }
        }
    }
}

internal static class ExitCodes
{
    internal const int Success = 0;
    internal const int InvalidArguments = 2;
    internal const int ParentUnavailable = 3;
    internal const int FatalError = 10;
}

internal static class ExceptionMessages
{
    internal static string ForProtocol(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null &&
               current is AggregateException or InvalidOperationException)
        {
            current = current.InnerException;
        }

        return ProtocolText.Clean(current.Message);
    }
}
