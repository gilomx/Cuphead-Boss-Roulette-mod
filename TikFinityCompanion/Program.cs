using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.Runtime;

namespace LaPichiRuleta.TikFinity;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        await using var output = new NdjsonWriter(Console.OpenStandardOutput());

        if (!CompanionOptions.TryParse(args, out var options, out var optionError))
        {
            await output.WriteStatusAsync(
                CompanionStatusStates.Error,
                optionError,
                retryAttempt: 0,
                CancellationToken.None).ConfigureAwait(false);
            return ExitCodes.InvalidArguments;
        }

        if (!ParentProcessLifetime.TryCreate(
                options!.ParentProcessId,
                out var parentLifetime,
                out var parentError))
        {
            await output.WriteStatusAsync(
                CompanionStatusStates.Error,
                parentError,
                retryAttempt: 0,
                CancellationToken.None).ConfigureAwait(false);
            return ExitCodes.ParentUnavailable;
        }

        using (parentLifetime)
        {
            try
            {
                var host = new CompanionHost(output, parentLifetime!);
                return await host.RunAsync().ConfigureAwait(false);
            }
            catch (OutputClosedException)
            {
                // The owner closed its stdout pipe. There is no consumer left,
                // so exiting is safer than keeping a hidden orphan alive.
                return ExitCodes.Success;
            }
            catch (Exception exception)
            {
                try
                {
                    await output.WriteStatusAsync(
                        CompanionStatusStates.Error,
                        ExceptionMessages.ForProtocol(exception),
                        retryAttempt: 0,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (OutputClosedException)
                {
                    // The original failure is no longer observable by the owner.
                }

                return ExitCodes.FatalError;
            }
        }
    }
}
