using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class HomingCarrotInteractionExecutor :
        ICreatorToolsInteractionExecutor
    {
        private readonly NativeHomingCarrotCache nativeCache;
        private readonly Func<bool> canSpawn;
        private readonly List<ActiveSpawn> activeSpawns =
            new List<ActiveSpawn>();

        internal HomingCarrotInteractionExecutor(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.canSpawn = canSpawn;
            nativeCache = new NativeHomingCarrotCache(
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
                CreatorToolsInteractionIds.HomingCarrot,
                StringComparison.Ordinal);
        }

        public bool IsAvailable(string item)
        {
            return Supports(item) && nativeCache.CanSpawn;
        }

        public void Update()
        {
            nativeCache.Update();
            RemoveFinishedSpawns();
        }

        public bool TrySpawn(
            string item,
            string donor,
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
                error = "The homing carrot executor does not support " +
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

            NativeHomingCarrotSpawnParameters parameters;
            if (!NativeHomingCarrotSpawnPattern.TryCreate(
                ReservedPositions(),
                out parameters,
                out error))
                return false;

            VeggiesLevelCarrotHomingProjectile spawned;
            if (!nativeCache.TrySpawn(
                parameters,
                donor,
                out spawned,
                out error))
                return false;

            activeSpawns.Add(new ActiveSpawn
            {
                Actor = spawned
            });
            handle = new CreatorToolsUnityObjectInteractionHandle(spawned);
            return true;
        }

        public void EndGameplayLevel()
        {
            nativeCache.ClearSpawnedActors();
            activeSpawns.Clear();
        }

        public void Dispose()
        {
            nativeCache.Dispose();
            activeSpawns.Clear();
        }

        private List<Vector2> ReservedPositions()
        {
            RemoveFinishedSpawns();
            var result = new List<Vector2>(activeSpawns.Count + 4);
            for (var i = 0; i < activeSpawns.Count; i++)
                if (activeSpawns[i].Actor != null)
                    result.Add(activeSpawns[i].Actor.transform.position);

            var catalogActors = UnityEngine.Object.FindObjectsOfType<
                CreatorToolsDonorLabel>();
            for (var i = 0; i < catalogActors.Length; i++)
                if (catalogActors[i] != null)
                    result.Add(catalogActors[i].transform.position);

            var players = UnityEngine.Object.FindObjectsOfType<
                AbstractPlayerController>();
            for (var i = 0; i < players.Length; i++)
                if (players[i] != null &&
                    players[i].gameObject.activeInHierarchy)
                    result.Add(players[i].transform.position);
            return result;
        }

        private void RemoveFinishedSpawns()
        {
            for (var i = activeSpawns.Count - 1; i >= 0; i--)
                if (activeSpawns[i].Actor == null)
                    activeSpawns.RemoveAt(i);
        }

        private static bool Evaluate(Func<bool> predicate)
        {
            if (predicate == null)
                return false;
            try { return predicate(); }
            catch { return false; }
        }

        private sealed class ActiveSpawn
        {
            internal VeggiesLevelCarrotHomingProjectile Actor;
        }
    }

}
