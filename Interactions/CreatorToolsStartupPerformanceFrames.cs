using System;
using System.Globalization;

namespace Gilomx.CupheadBossRoulette
{
    internal enum CreatorToolsStartupStage
    {
        Executors, TimedExecutor, Commands, Availability, Dispatch,
        LiveEvents, Publication, StreamDrain, BattleHud, TimedHud,
        NativePrompts, LateVisuals
    }

    internal sealed partial class CreatorToolsStartupPerformance
    {
        private const int SlowFrameCapacity = 5;
        // One-ms percentile buckets, capped at 1000+ ms. The exact duration
        // remains available in the bounded list of slowest frame samples.
        private readonly int[] frameHistogram = new int[1001];
        private readonly FrameSample[] slowFrames = new FrameSample[SlowFrameCapacity];
        private FrameSample currentFrame;
        private bool hasCurrentFrame;
        private int syntheticFrameId;
        private int slowFrameCount;
        private int pairedFrames;
        private int framesOver25ms;
        private int framesOver30ms;

        internal long BeginStage()
        {
            return recording && hasCurrentFrame && previousEligible ? clock() : 0L;
        }

        internal void EndStage(long timestamp, CreatorToolsStartupStage stage)
        {
            if (!recording || !hasCurrentFrame || !previousEligible || timestamp == 0L) return;
            currentFrame.AddStage(stage, Elapsed(timestamp));
        }

        private int NextSyntheticFrameId() { return ++syntheticFrameId; }

        private void StartFrameSample(int frameId, int collectionCount)
        {
            currentFrame = new FrameSample { Frame = frameId, CollectionsAtStart = collectionCount };
            hasCurrentFrame = true;
        }

        private void CompletePreviousFrame(int nextFrameId, double frameSeconds, int collectionCount)
        {
            if (!hasCurrentFrame) return;
            if (!previousEligible || nextFrameId != currentFrame.Frame + 1)
            {
                DiscardFrameSample();
                return;
            }
            var delta = Math.Max(0d, frameSeconds);
            currentFrame.FrameMs = delta * 1000d;
            currentFrame.Gc0Delta = Math.Max(0, collectionCount - currentFrame.CollectionsAtStart);
            gcCollections += currentFrame.Gc0Delta;
            seconds += delta;
            pairedFrames++;
            maxFrameMs = Math.Max(maxFrameMs, currentFrame.FrameMs);
            if (currentFrame.FrameMs > 25d) framesOver25ms++;
            if (currentFrame.FrameMs > 30d) framesOver30ms++;
            if (currentFrame.FrameMs > 40d) spikes++;
            var bucket = (int)Math.Min(1000d, Math.Ceiling(currentFrame.FrameMs));
            frameHistogram[bucket]++;
            RetainSlowFrame(currentFrame);
            hasCurrentFrame = false;
        }

        private void CaptureUnpairedCollections()
        {
            // A teardown can happen before the following Update supplies a
            // duration. Keep its GC total, but do not invent a frame duration
            // or put that unpaired frame in the percentile/top-frame results.
            if (recording && hasCurrentFrame && previousEligible)
                gcCollections += Math.Max(0, collections() - currentFrame.CollectionsAtStart);
            hasCurrentFrame = false;
        }

        private void DiscardFrameSample() { hasCurrentFrame = false; }
        private void RecordPluginCost(double value) { if (hasCurrentFrame) currentFrame.PluginMs += value; }
        private void RecordInteractionCost(double value) { if (hasCurrentFrame) currentFrame.InteractionMs += value; }
        private void RecordVisualCost(double value, bool gui)
        {
            if (!hasCurrentFrame) return;
            if (gui) currentFrame.GuiMs += value;
            else currentFrame.LateMs += value;
        }
        private void RecordSpawnCost(double value, bool spawned)
        {
            if (!hasCurrentFrame) return;
            currentFrame.SpawnTotalMs += value;
            currentFrame.SpawnMaxMs = Math.Max(currentFrame.SpawnMaxMs, value);
            currentFrame.SpawnAttempts++;
            if (spawned) currentFrame.Spawns++;
        }

        private void RetainSlowFrame(FrameSample sample)
        {
            var index = 0;
            while (index < slowFrameCount && slowFrames[index].FrameMs >= sample.FrameMs) index++;
            if (index >= SlowFrameCapacity) return;
            var last = Math.Min(slowFrameCount, SlowFrameCapacity - 1);
            for (var i = last; i > index; i--) slowFrames[i] = slowFrames[i - 1];
            slowFrames[index] = sample;
            slowFrameCount = Math.Min(SlowFrameCapacity, slowFrameCount + 1);
        }

