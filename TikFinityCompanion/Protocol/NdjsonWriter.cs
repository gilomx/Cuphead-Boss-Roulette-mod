using System.Text;
using System.Text.Json;

namespace LaPichiRuleta.TikFinity.Protocol;

internal sealed class NdjsonWriter : IAsyncDisposable
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private readonly Stream output;
    private readonly SemaphoreSlim gate = new(initialCount: 1, maxCount: 1);
    private bool disposed;

    internal NdjsonWriter(Stream output)
    {
        this.output = output ?? throw new ArgumentNullException(nameof(output));
    }

    internal Task WriteStatusAsync(
        string state,
        string message,
        int retryAttempt,
        CancellationToken cancellationToken)
    {
        var status = new CompanionStatus
        {
            State = state,
            Message = ProtocolText.Clean(message),
            OccurredAt = DateTimeOffset.UtcNow,
            RetryAttempt = Math.Max(0, retryAttempt),
        };

        return WriteAsync(
            status,
            CompanionJsonContext.Default.CompanionStatus,
            cancellationToken);
    }

    internal Task WriteEventAsync(
        CompanionEvent streamEvent,
        CancellationToken cancellationToken)
    {
        return WriteAsync(
            streamEvent,
            CompanionJsonContext.Default.CompanionEvent,
            cancellationToken);
    }

    internal static string Serialize(CompanionStatus status)
    {
        return JsonSerializer.Serialize(
            status,
            CompanionJsonContext.Default.CompanionStatus);
    }

    internal static string Serialize(CompanionEvent streamEvent)
    {
        return JsonSerializer.Serialize(
            streamEvent,
            CompanionJsonContext.Default.CompanionEvent);
    }

    private async Task WriteAsync<T>(
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(NdjsonWriter));

        byte[] payload;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                "The companion could not serialize a protocol message.",
                exception);
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(NewLine, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            throw new OutputClosedException(exception);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await output.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }
}

internal sealed class OutputClosedException : Exception
{
    internal OutputClosedException(Exception innerException)
        : base("The protocol output pipe is closed.", innerException)
    {
    }
}

internal static class ProtocolText
{
    private const int MaximumLength = 512;

    internal static string Clean(string? value)
    {
        return Clean(value, MaximumLength);
    }

    internal static string Clean(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        maximumLength = Math.Clamp(maximumLength, 1, 8192);
        var builder = new StringBuilder(Math.Min(value.Length, maximumLength));
        foreach (var character in value.Trim())
        {
            if (builder.Length == maximumLength)
                break;

            builder.Append(character is '\r' or '\n' or '\t' ? ' ' : character);
        }

        return builder.ToString();
    }
}
