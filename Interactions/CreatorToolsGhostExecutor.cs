using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsGhostExecutor :
        ICreatorToolsInteractionExecutor,
        ICreatorToolsPhasePersistentInteractionExecutor
    {
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly CreatorToolsGhostQueue queue =
            new CreatorToolsGhostQueue();

        private CreatorToolsGhostActor ghost;
        private int attemptSerial;
        private bool gameplayAdvancing;
        private bool defeatedDuringAttempt;
        private bool disposed;

        internal CreatorToolsGhostExecutor(
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.canSpawn = canSpawn;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        public bool NativeAssetsSettled
        {
            get { return true; }
        }

        public bool Supports(string item)
        {
            return string.Equals(
                item, CreatorToolsHelp.Ghost,
                StringComparison.Ordinal);
        }

        public bool IsAvailable(string item)
        {
            return Supports(item) && !disposed && Evaluate(canSpawn) &&
                PlayerOne() != null;
        }

        internal void SetGameplayAdvancing(bool value)
        {
            gameplayAdvancing = value;
        }

        public void Update()
        {
            if (disposed)
                return;

            var currentAttempt = CurrentAttemptSerial();
            if (currentAttempt != attemptSerial)
            {
                attemptSerial = currentAttempt;
                defeatedDuringAttempt = false;
                DestroyGhost();
            }

            var player = PlayerOne();
            if (player == null)
                return;
            if (player.IsDead)
            {
                defeatedDuringAttempt = true;
                DestroyGhost();
                return;
            }
            if (!player.gameObject.activeInHierarchy)
                return;

            if (ghost == null && !defeatedDuringAttempt &&
                gameplayAdvancing && Evaluate(canSpawn))
            {
                string ignored;
                TryStartNext(player, out ignored);
            }
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
            feedbackCode = "requires_gameplay_level";
            error = string.Empty;
            if (!Supports(item))
            {
                feedbackCode = "unknown_item";
                return false;
            }
            if (!IsAvailable(item))
                return false;

            var credit = queue.Enqueue(donor, giftImagePath);
            if (ghost == null && !defeatedDuringAttempt)
            {
                var player = PlayerOne();
                if (!TryStartNext(player, out error))
                {
                    queue.Remove(credit.Id);
                    feedbackCode = "spawn_failed";
                    return false;
                }
            }

            handle = CompletedHandle.Instance;
            feedbackCode = "spawned";
            return true;
        }

        internal bool IsDamageBoostActive(PlayerId playerId)
        {
            return !disposed && playerId == PlayerId.PlayerOne &&
                ghost != null && ghost.EffectActive;
        }

        internal bool TryGetProjectileOffset(
            PlayerId playerId, out Vector3 offset)
        {
            offset = Vector3.zero;
            return IsDamageBoostActive(playerId) &&
                ghost.TryGetProjectileOffset(out offset);
        }

        public void EndGameplayLevel()
        {
            gameplayAdvancing = false;
            defeatedDuringAttempt = false;
            attemptSerial = 0;
            DestroyGhost();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            queue.Clear();
            DestroyGhost();
        }

        private bool TryStartNext(
            AbstractPlayerController player, out string error)
        {
            error = string.Empty;
            if (ghost != null || player == null || player.IsDead ||
                queue.Count == 0)
                return ghost != null;

            CreatorToolsGhostQueue.Credit credit;
            if (!queue.TryDequeue(out credit))
                return false;

            GameObject root = null;
            try
            {
                root = new GameObject("CreatorTools_GhostHelp");
                var created = root.AddComponent<CreatorToolsGhostActor>();
                created.Initialize(
                    player,
                    credit.Donor,
                    credit.GiftImagePath,
                    CanAdvance);
                ghost = created;
                if (logInfo != null)
                    logInfo(
                        "Ayuda fantasmal activada durante " +
                        CreatorToolsGhostActor.LifetimeSeconds +
                        " segundos para " + credit.Donor + ".");
                return true;
            }
            catch (Exception exception)
            {
                queue.RestoreFront(credit);
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                error = exception.ToString();
                if (logWarning != null)
                    logWarning(
                        "Could not create Ghost Help: " + error);
                return false;
            }
        }

        private bool CanAdvance()
        {
            return gameplayAdvancing && Evaluate(canSpawn);
        }

        private void DestroyGhost()
        {
            if (ghost != null)
                UnityEngine.Object.Destroy(ghost.gameObject);
            ghost = null;
        }

        private static AbstractPlayerController PlayerOne()
        {
            try
            {
                return PlayerManager.GetPlayer(PlayerId.PlayerOne);
            }
            catch
            {
                return null;
            }
        }

        private static int CurrentAttemptSerial()
        {
            try
            {
                var level = Level.Current;
                return level == null ? 0 : level.GetInstanceID();
            }
            catch
            {
                return 0;
            }
        }

        private static bool Evaluate(Func<bool> predicate)
        {
            if (predicate == null)
                return false;
            try { return predicate(); }
            catch { return false; }
        }

        private sealed class CompletedHandle :
            ICreatorToolsInteractionHandle
        {
            internal static readonly CompletedHandle Instance =
                new CompletedHandle();

            public bool IsComplete
            {
                get { return true; }
            }

            public void Dispose()
            {
            }
        }
    }
}
