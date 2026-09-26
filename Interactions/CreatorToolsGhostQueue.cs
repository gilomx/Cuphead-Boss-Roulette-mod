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
        }

        private readonly List<Credit> credits = new List<Credit>();
        private int nextId = 1;

        internal int Count
        {
            get { return credits.Count; }
        }

        internal Credit Enqueue(string donor, string giftImagePath)
        {
            var credit = new Credit
            {
                Id = nextId++,
                Donor = (donor ?? string.Empty).Trim(),
                GiftImagePath = giftImagePath ?? string.Empty
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
    }
}
