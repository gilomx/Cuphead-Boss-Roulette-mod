using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Owns the hidden TikFinity adapter process. Background callbacks only
    /// parse and enqueue bounded messages; Unity consumes them from Update.
    /// </summary>
    internal sealed class TikFinityCompanionHost : IDisposable
    {
        internal const string RelativeExecutablePath =
            "companion\\LaPichiRuleta.TikFinity.exe";
        private const int MaximumPendingMessages = 1024;
        private const int MaximumRestarts = 1000000;
        private const int QueuePressureReportDelaySeconds = 2;

        private readonly string executablePath;
        private readonly string workingDirectory;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly object queueLock = new object();
        private readonly LinkedList<CreatorToolsStreamMessage> pending =
            new LinkedList<CreatorToolsStreamMessage>();

        private Process process;
        private DateTime nextStartAt = DateTime.MinValue;
        private DateTime queuePressureReportAt = DateTime.MaxValue;
        private int restartAttempt;
        private int queueHighWaterMark;
        private long coalescedConnectionUpdates;
        private long droppedConnectionUpdates;
        private long droppedGiftProgressEvents;
        private long droppedLikeEvents;
        private long droppedOtherEvents;
        private long droppedPriorityEvents;
        private long preservedPriorityEvents;
        private bool disposed;
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

        internal void Update()
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

        internal bool TryTakeMessage(out CreatorToolsStreamMessage message)
        {
            string pressureReport;
            lock (queueLock)
            {
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
                if (!candidate.Start())
                    throw new InvalidOperationException(
                        "El proceso no pudo iniciarse.");
                process = candidate;
                candidate.BeginOutputReadLine();
                candidate.BeginErrorReadLine();
                PublishLocalStatus("starting", "companion_starting",
                    "Iniciando el acompañante de TikFinity.");
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
            if (disposed || string.IsNullOrEmpty(args.Data))
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
            if (disposed || string.IsNullOrEmpty(args.Data) ||
                logWarning == null)
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
                    RecordDroppedMessage(message);
                    return;
                }

                var evicted = candidate.Value;
                pending.Remove(candidate);
                RecordDroppedMessage(evicted);
                if (incomingPriority == QueuePriority.Critical)
                    IncrementSaturated(ref preservedPriorityEvents);
                pending.AddLast(message);
                TrackQueueHighWaterMark();
            }
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
                coalescedConnectionUpdates == 0L)
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

        private void LogQueuePressure(string report)
        {
            if (string.IsNullOrEmpty(report) || logWarning == null)
                return;
            try { logWarning(report); }
            catch { }
        }

        private void CleanupProcess(bool kill)
        {
            var current = process;
            process = null;
            if (current == null)
                return;
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
            }
            LogQueuePressure(pressureReport);
        }

        private enum QueuePriority
        {
            Disposable = 0,
            Low = 1,
            Normal = 2,
            Critical = 3
        }
    }
}
