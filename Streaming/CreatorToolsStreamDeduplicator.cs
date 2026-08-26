using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsStreamDeduplicator
    {
        private const int MaximumRememberedKeys = 4096;
        private readonly HashSet<string> keys = new HashSet<string>(
            StringComparer.Ordinal);
        private readonly Queue<string> order = new Queue<string>();

        internal bool TryAccept(CreatorToolsStreamEvent streamEvent)
        {
            if (streamEvent == null)
                return false;
            var key = (streamEvent.ConnectionId ?? string.Empty) + ":" +
                (streamEvent.IdempotencyKey ?? string.Empty);
            if (key == ":" || !keys.Add(key))
                return false;
            order.Enqueue(key);
            while (order.Count > MaximumRememberedKeys)
                keys.Remove(order.Dequeue());
            return true;
        }
    }
}
