using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.TikFinity;

namespace LaPichiRuleta.TikFinity.Runtime;

internal sealed class CompanionHost
{
    private readonly NdjsonWriter output;
    private readonly ParentProcessLifetime parentLifetime;

    internal CompanionHost(
        NdjsonWriter output,
        ParentProcessLifetime parentLifetime)
    {
        this.output = output;
        this.parentLifetime = parentLifetime;
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
        var parentExitTask = parentLifetime.WaitForExitAsync(
            lifetimeCancellation.Token);

        var completedTask = await Task.WhenAny(
            connectorTask,
            parentExitTask).ConfigureAwait(false);

        if (completedTask == parentExitTask)
        {
            try
            {
                await parentExitTask.ConfigureAwait(false);
            }
            catch
            {
                lifetimeCancellation.Cancel();
                try
                {
                    await connectorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The connector was stopped after its parent monitor
                    // failed, before propagating that monitor failure.
                }

                throw;
            }

            lifetimeCancellation.Cancel();
            try
            {
                await connectorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected shutdown after Cuphead exits.
            }

            return ExitCodes.Success;
        }

        lifetimeCancellation.Cancel();
        try
        {
            await parentExitTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The connector stopped first, so its pending parent wait is no
            // longer needed.
        }

        await connectorTask.ConfigureAwait(false);
        return ExitCodes.FatalError;
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
