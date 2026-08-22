using System;
using System.Collections.Generic;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsInteractionQueue : IDisposable
    {
        internal const int MaximumActive = 1;
        internal const int MaximumBatchSize = 50;
        private const int MaximumQueued = 200;

        private readonly List<Entry> pending = new List<Entry>();
        private readonly List<Entry> active = new List<Entry>();
        private int nextId = 1;

        internal int Count
        {
            get { return active.Count + pending.Count; }
        }

        internal int ActiveCount
        {
            get { return active.Count; }
        }

        internal int Enqueue(
            string item,
            NativeZeppelinVariant variant,
            string donor,
            int quantity)
        {
            var availableSlots = MaximumQueued - Count;
            var count = Math.Max(
                0,
                Math.Min(
                    Math.Min(quantity, MaximumBatchSize),
                    availableSlots));
            for (var i = 0; i < count; i++)
            {
                pending.Add(new Entry
                {
                    Id = nextId++,
                    Item = item,
                    Variant = variant,
                    Donor = donor
                });
                if (nextId <= 0)
                    nextId = 1;
            }
            return count;
        }

        internal Entry Peek()
        {
            return pending.Count == 0 ? null : pending[0];
        }

        internal void ActivateFirst(FlyingBlimpLevelEnemy actor)
        {
            if (pending.Count == 0)
                return;
            var entry = pending[0];
            pending.RemoveAt(0);
            entry.Actor = actor;
            active.Add(entry);
        }

        internal void RejectFirst()
        {
            if (pending.Count > 0)
                pending.RemoveAt(0);
        }

        internal bool RemoveFinished()
        {
            var changed = false;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Actor != null)
                    continue;
                active.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append('[');
            var first = true;
            for (var i = 0; i < active.Count; i++)
            {
                AppendEntry(builder, active[i], "active", first);
                first = false;
            }
            for (var i = 0; i < pending.Count; i++)
            {
                AppendEntry(builder, pending[i], "queued", first);
                first = false;
            }
            builder.Append(']');
        }

        private static void AppendEntry(
            StringBuilder builder,
            Entry entry,
            string status,
            bool first)
        {
            if (!first)
                builder.Append(',');
            builder.Append("{\"id\":")
                .Append(entry.Id)
                .Append(",\"item\":\"");
            AppendJsonValue(builder, entry.Item);
            builder.Append("\",\"donor\":\"");
            AppendJsonValue(builder, entry.Donor);
            builder.Append("\",\"status\":\"")
                .Append(status)
                .Append("\"}");
        }

        private static void AppendJsonValue(
            StringBuilder builder,
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\');
                builder.Append(character);
            }
        }

        public void Dispose()
        {
            pending.Clear();
            active.Clear();
        }

        internal sealed class Entry
        {
            internal int Id;
            internal string Item;
            internal NativeZeppelinVariant Variant;
            internal string Donor;
            internal FlyingBlimpLevelEnemy Actor;
        }
    }
}
