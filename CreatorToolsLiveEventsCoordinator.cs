using System;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsLiveEventIds
    {
        internal const string PeskyBattle = "pesky_battle";
        internal const string TapFarming = "tap_farming";
    }

    internal sealed class CreatorToolsLiveEventLease
    {
        internal readonly string EventId;
        internal readonly long Epoch;

        internal CreatorToolsLiveEventLease(string eventId, long epoch)
        {
            EventId = eventId ?? string.Empty;
            Epoch = epoch;
        }
    }

    internal sealed class CreatorToolsLiveEventsSnapshot
    {
        internal readonly string ActiveEvent;
        internal readonly string Status;
        internal readonly long Epoch;
        internal readonly int Revision;

        internal CreatorToolsLiveEventsSnapshot(
            string activeEvent,
            string status,
            long epoch,
            int revision)
        {
            ActiveEvent = activeEvent ?? string.Empty;
            Status = status ?? "idle";
            Epoch = epoch;
            Revision = revision;
        }
    }

    /// <summary>
    /// Owns the single runtime reservation shared by all Creator Tools live
    /// events. A lease is deliberately kept while an event is stopping so a
    /// second event cannot start before Unity-side cleanup has completed.
    /// </summary>
    internal sealed class CreatorToolsLiveEventsCoordinator
    {
        private readonly object stateLock = new object();
        private string activeEvent = string.Empty;
        private string status = "idle";
        private long epoch;
        private int revision;

        internal bool TryAcquire(
            string eventId,
            out CreatorToolsLiveEventLease lease,
            out string blockingEvent)
        {
            eventId = NormalizeEventId(eventId);
            lock (stateLock)
            {
                if (eventId.Length == 0 || activeEvent.Length > 0)
                {
                    lease = null;
                    blockingEvent = activeEvent;
                    return false;
                }

                AdvanceEpochLocked();
                activeEvent = eventId;
                status = "active";
                TouchLocked();
                lease = new CreatorToolsLiveEventLease(eventId, epoch);
                blockingEvent = string.Empty;
                return true;
            }
        }

        internal bool BeginStopping(CreatorToolsLiveEventLease lease)
        {
            lock (stateLock)
            {
                if (!MatchesLocked(lease))
                    return false;
                if (status == "stopping")
                    return true;
                status = "stopping";
                TouchLocked();
                return true;
            }
        }

        internal bool CompleteRelease(CreatorToolsLiveEventLease lease)
        {
            lock (stateLock)
            {
                if (!MatchesLocked(lease))
                    return false;
                activeEvent = string.Empty;
                status = "idle";
                TouchLocked();
                return true;
            }
        }

        internal bool IsOwner(CreatorToolsLiveEventLease lease)
        {
            lock (stateLock)
                return MatchesLocked(lease);
        }

        internal CreatorToolsLiveEventsSnapshot Snapshot
        {
            get
            {
                lock (stateLock)
                    return new CreatorToolsLiveEventsSnapshot(
                        activeEvent, status, epoch, revision);
            }
        }

        private bool MatchesLocked(CreatorToolsLiveEventLease lease)
        {
            return lease != null &&
                lease.Epoch == epoch &&
                string.Equals(
                    lease.EventId,
                    activeEvent,
                    StringComparison.Ordinal);
        }

        private void AdvanceEpochLocked()
        {
            epoch++;
            if (epoch <= 0L)
                epoch = 1L;
        }

        private void TouchLocked()
        {
            revision++;
            if (revision < 0)
                revision = 1;
        }

        private static string NormalizeEventId(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
