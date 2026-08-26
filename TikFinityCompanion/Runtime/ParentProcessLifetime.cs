using System.Diagnostics;

namespace LaPichiRuleta.TikFinity.Runtime;

internal sealed class ParentProcessLifetime : IDisposable
{
    private readonly Process parentProcess;

    private ParentProcessLifetime(Process parentProcess)
    {
        this.parentProcess = parentProcess;
    }

    internal static bool TryCreate(
        int parentProcessId,
        out ParentProcessLifetime? lifetime,
        out string error)
    {
        lifetime = null;
        error = string.Empty;

        try
        {
            var process = Process.GetProcessById(parentProcessId);
            if (process.HasExited)
            {
                process.Dispose();
                error = "The parent process has already exited.";
                return false;
            }

            lifetime = new ParentProcessLifetime(process);
            return true;
        }
        catch (ArgumentException)
        {
            error = "The parent process does not exist.";
            return false;
        }
        catch (InvalidOperationException)
        {
            error = "The parent process is no longer available.";
            return false;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or NotSupportedException)
        {
            error = "The parent process cannot be monitored: " +
                    ExceptionMessages.ForProtocol(exception);
            return false;
        }
    }

    internal async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        await parentProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        parentProcess.Dispose();
    }
}
