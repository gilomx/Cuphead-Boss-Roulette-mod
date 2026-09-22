using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class TimedChallengeInteractionExecutor :
        ICreatorToolsInteractionExecutor, ICreatorToolsExclusiveInteractionExecutor,
        ICreatorToolsTimedInteractionExecutor
    {
        private readonly Func<bool> canPlay;
        private readonly Func<bool> baseHalfDamage;
        private readonly CreatorToolsTimedChallenge timer = new CreatorToolsTimedChallenge();

        internal TimedChallengeInteractionExecutor(Func<bool> canPlay, Func<bool> baseHalfDamage)
        {
            this.canPlay = canPlay;
            this.baseHalfDamage = baseHalfDamage;
        }

        internal bool Active { get { return timer.Active; } }
        internal bool Busy { get { return timer.Busy; } }
        internal bool GameplayAvailable { get { return canPlay(); } }
        internal CreatorToolsTimedChallenge Presentation { get { return timer; } }
        internal string Donor { get; private set; }
        internal int SecondsRemaining { get { return Mathf.CeilToInt(timer.Remaining); } }
        public bool NativeAssetsSettled { get { return true; } }
        public bool Supports(string item) { return item == CreatorToolsTimedChallenge.HalfDamage; }
        public bool BlocksConcurrentSpawn(string item) { return timer.Busy || baseHalfDamage(); }
        public bool IsAvailable(string item)
        {
            return Supports(item) && !BlocksConcurrentSpawn(item) && canPlay();
        }

        public void Update() { if (timer.Busy) timer.Advance(Time.deltaTime, canPlay()); }
        public void EndGameplayLevel() { timer.End(); }
        public void Dispose() { timer.End(); }

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
            feedbackCode = BlocksConcurrentSpawn(item) ? "interaction_type_active" : "requires_gameplay_level";
            if (!IsAvailable(item)) return false;
            if (!timer.Start(duration, countdown)) { feedbackCode = "invalid_setting"; return false; }
            Donor = (donor ?? string.Empty).Trim();
            handle = new Lease(timer);
            feedbackCode = "spawned";
            return true;
        }

        private sealed class Lease : ICreatorToolsInteractionHandle, ICreatorToolsTimedInteractionHandle
        {
            private readonly CreatorToolsTimedChallenge timer;
            private readonly int revision;
            internal Lease(CreatorToolsTimedChallenge timer) { this.timer = timer; revision = timer.Revision; }
            public bool IsComplete { get { return revision != timer.Revision || !timer.Busy; } }
            public bool CountingDown { get { return !IsComplete && timer.CountingDown; } }
            public int CountdownSecondsRemaining { get { return CountingDown ? Mathf.Max(1, Mathf.CeilToInt(timer.CountdownRemaining)) : 0; } }
            public int SecondsRemaining { get { return IsComplete ? 0 : Mathf.CeilToInt(timer.Remaining); } }
            public void Dispose() { timer.End(revision); }
        }
    }
}
