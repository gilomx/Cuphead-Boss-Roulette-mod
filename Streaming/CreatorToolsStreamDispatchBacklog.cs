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
            long quantity,
            int durationSeconds = CreatorToolsTimedChallenge.DefaultDuration,
            int countdownSeconds = CreatorToolsTimedChallenge.DefaultCountdown)
        {
            return Add(ruleId, connectionId, interaction, giftImagePath,
                donor, quantity, DateTime.MinValue,
                durationSeconds, countdownSeconds);
        }

        internal Entry Add(
            long ruleId,
            string connectionId,
            string interaction,
            string giftImagePath,
            string donor,
            long quantity,
            DateTime notBeforeUtc,
            int durationSeconds = CreatorToolsTimedChallenge.DefaultDuration,
            int countdownSeconds = CreatorToolsTimedChallenge.DefaultCountdown)
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
                    current.Interaction != interaction || current.DurationSeconds != durationSeconds ||
                    current.CountdownSeconds != countdownSeconds)
                    continue;

                current.Add(donor, quantity, notBeforeUtc);
                return current;
            }

            var entry = new Entry
            {
                RuleId = ruleId,
                ConnectionId = connectionId,
                Interaction = interaction,
                GiftImagePath = giftImagePath,
                DurationSeconds = durationSeconds,
                CountdownSeconds = countdownSeconds
            };
            entry.Add(donor, quantity, notBeforeUtc);
            entries.Add(entry);
            return entry;
        }

        internal Entry AddSeries(
            long ruleId,
            string connectionId,
            string interaction,
            string giftImagePath,
            string donor,
            int quantityPerActivation,
            long activationCount,
            DateTime firstNotBeforeUtc,
            int intervalSeconds,
            int durationSeconds = CreatorToolsTimedChallenge.DefaultDuration,
            int countdownSeconds = CreatorToolsTimedChallenge.DefaultCountdown)
        {
            if (quantityPerActivation <= 0 || activationCount <= 0)
                return null;

            var quantity = activationCount >
                long.MaxValue / quantityPerActivation
                ? long.MaxValue
                : activationCount * quantityPerActivation;
            if (intervalSeconds <= 0 || activationCount == 1)
                return Add(ruleId, connectionId, interaction,
                    giftImagePath, donor, quantity, firstNotBeforeUtc,
                    durationSeconds, countdownSeconds);

            connectionId = connectionId ?? string.Empty;
            interaction = interaction ?? string.Empty;
            giftImagePath = giftImagePath ?? string.Empty;
            donor = donor ?? string.Empty;
            for (var i = 0; i < entries.Count; i++)
            {
                var current = entries[i];
                if (current.RuleId != ruleId ||
                    current.ConnectionId != connectionId ||
                    current.Interaction != interaction ||
                    current.DurationSeconds != durationSeconds ||
                    current.CountdownSeconds != countdownSeconds)
                    continue;

                current.AddSeries(donor, quantity,
                    quantityPerActivation, firstNotBeforeUtc,
                    intervalSeconds);
                return current;
            }

            var entry = new Entry
            {
                RuleId = ruleId,
                ConnectionId = connectionId,
                Interaction = interaction,
                GiftImagePath = giftImagePath,
                DurationSeconds = durationSeconds,
                CountdownSeconds = countdownSeconds
            };
            entry.AddSeries(donor, quantity,
                quantityPerActivation, firstNotBeforeUtc,
                intervalSeconds);
            entries.Add(entry);
            return entry;
        }

        internal int Drain(
            CreatorToolsInteractionController interactions)
        {
            return Drain(interactions, DateTime.UtcNow);
        }

        internal int Drain(
            CreatorToolsInteractionController interactions,
            DateTime utcNow)
        {
            if (interactions == null || entries.Count == 0)
                return 0;

            var queued = 0;
            var batches = 0;
            while (entries.Count > 0 &&
                   batches < MaximumBatchesPerUpdate)
            {
                var entryIndex = FindNextDrainableEntry(interactions);
                if (entryIndex < 0)
                    break;
                var entry = entries[entryIndex];
                string feedbackCode;
                var added = DrainEntry(
                    entry, interactions, utcNow, out feedbackCode);
                batches++;
                if (added <= 0)
                    break;

                queued += added;
                if (entry.Remaining <= 0)
                {
                    entries.RemoveAt(entryIndex);
                    if (entryIndex < nextEntry)
                        nextEntry--;
                    if (nextEntry < 0 || nextEntry >= entries.Count)
                        nextEntry = 0;
                }
                else if (!CreatorToolsHelp.Supports(entry.Interaction))
                    nextEntry = (entryIndex + 1) % entries.Count;
            }
            return queued;
        }

        private int FindNextDrainableEntry(
            CreatorToolsInteractionController interactions)
        {
            for (var i = 0; i < entries.Count; i++)
                if (CreatorToolsHelp.Supports(entries[i].Interaction) &&
                    interactions.StreamQueueAvailableCapacityFor(
                        entries[i].Interaction) > 0)
                    return i;

            if (nextEntry < 0 || nextEntry >= entries.Count)
                nextEntry = 0;
            for (var offset = 0; offset < entries.Count; offset++)
            {
                var index = (nextEntry + offset) % entries.Count;
                if (CreatorToolsHelp.Supports(entries[index].Interaction))
                    continue;
                if (interactions.StreamQueueAvailableCapacityFor(
                        entries[index].Interaction) > 0)
                    return index;
            }
            return -1;
        }

        internal int DrainEntry(
            Entry entry,
            CreatorToolsInteractionController interactions,
            DateTime utcNow,
            out string feedbackCode)
        {
            feedbackCode = string.Empty;
            if (entry == null || entry.Remaining <= 0 ||
                interactions == null)
                return 0;
            if (interactions.StreamQueueAvailableCapacityFor(
                    entry.Interaction) <= 0)
            {
                feedbackCode = "queue_full";
                return 0;
            }

            var requested = (int)Math.Min(
                CreatorToolsInteractionQueue.MaximumBatchSize,
                entry.NextDispatchQuantity);
            var added = interactions.EnqueueStreamInteraction(
                entry.Interaction,
                entry.NextDonor,
                entry.GiftImagePath,
                requested,
                out feedbackCode, entry.DurationSeconds,
                entry.CountdownSeconds,
                DelaySeconds(entry.NextNotBeforeUtc, utcNow));
            entry.Consume(Math.Max(0, added));
            return Math.Max(0, added);
        }

        private static float DelaySeconds(
            DateTime notBeforeUtc, DateTime utcNow)
        {
            if (notBeforeUtc <= utcNow)
                return 0f;
            var seconds = (notBeforeUtc - utcNow).TotalSeconds;
            return seconds >= float.MaxValue
                ? float.MaxValue
                : (float)seconds;
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
            internal int DurationSeconds;
            internal int CountdownSeconds;
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

            internal long NextDispatchQuantity
            {
                get
                {
                    return donorSegments.Count == 0
                        ? 0L
                        : donorSegments[0].NextQuantity;
                }
            }

            internal DateTime NextNotBeforeUtc
            {
                get
                {
                    return donorSegments.Count == 0
                        ? DateTime.MinValue
                        : donorSegments[0].NotBeforeUtc;
                }
            }

            internal void Add(
                string donor, long quantity, DateTime notBeforeUtc)
            {
                if (quantity <= 0 || Remaining >= long.MaxValue)
                    return;
                donor = donor ?? string.Empty;
                var accepted = Math.Min(
                    quantity, long.MaxValue - Remaining);
                Remaining += accepted;

                if (donorSegments.Count == 0)
                {
                    donorSegments.Add(new DonorSegment(
                        donor, accepted, notBeforeUtc));
                    return;
                }

                var last = donorSegments[donorSegments.Count - 1];
                if (last.IsOverflow ||
                    (string.Equals(last.Donor, donor,
                         StringComparison.Ordinal) &&
                     last.NotBeforeUtc == notBeforeUtc))
                {
                    last.Remaining = SaturatingAdd(
                        last.Remaining, accepted);
                    if (notBeforeUtc > last.NotBeforeUtc)
                        last.NotBeforeUtc = notBeforeUtc;
                    return;
                }

                // Keep exact FIFO attribution for the first 255 contiguous
                // donor groups. The final reserved segment safely coalesces
                // pathological viewer churn without losing any redemptions.
                if (donorSegments.Count <
                    MaximumDonorSegmentsPerEntry - 1)
                    donorSegments.Add(new DonorSegment(
                        donor, accepted, notBeforeUtc));
                else
                    donorSegments.Add(new DonorSegment(
                        string.Empty, accepted, notBeforeUtc, true));
            }

            internal void AddSeries(
                string donor,
                long quantity,
                int quantityPerActivation,
                DateTime firstNotBeforeUtc,
                int intervalSeconds)
            {
                if (quantity <= 0 || quantityPerActivation <= 0 ||
                    intervalSeconds <= 0 || Remaining >= long.MaxValue)
                    return;
                donor = donor ?? string.Empty;
                var accepted = Math.Min(
                    quantity, long.MaxValue - Remaining);
                Remaining += accepted;

                if (donorSegments.Count <
                    MaximumDonorSegmentsPerEntry)
                {
                    donorSegments.Add(new DonorSegment(
                        donor, accepted, firstNotBeforeUtc, false,
                        quantityPerActivation, intervalSeconds));
                    return;
                }

                // Pathological viewer churn remains bounded. The overflow
                // segment keeps every earned interaction at the latest safe
                // due time, though its fine-grained spacing is intentionally
                // collapsed once the attribution limit is reached.
                var last = donorSegments[donorSegments.Count - 1];
                last.CollapseIntoOverflow(
                    donor, accepted, firstNotBeforeUtc);
            }

            internal void Consume(int quantity)
            {
                var remainingToConsume = Math.Max(0, quantity);
                while (remainingToConsume > 0 && donorSegments.Count > 0)
                {
                    var segment = donorSegments[0];
                    var consumed = (long)Math.Min(
                        remainingToConsume, segment.NextQuantity);
                    segment.Consume(consumed);
                    Remaining -= consumed;
                    remainingToConsume -= (int)consumed;
                    if (segment.Remaining <= 0)
                        donorSegments.RemoveAt(0);
                }
            }
        }

        private sealed class DonorSegment
        {
            internal string Donor;
            internal bool IsOverflow;
            internal long Remaining;
            internal DateTime NotBeforeUtc;
            private int quantityPerActivation;
            private int remainingInActivation;
            private int intervalSeconds;

            internal long NextQuantity
            {
                get
                {
                    return intervalSeconds > 0
                        ? Math.Min(Remaining, remainingInActivation)
                        : Remaining;
                }
            }

            internal DonorSegment(
                string donor,
                long remaining,
                DateTime notBeforeUtc,
                bool isOverflow = false,
                int quantityPerActivation = 0,
                int intervalSeconds = 0)
            {
                Donor = donor ?? string.Empty;
                Remaining = remaining;
                NotBeforeUtc = notBeforeUtc;
                IsOverflow = isOverflow;
                this.quantityPerActivation = Math.Max(
                    0, quantityPerActivation);
                this.intervalSeconds = Math.Max(0, intervalSeconds);
                remainingInActivation = this.intervalSeconds > 0
                    ? (int)Math.Min(Remaining,
                        this.quantityPerActivation)
                    : 0;
            }

            internal void Consume(long quantity)
            {
                var consumed = Math.Min(
                    Math.Max(0L, quantity), Remaining);
                Remaining -= consumed;
                if (intervalSeconds <= 0)
                    return;
                remainingInActivation -= (int)consumed;
                if (remainingInActivation > 0 || Remaining <= 0)
                    return;
                NotBeforeUtc = AddSecondsSafely(
                    NotBeforeUtc, intervalSeconds);
                remainingInActivation = (int)Math.Min(
                    Remaining, quantityPerActivation);
            }

            internal void CollapseIntoOverflow(
                string donor, long quantity, DateTime notBeforeUtc)
            {
                Donor = string.Empty;
                IsOverflow = true;
                Remaining = SaturatingAdd(Remaining, quantity);
                if (notBeforeUtc > NotBeforeUtc)
                    NotBeforeUtc = notBeforeUtc;
                quantityPerActivation = 0;
                remainingInActivation = 0;
                intervalSeconds = 0;
            }

            private static DateTime AddSecondsSafely(
                DateTime value, int seconds)
            {
                if (seconds <= 0)
                    return value;
                var maximumSeconds = (DateTime.MaxValue - value)
                    .TotalSeconds;
                return seconds >= maximumSeconds
                    ? DateTime.MaxValue
                    : value.AddSeconds(seconds);
            }
        }
    }
}
