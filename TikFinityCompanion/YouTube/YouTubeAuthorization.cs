using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace LaPichiRuleta.TikFinity.YouTube;

internal sealed class YouTubeAuthorization(string clientId = YouTubeApplication.ClientId,
    Action<string>? openBrowser = null) : IYouTubeAuthorization
{
    internal static string RandomProof() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static string Challenge(string verifier) => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string AuthorizationUrl(string clientId, string redirectUri, string state, string verifier) =>
        "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new Dictionary<string, string> {
            ["client_id"] = clientId, ["redirect_uri"] = redirectUri, ["response_type"] = "code",
            ["scope"] = YouTubeApplication.Scope, ["state"] = state, ["code_challenge"] = Challenge(verifier),
            ["code_challenge_method"] = "S256", ["access_type"] = "offline", ["prompt"] = "consent select_account",
        }.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

    public async Task<YouTubeTokens> AuthorizeAsync(IYouTubeApi api, Func<string, DateTimeOffset, Task> ready,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(4);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirect = $"http://127.0.0.1:{port}/oauth2/callback";
        var state = RandomProof(); var verifier = RandomProof();
        var url = AuthorizationUrl(clientId, redirect, state, verifier);
        await ready(url, DateTimeOffset.UtcNow.AddMinutes(5));
        cancellationToken.ThrowIfCancellationRequested();
        try { (openBrowser ?? OpenBrowser)(url); }
        catch { /* The panel exposes a retry link if the system browser cannot open. */ }
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            using var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            readDeadline.CancelAfter(TimeSpan.FromSeconds(3));
            string? code = null; var denied = false;
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 512, true);
                var headers = new StringBuilder();
                var requestLine = await ReadLineAsync(reader, readDeadline.Token) ?? "";
                headers.Append(requestLine);
                string? host = null;
                while (true)
                {
                    var line = await ReadLineAsync(reader, readDeadline.Token);
                    if (line == null || line.Length == 0) break;
                    headers.Append(line);
                    if (headers.Length > 8192) throw new InvalidDataException();
                    if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) host = line[5..].Trim();
                }
                var parts = requestLine.Split(' ');
                var valid = host == $"127.0.0.1:{port}" && parts.Length == 3 && parts[0] == "GET" &&
                    parts[1].StartsWith("/oauth2/callback?", StringComparison.Ordinal);
                var query = valid ? ParseQuery(parts[1][17..]) : new Dictionary<string, string>();
                valid = valid && query.TryGetValue("state", out var returnedState) && returnedState == state;
                if (valid)
                {
                    denied = query.ContainsKey("error");
                    query.TryGetValue("code", out code);
                    valid = denied || !string.IsNullOrEmpty(code);
                }
                var message = !valid ? "Solicitud de autorización no válida." : denied
                    ? "No se concedió el permiso. Puedes volver a La Pichi Ruleta."
                    : "Autorización recibida. La conexión se está completando; vuelve al panel de La Pichi Ruleta para comprobar su estado.";
                var bytes = Encoding.UTF8.GetBytes("<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><title>La Pichi Ruleta</title><body>" + message + "</body></html>");
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\nContent-Length: {bytes.Length}\r\n\r\n");
                await stream.WriteAsync(response, readDeadline.Token); await stream.WriteAsync(bytes, readDeadline.Token);
                if (!valid) continue;
            }
            catch (Exception ex) when (!deadline.IsCancellationRequested && ex is IOException or OperationCanceledException or InvalidDataException)
            { continue; }
            if (denied) throw new YouTubeAuthorizationDeniedException();
            deadline.Token.ThrowIfCancellationRequested();
            return await api.ExchangeAsync(code!, verifier, redirect, deadline.Token);
        }
    }

    private static void OpenBrowser(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) != 0)
        {
            if (buffer[0] == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= 8192) throw new InvalidDataException();
            line.Append(buffer[0]);
        }
        return line.Length == 0 ? null : line.ToString();
    }
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !values.TryAdd(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1].Replace('+', ' '))))
                return new();
        }
        return values;
    }
}
