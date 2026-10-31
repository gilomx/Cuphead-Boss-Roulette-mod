using System;
using System.Diagnostics;
using System.Globalization;

namespace Gilomx.CupheadBossRoulette
{
    // One bounded diagnostic window per attempt. No Unity APIs, per-frame
    // strings, collections or logging; the owner supplies eligibility/time.
    internal sealed partial class CreatorToolsStartupPerformance
    {
        internal const double WindowSeconds = 15d;
        private readonly Action<string> log;
        private readonly Func<long> clock;
        private readonly Func<int> collections;
        private readonly double millisecondsPerTick;
        private bool armed;
        private bool recording;
        private bool frameOpen;
        private bool pluginSample;
        private bool previousEligible;
        private string level;
        private long interactionStartedAt;
        private int gcCollections;
        private int frames;
        private int spikes;
        private int spawns;
        private int spawnAttempts;
        private double seconds;
        private double maxFrameMs;
        private double maxInteractionMs;
        private double maxPluginMs;
        private double maxSpawnMs;
        private double maxGuiMs;
        private double maxLateUpdateMs;

        internal CreatorToolsStartupPerformance(Action<string> log,
            Func<long> clock = null, Func<int> collections = null,
            long frequency = 0L)
        {
            this.log = log;
            this.clock = clock ?? Stopwatch.GetTimestamp;
            this.collections = collections ?? delegate { return GC.CollectionCount(0); };
            millisecondsPerTick = 1000d / (frequency > 0L ? frequency : Stopwatch.Frequency);
        }

        internal bool NeedsLevelName { get { return armed && !recording; } }
        internal bool Pending { get { return armed || recording; } }

        internal void BeginAttempt()
        {
            EndAttempt();
            armed = true;
        }

        internal long BeginPluginUpdate()
        {
            return Pending ? clock() : 0L;
        }

        internal void BeginFrame(bool eligible, double frameSeconds, string levelName,
            int frameId = -1)
        {
            frameOpen = false;
            pluginSample = false;
            if (!Pending) return;
            if (!eligible)
            {
                previousEligible = false;
                DiscardFrameSample();
                return;
            }
            var collectionCount = collections();
            var nextFrameId = frameId >= 0 ? frameId : NextSyntheticFrameId();
            // Unity reports the elapsed duration of frame N-1 at Update N.
            // Its Update, LateUpdate, GUI and GC measurements are all complete
            // now. Never pair work across a pause, focus gap or skipped frame.
            CompletePreviousFrame(nextFrameId, frameSeconds, collectionCount);
            if (recording && seconds >= WindowSeconds)
            {
                EndAttempt(false);
                return;
            }
            if (!recording)
            {
                recording = true;
                armed = false;
                level = levelName ?? "unknown";
            }
            previousEligible = true;
            frames++;
            pluginSample = true;
            frameOpen = true;
            StartFrameSample(nextFrameId, collectionCount);
            interactionStartedAt = clock();
        }

        internal void EndInteractionUpdate()
        {
            if (!frameOpen) return;
            var elapsed = Elapsed(interactionStartedAt);
            maxInteractionMs = Math.Max(maxInteractionMs, elapsed);
            RecordInteractionCost(elapsed);
            frameOpen = false;
        }

        internal long BeginSpawn()
        {
            return frameOpen ? clock() : 0L;
        }

        internal long BeginVisualUpdate()
        {
            return recording && previousEligible ? clock() : 0L;
        }

        internal void EndVisualUpdate(long timestamp, bool isGui)
        {
            if (!recording || !previousEligible || timestamp == 0L) return;
            var elapsed = Elapsed(timestamp);
            if (isGui) maxGuiMs = Math.Max(maxGuiMs, elapsed);
            else maxLateUpdateMs = Math.Max(maxLateUpdateMs, elapsed);
            RecordVisualCost(elapsed, isGui);
        }

        internal void EndSpawn(long timestamp, bool spawned)
        {
            if (!frameOpen || timestamp == 0L) return;
            spawnAttempts++;
            if (spawned) spawns++;
            var elapsed = Elapsed(timestamp);
            maxSpawnMs = Math.Max(maxSpawnMs, elapsed);
            RecordSpawnCost(elapsed, spawned);
        }

        internal void EndPluginUpdate(long timestamp)
        {
            if (!pluginSample || !recording) return;
            pluginSample = false;
            if (timestamp != 0L)
            {
                var elapsed = Elapsed(timestamp);
                maxPluginMs = Math.Max(maxPluginMs, elapsed);
                RecordPluginCost(elapsed);
            }
        }

        internal void EndAttempt() { EndAttempt(true); }

        private double Elapsed(long timestamp)
        {
            return Math.Max(0d, clock() - timestamp) * millisecondsPerTick;
        }

        private void EndAttempt(bool partial)
        {
            CaptureUnpairedCollections();
            if (recording && frames > 0 && log != null)
            {
                var summary = string.Format(CultureInfo.InvariantCulture,
                    "[StartupPerformance] level={0} partial={1} gameplaySeconds={2:0.000} frames={3} " +
                    "spikesOver40ms={4} maxFrameMs={5:0.00} maxPluginUpdateMs={6:0.00} " +
                    "maxInteractionUpdateMs={7:0.00} maxSpawnMs={8:0.00} spawnCount={9} " +
                    "spawnAttempts={10} gc0Delta={11} maxGuiMs={12:0.00} maxLateUpdateMs={13:0.00} " +
                    "pairedFrames={14} framesOver25ms={15} framesOver30ms={16} " +
                    "p50FrameApproxMs={17} p95FrameApproxMs={18} p99FrameApproxMs={19}",
                    level, partial ? "true" : "false", seconds, frames, spikes,
                    maxFrameMs, maxPluginMs, maxInteractionMs, maxSpawnMs,
                    spawns, spawnAttempts, gcCollections, maxGuiMs, maxLateUpdateMs,
                    pairedFrames, framesOver25ms, framesOver30ms,
                    FramePercentile(0.50d), FramePercentile(0.95d), FramePercentile(0.99d));
                try { log(summary); }
                catch { } // Diagnostics cannot break gameplay or teardown.
                EmitSlowFrameDetails();
            }
            armed = recording = frameOpen = pluginSample = previousEligible = false;
            level = null;
            frames = spikes = spawns = spawnAttempts = gcCollections = 0;
            seconds = maxFrameMs = maxInteractionMs = maxPluginMs = maxSpawnMs = 0d;
            maxGuiMs = maxLateUpdateMs = 0d;
            ResetFrameSamples();
        }
    }
}
