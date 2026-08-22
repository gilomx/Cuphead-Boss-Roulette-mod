using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsServer : IDisposable
    {
        private const string WebSocketMagic =
            "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const int MaximumHeaderBytes = 16384;
        private const int MaximumClientPayloadBytes = 1024 * 1024;

        private readonly string assetsDirectory;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly object stateLock = new object();
        private readonly object configLock = new object();
        private readonly Queue<string> configCommands =
            new Queue<string>();
        private readonly object interactionsLock = new object();
        private readonly Queue<string> interactionCommands =
            new Queue<string>();
        private readonly object clientsLock = new object();
        private readonly List<WebSocketClient> clients =
            new List<WebSocketClient>();
        private readonly AutoResetEvent broadcastWake =
            new AutoResetEvent(false);

        private TcpListener listener;
        private Thread acceptThread;
        private Thread broadcastThread;
        private volatile bool running;
        private string latestMessage = "{\"type\":\"state\",\"active\":false}";
        private long latestRevision;
        private byte[] challengeLabelPng;
        private int challengeLabelRevision;
        private string latestConfigState =
            "{\"enabled\":false,\"ready\":false}";
        private string latestInteractionsState =
            "{\"ready\":false,\"available\":false}";

        internal int Port { get; private set; }

        internal bool IsRunning
        {
            get { return running; }
        }

        internal int ClientCount
        {
            get
            {
                lock (clientsLock)
                    return clients.Count;
            }
        }

        internal CreatorToolsServer(
            string assetsDirectory,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.assetsDirectory = Path.GetFullPath(
                assetsDirectory ?? string.Empty);
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool Start(int preferredPort, int candidateCount)
        {
            if (running)
                return true;

            var firstPort = Math.Max(1024, Math.Min(65535, preferredPort));
            var attempts = Math.Max(1, Math.Min(100, candidateCount));
            for (var i = 0; i < attempts; i++)
            {
                var candidate = firstPort + i;
                if (candidate > 65535)
                    break;

                TcpListener candidateListener = null;
                try
                {
                    candidateListener = new TcpListener(
                        IPAddress.Loopback, candidate);
                    candidateListener.Start();
                    listener = candidateListener;
                    Port = candidate;
                    break;
                }
                catch (SocketException)
                {
                    if (candidateListener != null)
                    {
                        try { candidateListener.Stop(); }
                        catch { }
                    }
                }
            }

            if (listener == null)
                return false;

            running = true;
            acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Name = "La Pichi Ruleta Creator Tools HTTP";
            acceptThread.Start();

            broadcastThread = new Thread(BroadcastLoop);
            broadcastThread.IsBackground = true;
            broadcastThread.Name = "La Pichi Ruleta Creator Tools WebSocket";
            broadcastThread.Start();

            if (logInfo != null)
                logInfo("Creator Tools listening on http://127.0.0.1:" +
                    Port + "/.");
            return true;
        }

        internal void Publish(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (stateLock)
            {
                latestMessage = message;
                latestRevision++;
            }
            if (running)
                broadcastWake.Set();
        }

        internal void SetChallengeLabel(byte[] png, int revision)
        {
            lock (stateLock)
            {
                challengeLabelPng = png;
                challengeLabelRevision = revision;
            }
        }

        internal void SetConfigState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (configLock)
                latestConfigState = json;
        }

        internal bool TryTakeConfigCommand(out string command)
        {
            lock (configLock)
            {
                if (configCommands.Count == 0)
                {
                    command = null;
                    return false;
                }
                command = configCommands.Dequeue();
                return true;
            }
        }

        internal void SetInteractionsState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (interactionsLock)
                latestInteractionsState = json;
        }

        internal bool TryTakeInteractionCommand(out string command)
        {
            lock (interactionsLock)
            {
                if (interactionCommands.Count == 0)
                {
                    command = null;
                    return false;
                }
                command = interactionCommands.Dequeue();
                return true;
            }
        }
        internal void Stop()
        {
            if (!running && listener == null)
                return;

            running = false;
            broadcastWake.Set();
            var currentListener = listener;
            listener = null;
            if (currentListener != null)
            {
                try { currentListener.Stop(); }
                catch { }
            }

            WebSocketClient[] snapshot;
            lock (clientsLock)
            {
                snapshot = clients.ToArray();
                clients.Clear();
            }
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i].Close();

            JoinBackgroundThread(acceptThread);
            JoinBackgroundThread(broadcastThread);
            acceptThread = null;
            broadcastThread = null;
            Port = 0;
        }

        public void Dispose()
        {
            Stop();
            broadcastWake.Close();
        }

        private static void JoinBackgroundThread(Thread thread)
        {
            if (thread == null || thread == Thread.CurrentThread)
                return;
            try { thread.Join(500); }
            catch { }
        }

        private void AcceptLoop()
        {
            while (running)
            {
                TcpClient connection = null;
                try
                {
                    var current = listener;
                    if (current == null)
                        break;
                    connection = current.AcceptTcpClient();
                    connection.NoDelay = true;
                    var thread = new Thread(HandleConnection);
                    thread.IsBackground = true;
                    thread.Name = "La Pichi Ruleta Creator Tools Client";
                    thread.Start(connection);
                    connection = null;
                }
                catch (SocketException)
                {
                    if (running && logWarning != null)
                        logWarning("Creator Tools stopped accepting clients.");
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception exception)
                {
                    if (running && logWarning != null)
                        logWarning("Creator Tools client accept failed: " +
                            exception.Message);
                }
                finally
                {
                    if (connection != null)
                    {
                        try { connection.Close(); }
                        catch { }
                    }
                }
            }
        }

        private void HandleConnection(object state)
        {
            var connection = state as TcpClient;
            if (connection == null)
                return;

            var upgraded = false;
            try
            {
                var stream = connection.GetStream();
                var request = ReadHttpRequest(stream);
                if (request == null)
                    return;

                if (request.Path == "/ws" && request.IsWebSocket)
                {
                    if (!CompleteWebSocketHandshake(stream, request))
                        return;
                    upgraded = true;
                    RunWebSocketClient(connection, stream);
                    return;
                }

                ServeHttp(stream, request);
            }
            catch (Exception exception)
            {
                if (running && logWarning != null)
                    logWarning("Creator Tools request failed: " +
                        exception.Message);
            }
            finally
            {
                if (!upgraded)
                {
                    try { connection.Close(); }
                    catch { }
                }
            }
        }

        private void RunWebSocketClient(
            TcpClient connection, NetworkStream stream)
        {
            var client = new WebSocketClient(connection, stream);
            lock (clientsLock)
                clients.Add(client);

            try
            {
                string initialMessage;
                long initialRevision;
                lock (stateLock)
                {
                    initialMessage = latestMessage;
                    initialRevision = latestRevision;
                }
                client.SendText(initialMessage);
                client.LastRevision = initialRevision;
                ReadClientFrames(client);
            }
            finally
            {
                lock (clientsLock)
                    clients.Remove(client);
                client.Close();
            }
        }

        private void BroadcastLoop()
        {
            while (running)
            {
                var stateChanged = broadcastWake.WaitOne(10000, false);
                if (!running)
                    break;

                string message;
                long revision;
                lock (stateLock)
                {
                    message = latestMessage;
                    revision = latestRevision;
                }

                WebSocketClient[] snapshot;
                lock (clientsLock)
                    snapshot = clients.ToArray();

                for (var i = 0; i < snapshot.Length; i++)
                {
                    var client = snapshot[i];
                    try
                    {
                        if (stateChanged && client.LastRevision != revision)
                        {
                            client.SendText(message);
                            client.LastRevision = revision;
                        }
                        else if (!stateChanged)
                            client.SendPing();
                    }
                    catch
                    {
                        client.Close();
                    }
                }
            }
        }

        private static HttpRequest ReadHttpRequest(NetworkStream stream)
        {
            var bytes = new List<byte>();
            while (bytes.Count < MaximumHeaderBytes)
            {
                var value = stream.ReadByte();
                if (value < 0)
                    return null;
                bytes.Add((byte)value);
                var count = bytes.Count;
                if (count >= 4 && bytes[count - 4] == 13 &&
                    bytes[count - 3] == 10 && bytes[count - 2] == 13 &&
                    bytes[count - 1] == 10)
                    break;
            }

            if (bytes.Count >= MaximumHeaderBytes)
                return null;

            var header = Encoding.ASCII.GetString(bytes.ToArray());
            var lines = header.Split(new[] { "\r\n" },
                StringSplitOptions.None);
            if (lines.Length == 0)
                return null;

            var requestParts = lines[0].Split(' ');
            if (requestParts.Length < 2 || requestParts[0] != "GET")
                return null;

            var request = new HttpRequest();
            request.Path = requestParts[1];
            var queryIndex = request.Path.IndexOf('?');
            if (queryIndex >= 0)
            {
                request.Query = request.Path.Substring(queryIndex + 1);
                request.Path = request.Path.Substring(0, queryIndex);
            }
            else
                request.Query = string.Empty;
            request.Headers = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < lines.Length; i++)
            {
                var separator = lines[i].IndexOf(':');
                if (separator <= 0)
                    continue;
                request.Headers[lines[i].Substring(0, separator).Trim()] =
                    lines[i].Substring(separator + 1).Trim();
            }

            string upgrade;
            request.IsWebSocket =
                request.Headers.TryGetValue("Upgrade", out upgrade) &&
                string.Equals(upgrade, "websocket",
                    StringComparison.OrdinalIgnoreCase);
            return request;
        }

        private static bool CompleteWebSocketHandshake(
            NetworkStream stream, HttpRequest request)
        {
            string key;
            if (!request.Headers.TryGetValue("Sec-WebSocket-Key", out key) ||
                string.IsNullOrEmpty(key))
                return false;

            string accept;
            using (var sha1 = SHA1.Create())
            {
                accept = Convert.ToBase64String(sha1.ComputeHash(
                    Encoding.ASCII.GetBytes(key + WebSocketMagic)));
            }

            var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                           "Upgrade: websocket\r\n" +
                           "Connection: Upgrade\r\n" +
                           "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(response);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            return true;
        }

        private void ServeHttp(NetworkStream stream, HttpRequest request)
        {
            var path = request.Path;
            if (path == "/" || path == "/index.html")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\overlay.html"),
                    "text/html; charset=utf-8", false);
                return;
            }
            if (path == "/overlay.css")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\overlay.css"),
                    "text/css; charset=utf-8", false);
                return;
            }
            if (path == "/overlay.js")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\overlay.js"),
                    "application/javascript; charset=utf-8", false);
                return;
            }
            if (path == "/config" || path == "/config/" ||
                path == "/config.html" ||
                path == "/config/roulette" ||
                path == "/config/roulette/" ||
                path == "/config/roulette.html" ||
                path == "/config/interactions" ||
                path == "/config/interactions/" ||
                path == "/config/interactions.html")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\config.html"),
                    "text/html; charset=utf-8", false);
                return;
            }
            if (path == "/config/roulette.css" || path == "/config.css")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\config.css"),
                    "text/css; charset=utf-8", false);
                return;
            }
            if (path == "/config/roulette.js" || path == "/config.js")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\config.js"),
                    "application/javascript; charset=utf-8", false);
                return;
            }
            if (path == "/api/config/roulette" || path == "/api/config")
            {
                string json;
                lock (configLock)
                    json = latestConfigState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/config/roulette/set" ||
                path == "/api/config/set")
            {
                lock (configLock)
                    configCommands.Enqueue(request.Query ?? string.Empty);
                WriteResponse(stream, 202, "Accepted",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes("{\"ok\":true}"), false);
                return;
            }
            if (path == "/api/config/interactions")
            {
                string json;
                lock (interactionsLock)
                    json = latestInteractionsState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/config/interactions/test" ||
                path == "/api/config/interactions/set")
            {
                lock (interactionsLock)
                    interactionCommands.Enqueue(request.Query ?? string.Empty);
                WriteResponse(stream, 202, "Accepted",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes("{\"ok\":true}"), false);
                return;
            }
            if (path == "/generated/challenge.png")
            {
                byte[] png;
                int revision;
                lock (stateLock)
                {
                    png = challengeLabelPng;
                    revision = challengeLabelRevision;
                }
                if (png == null || png.Length == 0)
                    WriteResponse(stream, 404, "Not Found", "text/plain",
                        Encoding.UTF8.GetBytes("Challenge label unavailable."),
                        false);
                else
                    WriteResponse(stream, 200, "OK", "image/png", png, false,
                        "ETag: \"challenge-" + revision + "\"\r\n");
                return;
            }
            if (path.StartsWith("/assets/", StringComparison.Ordinal))
            {
                ServeAsset(stream, path.Substring(8));
                return;
            }

            WriteResponse(stream, 404, "Not Found", "text/plain",
                Encoding.UTF8.GetBytes("Not found."), false);
        }

        private void ServeAsset(NetworkStream stream, string relativeUrl)
        {
            string decoded;
            try { decoded = Uri.UnescapeDataString(relativeUrl); }
            catch
            {
                WriteResponse(stream, 400, "Bad Request", "text/plain",
                    Encoding.UTF8.GetBytes("Invalid asset path."), false);
                return;
            }

            decoded = decoded.Replace('/', Path.DirectorySeparatorChar);
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(
                    assetsDirectory, decoded));
            }
            catch
            {
                WriteResponse(stream, 400, "Bad Request", "text/plain",
                    Encoding.UTF8.GetBytes("Invalid asset path."), false);
                return;
            }

            var root = assetsDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                WriteResponse(stream, 403, "Forbidden", "text/plain",
                    Encoding.UTF8.GetBytes("Forbidden."), false);
                return;
            }

            ServeFile(stream, fullPath, MimeType(fullPath), true);
        }

        private static void ServeFile(
            NetworkStream stream, string path, string contentType,
            bool cache)
        {
            if (!File.Exists(path))
            {
                WriteResponse(stream, 404, "Not Found", "text/plain",
                    Encoding.UTF8.GetBytes("File unavailable."), false);
                return;
            }

            byte[] body;
            try { body = File.ReadAllBytes(path); }
            catch
            {
                WriteResponse(stream, 500, "Internal Server Error",
                    "text/plain", Encoding.UTF8.GetBytes(
                        "Unable to read file."), false);
                return;
            }
            WriteResponse(stream, 200, "OK", contentType, body, cache);
        }

        private static void WriteResponse(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string contentType,
            byte[] body,
            bool cache,
            string additionalHeaders = null)
        {
            body = body ?? new byte[0];
            var header = "HTTP/1.1 " + statusCode + " " + statusText +
                         "\r\nContent-Type: " + contentType +
                         "\r\nContent-Length: " + body.Length +
                         "\r\nConnection: close\r\n" +
                         (cache
                             ? "Cache-Control: public, max-age=3600\r\n"
                             : "Cache-Control: no-store\r\n") +
                         "X-Content-Type-Options: nosniff\r\n" +
                         (additionalHeaders ?? string.Empty) + "\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string MimeType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".png")
                return "image/png";
            if (extension == ".jpg" || extension == ".jpeg")
                return "image/jpeg";
            if (extension == ".webp")
                return "image/webp";
            if (extension == ".css")
                return "text/css; charset=utf-8";
            if (extension == ".js")
                return "application/javascript; charset=utf-8";
            if (extension == ".html")
                return "text/html; charset=utf-8";
            return "application/octet-stream";
        }

        private static void ReadClientFrames(WebSocketClient client)
        {
            while (client.IsAlive)
            {
                int first;
                try { first = client.Stream.ReadByte(); }
                catch { break; }
                if (first < 0)
                    break;
                var second = client.Stream.ReadByte();
                if (second < 0)
                    break;

                var opcode = first & 0x0f;
                var masked = (second & 0x80) != 0;
                long length = second & 0x7f;
                if (length == 126)
                {
                    var extended = ReadExact(client.Stream, 2);
                    if (extended == null)
                        break;
                    length = (extended[0] << 8) | extended[1];
                }
                else if (length == 127)
                {
                    var extended = ReadExact(client.Stream, 8);
                    if (extended == null)
                        break;
                    length = 0;
                    for (var i = 0; i < extended.Length; i++)
                        length = (length << 8) | extended[i];
                }

                if (length < 0 || length > MaximumClientPayloadBytes)
                    break;
                var mask = masked ? ReadExact(client.Stream, 4) : null;
                if (masked && mask == null)
                    break;
                var payload = ReadExact(client.Stream, (int)length);
                if (payload == null)
                    break;
                if (masked)
                {
                    for (var i = 0; i < payload.Length; i++)
                        payload[i] = (byte)(payload[i] ^ mask[i % 4]);
                }

                if (opcode == 0x8)
                {
                    try { client.SendFrame(0x8, payload); }
                    catch { }
                    break;
                }
                if (opcode == 0x9)
                {
                    try { client.SendFrame(0xA, payload); }
                    catch { break; }
                }
            }
        }

        private static byte[] ReadExact(Stream stream, int length)
        {
            var result = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                int read;
                try { read = stream.Read(result, offset, length - offset); }
                catch { return null; }
                if (read <= 0)
                    return null;
                offset += read;
            }
            return result;
        }

        private sealed class HttpRequest
        {
            internal string Path;
            internal string Query;
            internal Dictionary<string, string> Headers;
            internal bool IsWebSocket;
        }

        private sealed class WebSocketClient
        {
            private readonly object sendLock = new object();
            private readonly TcpClient connection;
            private volatile bool alive = true;

            internal readonly NetworkStream Stream;
            internal long LastRevision = -1;

            internal bool IsAlive
            {
                get { return alive && connection.Connected; }
            }

            internal WebSocketClient(
                TcpClient connection, NetworkStream stream)
            {
                this.connection = connection;
                Stream = stream;
            }

            internal void SendText(string message)
            {
                SendFrame(0x1, Encoding.UTF8.GetBytes(message ?? string.Empty));
            }

            internal void SendPing()
            {
                SendFrame(0x9, new byte[0]);
            }

            internal void SendFrame(int opcode, byte[] payload)
            {
                if (!IsAlive)
                    throw new IOException("WebSocket is closed.");
                payload = payload ?? new byte[0];

                lock (sendLock)
                {
                    var header = new List<byte>();
                    header.Add((byte)(0x80 | (opcode & 0x0f)));
                    if (payload.Length <= 125)
                        header.Add((byte)payload.Length);
                    else if (payload.Length <= ushort.MaxValue)
                    {
                        header.Add(126);
                        header.Add((byte)((payload.Length >> 8) & 0xff));
                        header.Add((byte)(payload.Length & 0xff));
                    }
                    else
                    {
                        header.Add(127);
                        var length = (ulong)payload.Length;
                        for (var shift = 56; shift >= 0; shift -= 8)
                            header.Add((byte)((length >> shift) & 0xff));
                    }

                    var headerBytes = header.ToArray();
                    Stream.Write(headerBytes, 0, headerBytes.Length);
                    if (payload.Length > 0)
                        Stream.Write(payload, 0, payload.Length);
                    Stream.Flush();
                }
            }

            internal void Close()
            {
                if (!alive)
                    return;
                alive = false;
                try { connection.Close(); }
                catch { }
            }
        }
    }
}
