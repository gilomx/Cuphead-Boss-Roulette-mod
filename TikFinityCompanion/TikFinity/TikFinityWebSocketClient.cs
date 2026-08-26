using System.Net.WebSockets;
using System.Text;
using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.Runtime;

namespace LaPichiRuleta.TikFinity.TikFinity;

internal sealed class TikFinityWebSocketClient
{
    private static readonly Uri Endpoint = new("ws://localhost:21213/");
    private const int ReceiveBufferSize = 16 * 1024;
    private const int MaximumMessageSize = 1024 * 1024;

    private readonly NdjsonWriter output;
    private readonly TikFinityEventNormalizer normalizer;

    internal TikFinityWebSocketClient(
        NdjsonWriter output,
        TikFinityEventNormalizer normalizer)
    {
        this.output = output;
        this.normalizer = normalizer;
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        var retryAttempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            DateTimeOffset? connectedAt = null;
            await output.WriteStatusAsync(
                CompanionStatusStates.Connecting,
                "Connecting to TikFinity at ws://localhost:21213/.",
                retryAttempt,
                cancellationToken).ConfigureAwait(false);

            try
            {
                using var socket = new ClientWebSocket();
                socket.Options.Proxy = null;
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                await socket.ConnectAsync(Endpoint, cancellationToken).ConfigureAwait(false);
                connectedAt = DateTimeOffset.UtcNow;

                await output.WriteStatusAsync(
                    CompanionStatusStates.Connected,
                    "Connected to the local TikFinity WebSocket.",
                    retryAttempt,
                    cancellationToken).ConfigureAwait(false);

                var closeMessage = await ReceiveLoopAsync(
                    socket,
                    cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                    break;

                await output.WriteStatusAsync(
                    CompanionStatusStates.Disconnected,
                    closeMessage,
                    retryAttempt: 0,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OutputClosedException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is WebSocketException or IOException or InvalidDataException)
            {
                await output.WriteStatusAsync(
                    CompanionStatusStates.Error,
                    "TikFinity connection error: " + ExceptionMessages.ForProtocol(exception),
                    retryAttempt,
                    cancellationToken).ConfigureAwait(false);
            }


            if (connectedAt.HasValue && ReconnectBackoff.WasStable(
                    DateTimeOffset.UtcNow - connectedAt.Value))
            {
                retryAttempt = 0;
            }

            retryAttempt = Math.Min(retryAttempt + 1, 31);
            var delay = ReconnectBackoff.ForAttempt(retryAttempt);
            await output.WriteStatusAsync(
                CompanionStatusStates.Disconnected,
                "Retrying the TikFinity connection in " +
                ((int)delay.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " seconds.",
                retryAttempt,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> ReceiveLoopAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return BuildCloseMessage(result);
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidDataException("TikFinity sent a non-text WebSocket message.");

                if (message.Length + result.Count > MaximumMessageSize)
                    throw new InvalidDataException("TikFinity sent a WebSocket message larger than 1 MiB.");

                await message.WriteAsync(
                    buffer.AsMemory(0, result.Count),
                    cancellationToken).ConfigureAwait(false);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
            var batch = normalizer.Normalize(json, DateTimeOffset.UtcNow);

            foreach (var error in batch.Errors)
            {
                await output.WriteStatusAsync(
                    CompanionStatusStates.Error,
                    error,
                    retryAttempt: 0,
                    cancellationToken).ConfigureAwait(false);
                await output.WriteStatusAsync(
                    CompanionStatusStates.Connected,
                    "Connected to TikFinity; an invalid event was ignored.",
                    retryAttempt: 0,
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var streamEvent in batch.Events)
                await output.WriteEventAsync(streamEvent, cancellationToken).ConfigureAwait(false);
        }

        return "The TikFinity connection was stopped.";
    }

    private static string BuildCloseMessage(WebSocketReceiveResult result)
    {
        var status = result.CloseStatus?.ToString() ?? "unknown";
        var description = ProtocolText.Clean(result.CloseStatusDescription);
        return description.Length == 0
            ? "TikFinity closed the WebSocket (" + status + ")."
            : "TikFinity closed the WebSocket (" + status + "): " + description;
    }
}
