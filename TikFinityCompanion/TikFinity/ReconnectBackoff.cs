namespace LaPichiRuleta.TikFinity.TikFinity;

internal static class ReconnectBackoff
{
    private const int MaximumDelaySeconds = 30;
    private static readonly TimeSpan StableConnectionWindow =
        TimeSpan.FromSeconds(30);

    internal static TimeSpan ForAttempt(int retryAttempt)
    {
        if (retryAttempt <= 1)
            return TimeSpan.FromSeconds(1);

        var exponent = Math.Min(retryAttempt - 1, 5);
        var seconds = Math.Min(1 << exponent, MaximumDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    internal static bool WasStable(TimeSpan connectedDuration)
    {
        return connectedDuration >= StableConnectionWindow;
    }
}
