using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal enum CreatorToolsInteractionSource
    {
        Manual,
        Stream,
        Pesky,
        PeskyBattle
    }

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

        internal int PendingCount
        {
            get { return pending.Count; }
        }

        internal int AvailableCapacity
        {
            get
            {
                return Math.Max(
                    0,
                    MaximumQueued - CountExcept(
                        CreatorToolsInteractionSource.PeskyBattle));
            }
        }

        internal int Enqueue(
            string item,
            string donor,
            string giftImagePath,
            int quantity,
            float delaySeconds,
            CreatorToolsInteractionSource source)
        {
            // Battle work shares this physical queue, but owns one reserved
            // pending slot. A paused stream backlog may therefore contain the
            // full 200 regular entries without starving the battle scheduler.
            var availableSlots = source ==
                    CreatorToolsInteractionSource.PeskyBattle
                ? Math.Max(0, 1 - PendingCountFor(source))
                : AvailableCapacity;
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
                    Source = source,
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

        internal Entry Peek(Func<Entry, bool> predicate)
        {
            if (predicate == null)
                return Peek();
            for (var i = 0; i < pending.Count; i++)
                if (predicate(pending[i]))
                    return pending[i];
            return null;
        }

        internal void PeekBySource(
            Func<Entry, bool> predicate,
            CreatorToolsInteractionSource source,
            out Entry matching,
            out Entry other)
        {
            // Keep one physical list and its order. The controller arbitrates
            // between the first dispatchable entry in each logical lane.
            matching = null;
            other = null;
            for (var i = 0; i < pending.Count; i++)
            {
                var candidate = pending[i];
                if (predicate != null && !predicate(candidate))
                    continue;
                if (candidate.Source == source)
                {
                    if (matching == null)
                        matching = candidate;
                }
                else if (other == null)
                    other = candidate;
                if (matching != null && other != null)
                    break;
            }
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

        internal void Activate(
            Entry entry, ICreatorToolsInteractionHandle handle)
        {
            var index = pending.IndexOf(entry);
            if (index < 0)
                return;
            pending.RemoveAt(index);
            entry.Handle = handle;
            active.Add(entry);
        }

        internal void RejectFirst()
        {
            if (pending.Count > 0)
                pending.RemoveAt(0);
        }

        internal void Reject(Entry entry)
        {
            if (entry != null)
                pending.Remove(entry);
        }

        internal int CountFor(CreatorToolsInteractionSource source)
        {
            return ActiveCountFor(source) + PendingCountFor(source);
        }

        private int CountExcept(CreatorToolsInteractionSource source)
        {
            var count = 0;
            for (var i = 0; i < active.Count; i++)
                if (active[i].Source != source)
                    count++;
            for (var i = 0; i < pending.Count; i++)
                if (pending[i].Source != source)
                    count++;
            return count;
        }

        internal int ActiveCountFor(CreatorToolsInteractionSource source)
        {
            var count = 0;
            for (var i = 0; i < active.Count; i++)
                if (active[i].Source == source)
                    count++;
            return count;
        }

        internal int PendingCountFor(CreatorToolsInteractionSource source)
        {
            var count = 0;
            for (var i = 0; i < pending.Count; i++)
                if (pending[i].Source == source)
                    count++;
            return count;
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

        internal int ClearPending()
        {
            var cleared = pending.Count;
            pending.Clear();
            return cleared;
        }

        internal int ClearPending(CreatorToolsInteractionSource source)
        {
            var cleared = 0;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].Source != source)
                    continue;
                pending.RemoveAt(i);
                cleared++;
            }
            return cleared;
        }

        internal int ClearSource(CreatorToolsInteractionSource source)
        {
            var cleared = ClearPending(source);
            for (var i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Source != source)
                    continue;
                DisposeHandle(active[i]);
                active.RemoveAt(i);
                cleared++;
            }
            return cleared;
        }

        internal void Clear()
        {
            ClearActive();
            ClearPending();
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
                .Append("\",\"source\":\"")
                .Append(SourceValue(entry.Source))
                .Append("\",\"delaySeconds\":")
                .Append(entry.DelaySeconds.ToString(
                    "0.###", CultureInfo.InvariantCulture))
                .Append('}');
        }

        private static string SourceValue(
            CreatorToolsInteractionSource source)
        {
            if (source == CreatorToolsInteractionSource.Stream)
                return "stream";
            if (source == CreatorToolsInteractionSource.PeskyBattle)
                return "pesky_battle";
            if (source == CreatorToolsInteractionSource.Pesky)
                return "pesky";
            return "manual";
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
            internal CreatorToolsInteractionSource Source;
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
