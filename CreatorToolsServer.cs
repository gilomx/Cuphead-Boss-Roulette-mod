using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const int MaximumHeaderBytes = 65536;
        private const int MaximumHttpBodyBytes = 65536;
        private const int HttpHeaderReadTimeoutMilliseconds = 5000;
        private const int MaximumClientPayloadBytes = 1024 * 1024;
        private const int MaximumConfigCommands = 256;
        private const int MaximumDashboardQueryLength = 4096;
        private const int MaximumInteractionControlCommands = 256;
        private const int MaximumInteractionTestCommands = 128;
        private const int MaximumQueuedInteractionTests = 200;
        private const int MaximumPeskyCommands = 256;
        private const int MaximumPeskyBattleQueryLength = 4096;
        private const int MaximumPeskyBattleCommands = 128;
        private const int MaximumTapFarmingQueryLength = 4096;
        private const int MaximumStreamRuleCommands = 256;
        private const int MaximumStreamRuleQueryLength = 4096;

        private readonly string assetsDirectory;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly object stateLock = new object();
        private readonly object configLock = new object();
        private readonly Queue<string> configCommands =
            new Queue<string>();
        private readonly object interactionsLock = new object();
        private readonly object interactionsProcessingLock = new object();
        private readonly Queue<InteractionCommand> interactionControlCommands =
            new Queue<InteractionCommand>();
        private readonly Queue<InteractionCommand> interactionTestCommands =
            new Queue<InteractionCommand>();
        private readonly object dashboardLock = new object();
        private readonly object peskyLock = new object();
        private readonly Queue<string> peskyCommands =
            new Queue<string>();
        private readonly object peskyBattleLock = new object();
        private readonly object peskyBattleProcessingLock = new object();
        private readonly Queue<string> peskyBattleCommands =
            new Queue<string>();
        private readonly object tapFarmingLock = new object();
        private readonly object tapFarmingProcessingLock = new object();
        private readonly object liveEventsLock = new object();
        private readonly object streamRulesLock = new object();
        private readonly object streamRulesProcessingLock = new object();
        private readonly Queue<string> streamRuleCommands =
            new Queue<string>();
        private readonly object overlayComposerLock = new object();
        private readonly object overlayComposerProcessingLock = new object();
        private readonly object clientsLock = new object();
        private readonly List<WebSocketClient> clients =
            new List<WebSocketClient>();
        private readonly AutoResetEvent broadcastWake =
            new AutoResetEvent(false);

        private TcpListener listener;
        private Thread acceptThread;
        private Thread broadcastThread;
        private volatile bool running;
        private Func<string, string> streamRuleCommandHandler;
        private Func<string, string> dashboardSimulationHandler;
        private Func<string, long, bool> interactionControlObserver;
        private Func<string, bool> peskyBattleCommandHandler;
        private Func<string, bool> tapFarmingCommandHandler;
        private CreatorToolsOverlayComposerController
            overlayComposerController;
        private string latestMessage = "{\"type\":\"state\",\"active\":false}";
        private long latestRevision;
        private byte[] challengeLabelPng;
        private int challengeLabelRevision;
        private string latestConfigState =
            "{\"enabled\":false,\"ready\":false}";
        private string latestInteractionsState =
            "{\"ready\":false,\"available\":false," +
            "\"interactionsEnabled\":false,\"masterRevision\":0," +
            "\"queuePaused\":false,\"queueControlRevision\":0," +
            "\"pendingClearProjected\":false," +
            "\"pendingCount\":0,\"backlogCount\":0," +
            "\"deferredTestCount\":0," +
            "\"showGiftImage\":true,\"settingsRevision\":0}";
        private long latestInteractionBacklogCount;
        private long pendingInteractionTestCount;
        private int inFlightInteractionTestCommands;
        private bool latestInteractionsEnabled;
        private long latestInteractionMasterRevision;
        private long latestInteractionQueueControlRevision;
        private bool hasInteractionMasterProjection;
        private bool projectedInteractionsEnabled;
        private long projectedInteractionMasterRevision;
        private long projectedInteractionMasterCommandSequence;
        private bool hasInteractionQueueControlProjection;
        private long projectedInteractionQueueControlRevision;
        private long projectedInteractionQueueCommandSequence;
        private long nextInteractionControlSequence;
        private long interactionTestGeneration = 1L;
        private string latestDashboardState =
            "{\"ready\":false,\"schemaVersion\":2,\"revision\":0," +
            "\"engineStatus\":\"starting\",\"connections\":[]," +
            "\"counters\":{\"received\":0,\"matched\":0," +
            "\"queued\":0,\"ignored\":0,\"gifts\":0," +
            "\"valued\":0,\"likes\":0,\"follows\":0," +
            "\"subscriptions\":0,\"coins\":0,\"bits\":0}," +
            "\"events\":[]}";
        private string latestPeskyState =
            "{\"ready\":false,\"available\":false,\"enabled\":false}";
        private string latestPeskyBattleState =
            "{\"ready\":false,\"schemaVersion\":1," +
            "\"revision\":0,\"phase\":\"off\"," +
            "\"sessionId\":0,\"attempt\":0,\"capacity\":5," +
            "\"exclusive\":false,\"gameplayAvailable\":false," +
            "\"targetLevel\":\"\",\"trigger\":{" +
            "\"giftId\":\"\",\"giftName\":\"\"," +
            "\"giftImagePath\":\"\",\"coinsPerUnit\":0}," +
            "\"allowStreamAttacks\":true,\"participants\":[]," +
            "\"items\":[],\"disabledItems\":[]," +
            "\"feedback\":\"starting\",\"error\":false}";
        private string latestTapFarmingState =
            "{\"ready\":false,\"schemaVersion\":2," +
            "\"revision\":0,\"phase\":\"off\"," +
            "\"sessionId\":0,\"attempt\":0,\"enabled\":false," +
            "\"isLiveEventOwner\":false," +
            "\"blockedByLiveEvent\":\"\"," +
            "\"gameplayAvailable\":false,\"levelId\":\"\"," +
            "\"bossName\":\"\"," +
            "\"conversion\":{\"tapsPerConversion\":2," +
            "\"healthPointsPerConversion\":1," +
            "\"tapsPerHealthPoint\":2}," +
            "\"counters\":{\"totalTaps\":0,\"bankedTaps\":0," +
            "\"unconvertedTaps\":0,\"convertedHealth\":0," +
            "\"reserveHealth\":0,\"spentHealth\":0}," +
            "\"boss\":{\"currentHealth\":0,\"totalHealth\":0," +
            "\"progress\":0},\"effectiveHealth\":{" +
            "\"available\":false,\"current\":0," +
            "\"total\":0,\"ratio\":0},\"phaseIndex\":0," +
            "\"phaseCount\":0,\"overallProgress\":0," +
            "\"phases\":[],\"feedback\":\"starting\"," +
            "\"error\":false}";
        private string latestLiveEventsState =
            "{\"ready\":false,\"schemaVersion\":1,\"revision\":0," +
            "\"activeEvent\":\"\",\"status\":\"idle\"," +
            "\"stoppingEvent\":\"\",\"feedback\":\"starting\"," +
            "\"error\":false}";
        private string latestStreamRulesState =
            "{\"ready\":false,\"schemaVersion\":1,\"revision\":0," +
            "\"engineActive\":false,\"rules\":[]}";

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

        internal CreatorToolsServer(
            string assetsDirectory,
            Action<string> logInfo,
            Action<string> logWarning,
            CreatorToolsOverlayComposerController overlayComposerController)
            : this(assetsDirectory, logInfo, logWarning)
        {
            this.overlayComposerController = overlayComposerController;
        }

        internal bool Start(int port)
        {
            if (running)
                return true;

            port = Math.Max(1024, Math.Min(65535, port));
            TcpListener candidateListener = null;
            try
            {
                candidateListener = new TcpListener(
                    IPAddress.Loopback, port);
                candidateListener.Start();
                listener = candidateListener;
                Port = port;
            }
            catch (SocketException ex)
            {
                if (candidateListener != null)
                {
                    try { candidateListener.Stop(); }
                    catch { }
                }
                if (logWarning != null)
                    logWarning("Creator Tools could not bind fixed port " +
                        port + ": " + ex.SocketErrorCode + ".");
                return false;
            }

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
            SetInteractionsState(json, -1L, -1L, -1L);
        }

        internal void SetInteractionsState(
            string json, long masterRevision, long queueControlRevision)
        {
            SetInteractionsState(
                json, masterRevision, queueControlRevision, -1L);
        }

        internal void SetInteractionsState(
            string json,
            long masterRevision,
            long queueControlRevision,
            long processedControlSequence)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (interactionsLock)
            {
                latestInteractionsState = json;
                var masterProjectionAcknowledged =
                    hasInteractionMasterProjection &&
                    processedControlSequence >=
                        projectedInteractionMasterCommandSequence;
                bool enabled;
                if (TryReadBooleanProperty(
                        json, "interactionsEnabled", out enabled) &&
                    (!hasInteractionMasterProjection ||
                     masterProjectionAcknowledged))
                    latestInteractionsEnabled = enabled;
                if (masterRevision >= 0L)
                {
                    latestInteractionMasterRevision = masterRevision;
                }
                if (queueControlRevision >= 0L)
                {
                    latestInteractionQueueControlRevision =
                        queueControlRevision;
                }
                if (processedControlSequence >= 0L)
                {
                    if (masterProjectionAcknowledged)
                        hasInteractionMasterProjection = false;
                    if (hasInteractionQueueControlProjection &&
                        processedControlSequence >=
                            projectedInteractionQueueCommandSequence)
                        hasInteractionQueueControlProjection = false;
                }
            }
        }

        /// <summary>
        /// Publishes the worker-owned stream backlog without reading or
        /// serializing Unity's gameplay queue from a background thread.
        /// </summary>
        internal void SetInteractionBacklogCount(long count)
        {
            lock (interactionsLock)
                latestInteractionBacklogCount = Math.Max(0L, count);
        }

        internal void ProjectInteractionMasterState(
            bool enabled, long commandSequence)
        {
            lock (interactionsLock)
            {
                latestInteractionsEnabled = enabled;
                projectedInteractionsEnabled = enabled;
                projectedInteractionMasterRevision = Math.Max(
                    latestInteractionMasterRevision,
                    projectedInteractionMasterRevision) + 1L;
                projectedInteractionMasterCommandSequence = Math.Max(
                    0L, commandSequence);
                hasInteractionMasterProjection = true;
                if (!enabled)
                {
                    AdvanceInteractionTestGenerationLocked();
                    interactionTestCommands.Clear();
                    pendingInteractionTestCount = 0L;
                    inFlightInteractionTestCommands = 0;
                }
            }
        }

        internal void ProjectInteractionQueueCleared(long commandSequence)
        {
            lock (interactionsLock)
            {
                projectedInteractionQueueControlRevision = Math.Max(
                    latestInteractionQueueControlRevision,
                    projectedInteractionQueueControlRevision) + 1L;
                projectedInteractionQueueCommandSequence = Math.Max(
                    0L, commandSequence);
                hasInteractionQueueControlProjection = true;
            }
        }

        internal void ClearPendingInteractionTests()
        {
            lock (interactionsLock)
            {
                AdvanceInteractionTestGenerationLocked();
                interactionTestCommands.Clear();
                pendingInteractionTestCount = 0L;
                inFlightInteractionTestCommands = 0;
            }
        }

        private void AdvanceInteractionTestGenerationLocked()
        {
            interactionTestGeneration++;
            if (interactionTestGeneration <= 0L)
                interactionTestGeneration = 1L;
        }

        internal void SetInteractionControlObserver(
            Func<string, long, bool> observer)
        {
            if (observer != null)
            {
                lock (interactionsLock)
                    interactionControlObserver = observer;
                return;
            }
            lock (interactionsProcessingLock)
            {
                lock (interactionsLock)
                    interactionControlObserver = null;
            }
        }

        internal bool TryTakeInteractionCommand(
            out string command, out bool backgroundApplied)
        {
            bool isTest;
            int pendingQuantity;
            long commandSequence;
            long testGeneration;
            return TryTakeInteractionCommand(
                true,
                out command,
                out backgroundApplied,
                out isTest,
                out pendingQuantity,
                out commandSequence,
                out testGeneration);
        }

        internal bool TryTakeInteractionCommand(
            bool includeTests,
            out string command,
            out bool backgroundApplied,
            out bool isTest,
            out int pendingQuantity,
            out long commandSequence,
            out long testGeneration)
        {
            lock (interactionsLock)
            {
                InteractionCommand entry;
                if (interactionControlCommands.Count > 0)
                    entry = interactionControlCommands.Dequeue();
                else if (includeTests && interactionTestCommands.Count > 0)
                    entry = interactionTestCommands.Dequeue();
                else
                {
                    command = null;
                    backgroundApplied = false;
                    isTest = false;
                    pendingQuantity = 0;
                    commandSequence = 0L;
                    testGeneration = 0L;
                    return false;
                }
                if (entry.IsTest)
                    inFlightInteractionTestCommands++;
                command = entry.Query;
                backgroundApplied = entry.BackgroundApplied;
                isTest = entry.IsTest;
                pendingQuantity = entry.PendingQuantity;
                commandSequence = entry.Sequence;
                testGeneration = entry.TestGeneration;
                return true;
            }
        }

        internal void ProcessInteractionTestCommand(
            string query,
            int pendingQuantity,
            long testGeneration,
            Func<int> materialize)
        {
            lock (interactionsProcessingLock)
            {
                lock (interactionsLock)
                {
                    if (testGeneration != interactionTestGeneration)
                    {
                        inFlightInteractionTestCommands = Math.Max(
                            0, inFlightInteractionTestCommands - 1);
                        return;
                    }
                }

                pendingQuantity = Math.Max(0, pendingQuantity);
                var remainder = 0;
                Exception failure = null;
                try
                {
                    if (materialize == null)
                        throw new InvalidOperationException(
                            "The interaction test materializer is missing.");
                    remainder = Math.Max(
                        0, Math.Min(pendingQuantity, materialize()));
                }
                catch (Exception exception)
                {
                    // A broken test request must not poison the Unity update
                    // loop or leave the deferred counters permanently stuck.
                    // Drop this command; a user can submit a fresh test after
                    // the underlying problem is corrected.
                    failure = exception;
                    remainder = 0;
                }
                lock (interactionsLock)
                {
                    inFlightInteractionTestCommands = Math.Max(
                        0, inFlightInteractionTestCommands - 1);
                    if (testGeneration != interactionTestGeneration)
                        return;
                    pendingInteractionTestCount = Math.Max(
                        0L,
                        pendingInteractionTestCount -
                            (pendingQuantity - remainder));
                    if (remainder > 0)
                        interactionTestCommands.Enqueue(
                            new InteractionCommand(
                                query, false, true, remainder, 0L,
                                testGeneration));
                }
                if (failure != null && logWarning != null)
                    logWarning("Creator Tools dropped a deferred interaction " +
                        "test after it failed to materialize: " +
                        failure.Message);
            }
        }

        internal void SetDashboardState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (dashboardLock)
                latestDashboardState = json;
        }

        /// <summary>
        /// Registers a background-safe scheduler. The handler may validate
        /// catalog data and record a due time, but never touches Unity state.
        /// </summary>
        internal void SetDashboardSimulationHandler(
            Func<string, string> handler)
        {
            lock (dashboardLock)
                dashboardSimulationHandler = handler;
        }

        internal void SetPeskyState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (peskyLock)
                latestPeskyState = json;
        }

        internal bool TryTakePeskyCommand(out string command)
        {
            lock (peskyLock)
            {
                if (peskyCommands.Count == 0)
                {
                    command = null;
                    return false;
                }
                command = peskyCommands.Dequeue();
                return true;
            }
        }

        internal void SetPeskyBattleState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (peskyBattleLock)
                latestPeskyBattleState = json;
        }

        internal bool TryTakePeskyBattleCommand(out string command)
        {
            lock (peskyBattleLock)
            {
                if (peskyBattleCommands.Count == 0)
                {
                    command = null;
                    return false;
                }
                command = peskyBattleCommands.Dequeue();
                return true;
            }
        }

        internal void SetPeskyBattleCommandHandler(
            Func<string, bool> handler)
        {
            if (handler != null)
            {
                lock (peskyBattleLock)
                    peskyBattleCommandHandler = handler;
                return;
            }
            lock (peskyBattleProcessingLock)
            {
                lock (peskyBattleLock)
                    peskyBattleCommandHandler = null;
            }
        }

        internal void ApplyPeskyBattleMainThreadActions(Action action)
        {
            if (action == null)
                return;
            lock (peskyBattleProcessingLock)
                action();
        }

        internal void SetTapFarmingState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (tapFarmingLock)
                latestTapFarmingState = json;
        }

        internal void SetTapFarmingCommandHandler(
            Func<string, bool> handler)
        {
            if (handler != null)
            {
                lock (tapFarmingLock)
                    tapFarmingCommandHandler = handler;
                return;
            }
            lock (tapFarmingProcessingLock)
            {
                lock (tapFarmingLock)
                    tapFarmingCommandHandler = null;
            }
        }

        internal void SetOverlayComposerController(
            CreatorToolsOverlayComposerController controller)
        {
            if (controller != null)
            {
                lock (overlayComposerLock)
                    overlayComposerController = controller;
                return;
            }
            lock (overlayComposerProcessingLock)
            {
                lock (overlayComposerLock)
                    overlayComposerController = null;
            }
        }

        internal void SetLiveEventsState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (liveEventsLock)
                latestLiveEventsState = json;
        }

        internal void SetStreamRulesState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            lock (streamRulesLock)
                latestStreamRulesState = json;
        }

        /// <summary>
        /// Registers the background-safe persistent rule handler. Gameplay
        /// dispatch remains on Unity's main thread; only CRUD and JSON state
        /// publication run on the HTTP worker so browser focus is irrelevant.
        /// </summary>
        internal void SetStreamRuleCommandHandler(
            Func<string, string> handler)
        {
            if (handler != null)
            {
                lock (streamRulesLock)
                    streamRuleCommandHandler = handler;
                return;
            }
            lock (streamRulesProcessingLock)
            {
                lock (streamRulesLock)
                    streamRuleCommandHandler = null;
            }
        }

        internal bool TryTakeStreamRuleCommand(out string command)
        {
            lock (streamRulesLock)
            {
                if (streamRuleCommands.Count == 0)
                {
                    command = null;
                    return false;
                }
                command = streamRuleCommands.Dequeue();
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
                    connection.ReceiveTimeout =
                        HttpHeaderReadTimeoutMilliseconds;
                    connection.SendTimeout =
                        HttpHeaderReadTimeoutMilliseconds;
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

                if (!IsAllowedLocalRequest(request))
                {
                    WriteResponse(stream, 403, "Forbidden", "text/plain",
                        Encoding.UTF8.GetBytes("Forbidden."), false);
                    return;
                }

                if (request.ErrorStatusCode != 0)
                {
                    WriteResponse(stream, request.ErrorStatusCode,
                        request.ErrorStatusText,
                        "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(
                            "{\"ok\":false,\"error\":\"" +
                            request.ErrorCode + "\"}"), false);
                    return;
                }
                if (request.Method != "GET" && request.Method != "POST")
                {
                    WriteResponse(stream, 405, "Method Not Allowed",
                        "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(
                            "{\"ok\":false,\"error\":" +
                            "\"method_not_allowed\"}"), false,
                        "Allow: GET, POST\r\n");
                    return;
                }

                if (request.Path == "/ws" && request.IsWebSocket)
                {
                    if (request.Method != "GET")
                    {
                        WriteResponse(stream, 405,
                            "Method Not Allowed", "text/plain",
                            Encoding.UTF8.GetBytes(
                                "WebSocket upgrades require GET."), false,
                            "Allow: GET\r\n");
                        return;
                    }
                    if (!CompleteWebSocketHandshake(stream, request))
                        return;
                    connection.ReceiveTimeout = 0;
                    connection.SendTimeout = 0;
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

        private bool IsAllowedLocalRequest(HttpRequest request)
        {
            if (request == null || request.Headers == null || Port <= 0)
                return false;

            string host;
            if (!request.Headers.TryGetValue("Host", out host))
                return false;
            host = (host ?? string.Empty).Trim();
            var ipv4Host = "127.0.0.1:" + Port;
            var localHost = "localhost:" + Port;
            if (!string.Equals(host, ipv4Host,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(host, localHost,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            string fetchSite;
            if (request.Headers.TryGetValue(
                    "Sec-Fetch-Site", out fetchSite) &&
                string.Equals((fetchSite ?? string.Empty).Trim(),
                    "cross-site", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!request.IsWebSocket)
                return true;

            string origin;
            if (!request.Headers.TryGetValue("Origin", out origin) ||
                string.IsNullOrEmpty(origin))
                return true;
            Uri parsedOrigin;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out parsedOrigin) ||
                parsedOrigin.Port != Port ||
                !string.Equals(parsedOrigin.Scheme, "http",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(parsedOrigin.Host, "127.0.0.1",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parsedOrigin.Host, "localhost",
                       StringComparison.OrdinalIgnoreCase);
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
                bool stateChanged;
                try
                {
                    stateChanged = broadcastWake.WaitOne(10000, false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
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
            if (requestParts.Length < 2 ||
                string.IsNullOrEmpty(requestParts[0]))
                return null;

            var request = new HttpRequest();
            request.Method = requestParts[0].Trim().ToUpperInvariant();
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

            if (request.Method == "POST")
            {
                string transferEncoding;
                if (request.Headers.TryGetValue(
                        "Transfer-Encoding", out transferEncoding) &&
                    !string.IsNullOrEmpty(transferEncoding) &&
                    !string.Equals(transferEncoding.Trim(), "identity",
                        StringComparison.OrdinalIgnoreCase))
                {
                    request.SetError(400, "Bad Request",
                        "chunked_body_not_supported");
                    return request;
                }
                string contentLengthValue;
                int contentLength;
                if (!request.Headers.TryGetValue(
                        "Content-Length", out contentLengthValue) ||
                    !int.TryParse(contentLengthValue,
                        NumberStyles.None, CultureInfo.InvariantCulture,
                        out contentLength) || contentLength < 0)
                {
                    request.SetError(400, "Bad Request",
                        "invalid_content_length");
                    return request;
                }
                if (contentLength > MaximumHttpBodyBytes)
                {
                    request.SetError(413, "Payload Too Large",
                        "body_too_large");
                    return request;
                }
                var body = new byte[contentLength];
                var offset = 0;
                while (offset < body.Length)
                {
                    var read = stream.Read(
                        body, offset, body.Length - offset);
                    if (read <= 0)
                    {
                        request.SetError(400, "Bad Request",
                            "incomplete_body");
                        return request;
                    }
                    offset += read;
                }
                request.Body = Encoding.UTF8.GetString(body);
            }
            else
                request.Body = string.Empty;
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
            if (request.Method == "POST" &&
                path != "/api/overlay-composer/config/set" &&
                path != "/api/overlay-composer/preview/set")
            {
                WriteMethodNotAllowed(stream, "GET");
                return;
            }
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
            if (path == "/overlay/vertical" ||
                path == "/overlay/vertical/" ||
                path == "/overlay/horizontal" ||
                path == "/overlay/horizontal/" ||
                path == "/live-overlay" ||
                path == "/live-overlay/")
            {
                if (request.Method != "GET")
                {
                    WriteMethodNotAllowed(stream, "GET");
                    return;
                }
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\live-overlay.html"),
                    "text/html; charset=utf-8", false, true);
                return;
            }
            if (path == "/config" || path == "/config/" ||
                path == "/config.html" ||
                path == "/config/roulette" ||
                path == "/config/roulette/" ||
                path == "/config/roulette.html" ||
                path == "/config/interactions" ||
                path == "/config/interactions/" ||
                path == "/config/interactions.html" ||
                path == "/config/pesky" ||
                path == "/config/pesky/" ||
                path == "/config/pesky.html" ||
                path == "/config/pesky-battle" ||
                path == "/config/pesky-battle/" ||
                path == "/config/pesky-battle.html" ||
                path == "/config/tap-farming" ||
                path == "/config/tap-farming/" ||
                path == "/config/tap-farming.html" ||
                path == "/config/overlay-designer" ||
                path == "/config/overlay-designer/" ||
                path == "/config/overlay-designer.html" ||
                path == "/dashboard" ||
                path == "/dashboard/" ||
                path == "/dashboard.html")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\config.html"),
                    "text/html; charset=utf-8", false);
                return;
            }
            if (path == "/pesky-battle-overlay" ||
                path == "/pesky-battle-overlay/" ||
                path == "/pesky-battle-overlay.html")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\pesky-battle-overlay.html"),
                    "text/html; charset=utf-8", false, true);
                return;
            }
            if (path == "/pesky-battle-overlay.css")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\pesky-battle-overlay.css"),
                    "text/css; charset=utf-8", false);
                return;
            }
            if (path == "/pesky-battle-overlay.js")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\pesky-battle-overlay.js"),
                    "application/javascript; charset=utf-8", false);
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
            if (path == "/api/overlay-composer/config")
            {
                if (request.Method != "GET")
                {
                    WriteMethodNotAllowed(stream, "GET");
                    return;
                }
                CreatorToolsOverlayComposerController controller;
                lock (overlayComposerLock)
                    controller = overlayComposerController;
                if (controller == null)
                {
                    WriteOverlayComposerUnavailable(stream);
                    return;
                }
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(controller.GetConfigState()),
                    false);
                return;
            }
            if (path == "/api/overlay-composer/config/set")
            {
                if (request.Method != "POST")
                {
                    WriteMethodNotAllowed(stream, "POST");
                    return;
                }
                if (!HasJsonContentType(request))
                {
                    WriteUnsupportedMediaType(stream);
                    return;
                }
                CreatorToolsOverlayComposerResponse response;
                lock (overlayComposerProcessingLock)
                {
                    CreatorToolsOverlayComposerController controller;
                    lock (overlayComposerLock)
                        controller = overlayComposerController;
                    if (controller == null)
                    {
                        WriteOverlayComposerUnavailable(stream);
                        return;
                    }
                    response = controller.ProcessConfigCommand(
                        request.Body ?? string.Empty);
                }
                WriteOverlayComposerResponse(stream, response);
                return;
            }
            if (path == "/api/overlay-composer/preview")
            {
                if (request.Method != "GET")
                {
                    WriteMethodNotAllowed(stream, "GET");
                    return;
                }
                CreatorToolsOverlayComposerController controller;
                lock (overlayComposerLock)
                    controller = overlayComposerController;
                if (controller == null)
                {
                    WriteOverlayComposerUnavailable(stream);
                    return;
                }
                WriteOverlayComposerResponse(stream,
                    controller.GetPreviewState(
                        ReadQueryValue(request.Query, "profile")));
                return;
            }
            if (path == "/api/overlay-composer/preview/set")
            {
                if (request.Method != "POST")
                {
                    WriteMethodNotAllowed(stream, "POST");
                    return;
                }
                if (!HasJsonContentType(request))
                {
                    WriteUnsupportedMediaType(stream);
                    return;
                }
                CreatorToolsOverlayComposerResponse response;
                lock (overlayComposerProcessingLock)
                {
                    CreatorToolsOverlayComposerController controller;
                    lock (overlayComposerLock)
                        controller = overlayComposerController;
                    if (controller == null)
                    {
                        WriteOverlayComposerUnavailable(stream);
                        return;
                    }
                    response = controller.ProcessPreviewCommand(
                        request.Body ?? string.Empty);
                }
                WriteOverlayComposerResponse(stream, response);
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
                var accepted = false;
                lock (configLock)
                {
                    if (configCommands.Count < MaximumConfigCommands)
                    {
                        configCommands.Enqueue(request.Query ?? string.Empty);
                        accepted = true;
                    }
                }
                WriteResponse(stream, accepted ? 202 : 429,
                    accepted ? "Accepted" : "Too Many Requests",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}"
                        : "{\"ok\":false,\"error\":" +
                          "\"config_command_queue_full\"}"), false);
                return;
            }
            if (path == "/api/config/interactions")
            {
                string json;
                lock (interactionsLock)
                {
                    json = ReplaceNonnegativeIntegerProperty(
                        latestInteractionsState,
                        "backlogCount",
                        latestInteractionBacklogCount);
                    json = ReplaceNonnegativeIntegerProperty(
                        json,
                        "deferredTestCount",
                        pendingInteractionTestCount);
                    if (hasInteractionMasterProjection)
                    {
                        json = ReplaceBooleanProperty(
                            json,
                            "interactionsEnabled",
                            projectedInteractionsEnabled);
                        json = ReplaceNonnegativeIntegerProperty(
                            json,
                            "masterRevision",
                            projectedInteractionMasterRevision);
                        if (!projectedInteractionsEnabled)
                        {
                            json = ReplaceBooleanProperty(
                                json, "queuePaused", false);
                            json = ReplaceBooleanProperty(
                                json, "available", false);
                        }
                    }
                    if (hasInteractionQueueControlProjection)
                    {
                        json = ReplaceNonnegativeIntegerProperty(
                            json,
                            "queueControlRevision",
                            projectedInteractionQueueControlRevision);
                        json = ReplaceBooleanProperty(
                            json, "pendingClearProjected", true);
                        json = ReplaceNonnegativeIntegerProperty(
                            json, "pendingCount", 0L);
                        long activeCount;
                        if (TryReadNonnegativeIntegerProperty(
                                json, "activeCount", out activeCount))
                            json = ReplaceNonnegativeIntegerProperty(
                                json, "queueCount", activeCount);
                    }
                }
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/tap-farming-overlay" ||
                path == "/tap-farming-overlay/" ||
                path == "/tap-farming-overlay.html")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\tap-farming-overlay.html"),
                    "text/html; charset=utf-8", false, true);
                return;
            }
            if (path == "/tap-farming-overlay.css")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\tap-farming-overlay.css"),
                    "text/css; charset=utf-8", false);
                return;
            }
            if (path == "/tap-farming-overlay.js")
            {
                ServeFile(stream, Path.Combine(assetsDirectory,
                    "creator-tools\\tap-farming-overlay.js"),
                    "application/javascript; charset=utf-8", false);
                return;
            }
            if (path == "/api/config/interactions/test")
            {
                var query = request.Query ?? string.Empty;
                var quantity = ParseInteractionTestQuantity(query);
                var accepted = false;
                var enabled = false;
                lock (interactionsProcessingLock)
                {
                    lock (interactionsLock)
                    {
                        enabled = hasInteractionMasterProjection
                            ? projectedInteractionsEnabled
                            : latestInteractionsEnabled;
                        if (enabled && interactionTestCommands.Count +
                                inFlightInteractionTestCommands <
                                MaximumInteractionTestCommands &&
                            pendingInteractionTestCount + quantity <=
                                MaximumQueuedInteractionTests)
                        {
                            interactionTestCommands.Enqueue(
                                new InteractionCommand(
                                    query, false, true, quantity, 0L,
                                    interactionTestGeneration));
                            pendingInteractionTestCount += quantity;
                            accepted = true;
                        }
                    }
                }
                WriteResponse(stream,
                    accepted ? 202 : enabled ? 429 : 409,
                    accepted ? "Accepted" : enabled
                        ? "Too Many Requests"
                        : "Conflict",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}"
                        : enabled
                            ? "{\"ok\":false,\"error\":" +
                              "\"interaction_command_queue_full\"}"
                            : "{\"ok\":false,\"error\":" +
                              "\"interactions_disabled\"}"), false);
                return;
            }
            if (path == "/api/config/interactions/set")
            {
                var query = request.Query ?? string.Empty;
                var backgroundApplied = false;
                var accepted = false;
                var commandSequence = 0L;
                lock (interactionsProcessingLock)
                {
                    lock (interactionsLock)
                    {
                        accepted = interactionControlCommands.Count <
                            MaximumInteractionControlCommands;
                        if (accepted)
                        {
                            nextInteractionControlSequence++;
                            if (nextInteractionControlSequence <= 0L)
                                nextInteractionControlSequence = 1L;
                            commandSequence =
                                nextInteractionControlSequence;
                        }
                    }
                    if (accepted)
                    {
                        Func<string, long, bool> observer;
                        lock (interactionsLock)
                            observer = interactionControlObserver;
                        if (observer != null)
                        {
                            try
                            {
                                backgroundApplied = observer(
                                    query, commandSequence);
                            }
                            catch (Exception exception)
                            {
                                if (logWarning != null)
                                    logWarning("Creator Tools could not apply " +
                                        "an interaction control in the " +
                                        "background: " + exception.Message);
                            }
                        }
                        lock (interactionsLock)
                            interactionControlCommands.Enqueue(
                                new InteractionCommand(
                                    query, backgroundApplied, false, 0,
                                    commandSequence, 0L));
                    }
                }
                WriteResponse(stream, accepted ? 202 : 429,
                    accepted ? "Accepted" : "Too Many Requests",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}"
                        : "{\"ok\":false,\"error\":" +
                          "\"interaction_control_queue_full\"}"), false);
                return;
            }
            if (path == "/api/config/interactions/rules")
            {
                string json;
                lock (streamRulesLock)
                    json = latestStreamRulesState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/config/interactions/rules/set")
            {
                var query = request.Query ?? string.Empty;
                if (query.Length > MaximumStreamRuleQueryLength)
                    query = query.Substring(0, MaximumStreamRuleQueryLength);

                Func<string, string> handler;
                lock (streamRulesLock)
                    handler = streamRuleCommandHandler;

                if (handler != null)
                {
                    try
                    {
                        string state;
                        lock (streamRulesProcessingLock)
                        {
                            state = handler(query);
                            if (string.IsNullOrEmpty(state))
                                throw new InvalidOperationException(
                                    "The stream-rule handler returned no " +
                                    "state.");
                            // Publish under the same serialization gate as
                            // persistence so concurrent HTTP clients cannot
                            // overwrite a newer snapshot with an older one.
                            SetStreamRulesState(state);
                        }
                        WriteResponse(stream, 200, "OK",
                            "application/json; charset=utf-8",
                            Encoding.UTF8.GetBytes(state),
                            false);
                    }
                    catch (Exception exception)
                    {
                        if (logWarning != null)
                            logWarning("Creator Tools could not process a " +
                                "stream-rule command: " +
                                exception.Message);
                        WriteResponse(stream, 500,
                            "Internal Server Error",
                            "application/json; charset=utf-8",
                            Encoding.UTF8.GetBytes(
                                "{\"ok\":false,\"error\":" +
                                "\"rules_processing_failed\"}"), false);
                    }
                    return;
                }

                var accepted = false;
                lock (streamRulesLock)
                {
                    if (streamRuleCommands.Count < MaximumStreamRuleCommands)
                    {
                        streamRuleCommands.Enqueue(query);
                        accepted = true;
                    }
                }
                WriteResponse(stream,
                    accepted ? 202 : 429,
                    accepted ? "Accepted" : "Too Many Requests",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}" :
                          "{\"ok\":false,\"error\":" +
                          "\"rules_queue_full\"}"),
                    false);
                return;
            }
            if (path == "/api/dashboard")
            {
                string json;
                lock (dashboardLock)
                    json = latestDashboardState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/dashboard/simulate")
            {
                var query = request.Query ?? string.Empty;
                if (query.Length > MaximumDashboardQueryLength)
                    query = query.Substring(0, MaximumDashboardQueryLength);

                Func<string, string> handler;
                lock (dashboardLock)
                    handler = dashboardSimulationHandler;
                if (handler == null)
                {
                    WriteResponse(stream, 503, "Service Unavailable",
                        "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(
                            "{\"ok\":false,\"error\":" +
                            "\"simulation_unavailable\"}"), false);
                    return;
                }

                try
                {
                    var error = handler(query) ?? string.Empty;
                    var accepted = error.Length == 0;
                    var queueFull = error == "simulation_queue_full";
                    WriteResponse(stream,
                        accepted ? 202 : queueFull ? 429 : 400,
                        accepted ? "Accepted" : queueFull
                            ? "Too Many Requests"
                            : "Bad Request",
                        "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(accepted
                            ? "{\"ok\":true,\"queued\":true}"
                            : "{\"ok\":false,\"error\":\"" +
                              error + "\"}"), false);
                }
                catch (Exception exception)
                {
                    if (logWarning != null)
                        logWarning("Creator Tools could not schedule a " +
                            "dashboard simulation: " + exception.Message);
                    WriteResponse(stream, 500,
                        "Internal Server Error",
                        "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(
                            "{\"ok\":false,\"error\":" +
                            "\"simulation_scheduling_failed\"}"), false);
                }
                return;
            }
            if (path == "/api/config/pesky")
            {
                string json;
                lock (peskyLock)
                    json = latestPeskyState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/config/pesky/set")
            {
                var query = request.Query ?? string.Empty;
                var accepted = false;
                lock (peskyLock)
                {
                    if (peskyCommands.Count < MaximumPeskyCommands)
                    {
                        peskyCommands.Enqueue(query);
                        accepted = true;
                    }
                }
                WriteResponse(stream, accepted ? 202 : 429,
                    accepted ? "Accepted" : "Too Many Requests",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}"
                        : "{\"ok\":false,\"error\":" +
                          "\"pesky_command_queue_full\"}"), false);
                return;
            }
            if (path == "/api/config/pesky-battle")
            {
                string json;
                lock (peskyBattleLock)
                    json = latestPeskyBattleState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/config/pesky-battle/set")
            {
                var query = request.Query ?? string.Empty;
                if (query.Length > MaximumPeskyBattleQueryLength)
                    query = query.Substring(0,
                        MaximumPeskyBattleQueryLength);
                var accepted = false;
                Func<string, bool> handler;
                lock (peskyBattleLock)
                    handler = peskyBattleCommandHandler;
                if (handler != null)
                {
                    try
                    {
                        lock (peskyBattleProcessingLock)
                            accepted = handler(query);
                    }
                    catch (Exception exception)
                    {
                        if (logWarning != null)
                            logWarning("Creator Tools could not process a " +
                                "Pesky Battle command in the background: " +
                                exception.Message);
                    }
                }
                else
                {
                    lock (peskyBattleLock)
                    {
                        if (peskyBattleCommands.Count <
                            MaximumPeskyBattleCommands)
                        {
                            peskyBattleCommands.Enqueue(query);
                            accepted = true;
                        }
                    }
                }
                WriteResponse(stream, accepted ? 202 : 429,
                    accepted ? "Accepted" : "Too Many Requests",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}"
                        : "{\"ok\":false,\"error\":" +
                          "\"battle_command_queue_full\"}"), false);
                return;
            }
            if (path == "/api/config/tap-farming")
            {
                string json;
                lock (tapFarmingLock)
                    json = latestTapFarmingState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
                return;
            }
            if (path == "/api/config/tap-farming/set")
            {
                var query = request.Query ?? string.Empty;
                if (query.Length > MaximumTapFarmingQueryLength)
                    query = query.Substring(
                        0, MaximumTapFarmingQueryLength);
                Func<string, bool> handler;
                lock (tapFarmingLock)
                    handler = tapFarmingCommandHandler;
                if (handler == null)
                {
                    WriteResponse(stream, 503, "Service Unavailable",
                        "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(
                            "{\"ok\":false,\"error\":" +
                            "\"tap_farming_unavailable\"}"), false);
                    return;
                }

                var accepted = false;
                try
                {
                    lock (tapFarmingProcessingLock)
                        accepted = handler(query);
                }
                catch (Exception exception)
                {
                    if (logWarning != null)
                        logWarning("Creator Tools could not process a " +
                            "Tap Farming command in the background: " +
                            exception.Message);
                }
                WriteResponse(stream,
                    accepted ? 202 : 409,
                    accepted ? "Accepted" : "Conflict",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(accepted
                        ? "{\"ok\":true}"
                        : "{\"ok\":false,\"error\":" +
                          "\"tap_farming_command_rejected\"}"), false);
                return;
            }
            if (path == "/api/config/live-events")
            {
                string json;
                lock (liveEventsLock)
                    json = latestLiveEventsState;
                WriteResponse(stream, 200, "OK",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(json), false);
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
            bool cache, bool allowSameOriginFrame = false)
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
            WriteResponse(stream, 200, "OK", contentType, body, cache,
                null, allowSameOriginFrame);
        }

        private static void WriteResponse(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string contentType,
            byte[] body,
            bool cache,
            string additionalHeaders = null,
            bool allowSameOriginFrame = false)
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
                         (allowSameOriginFrame
                             ? "X-Frame-Options: SAMEORIGIN\r\n" +
                               "Content-Security-Policy: " +
                               "frame-ancestors 'self'\r\n"
                             : "X-Frame-Options: DENY\r\n" +
                               "Content-Security-Policy: " +
                               "frame-ancestors 'none'\r\n") +
                         (additionalHeaders ?? string.Empty) + "\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static void WriteOverlayComposerResponse(
            NetworkStream stream,
            CreatorToolsOverlayComposerResponse response)
        {
            if (response == null)
            {
                WriteResponse(stream, 500, "Internal Server Error",
                    "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(
                        "{\"ready\":false,\"feedback\":" +
                        "\"empty_response\",\"error\":true}"), false);
                return;
            }
            WriteResponse(stream, response.StatusCode,
                response.StatusText,
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(response.Json), false);
        }

        private static void WriteOverlayComposerUnavailable(
            NetworkStream stream)
        {
            WriteResponse(stream, 503, "Service Unavailable",
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(
                    "{\"ready\":false,\"feedback\":" +
                    "\"overlay_composer_unavailable\"," +
                    "\"error\":true}"), false);
        }

        private static void WriteUnsupportedMediaType(NetworkStream stream)
        {
            WriteResponse(stream, 415, "Unsupported Media Type",
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(
                    "{\"ready\":false,\"feedback\":" +
                    "\"application_json_required\"," +
                    "\"error\":true}"), false);
        }

        private static void WriteMethodNotAllowed(
            NetworkStream stream, string allowed)
        {
            WriteResponse(stream, 405, "Method Not Allowed",
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(
                    "{\"ready\":false,\"feedback\":" +
                    "\"method_not_allowed\",\"error\":true}"), false,
                "Allow: " + (allowed ?? "GET") + "\r\n");
        }

        private static bool HasJsonContentType(HttpRequest request)
        {
            string contentType;
            if (request == null || request.Headers == null ||
                !request.Headers.TryGetValue(
                    "Content-Type", out contentType))
                return false;
            contentType = (contentType ?? string.Empty).Trim();
            var separator = contentType.IndexOf(';');
            if (separator >= 0)
                contentType = contentType.Substring(0, separator).Trim();
            return string.Equals(contentType, "application/json",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadQueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(key))
                return string.Empty;
            var pairs = query.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var separator = pairs[i].IndexOf('=');
                var rawKey = separator < 0
                    ? pairs[i]
                    : pairs[i].Substring(0, separator);
                var rawValue = separator < 0
                    ? string.Empty
                    : pairs[i].Substring(separator + 1);
                try
                {
                    rawKey = Uri.UnescapeDataString(
                        rawKey.Replace('+', ' '));
                    rawValue = Uri.UnescapeDataString(
                        rawValue.Replace('+', ' '));
                }
                catch
                {
                    continue;
                }
                if (string.Equals(rawKey, key,
                        StringComparison.OrdinalIgnoreCase))
                    return rawValue.Length <= 128
                        ? rawValue : rawValue.Substring(0, 128);
            }
            return string.Empty;
        }

        private static string ReplaceNonnegativeIntegerProperty(
            string json, string property, long value)
        {
            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(property))
                return json ?? string.Empty;
            var marker = "\"" + property + "\":";
            var markerIndex = json.IndexOf(
                marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return json;
            var start = markerIndex + marker.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            var end = start;
            while (end < json.Length && char.IsDigit(json[end]))
                end++;
            if (end == start)
                return json;
            return json.Substring(0, start) +
                Math.Max(0L, value).ToString(CultureInfo.InvariantCulture) +
                json.Substring(end);
        }

        private static string ReplaceBooleanProperty(
            string json, string property, bool value)
        {
            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(property))
                return json ?? string.Empty;
            var marker = "\"" + property + "\":";
            var markerIndex = json.IndexOf(
                marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return json;
            var start = markerIndex + marker.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            var length = json.IndexOf(
                    "true", start, StringComparison.Ordinal) == start
                ? 4
                : json.IndexOf(
                    "false", start, StringComparison.Ordinal) == start
                    ? 5
                    : 0;
            if (length == 0)
                return json;
            return json.Substring(0, start) +
                (value ? "true" : "false") +
                json.Substring(start + length);
        }

        private static bool TryReadBooleanProperty(
            string json, string property, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(property))
                return false;
            var marker = "\"" + property + "\":";
            var markerIndex = json.IndexOf(
                marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;
            var start = markerIndex + marker.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            if (json.IndexOf(
                    "true", start, StringComparison.Ordinal) == start)
            {
                value = true;
                return true;
            }
            return json.IndexOf(
                "false", start, StringComparison.Ordinal) == start;
        }

        private static bool TryReadNonnegativeIntegerProperty(
            string json, string property, out long value)
        {
            value = 0L;
            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(property))
                return false;
            var marker = "\"" + property + "\":";
            var markerIndex = json.IndexOf(
                marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;
            var start = markerIndex + marker.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            var end = start;
            while (end < json.Length && char.IsDigit(json[end]))
                end++;
            return end > start && long.TryParse(
                json.Substring(start, end - start),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static int ParseInteractionTestQuantity(string query)
        {
            if (string.IsNullOrEmpty(query))
                return 1;
            var pairs = query.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var separator = pairs[i].IndexOf('=');
                if (separator < 0)
                    continue;
                string key;
                string value;
                try
                {
                    key = Uri.UnescapeDataString(
                        pairs[i].Substring(0, separator).Replace('+', ' '));
                    value = Uri.UnescapeDataString(
                        pairs[i].Substring(separator + 1).Replace('+', ' '));
                }
                catch
                {
                    continue;
                }
                if (!string.Equals(
                        key, "quantity", StringComparison.OrdinalIgnoreCase))
                    continue;
                int quantity;
                if (!int.TryParse(
                        value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out quantity))
                    return 1;
                return Math.Max(
                    1,
                    Math.Min(
                        CreatorToolsInteractionQueue.MaximumBatchSize,
                        quantity));
            }
            return 1;
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
            if (extension == ".json")
                return "application/json; charset=utf-8";
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

        private sealed class InteractionCommand
        {
            internal readonly string Query;
            internal readonly bool BackgroundApplied;
            internal readonly bool IsTest;
            internal readonly int PendingQuantity;
            internal readonly long Sequence;
            internal readonly long TestGeneration;

            internal InteractionCommand(
                string query,
                bool backgroundApplied,
                bool isTest,
                int pendingQuantity,
                long sequence,
                long testGeneration)
            {
                Query = query ?? string.Empty;
                BackgroundApplied = backgroundApplied;
                IsTest = isTest;
                PendingQuantity = Math.Max(0, pendingQuantity);
                Sequence = Math.Max(0L, sequence);
                TestGeneration = Math.Max(0L, testGeneration);
            }
        }

        private sealed class HttpRequest
        {
            internal string Method;
            internal string Path;
            internal string Query;
            internal string Body;
            internal Dictionary<string, string> Headers;
            internal bool IsWebSocket;
            internal int ErrorStatusCode;
            internal string ErrorStatusText;
            internal string ErrorCode;

            internal void SetError(
                int statusCode, string statusText, string errorCode)
            {
                ErrorStatusCode = statusCode;
                ErrorStatusText = statusText ?? string.Empty;
                ErrorCode = errorCode ?? string.Empty;
            }
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
