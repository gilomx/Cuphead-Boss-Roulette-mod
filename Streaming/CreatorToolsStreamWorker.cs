using System;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    internal interface ICreatorToolsStreamSource
    {
        void Update();
        bool TryTakeMessage(out CreatorToolsStreamMessage message);
    }

    /// <summary>
    /// Keeps transport, dashboard accounting and rule evaluation alive while
    /// Unity's frame loop is suspended. This worker never drains into the
    /// gameplay queue and therefore never calls Unity APIs.
    /// </summary>
    internal sealed class CreatorToolsStreamWorker : IDisposable
    {
        private const int MaximumMessagesPerCycle = 256;
        private const int PollIntervalMilliseconds = 50;

        private readonly ICreatorToolsStreamSource companion;
        private readonly CreatorToolsDashboardController dashboard;
        private readonly CreatorToolsStreamRulesController rules;
        private readonly CreatorToolsServer server;
        private readonly Func<CreatorToolsStreamEvent,
            CreatorToolsStreamEvaluation> evaluate;
        private readonly Action publishBackgroundState;
        private readonly Action<string> logWarning;
        private readonly AutoResetEvent wake = new AutoResetEvent(false);

        private Thread thread;
        private volatile bool running;
        private volatile bool disposed;

        internal CreatorToolsStreamWorker(
            ICreatorToolsStreamSource companion,
            CreatorToolsDashboardController dashboard,
            CreatorToolsStreamRulesController rules,
            CreatorToolsServer server,
            Func<CreatorToolsStreamEvent, CreatorToolsStreamEvaluation>
                evaluate,
            Action publishBackgroundState,
            Action<string> logWarning)
        {
            this.companion = companion;
            this.dashboard = dashboard;
            this.rules = rules;
            this.server = server;
            this.evaluate = evaluate;
            this.publishBackgroundState = publishBackgroundState;
            this.logWarning = logWarning;
        }

        internal void Start()
        {
            if (disposed || running)
                return;
            running = true;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "La Pichi Ruleta Stream Worker";
            thread.Start();
        }

        internal string ScheduleSimulation(string query)
        {
            if (disposed || dashboard == null)
                return "simulation_unavailable";
            var error = dashboard.ScheduleSimulation(query);
            if (string.IsNullOrEmpty(error))
                Signal();
            return error;
        }

        internal void Signal()
        {
            if (disposed)
                return;
            try { wake.Set(); }
            catch (ObjectDisposedException) { }
        }

        private void Run()
        {
            while (running)
            {
                var processed = 0;
                try
                {
                    if (companion != null)
                        companion.Update();
                    if (!running)
                        break;

                    CreatorToolsStreamMessage message;
                    while (running && processed < MaximumMessagesPerCycle &&
                           companion != null &&
                           companion.TryTakeMessage(out message))
                    {
                        try { ProcessMessage(message); }
                        catch (Exception exception)
                        {
                            Warn("Creator Tools skipped one stream message " +
                                "after an evaluation error: " +
                                exception.Message);
                        }
                        processed++;
                    }

                    if (running && dashboard != null)
                        dashboard.Update(server, evaluate);
                    if (running && server != null && rules != null)
                    {
                        rules.PublishState(server);
                        server.SetInteractionBacklogCount(rules.BacklogCount);
                    }
                    if (running && publishBackgroundState != null)
                        publishBackgroundState();
                }
                catch (Exception exception)
                {
                    Warn("Creator Tools stream worker recovered from an " +
                        "error: " + exception.Message);
                }

                if (!running)
                    break;
                // A full batch probably means more data is already waiting.
                // Continue immediately so bursts larger than one Unity-era
                // 64-message frame are not artificially throttled.
                if (processed >= MaximumMessagesPerCycle)
                    continue;
                try { wake.WaitOne(PollIntervalMilliseconds, false); }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void ProcessMessage(CreatorToolsStreamMessage message)
        {
            if (message == null || dashboard == null)
                return;
            if (message.Connection != null)
                dashboard.ApplyConnectionUpdate(message.Connection);
            if (message.Event != null)
                dashboard.ProcessEvent(message.Event, evaluate);
        }

        private void Warn(string message)
        {
            if (logWarning == null)
                return;
            try { logWarning(message); }
            catch { }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            running = false;
            try { wake.Set(); }
            catch { }
            var current = thread;
            thread = null;
            var stopped = true;
            if (current != null && current != Thread.CurrentThread)
            {
                try
                {
                    stopped = current.Join(5000);
                    if (!stopped)
                        Warn("Creator Tools stream worker did not stop " +
                            "within the shutdown window.");
                }
                catch { stopped = false; }
            }
            // Never close a wait handle that a late worker could still touch.
            // On normal shutdown the loop is bounded and always joins here.
            if (stopped)
                wake.Close();
        }
    }
}
