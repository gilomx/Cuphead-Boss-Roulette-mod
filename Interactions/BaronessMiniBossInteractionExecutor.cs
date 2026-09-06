using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class BaronessMiniBossInteractionExecutor :
        ICreatorToolsInteractionExecutor, ICreatorToolsLevelRestrictedInteractionExecutor,
        ICreatorToolsExclusiveInteractionExecutor
    {
        private static readonly System.Reflection.FieldInfo NativeCastleField =
            HarmonyLib.AccessTools.Field(typeof(BaronessLevel), "castle");
        private readonly NativeBaronessMiniBossCache cache;
        private readonly Func<bool> canSpawn;
        private readonly Func<int> getMaximumMiniBosses;
        private readonly Action<string> logWarning;
        private readonly List<string> presentItems = new List<string>();
        private int presenceFrame = -1;
        private bool nativeMiniBossRoundPending;
        private readonly List<BaronessMiniBossInteractionState> active =
            new List<BaronessMiniBossInteractionState>();

        internal BaronessMiniBossInteractionExecutor(
            NativeBaronessHeadTossCache source, Func<bool> canSpawn,
            Func<int> getMaximumMiniBosses,
            Action<string> logInfo, Action<string> logWarning)
        {
            cache = new NativeBaronessMiniBossCache(source, logInfo, logWarning);
            this.canSpawn = canSpawn;
            this.getMaximumMiniBosses = getMaximumMiniBosses;
            this.logWarning = logWarning;
        }

        public bool Supports(string item) { return NativeBaronessMiniBossCache.Supports(item); }

        public bool SupportsCurrentLevel(string item)
        {
            return BaronessMiniBossInteractionState.CanSpawnInCurrentLevel(item);
        }

        public bool IsAvailable(string item)
        {
            return Supports(item) && cache.Ready && CanSpawn() &&
                BaronessMiniBossInteractionPatches.InstalledSuccessfully &&
                BaronessMiniBossInteractionState.CanSpawnInCurrentLevel(item) &&
                !BlocksConcurrentSpawn(item);
        }

        public bool BlocksConcurrentSpawn(string item)
        {
            if (!Supports(item))
                return false;
            RefreshPresentMiniBosses();
            if (nativeMiniBossRoundPending)
                return true;
            return !CreatorToolsMiniBossSpawnPolicy.CanSpawn(item, presentItems,
                getMaximumMiniBosses == null ? 1 : getMaximumMiniBosses());
        }

        private void RefreshPresentMiniBosses()
        {
            if (presenceFrame == Time.frameCount)
                return;
            presenceFrame = Time.frameCount;
            presentItems.Clear();
            nativeMiniBossRoundPending = false;
            if (Level.Current is BaronessLevel)
            {
                // The head-toss interaction also has an inert castle object;
                // read the encounter's own reference instead of a global search.
                var castle = NativeCastleField == null ? null :
                    NativeCastleField.GetValue(Level.Current) as BaronessLevelCastle;
                // The original encounter schedules its next actor independently
                // of our queues. Wait through that whole round, including gaps,
                // so a later native summon cannot duplicate a catalog actor.
                nativeMiniBossRoundPending = castle == null ||
                    castle.state != BaronessLevelCastle.State.Chase;
            }
            // Active native actors include every queue's instances and the
            // original Baroness actors. Inactive preload templates do not count.
            // Keep the slot through the death animation, until destruction.
            var actors = UnityEngine.Object.FindObjectsOfType<BaronessLevelMiniBossBase>();
            for (var i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor is BaronessLevelCupcake) presentItems.Add(CreatorToolsInteractionIds.BaronessCupcake);
                else if (actor is BaronessLevelGumball) presentItems.Add(CreatorToolsInteractionIds.BaronessGumball);
                else if (actor is BaronessLevelWaffle) presentItems.Add(CreatorToolsInteractionIds.BaronessWaffle);
                else if (actor is BaronessLevelCandyCorn) presentItems.Add(CreatorToolsInteractionIds.BaronessCandyCorn);
                else if (actor is BaronessLevelJawbreaker) presentItems.Add(CreatorToolsInteractionIds.BaronessJawbreaker);
            }
        }

        public void Update()
        {
            presenceFrame = -1;
            cache.Update();
            for (var i = active.Count - 1; i >= 0; i--)
                if (active[i] == null)
                    active.RemoveAt(i);
        }

        public bool TrySpawn(string item, string donor, string giftImagePath,
            out ICreatorToolsInteractionHandle handle, out string feedbackCode, out string error)
        {
            handle = null;
            error = null;
            feedbackCode = "spawn_failed";
            if (!Supports(item)) { feedbackCode = "unknown_item"; return false; }
            if (!BaronessMiniBossInteractionPatches.InstalledSuccessfully)
            {
                feedbackCode = "native_assets_unavailable";
                error = "The native mini-boss integration patches could not be installed.";
                return false;
            }
            if (!CanSpawn()) { feedbackCode = "requires_gameplay_level"; return false; }
            if (!BaronessMiniBossInteractionState.CanSpawnInCurrentLevel(item))
            {
                feedbackCode = "requires_ground_level";
                return false;
            }
            if (!cache.Ready)
            {
                feedbackCode = cache.Failed ? "native_assets_unavailable" : "native_assets_loading";
                return false;
            }
            // Re-check immediately before spawning as a defense for any caller
            // bypassing queue eligibility. A blocked redemption stays pending.
            presenceFrame = -1;
            if (BlocksConcurrentSpawn(item))
            {
                feedbackCode = "interaction_type_active";
                return false;
            }
            GameObject root = null;
            try
            {
                root = new GameObject("CreatorTools_" + item + "_State");
                var state = root.AddComponent<BaronessMiniBossInteractionState>();
                var actor = cache.CreateInactive(item, root.transform);
                state.Initialize(actor, item, donor, giftImagePath, logWarning);
                active.Add(state);
                presenceFrame = -1;
                handle = new CreatorToolsUnityObjectInteractionHandle(state);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (root != null) UnityEngine.Object.Destroy(root);
                return false;
            }
        }

        public void EndGameplayLevel()
        {
            for (var i = 0; i < active.Count; i++)
                if (active[i] != null)
                {
                    active[i].gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(active[i].gameObject);
                }
            active.Clear();
            presenceFrame = -1;
            presentItems.Clear();
            BaronessMiniBossInteractionState.ResetArenaCache();
        }

        public void Dispose() { EndGameplayLevel(); cache.Dispose(); }

        private bool CanSpawn()
        {
            try { return canSpawn != null && canSpawn(); }
            catch { return false; }
        }
    }
}