        private int FramePercentile(double percentile)
        {
            if (pairedFrames == 0) return 0;
            var rank = (int)Math.Ceiling(pairedFrames * percentile);
            var cumulative = 0;
            for (var i = 0; i < frameHistogram.Length; i++)
            {
                cumulative += frameHistogram[i];
                if (cumulative >= rank) return i;
            }
            return frameHistogram.Length - 1;
        }

        private void EmitSlowFrameDetails()
        {
            for (var i = 0; i < slowFrameCount; i++)
            {
                var sample = slowFrames[i];
                // Nested columns are deliberate: timedExecutor is part of
                // executors, and spawnTotal is part of dispatch. Do not add
                // these overlapping measurements to the top-level totals.
                var detail = string.Format(CultureInfo.InvariantCulture,
                    "[StartupPerformanceFrame] level={0} rank={1} frame={2} frameMs={3:0.00} " +
                    "pluginMs={4:0.00} interactionMs={5:0.00} lateMs={6:0.00} guiMs={7:0.00} gc0Delta={8} " +
                    "spawns={9} spawnAttempts={10} spawnTotalMs={11:0.00} spawnMaxMs={12:0.00} " +
                    "executorsMs={13:0.00} timedExecutorMs={14:0.00} commandsMs={15:0.00} " +
                    "availabilityMs={16:0.00} dispatchMs={17:0.00} liveEventsMs={18:0.00} " +
                    "publicationMs={19:0.00} streamDrainMs={20:0.00} battleHudMs={21:0.00} " +
                    "timedHudMs={22:0.00} nativePromptsMs={23:0.00} lateVisualsMs={24:0.00}",
                    level, i + 1, sample.Frame, sample.FrameMs, sample.PluginMs, sample.InteractionMs,
                    sample.LateMs, sample.GuiMs, sample.Gc0Delta, sample.Spawns, sample.SpawnAttempts,
                    sample.SpawnTotalMs, sample.SpawnMaxMs, sample.ExecutorsMs, sample.TimedExecutorMs,
                    sample.CommandsMs, sample.AvailabilityMs, sample.DispatchMs, sample.LiveEventsMs,
                    sample.PublicationMs, sample.StreamDrainMs, sample.BattleHudMs, sample.TimedHudMs,
                    sample.NativePromptsMs, sample.LateVisualsMs);
                try { log(detail); }
                catch { }
            }
        }

        private void ResetFrameSamples()
        {
            hasCurrentFrame = false;
            currentFrame = new FrameSample();
            slowFrameCount = pairedFrames = framesOver25ms = framesOver30ms = 0;
            syntheticFrameId = 0;
            Array.Clear(frameHistogram, 0, frameHistogram.Length);
            Array.Clear(slowFrames, 0, slowFrames.Length);
        }

        private struct FrameSample
        {
            internal int Frame, CollectionsAtStart, Gc0Delta, Spawns, SpawnAttempts;
            internal double FrameMs, PluginMs, InteractionMs, LateMs, GuiMs;
            internal double SpawnTotalMs, SpawnMaxMs, ExecutorsMs, TimedExecutorMs;
            internal double CommandsMs, AvailabilityMs, DispatchMs, LiveEventsMs, PublicationMs;
            internal double StreamDrainMs, BattleHudMs, TimedHudMs, NativePromptsMs, LateVisualsMs;

            internal void AddStage(CreatorToolsStartupStage stage, double value)
            {
                switch (stage)
                {
                    case CreatorToolsStartupStage.Executors: ExecutorsMs += value; break;
                    case CreatorToolsStartupStage.TimedExecutor: TimedExecutorMs += value; break;
                    case CreatorToolsStartupStage.Commands: CommandsMs += value; break;
                    case CreatorToolsStartupStage.Availability: AvailabilityMs += value; break;
                    case CreatorToolsStartupStage.Dispatch: DispatchMs += value; break;
                    case CreatorToolsStartupStage.LiveEvents: LiveEventsMs += value; break;
                    case CreatorToolsStartupStage.Publication: PublicationMs += value; break;
                    case CreatorToolsStartupStage.StreamDrain: StreamDrainMs += value; break;
                    case CreatorToolsStartupStage.BattleHud: BattleHudMs += value; break;
                    case CreatorToolsStartupStage.TimedHud: TimedHudMs += value; break;
                    case CreatorToolsStartupStage.NativePrompts: NativePromptsMs += value; break;
                    case CreatorToolsStartupStage.LateVisuals: LateVisualsMs += value; break;
                }
            }
        }
    }
}
