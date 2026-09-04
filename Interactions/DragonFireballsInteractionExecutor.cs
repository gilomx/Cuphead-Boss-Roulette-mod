using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class DragonFireballsInteractionExecutor :
        ICreatorToolsInteractionExecutor
    {
        private readonly NativeDragonFireballsCache nativeCache;
        private readonly Func<bool> canSpawn;
        private readonly List<DragonFireballsInteractionState> activeStates =
            new List<DragonFireballsInteractionState>();

        internal DragonFireballsInteractionExecutor(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.canSpawn = canSpawn;
            nativeCache = new NativeDragonFireballsCache(
                coroutineHost,
                canPreload,
                canSpawn,
                logInfo,
                logWarning);
        }

        public bool Supports(string item)
        {
            return string.Equals(
                item,
                CreatorToolsInteractionIds.DragonFireballs,
                StringComparison.Ordinal);
        }

        public bool IsAvailable(string item)
        {
            return Supports(item) && nativeCache.CanSpawn;
        }

        public void Update()
        {
            nativeCache.Update();
            RemoveFinishedStates();
        }

        public bool TrySpawn(
            string item,
            string donor,
            string giftImagePath,
            out ICreatorToolsInteractionHandle handle,
            out string feedbackCode,
            out string error)
        {
            handle = null;
            feedbackCode = "spawn_failed";
            error = null;
            if (!Supports(item))
            {
                feedbackCode = "unknown_item";
                error = "The Dragon fireball executor does not support " +
                    item + ".";
                return false;
            }
            if (!Evaluate(canSpawn))
            {
                feedbackCode = "requires_gameplay_level";
                return false;
            }
            if (!nativeCache.Ready)
            {
                feedbackCode = nativeCache.Failed
                    ? "native_assets_unavailable"
                    : "native_assets_loading";
                return false;
            }

            DragonFireballsInteractionState state;
            if (!nativeCache.TrySpawn(
                donor,
                giftImagePath,
                out state,
                out error))
                return false;

            activeStates.Add(state);
            handle = new CreatorToolsUnityObjectInteractionHandle(
                state,
                state == null ? null : state.gameObject,
                null);
            return true;
        }

        public void EndGameplayLevel()
        {
            nativeCache.ClearSpawnedActors();
            activeStates.Clear();
        }

        public void Dispose()
        {
            nativeCache.Dispose();
            activeStates.Clear();
        }

        private void RemoveFinishedStates()
        {
            for (var i = activeStates.Count - 1; i >= 0; i--)
                if (activeStates[i] == null)
                    activeStates.RemoveAt(i);
        }

        private static bool Evaluate(Func<bool> predicate)
        {
            if (predicate == null)
                return false;
            try { return predicate(); }
            catch { return false; }
        }
    }
}
