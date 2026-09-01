using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Owns the hidden TikFinity adapter process. Background callbacks parse
    /// and enqueue bounded messages; CreatorToolsStreamWorker consumes them.
    /// </summary>
    internal sealed class TikFinityCompanionHost : IDisposable,
        ICreatorToolsStreamSource
    {
        internal const string RelativeExecutablePath =
            "companion\\LaPichiRuleta.TikFinity.exe";
        private const int MaximumPendingMessages = 1024;
        private const int MaximumOverflowLikeViewers = 4096;
        private const int MaximumRememberedLikeIdentities = 65536;
        private const int MaximumLikeCountPerMessage = 1000000;
        private const int MaximumRestarts = 1000000;
        private const int QueuePressureReportDelaySeconds = 2;
        private const int OutputDrainTimeoutMilliseconds = 250;

        private readonly string executablePath;
        private readonly string workingDirectory;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly object queueLock = new object();
        private readonly LinkedList<CreatorToolsStreamMessage> pending =
            new LinkedList<CreatorToolsStreamMessage>();
        private readonly LinkedList<LikeAccumulator> overflowLikes =
            new LinkedList<LikeAccumulator>();
        private readonly Dictionary<string, LinkedListNode<LikeAccumulator>>
            overflowLikesByViewer =
                new Dictionary<string, LinkedListNode<LikeAccumulator>>(
                    StringComparer.Ordinal);
        private readonly HashSet<string> rememberedLikeIdentities =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> rememberedLikeIdentityOrder =
            new Queue<string>();
        private readonly ManualResetEvent outputEof =
            new ManualResetEvent(true);
        private readonly ManualResetEvent errorEof =
            new ManualResetEvent(true);

        private volatile Process process;
        private LinkedListNode<LikeAccumulator> syntheticLikeAccumulator;
        private DateTime nextStartAt = DateTime.MinValue;
        private DateTime queuePressureReportAt = DateTime.MaxValue;
        private int restartAttempt;
        private int queueHighWaterMark;
        private long coalescedConnectionUpdates;
        private long coalescedLikeEvents;
        private long coalescedLikeCount;
        private long deduplicatedLikeEvents;
        private long syntheticLikeCount;
        private long saturatedLikeAccumulators;
        private long droppedConnectionUpdates;
        private long droppedGiftProgressEvents;
        private long droppedLikeEvents;
        private long droppedOtherEvents;
        private long droppedPriorityEvents;
        private long preservedPriorityEvents;
        private bool disposed;
        private bool outputDrainWarningReported;
        private string lastLocalStatus = string.Empty;

        internal TikFinityCompanionHost(
            string pluginDirectory,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            var root = Path.GetFullPath(pluginDirectory ?? string.Empty);
            executablePath = Path.Combine(root,
                RelativeExecutablePath.Replace('\\',
                    Path.DirectorySeparatorChar));
            workingDirectory = Path.GetDirectoryName(executablePath) ?? root;
        }

        internal string ExecutablePath
        {
            get { return executablePath; }
        }

        public void Update()
        {
            if (disposed)
                return;
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                        return;
                    var exitCode = process.ExitCode;
                    if (!WaitForRedirectedOutput(process))
                        return;
                    CleanupProcess(false);
                    ScheduleRestart("companion_exited", "El acompañante de " +
                        "TikFinity terminó (código " + exitCode + ").");
                }
                catch (Exception exception)
                {
                    CleanupProcess(false);
                    ScheduleRestart("companion_process_error",
                        exception.Message);
                }
            }

            if (DateTime.UtcNow < nextStartAt)
                return;
            TryStart();
        }

        public bool TryTakeMessage(out CreatorToolsStreamMessage message)
        {
            string pressureReport;
            lock (queueLock)
            {
                RefillOnePendingLike();
                if (pending.Count == 0)
                {
                    message = null;
                    pressureReport = TakeQueuePressureReport(
                        DateTime.UtcNow, true);
                }
                else
                {
                    message = pending.First.Value;
                    pending.RemoveFirst();
                    RefillOnePendingLike();
                    pressureReport = TakeQueuePressureReport(
                        DateTime.UtcNow, pending.Count == 0);
                }
            }
            LogQueuePressure(pressureReport);
            return message != null;
        }

        private void TryStart()
        {
            if (!File.Exists(executablePath))
            {
                PublishLocalStatus("error", "companion_missing",
                    "No se encontró el acompañante de TikFinity.");
                nextStartAt = DateTime.UtcNow.AddSeconds(10);
                return;
            }

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--parent-pid " +
                        Process.GetCurrentProcess().Id.ToString(
                            CultureInfo.InvariantCulture),
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var candidate = new Process { StartInfo = info };
                candidate.OutputDataReceived += OnOutputDataReceived;
                candidate.ErrorDataReceived += OnErrorDataReceived;
                outputEof.Reset();
                errorEof.Reset();
                outputDrainWarningReported = false;
                if (!candidate.Start())
                    throw new InvalidOperationException(
                        "El proceso no pudo iniciarse.");
                process = candidate;
                PublishLocalStatus("starting", "companion_starting",
                    "Iniciando el acompañante de TikFinity.");
                candidate.BeginOutputReadLine();
                candidate.BeginErrorReadLine();
                if (logInfo != null)
                    logInfo("TikFinity companion started (PID " +
                        candidate.Id + ").");
            }
            catch (Exception exception)
            {
                CleanupProcess(true);
                ScheduleRestart("companion_start_failed",
                    exception.Message);
            }
        }

        private void OnOutputDataReceived(
            object sender, DataReceivedEventArgs args)
        {
            if (!ReferenceEquals(sender, process))
                return;
            if (args.Data == null)
            {
                SignalEof(outputEof);
                return;
            }
            if (disposed || args.Data.Length == 0)
                return;
            CreatorToolsStreamMessage message;
            if (!TikFinityCompanionProtocol.TryParse(args.Data, out message))
            {
                if (logWarning != null)
                    logWarning("TikFinity companion emitted an invalid " +
                        "protocol message.");
                return;
            }
            if (message.Connection != null &&
                message.Connection.State == "connected")
            {
                restartAttempt = 0;
                lastLocalStatus = string.Empty;
            }
            Enqueue(message);
        }

        private void OnErrorDataReceived(
            object sender, DataReceivedEventArgs args)
        {
            if (!ReferenceEquals(sender, process))
                return;
            if (args.Data == null)
            {
                SignalEof(errorEof);
                return;
            }
            if (disposed || args.Data.Length == 0 || logWarning == null)
                return;
            var text = args.Data.Trim();
            if (text.Length > 512)
                text = text.Substring(0, 512);
            logWarning("TikFinity companion: " + text);
        }

        private void ScheduleRestart(string code, string message)
        {
            restartAttempt = Math.Min(MaximumRestarts, restartAttempt + 1);
            var delaySeconds = Math.Min(30,
                (int)Math.Pow(2d, Math.Min(5, restartAttempt - 1)));
            nextStartAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            PublishLocalStatus("reconnecting", code, message);
        }

        private void PublishLocalStatus(
            string state, string code, string message)
        {
            var key = state + ":" + code + ":" + message;
            if (key == lastLocalStatus)
                return;
            lastLocalStatus = key;
            Enqueue(new CreatorToolsStreamMessage
            {
                Connection = new CreatorToolsStreamConnectionUpdate
                {
                    State = state,
                    MessageCode = code,
                    Message = message ?? string.Empty,
                    RetryAttempt = restartAttempt,
                    OccurredAt = DateTime.UtcNow.ToString(
                        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                        CultureInfo.InvariantCulture)
                }
            });
        }

        private void Enqueue(CreatorToolsStreamMessage message)
        {
            if (message == null)
                return;
            lock (queueLock)
            {
                if (disposed)
                    return;
                if (IsLikeMessage(message) &&
                    !TryRememberLikeIdentity(message.Event))
                {
                    IncrementSaturated(ref deduplicatedLikeEvents);
                    ScheduleQueuePressureReport();
                    return;
                }
                if (pending.Count < MaximumPendingMessages)
                {
                    pending.AddLast(message);
                    TrackQueueHighWaterMark();
                    return;
                }

                // A status update supersedes an older status for the same
                // connection. Coalescing it at capacity keeps the most recent
                // control-plane state without sacrificing a stream event.
                if (TryCoalesceConnectionUpdate(message))
                    return;

                var incomingPriority = MessagePriority(message);
                var candidate = FindEvictionCandidate(incomingPriority);
                if (candidate == null)
                {
                    if (incomingPriority == QueuePriority.Low &&
                        IsLikeMessage(message))
                    {
                        PreserveLikeMessage(message, true);
                        return;
                    }
                    RecordDroppedMessage(message);
                    return;
                }

                var evicted = candidate.Value;
                pending.Remove(candidate);
                if (IsLikeMessage(evicted))
                    PreserveLikeMessage(evicted,
                        !IsCoalescedLikeMessage(evicted));
                else
                    RecordDroppedMessage(evicted);
                if (incomingPriority == QueuePriority.Critical)
                    IncrementSaturated(ref preservedPriorityEvents);
                pending.AddLast(message);
                TrackQueueHighWaterMark();
            }
        }

        /// <summary>
        /// Likes are the only high-volume actionable event. When the bounded
        /// transport queue is full, retain their quantity in a bounded set of
        /// per-viewer accumulators. This keeps every normal burst exact while
        /// preserving the per-viewer semantics used by "cada X likes" rules.
        /// An explicit anonymous accumulator is the defensive last resort for
        /// more simultaneous viewer identities than the bounded map allows;
        /// it still reaches global tap farming/dashboard counters, but cannot
        /// be attributed to a viewer rule.
        /// </summary>
        private void PreserveLikeMessage(
            CreatorToolsStreamMessage message, bool recordCoalescing)
        {
            if (!IsLikeMessage(message))
                return;

            var streamEvent = message.Event;
            var count = Math.Max(1, streamEvent.Count);
            var viewerKey = LikeViewerKey(streamEvent);
            LinkedListNode<LikeAccumulator> node;
            if (viewerKey.Length > 0 &&
                overflowLikesByViewer.TryGetValue(viewerKey, out node))
            {
                AddLikeCount(node.Value, count);
            }
            else if (viewerKey.Length > 0 &&
                     overflowLikesByViewer.Count <
                        MaximumOverflowLikeViewers)
            {
                var accumulator = new LikeAccumulator
                {
                    ViewerKey = viewerKey,
                    Template = CloneLikeEvent(streamEvent, false),
                    Count = count
                };
                node = overflowLikes.AddLast(accumulator);
                overflowLikesByViewer.Add(viewerKey, node);
            }
            else
            {
                node = syntheticLikeAccumulator;
                if (node == null)
                {
                    var accumulator = new LikeAccumulator
                    {
                        ViewerKey = string.Empty,
                        Template = CloneLikeEvent(streamEvent, true),
                        Count = count,
                        Synthetic = true
                    };
                    node = overflowLikes.AddLast(accumulator);
                    syntheticLikeAccumulator = node;
                }
                else
                    AddLikeCount(node.Value, count);
                AddSaturated(ref syntheticLikeCount, count);
            }

            if (recordCoalescing)
            {
                IncrementSaturated(ref coalescedLikeEvents);
                AddSaturated(ref coalescedLikeCount, count);
            }
            ScheduleQueuePressureReport();
        }

        private void AddLikeCount(LikeAccumulator accumulator, int count)
        {
            if (accumulator.Count > long.MaxValue - count)
            {
                accumulator.Count = long.MaxValue;
                IncrementSaturated(ref saturatedLikeAccumulators);
                return;
            }
            accumulator.Count += count;
        }

        private void RefillOnePendingLike()
        {
            if (pending.Count >= MaximumPendingMessages ||
                overflowLikes.Count == 0)
                return;

            var node = overflowLikes.First;
            var accumulator = node.Value;
            var count = (int)Math.Min(
                (long)MaximumLikeCountPerMessage, accumulator.Count);
            accumulator.Count -= count;
            overflowLikes.Remove(node);
            if (accumulator.ViewerKey.Length > 0)
                overflowLikesByViewer.Remove(accumulator.ViewerKey);
            else if (accumulator.Synthetic)
                syntheticLikeAccumulator = null;
            if (accumulator.Count > 0L)
            {
                var replacement = overflowLikes.AddLast(accumulator);
                if (accumulator.ViewerKey.Length > 0)
                    overflowLikesByViewer[accumulator.ViewerKey] =
                        replacement;
                else if (accumulator.Synthetic)
                    syntheticLikeAccumulator = replacement;
            }

            var streamEvent = CloneLikeEvent(
                accumulator.Template, accumulator.Synthetic);
            var aggregateId = "host-like-" +
                Guid.NewGuid().ToString("N");
            streamEvent.EventId = aggregateId;
            streamEvent.IdempotencyKey = aggregateId;
            streamEvent.Count = count;
            streamEvent.RawEventType = accumulator.Synthetic
                ? "host_coalesced_like_overflow"
                : "host_coalesced_like";
            pending.AddLast(new CreatorToolsStreamMessage
            {
                Event = streamEvent
            });
            TrackQueueHighWaterMark();
        }

        private bool TryRememberLikeIdentity(
            CreatorToolsStreamEvent streamEvent)
        {
            if (streamEvent == null)
                return true;
            var identity = (streamEvent.ConnectionId ?? string.Empty) +
                ":" + (streamEvent.IdempotencyKey ?? string.Empty);
            if (identity == ":")
                identity = (streamEvent.ConnectionId ?? string.Empty) +
                    ":event:" + (streamEvent.EventId ?? string.Empty);
            if (identity == ":event:")
                return true;
            if (!rememberedLikeIdentities.Add(identity))
                return false;
            rememberedLikeIdentityOrder.Enqueue(identity);
            while (rememberedLikeIdentityOrder.Count >
                   MaximumRememberedLikeIdentities)
                rememberedLikeIdentities.Remove(
                    rememberedLikeIdentityOrder.Dequeue());
            return true;
        }

        private static string LikeViewerKey(
            CreatorToolsStreamEvent streamEvent)
        {
            if (streamEvent == null)
                return string.Empty;
            var connectionId = (streamEvent.ConnectionId ?? string.Empty)
                .Trim();
            var userId = (streamEvent.UserId ?? string.Empty).Trim();
            if (userId.Length > 0)
                return connectionId + "\n" + "id:" + userId;
            var userName = (streamEvent.UserName ?? string.Empty).Trim();
            return userName.Length == 0
                ? string.Empty
                : connectionId + "\n" + "name:" +
                    userName.ToLowerInvariant();
        }

        private static CreatorToolsStreamEvent CloneLikeEvent(
            CreatorToolsStreamEvent source, bool anonymous)
        {
            return new CreatorToolsStreamEvent
            {
                EventId = source.EventId ?? string.Empty,
                IdempotencyKey = source.IdempotencyKey ?? string.Empty,
                ConnectionId = source.ConnectionId ?? string.Empty,
                Platform = source.Platform ?? string.Empty,
                Connector = source.Connector ?? string.Empty,
                Type = "like",
                UserName = anonymous
                    ? string.Empty
                    : source.UserName ?? string.Empty,
                UserDisplayName = anonymous
                    ? string.Empty
                    : source.UserDisplayName ?? string.Empty,
                UserAvatarUrl = anonymous
                    ? string.Empty
                    : source.UserAvatarUrl ?? string.Empty,
                UserId = anonymous
                    ? string.Empty
                    : source.UserId ?? string.Empty,
                ItemId = string.Empty,
                ItemName = string.Empty,
                ItemImageUrl = string.Empty,
                Count = Math.Max(1, source.Count),
                UnitValue = 0m,
                TotalValue = 0m,
                Unit = source.Unit ?? string.Empty,
                Currency = source.Currency ?? string.Empty,
                StreakId = string.Empty,
                StreakState = "none",
                ReceivedAt = source.ReceivedAt ?? string.Empty,
                Simulated = source.Simulated,
                RawEventType = source.RawEventType ?? string.Empty
            };
        }

        private static bool IsLikeMessage(
            CreatorToolsStreamMessage message)
        {
            return message != null && message.Event != null &&
                message.Event.Type == "like";
        }

        private static bool IsCoalescedLikeMessage(
            CreatorToolsStreamMessage message)
        {
            if (!IsLikeMessage(message))
                return false;
            var rawType = message.Event.RawEventType ?? string.Empty;
            return rawType == "host_coalesced_like" ||
                rawType == "host_coalesced_like_overflow";
        }

        private bool TryCoalesceConnectionUpdate(
            CreatorToolsStreamMessage message)
        {
            if (!IsConnectionUpdate(message))
                return false;
            var connectionId = message.Connection.ConnectionId ?? string.Empty;
            var node = pending.Last;
            while (node != null)
            {
                var previous = node.Previous;
                if (IsConnectionUpdate(node.Value) &&
                    string.Equals(
                        node.Value.Connection.ConnectionId ?? string.Empty,
                        connectionId,
                        StringComparison.Ordinal))
                {
                    pending.Remove(node);
                    pending.AddLast(message);
                    IncrementSaturated(ref coalescedConnectionUpdates);
                    ScheduleQueuePressureReport();
                    return true;
                }
                node = previous;
            }
            return false;
        }

        private LinkedListNode<CreatorToolsStreamMessage>
            FindEvictionCandidate(QueuePriority incomingPriority)
        {
            // Only evict a strictly less important message. Searching each
            // priority from lowest to highest makes statuses/progress gifts
            // expendable before likes, then follows/subscriptions. Critical
            // gifts are never selected as victims.
            for (var priority = QueuePriority.Disposable;
                 priority < incomingPriority;
                 priority++)
            {
                var node = pending.First;
                while (node != null)
                {
                    if (MessagePriority(node.Value) == priority)
                        return node;
                    node = node.Next;
                }
            }
            return null;
        }

        private static QueuePriority MessagePriority(
            CreatorToolsStreamMessage message)
        {
            if (IsConnectionUpdate(message) || message == null ||
                message.Event == null)
                return QueuePriority.Disposable;
            var streamEvent = message.Event;
            if (streamEvent.Type == "gift")
            {
                if (streamEvent.StreakState == "progress")
                    return QueuePriority.Disposable;
                return QueuePriority.Critical;
            }
            if (streamEvent.Type == "currency" ||
                streamEvent.Type == "redemption")
                return QueuePriority.Critical;
            if (streamEvent.Type == "like")
                return QueuePriority.Low;
            return QueuePriority.Normal;
        }

        private static bool IsConnectionUpdate(
            CreatorToolsStreamMessage message)
        {
            return message != null && message.Connection != null &&
                message.Event == null;
        }

        private void RecordDroppedMessage(CreatorToolsStreamMessage message)
        {
            if (IsConnectionUpdate(message))
                IncrementSaturated(ref droppedConnectionUpdates);
            else if (message != null && message.Event != null &&
                message.Event.Type == "gift" &&
                message.Event.StreakState == "progress")
                IncrementSaturated(ref droppedGiftProgressEvents);
            else if (message != null && message.Event != null &&
                message.Event.Type == "like")
                IncrementSaturated(ref droppedLikeEvents);
            else if (MessagePriority(message) == QueuePriority.Critical)
                IncrementSaturated(ref droppedPriorityEvents);
            else
                IncrementSaturated(ref droppedOtherEvents);
            ScheduleQueuePressureReport();
        }

        private void ScheduleQueuePressureReport()
        {
            if (queuePressureReportAt == DateTime.MaxValue)
                queuePressureReportAt = DateTime.UtcNow.AddSeconds(
                    QueuePressureReportDelaySeconds);
        }

        private string TakeQueuePressureReport(DateTime now, bool force)
        {
            if (droppedConnectionUpdates == 0L &&
                droppedGiftProgressEvents == 0L &&
                droppedLikeEvents == 0L &&
                droppedOtherEvents == 0L &&
                droppedPriorityEvents == 0L &&
                preservedPriorityEvents == 0L &&
                coalescedConnectionUpdates == 0L &&
                coalescedLikeEvents == 0L &&
                coalescedLikeCount == 0L &&
                deduplicatedLikeEvents == 0L &&
                syntheticLikeCount == 0L &&
                saturatedLikeAccumulators == 0L)
                return null;
            if (!force && now < queuePressureReportAt)
                return null;

            var report = "TikFinity companion queue pressure: capacity=" +
                MaximumPendingMessages.ToString(CultureInfo.InvariantCulture) +
                ", peak=" + queueHighWaterMark.ToString(
                    CultureInfo.InvariantCulture) +
                ", pending=" + pending.Count.ToString(
                    CultureInfo.InvariantCulture) +
                ", coalesced_status=" + coalescedConnectionUpdates.ToString(
                    CultureInfo.InvariantCulture) +
                ", coalesced_like_events=" + coalescedLikeEvents.ToString(
                    CultureInfo.InvariantCulture) +
                ", coalesced_like_count=" + coalescedLikeCount.ToString(
                    CultureInfo.InvariantCulture) +
                ", deduplicated_like=" + deduplicatedLikeEvents.ToString(
                    CultureInfo.InvariantCulture) +
                ", synthetic_like_count=" + syntheticLikeCount.ToString(
                    CultureInfo.InvariantCulture) +
                ", saturated_like_accumulators=" +
                    saturatedLikeAccumulators.ToString(
                        CultureInfo.InvariantCulture) +
                ", overflow_like_viewers=" +
                    overflowLikesByViewer.Count.ToString(
                        CultureInfo.InvariantCulture) +
                ", dropped_status=" + droppedConnectionUpdates.ToString(
                    CultureInfo.InvariantCulture) +
                ", dropped_gift_progress=" +
                    droppedGiftProgressEvents.ToString(
                        CultureInfo.InvariantCulture) +
                ", dropped_like=" + droppedLikeEvents.ToString(
                    CultureInfo.InvariantCulture) +
                ", dropped_other=" + droppedOtherEvents.ToString(
                    CultureInfo.InvariantCulture) +
                ", dropped_priority=" + droppedPriorityEvents.ToString(
                    CultureInfo.InvariantCulture) +
                ", priority_preserved=" + preservedPriorityEvents.ToString(
                    CultureInfo.InvariantCulture) + ".";

            coalescedConnectionUpdates = 0L;
            coalescedLikeEvents = 0L;
            coalescedLikeCount = 0L;
            deduplicatedLikeEvents = 0L;
            syntheticLikeCount = 0L;
            saturatedLikeAccumulators = 0L;
            droppedConnectionUpdates = 0L;
            droppedGiftProgressEvents = 0L;
            droppedLikeEvents = 0L;
            droppedOtherEvents = 0L;
            droppedPriorityEvents = 0L;
            preservedPriorityEvents = 0L;
            queueHighWaterMark = pending.Count;
            queuePressureReportAt = DateTime.MaxValue;
            return report;
        }

        private void TrackQueueHighWaterMark()
        {
            if (pending.Count > queueHighWaterMark)
                queueHighWaterMark = pending.Count;
        }

        private static void IncrementSaturated(ref long value)
        {
            if (value < long.MaxValue)
                value++;
        }

        private static void AddSaturated(ref long value, long amount)
        {
            if (amount <= 0L)
                return;
            value = value > long.MaxValue - amount
                ? long.MaxValue
                : value + amount;
        }

        private void LogQueuePressure(string report)
        {
            if (string.IsNullOrEmpty(report) || logWarning == null)
                return;
            try { logWarning(report); }
            catch { }
        }

        private bool WaitForRedirectedOutput(Process current)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                current.WaitForExit(RemainingDrainMilliseconds(timer));
            }
            catch
            {
            }

            var outputFinished = WaitForEof(outputEof,
                RemainingDrainMilliseconds(timer));
            var errorFinished = WaitForEof(errorEof,
                RemainingDrainMilliseconds(timer));
            if (outputFinished && errorFinished)
            {
                outputDrainWarningReported = false;
                return true;
            }
            // Keep the exited process and its handlers attached. The worker
            // retries this bounded wait on the next cycle, so no final NDJSON
            // line is discarded merely because a burst took over 250 ms to
            // deliver through Process.OutputDataReceived.
            if (!outputDrainWarningReported && logWarning != null)
            {
                outputDrainWarningReported = true;
                try
                {
                    logWarning("TikFinity companion is still draining output " +
                        "(stdout_eof=" + outputFinished + ", stderr_eof=" +
                        errorFinished + ").");
                }
                catch
                {
                }
            }
            return false;
        }

        private static int RemainingDrainMilliseconds(Stopwatch timer)
        {
            var remaining = OutputDrainTimeoutMilliseconds -
                timer.ElapsedMilliseconds;
            return remaining > 0L ? (int)remaining : 0;
        }

        private static bool WaitForEof(
            ManualResetEvent signal, int milliseconds)
        {
            try { return signal.WaitOne(milliseconds, false); }
            catch (ObjectDisposedException) { return true; }
        }

        private static void SignalEof(ManualResetEvent signal)
        {
            try { signal.Set(); }
            catch (ObjectDisposedException) { }
        }

        private void CleanupProcess(bool kill)
        {
            var current = process;
            process = null;
            if (current == null)
            {
                SignalEof(outputEof);
                SignalEof(errorEof);
                return;
            }
            try
            {
                current.OutputDataReceived -= OnOutputDataReceived;
                current.ErrorDataReceived -= OnErrorDataReceived;
                if (kill && !current.HasExited)
                    current.Kill();
            }
            catch
            {
            }
            SignalEof(outputEof);
            SignalEof(errorEof);
            try { current.Dispose(); }
            catch { }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            CleanupProcess(true);
            string pressureReport;
            lock (queueLock)
            {
                pressureReport = TakeQueuePressureReport(
                    DateTime.UtcNow, true);
                pending.Clear();
                overflowLikes.Clear();
                overflowLikesByViewer.Clear();
                syntheticLikeAccumulator = null;
                rememberedLikeIdentities.Clear();
                rememberedLikeIdentityOrder.Clear();
            }
            LogQueuePressure(pressureReport);
            try { outputEof.Close(); }
            catch { }
            try { errorEof.Close(); }
            catch { }
        }

        private enum QueuePriority
        {
            Disposable = 0,
            Low = 1,
            Normal = 2,
            Critical = 3
        }

        private sealed class LikeAccumulator
        {
            internal string ViewerKey = string.Empty;
            internal CreatorToolsStreamEvent Template;
            internal long Count;
            internal bool Synthetic;
        }
    }
}
