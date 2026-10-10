using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using LaPichiRuleta.TikFinity.YouTube.Wire;

namespace LaPichiRuleta.TikFinity.YouTube;

internal sealed class YouTubeGrpcTransport : IYouTubeChatTransport, IDisposable
{
    private readonly GrpcChannel channel = GrpcChannel.ForAddress("https://youtube.googleapis.com", new GrpcChannelOptions {
        MaxReceiveMessageSize = 4 * 1024 * 1024,
    });
    public async IAsyncEnumerable<LiveChatMessageListResponse> StreamAsync(LiveChatMessageListRequest request,
        YouTubeTokens tokens, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = new V3DataLiveChatMessageService.V3DataLiveChatMessageServiceClient(channel);
        var headers = new Metadata { { "authorization", "Bearer " + tokens.AccessToken } };
        // Renew an idle stream before its bearer expires, retaining its cursor.
        var seconds = Math.Clamp((tokens.ExpiresAt - DateTimeOffset.UtcNow.AddMinutes(1)).TotalSeconds, 10, 2700);
        using var call = client.StreamList(request, headers, DateTime.UtcNow.AddSeconds(seconds), cancellationToken);
        while (await MoveNextAsync(call.ResponseStream, cancellationToken)) yield return call.ResponseStream.Current;
    }
    private static async Task<bool> MoveNextAsync(IAsyncStreamReader<LiveChatMessageListResponse> stream, CancellationToken cancellationToken)
    {
        try { return await stream.MoveNext(cancellationToken); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded) { return false; }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated) { throw new YouTubeAuthorizationException(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted) { throw new YouTubeApiException("quota_exceeded"); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied) { throw new YouTubeApiException("permission_required"); }
    }
    public void Dispose() => channel.Dispose();
}
