using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsGhostQueue
    {
        internal sealed class Credit
        {
            internal int Id;
            internal string Donor;
            internal string GiftImagePath;
            internal CreatorToolsInteractionSource Source;
            internal int QueueEntryId;
        }

        private readonly List<Credit> credits = new List<Credit>();
        private int nextId = 1;

        internal int Count
        {
            get { return credits.Count; }
        }

        internal Credit Enqueue(
            string donor,
            string giftImagePath,
            CreatorToolsInteractionSource source =
                CreatorToolsInteractionSource.Manual,
            int queueEntryId = 0)
        {
            var credit = new Credit
            {
                Id = nextId++,
                Donor = (donor ?? string.Empty).Trim(),
                GiftImagePath = giftImagePath ?? string.Empty,
                Source = source,
                QueueEntryId = queueEntryId
            };
            if (nextId <= 0)
                nextId = 1;
            credits.Add(credit);
            return credit;
        }

        internal bool TryDequeue(out Credit credit)
        {
            credit = null;
            if (credits.Count == 0)
                return false;
            credit = credits[0];
            credits.RemoveAt(0);
            return true;
        }

        internal bool Remove(int id)
        {
            for (var i = 0; i < credits.Count; i++)
            {
                if (credits[i].Id != id)
                    continue;
                credits.RemoveAt(i);
                return true;
            }
            return false;
        }

        internal void RestoreFront(Credit credit)
        {
            if (credit != null)
                credits.Insert(0, credit);
        }

        internal void Clear()
        {
            credits.Clear();
        }

        internal int Clear(CreatorToolsInteractionSource source)
        {
            var cleared = 0;
            for (var i = credits.Count - 1; i >= 0; i--)
            {
                if (credits[i].Source != source)
                    continue;
                credits.RemoveAt(i);
                cleared++;
            }
            return cleared;
        }

        internal Credit this[int index]
        {
            get { return credits[index]; }
        }
    }
}
