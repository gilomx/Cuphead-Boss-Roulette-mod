using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Keeps stream redemptions that did not fit in the live interaction
    /// queue. Entries are grouped by rule and connection so the backlog stays
    /// bounded by active rule sources instead of by the number of viewers.
    /// </summary>
    internal sealed class CreatorToolsStreamDispatchBacklog
    {
        private const int MaximumBatchesPerUpdate = 8;
        private const int MaximumDonorSegmentsPerEntry = 256;

        private readonly List<Entry> entries = new List<Entry>();
        private int nextEntry;

        internal bool HasEntries
        {
            get { return entries.Count > 0; }
        }

        internal long PendingCount
        {
            get
            {
                var total = 0L;
                for (var i = 0; i < entries.Count; i++)
                    total = SaturatingAdd(total, entries[i].Remaining);
                return total;
            }
        }

        internal long Clear()
        {
            var cleared = PendingCount;
            entries.Clear();
            nextEntry = 0;
            return cleared;
        }

        internal Entry Add(
            long ruleId,
            string connectionId,
            string interaction,
            string giftImagePath,
            string donor,
            long quantity)
        {
            if (quantity <= 0)
                return null;

            connectionId = connectionId ?? string.Empty;
            interaction = interaction ?? string.Empty;
            giftImagePath = giftImagePath ?? string.Empty;
            donor = donor ?? string.Empty;
            for (var i = 0; i < entries.Count; i++)
            {
                var current = entries[i];
                if (current.RuleId != ruleId ||
                    current.ConnectionId != connectionId ||
                    current.Interaction != interaction)
                    continue;

                current.Add(donor, quantity);
                return current;
            }

            var entry = new Entry
            {
                RuleId = ruleId,
                ConnectionId = connectionId,
                Interaction = interaction,
                GiftImagePath = giftImagePath
            };
            entry.Add(donor, quantity);
            entries.Add(entry);
            return entry;
        }

        internal int Drain(
            CreatorToolsInteractionController interactions)
        {
            if (interactions == null || entries.Count == 0)
                return 0;

            var queued = 0;
            var batches = 0;
            while (entries.Count > 0 &&
                   batches < MaximumBatchesPerUpdate)
            {
                if (nextEntry >= entries.Count)
                    nextEntry = 0;
                var entry = entries[nextEntry];
                string feedbackCode;
                var added = DrainEntry(
                    entry, interactions, out feedbackCode);
                batches++;
                if (added <= 0)
                    break;

                queued += added;
                if (entry.Remaining <= 0)
                    entries.RemoveAt(nextEntry);
                else
                    nextEntry = (nextEntry + 1) % entries.Count;

                if (interactions.StreamQueueAvailableCapacity <= 0)
                    break;
            }
            return queued;
        }

        internal int DrainEntry(
            Entry entry,
            CreatorToolsInteractionController interactions,
            out string feedbackCode)
        {
            feedbackCode = string.Empty;
            if (entry == null || entry.Remaining <= 0 ||
                interactions == null)
                return 0;
            if (interactions.StreamQueueAvailableCapacity <= 0)
            {
                feedbackCode = "queue_full";
                return 0;
            }

            var requested = (int)Math.Min(
                CreatorToolsInteractionQueue.MaximumBatchSize,
                entry.NextDonorQuantity);
            var added = interactions.EnqueueStreamInteraction(
                entry.Interaction,
                entry.NextDonor,
                entry.GiftImagePath,
                requested,
                out feedbackCode);
            entry.Consume(Math.Max(0, added));
            return Math.Max(0, added);
        }

        internal void RemoveIfComplete(Entry entry)
        {
            if (entry == null || entry.Remaining > 0)
                return;
            var index = entries.IndexOf(entry);
            if (index < 0)
                return;
            entries.RemoveAt(index);
            if (index < nextEntry)
                nextEntry--;
            if (nextEntry < 0 || nextEntry >= entries.Count)
                nextEntry = 0;
        }

        internal void RemoveRule(long ruleId)
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].RuleId != ruleId)
                    continue;
                entries.RemoveAt(i);
                if (i < nextEntry)
                    nextEntry--;
            }
            if (nextEntry < 0 || nextEntry >= entries.Count)
                nextEntry = 0;
        }

        private static long SaturatingAdd(long left, long right)
        {
            if (right <= 0)
                return left;
            return left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

        internal sealed class Entry
        {
            private readonly List<DonorSegment> donorSegments =
                new List<DonorSegment>();

            internal long RuleId;
            internal string ConnectionId;
            internal string Interaction;
            internal string GiftImagePath;
            internal long Remaining;

            internal string NextDonor
            {
                get
                {
                    return donorSegments.Count == 0
                        ? string.Empty
                        : donorSegments[0].Donor;
                }
            }

            internal long NextDonorQuantity
            {
                get
                {
                    return donorSegments.Count == 0
                        ? 0L
                        : donorSegments[0].Remaining;
                }
            }

            internal void Add(string donor, long quantity)
            {
                if (quantity <= 0 || Remaining >= long.MaxValue)
                    return;
                donor = donor ?? string.Empty;
                var accepted = Math.Min(
                    quantity, long.MaxValue - Remaining);
                Remaining += accepted;

                if (donorSegments.Count == 0)
                {
                    donorSegments.Add(new DonorSegment(donor, accepted));
                    return;
                }

                var last = donorSegments[donorSegments.Count - 1];
                if (last.IsOverflow ||
                    string.Equals(last.Donor, donor,
                        StringComparison.Ordinal))
                {
                    last.Remaining = SaturatingAdd(
                        last.Remaining, accepted);
                    return;
                }

                // Keep exact FIFO attribution for the first 255 contiguous
                // donor groups. The final reserved segment safely coalesces
                // pathological viewer churn without losing any redemptions.
                if (donorSegments.Count <
                    MaximumDonorSegmentsPerEntry - 1)
                    donorSegments.Add(new DonorSegment(donor, accepted));
                else
                    donorSegments.Add(new DonorSegment(
                        string.Empty, accepted, true));
            }

            internal void Consume(int quantity)
            {
                var remainingToConsume = Math.Max(0, quantity);
                while (remainingToConsume > 0 && donorSegments.Count > 0)
                {
                    var segment = donorSegments[0];
                    var consumed = (long)Math.Min(
                        remainingToConsume, segment.Remaining);
                    segment.Remaining -= consumed;
                    Remaining -= consumed;
                    remainingToConsume -= (int)consumed;
                    if (segment.Remaining <= 0)
                        donorSegments.RemoveAt(0);
                }
            }
        }

        private sealed class DonorSegment
        {
            internal readonly string Donor;
            internal readonly bool IsOverflow;
            internal long Remaining;

            internal DonorSegment(
                string donor, long remaining, bool isOverflow = false)
            {
                Donor = donor ?? string.Empty;
                Remaining = remaining;
                IsOverflow = isOverflow;
            }
        }
    }
}
