using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class TimedChallengeInteractionExecutor :
        ICreatorToolsInteractionExecutor, ICreatorToolsExclusiveInteractionExecutor,
        ICreatorToolsTimedInteractionExecutor, ICreatorToolsLevelRestrictedInteractionExecutor
    {
        private readonly Func<bool> canPlay;
        private readonly Func<string, bool> baseChallengeActive;
        private readonly Func<bool> planeControls;
        private readonly Func<string, bool> effectAvailable;
        private readonly Action<CreatorToolsTimedChallenge> syncEffects;
        private readonly Action<CreatorToolsTimedChallenge, float, bool> advanceEffects;
        private readonly CreatorToolsTimedChallenge timer = new CreatorToolsTimedChallenge();

        internal TimedChallengeInteractionExecutor(Func<bool> canPlay, Func<string, bool> baseChallengeActive, Func<bool> planeControls, Func<string, bool> effectAvailable,
            Action<CreatorToolsTimedChallenge> syncEffects,
            Action<CreatorToolsTimedChallenge, float, bool> advanceEffects = null)
        {
            this.canPlay = canPlay;
            this.baseChallengeActive = baseChallengeActive;
            this.planeControls = planeControls;
            this.effectAvailable = effectAvailable;
            this.syncEffects = syncEffects;
            this.advanceEffects = advanceEffects;
        }

        internal bool IsActive(string item) { return timer.IsActive(item); }
        internal bool Busy { get { return timer.Busy; } }
        internal bool GameplayAvailable { get { return canPlay(); } }
        internal CreatorToolsTimedChallenge Presentation { get { return timer; } }
        internal string Donor { get; private set; }
        internal int SecondsRemaining { get { return Mathf.CeilToInt(timer.Remaining); } }
        public bool NativeAssetsSettled { get { return true; } }
        public bool Supports(string item) { return CreatorToolsTimedChallenge.Supports(item); }
        public bool SupportsCurrentLevel(string item) { return CreatorToolsTimedChallenge.SupportsLevel(item, planeControls()); }
        public bool BlocksConcurrentSpawn(string item) { return timer.Busy || baseChallengeActive(item); }
        public bool IsAvailable(string item)
        {
            return SupportsCurrentLevel(item) && !BlocksConcurrentSpawn(item) && canPlay() && effectAvailable(item);
        }

        public void Update()
        {
            // Also drain a deferred weapon restore once an in-flight EX/super finishes.
            syncEffects(timer);
            var elapsed = timer.EffectElapsed;
            var playing = timer.Busy && canPlay() && SupportsCurrentLevel(timer.Item) && effectAvailable(timer.Item);
            if (timer.Busy) timer.Advance(Time.deltaTime, playing);
            syncEffects(timer);
            if (advanceEffects != null)
            {
                advanceEffects(timer, timer.EffectElapsed - elapsed, playing);
                syncEffects(timer);
            }
        }
        public void EndGameplayLevel() { timer.End(); syncEffects(timer); }
        public void Dispose() { EndGameplayLevel(); }

        public bool TrySpawn(string item, string donor, string giftImagePath,
            out ICreatorToolsInteractionHandle handle, out string feedbackCode, out string error)
        {
            return TrySpawn(item, donor, giftImagePath, CreatorToolsTimedChallenge.DefaultDuration,
                CreatorToolsTimedChallenge.DefaultCountdown,
                out handle, out feedbackCode, out error);
        }

        public bool TrySpawn(string item, string donor, string giftImagePath, int duration, int countdown,
            out ICreatorToolsInteractionHandle handle, out string feedbackCode, out string error)
        {
            handle = null;
            error = string.Empty;
            feedbackCode = !SupportsCurrentLevel(item) && CreatorToolsTimedChallenge.RequiresPlane(item) ? "requires_plane_level"
                : BlocksConcurrentSpawn(item) ? "interaction_type_active" : "requires_gameplay_level";
            if (!IsAvailable(item)) return false;
            if (!timer.Start(item, duration, countdown, planeControls())) { feedbackCode = "invalid_setting"; return false; }
            Donor = (donor ?? string.Empty).Trim();
            syncEffects(timer);
            handle = new Lease(timer, syncEffects);
            feedbackCode = "spawned";
            return true;
        }

        private sealed class Lease : ICreatorToolsInteractionHandle, ICreatorToolsTimedInteractionHandle
        {
            private readonly CreatorToolsTimedChallenge timer;
            private readonly int revision;
            private readonly Action<CreatorToolsTimedChallenge> syncEffects;
            internal Lease(CreatorToolsTimedChallenge timer, Action<CreatorToolsTimedChallenge> syncEffects) { this.timer = timer; this.syncEffects = syncEffects; revision = timer.Revision; }
            public bool IsComplete { get { return revision != timer.Revision || !timer.Busy; } }
            public bool CountingDown { get { return !IsComplete && timer.CountingDown; } }
            public int CountdownSecondsRemaining { get { return CountingDown ? Mathf.Max(1, Mathf.CeilToInt(timer.CountdownRemaining)) : 0; } }
            public int SecondsRemaining { get { return IsComplete ? 0 : Mathf.CeilToInt(timer.Remaining); } }
            public void Dispose() { timer.End(revision); syncEffects(timer); }
        }
    }
}
