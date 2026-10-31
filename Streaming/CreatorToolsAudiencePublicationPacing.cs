using System;
using System.Diagnostics;

namespace Gilomx.CupheadBossRoulette
{
    // Shared by HTTP readers and the Unity-owned JSON publisher. Requests
    // renew a short audience lease; they never wait for Unity or serialize
    // gameplay state. Only the owner acts on TryBegin's admission result.
    internal sealed class CreatorToolsAudiencePublicationPacing
    {
        internal const double ActiveIntervalSeconds = 0.5d;
        internal const double AudienceLeaseSeconds = 2d;
        private readonly object stateLock = new object();
        private readonly Func<long> clock;
        private readonly double activeIntervalTicks;
        private readonly double leaseTicks;
        private bool started;
        private bool requestObserved;
        private bool refreshPending;
        private long lastStartedAt;
        private long lastRequestAt;

        internal CreatorToolsAudiencePublicationPacing(Func<long> clock = null,
            long frequency = 0L)
        {
            this.clock = clock ?? Stopwatch.GetTimestamp;
            var ticksPerSecond = frequency > 0L ? frequency : Stopwatch.Frequency;
            activeIntervalTicks = ticksPerSecond * ActiveIntervalSeconds;
            leaseTicks = ticksPerSecond * AudienceLeaseSeconds;
        }

        internal void ObserveRequest()
        {
            lock (stateLock)
            {
                var now = clock();
                if (!AudienceActive(now)) refreshPending = true;
                lastRequestAt = now;
                requestObserved = true;
            }
        }

        internal bool TryBegin(bool urgent)
        {
            lock (stateLock)
            {
                var now = clock();
                // Bootstrap and explicit urgent controls/focus snapshots do
                // not need an audience. A deferred reader refresh, however,
                // is valid only while its lease still has readers: Unity may
                // have stopped updating for minutes after the HTTP request.
                if (!urgent && started)
                {
                    if (!AudienceActive(now))
                    {
                        refreshPending = false;
                        return false;
                    }
                    if (!refreshPending &&
                        (double)now - lastStartedAt < activeIntervalTicks)
                        return false;
                }
                started = true;
                lastStartedAt = now;
                refreshPending = false;
                return true;
            }
        }

        private bool AudienceActive(long now)
        {
            // Stopwatch is monotonic. Treat an injected/restarted clock as an
            // expired lease until a new request establishes its new epoch.
            return requestObserved && now >= lastRequestAt &&
                (double)now - lastRequestAt < leaseTicks;
        }
    }
}
