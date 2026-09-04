using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class RobotHomingBombInteractionExecutor :
        ICreatorToolsInteractionExecutor
    {
        private readonly NativeRobotHomingBombCache nativeCache;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logWarning;
        private readonly List<NativeRobotHomingBombSpawn> activeSpawns =
            new List<NativeRobotHomingBombSpawn>();

        internal RobotHomingBombInteractionExecutor(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.canSpawn = canSpawn;
            this.logWarning = logWarning;
            nativeCache = new NativeRobotHomingBombCache(
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
                CreatorToolsInteractionIds.RobotHomingBomb,
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
                error = "The Dr. Kahl homing bomb executor does not " +
                    "support " + item + ".";
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

            NativeRobotHomingBombSpawnParameters parameters;
            if (!NativeRobotHomingBombSpawnPattern.TryCreate(
                ReservedPositions(),
                out parameters,
                out error))
                return false;

            NativeRobotHomingBombSpawn spawned;
            if (!nativeCache.TrySpawn(
                parameters,
                donor,
                out spawned,
                out error))
                return false;

            CreatorToolsInteractionPresentation.SetGiftImage(
                spawned.Actor == null
                    ? null
                    : spawned.Actor.gameObject,
                giftImagePath,
                logWarning);
            activeSpawns.Add(spawned);
            handle = new CreatorToolsUnityObjectInteractionHandle(
                spawned.Actor,
                spawned.ScaleRoot,
                null);
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
            var result = new List<Vector2>(activeSpawns.Count + 8);
            for (var i = 0; i < activeSpawns.Count; i++)
                if (activeSpawns[i] != null &&
                    activeSpawns[i].Actor != null)
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
                if (activeSpawns[i] == null ||
                    activeSpawns[i].Actor == null)
                    activeSpawns.RemoveAt(i);
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
