using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CagneyHomingPlantInteractionExecutor :
        ICreatorToolsInteractionExecutor
    {
        private readonly NativeCagneyHomingPlantCache nativeCache;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logWarning;
        private readonly List<CagneyHomingPlantInteractionState> activeStates =
            new List<CagneyHomingPlantInteractionState>();

        internal CagneyHomingPlantInteractionExecutor(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.canSpawn = canSpawn;
            this.logWarning = logWarning;
            nativeCache = new NativeCagneyHomingPlantCache(
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
                CreatorToolsInteractionIds.CagneyHomingPlant,
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
                error = "The Cagney homing plant executor does not support " +
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

            NativeCagneyHomingPlantSpawnParameters parameters;
            if (!NativeCagneyHomingPlantSpawnPattern.TryCreate(
                ReservedPositions(),
                out parameters,
                out error))
                return false;

            CagneyHomingPlantInteractionState state;
            if (!nativeCache.TrySpawn(
                parameters,
                donor,
                out state,
                out error))
                return false;

            CreatorToolsInteractionPresentation.SetGiftImage(
                state,
                giftImagePath,
                logWarning);
            activeStates.Add(state);
            handle = new CreatorToolsUnityObjectInteractionHandle(state);
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

        private List<Vector2> ReservedPositions()
        {
            RemoveFinishedStates();
            var result = new List<Vector2>(activeStates.Count + 8);
            for (var i = 0; i < activeStates.Count; i++)
            {
                Vector2 position;
                if (activeStates[i] != null &&
                    activeStates[i].TryGetActorPosition(out position))
                    result.Add(position);
            }

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
