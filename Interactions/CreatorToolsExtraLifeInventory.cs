using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsExtraLifeInventory
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

        internal Credit this[int index]
        {
            get { return credits[index]; }
        }

        internal Credit Add(
            string donor, string giftImagePath = "")
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

        internal bool TryConsume(bool suspended, out Credit credit)
        {
            credit = null;
            if (suspended || credits.Count == 0)
                return false;
            credit = credits[0];
            credits.RemoveAt(0);
            return true;
        }

        internal bool Contains(int id)
        {
            for (var i = 0; i < credits.Count; i++)
                if (credits[i].Id == id)
                    return true;
            return false;
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
            if (credit != null && !Contains(credit.Id))
                credits.Insert(0, credit);
        }

        internal void Clear()
        {
            credits.Clear();
        }
    }
}
