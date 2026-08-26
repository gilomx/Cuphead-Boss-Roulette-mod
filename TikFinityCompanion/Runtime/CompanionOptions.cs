using System.Globalization;

namespace LaPichiRuleta.TikFinity.Runtime;

internal sealed class CompanionOptions
{
    private CompanionOptions(int parentProcessId)
    {
        ParentProcessId = parentProcessId;
    }

    internal int ParentProcessId { get; }

    internal static bool TryParse(
        IReadOnlyList<string> arguments,
        out CompanionOptions? options,
        out string error)
    {
        options = null;
        error = string.Empty;
        int? parentProcessId = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index] ?? string.Empty;
            string? value = null;

            if (argument.Equals("--parent-pid", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= arguments.Count)
                {
                    error = "--parent-pid requires a positive process ID.";
                    return false;
                }

                value = arguments[index];
            }
            else if (argument.StartsWith("--parent-pid=", StringComparison.OrdinalIgnoreCase))
            {
                value = argument["--parent-pid=".Length..];
            }
            else
            {
                error = "Unknown companion argument: " + Protocol.ProtocolText.Clean(argument);
                return false;
            }

            if (parentProcessId.HasValue)
            {
                error = "--parent-pid can only be provided once.";
                return false;
            }

            if (!int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedProcessId) ||
                parsedProcessId <= 0)
            {
                error = "--parent-pid requires a positive process ID.";
                return false;
            }

            parentProcessId = parsedProcessId;
        }

        if (!parentProcessId.HasValue)
        {
            error = "Missing required argument --parent-pid.";
            return false;
        }

        options = new CompanionOptions(parentProcessId.Value);
        return true;
    }
}
