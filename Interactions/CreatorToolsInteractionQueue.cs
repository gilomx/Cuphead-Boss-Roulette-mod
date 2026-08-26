using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsInteractionQueue : IDisposable
    {
        internal const int MaximumBatchSize = 50;
        internal const int MaximumDelaySeconds = 3600;
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

        internal int AvailableCapacity
        {
            get { return Math.Max(0, MaximumQueued - Count); }
        }

        internal int Enqueue(
            string item,
            string donor,
            string giftImagePath,
            int quantity,
            float delaySeconds)
        {
            var availableSlots = AvailableCapacity;
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
                    Donor = donor,
                    GiftImagePath = giftImagePath ?? string.Empty,
                    DelaySeconds = delaySeconds,
                    ReadyAt = Time.realtimeSinceStartup + delaySeconds
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

        internal void ActivateFirst(ICreatorToolsInteractionHandle handle)
        {
            if (pending.Count == 0)
                return;
            var entry = pending[0];
            pending.RemoveAt(0);
            entry.Handle = handle;
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
                if (!IsFinished(active[i]))
                    continue;
                DisposeHandle(active[i]);
                active.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        internal void ClearActive()
        {
            for (var i = 0; i < active.Count; i++)
                DisposeHandle(active[i]);
            active.Clear();
        }

        internal void Clear()
        {
            ClearActive();
            pending.Clear();
        }

        private static bool IsFinished(Entry entry)
        {
            if (entry == null || entry.Handle == null)
                return true;
            try { return entry.Handle.IsComplete; }
            catch { return true; }
        }

        private static void DisposeHandle(Entry entry)
        {
            if (entry == null || entry.Handle == null)
                return;
            try { entry.Handle.Dispose(); }
            catch { }
            entry.Handle = null;
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
                AppendEntry(
                    builder,
                    pending[i],
                    pending[i].IsReady ? "queued" : "scheduled", first);
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
                .Append("\",\"delaySeconds\":")
                .Append(entry.DelaySeconds.ToString(
                    "0.###", CultureInfo.InvariantCulture))
                .Append('}');
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
                    builder.Append('\\').Append(character);
                else if (character == '\n')
                    builder.Append("\\n");
                else if (character == '\r')
                    builder.Append("\\r");
                else if (character == '\t')
                    builder.Append("\\t");
                else if (character < 32)
                    builder.Append("\\u")
                        .Append(((int)character).ToString("x4"));
                else
                    builder.Append(character);
            }
        }

        public void Dispose()
        {
            Clear();
        }

        internal sealed class Entry
        {
            internal int Id;
            internal string Item;
            internal string Donor;
            internal string GiftImagePath;
            internal float DelaySeconds;
            internal float ReadyAt;
            internal ICreatorToolsInteractionHandle Handle;

            internal bool IsReady
            {
                get { return Time.realtimeSinceStartup >= ReadyAt; }
            }
        }
    }
}
