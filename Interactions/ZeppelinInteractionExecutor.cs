using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class ZeppelinInteractionExecutor : IDisposable
    {
        private static readonly System.Reflection.FieldInfo StopPointField =
            AccessTools.Field(typeof(FlyingBlimpLevelEnemy), "stopPoint");

        private readonly NativeZeppelinCache nativeCache;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logWarning;
        private readonly List<ActiveSpawn> activeSpawns =
            new List<ActiveSpawn>();
        private int purpleSpawnCounter;

        internal ZeppelinInteractionExecutor(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.canSpawn = canSpawn;
            this.logWarning = logWarning;
            nativeCache = new NativeZeppelinCache(
                coroutineHost,
                canPreload,
                canSpawn,
                logInfo,
                logWarning);
        }

        internal bool Available
        {
            get
            {
                return Evaluate(canSpawn) &&
                    (FindBlimpLady() != null ||
                     (nativeCache.CanSpawn(NativeZeppelinVariant.Purple) &&
                      nativeCache.CanSpawn(NativeZeppelinVariant.Green)));
            }
        }

        internal void Update()
        {
            nativeCache.Update();
            RemoveFinishedSpawns();
        }

        internal bool TrySpawn(
            NativeZeppelinVariant variant,
            string donor,
            out FlyingBlimpLevelEnemy spawned,
            out string feedbackCode,
            out string error)
        {
            spawned = null;
            feedbackCode = "spawn_failed";
            error = null;

            if (!Evaluate(canSpawn))
            {
                feedbackCode = "requires_gameplay_level";
                return false;
            }

            NativeZeppelinSpawnParameters parameters;
            if (!NativeZeppelinSpawnPattern.TryCreate(
                variant,
                ActiveLanes(),
                ref purpleSpawnCounter,
                out parameters,
                out error))
                return false;

            var lady = FindBlimpLady();
            if (lady == null)
            {
                if (!nativeCache.Ready(variant))
                {
                    feedbackCode = nativeCache.Failed
                        ? "native_assets_unavailable"
                        : "native_assets_loading";
                    return false;
                }
                if (!nativeCache.CanSpawn(variant))
                {
                    feedbackCode = "requires_gameplay_level";
                    return false;
                }

                if (!nativeCache.TrySpawn(
                    variant,
                    parameters,
                    donor,
                    out spawned,
                    out error))
                    return false;
                TrackSpawn(spawned, parameters.Lane);
                return true;
            }
            var spawnedNative = TrySpawnNative(
                lady,
                variant,
                parameters,
                donor,
                out spawned,
                out feedbackCode,
                out error);
            if (spawnedNative)
                TrackSpawn(spawned, parameters.Lane);
            return spawnedNative;
        }

        private bool TrySpawnNative(
            FlyingBlimpLevelBlimpLady lady,
            NativeZeppelinVariant variant,
            NativeZeppelinSpawnParameters parameters,
            string donor,
            out FlyingBlimpLevelEnemy spawned,
            out string feedbackCode,
            out string error)
        {
            spawned = null;
            feedbackCode = "spawn_failed";
            error = null;
            try
            {
                var prefabField = AccessTools.Field(
                    typeof(FlyingBlimpLevelBlimpLady),
                    variant == NativeZeppelinVariant.Green
                        ? "enemyPrefabB"
                        : "enemyPrefabA");
                var summonMethod = AccessTools.Method(
                    typeof(FlyingBlimpLevelBlimpLady), "SummonEnemy");
                var prefab = prefabField == null
                    ? null
                    : prefabField.GetValue(lady) as FlyingBlimpLevelEnemy;
                if (prefab == null || summonMethod == null)
                    throw new MissingMemberException(
                        "Cuphead did not expose the requested native zeppelin prefab.");

                var before = Resources.FindObjectsOfTypeAll<
                    FlyingBlimpLevelEnemy>();
                summonMethod.Invoke(lady, new object[]
                {
                    prefab,
                    new Vector3(0f, parameters.Lane, 0f),
                    parameters.StopDistance,
                    parameters.Parryable
                });
                spawned = FindNewEnemy(before);
                if (spawned == null)
                    throw new InvalidOperationException(
                        "Cuphead did not return the summoned enemy.");
                if (StopPointField == null)
                    throw new MissingFieldException(
                        "Cuphead did not expose the zeppelin stop point.");
                StopPointField.SetValue(
                    spawned, parameters.StopDistance);

                CreatorToolsInteractionPresentation.PrepareActor(
                    spawned.gameObject,
                    donor,
                    logWarning);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        private List<float> ActiveLanes()
        {
            RemoveFinishedSpawns();
            var lanes = new List<float>(activeSpawns.Count);
            for (var i = 0; i < activeSpawns.Count; i++)
                lanes.Add(activeSpawns[i].Lane);
            return lanes;
        }

        private void TrackSpawn(
            FlyingBlimpLevelEnemy actor,
            float lane)
        {
            if (actor == null)
                return;
            activeSpawns.Add(new ActiveSpawn
            {
                Actor = actor,
                Lane = lane
            });
        }

        private void RemoveFinishedSpawns()
        {
            for (var i = activeSpawns.Count - 1; i >= 0; i--)
                if (activeSpawns[i].Actor == null)
                    activeSpawns.RemoveAt(i);
        }

        internal void ClearActiveSpawns()
        {
            for (var i = 0; i < activeSpawns.Count; i++)
                if (activeSpawns[i].Actor != null)
                    UnityEngine.Object.Destroy(
                        activeSpawns[i].Actor.gameObject);
            activeSpawns.Clear();
        }

        private sealed class ActiveSpawn
        {
            internal FlyingBlimpLevelEnemy Actor;
            internal float Lane;
        }

        private static FlyingBlimpLevelBlimpLady FindBlimpLady()
        {
            return UnityEngine.Object.FindObjectOfType<
                FlyingBlimpLevelBlimpLady>();
        }

        private static FlyingBlimpLevelEnemy FindNewEnemy(
            FlyingBlimpLevelEnemy[] before)
        {
            var known = new HashSet<int>();
            for (var i = 0; i < before.Length; i++)
                if (before[i] != null)
                    known.Add(before[i].GetInstanceID());

            var after = Resources.FindObjectsOfTypeAll<
                FlyingBlimpLevelEnemy>();
            for (var i = 0; i < after.Length; i++)
            {
                if (after[i] != null &&
                    !known.Contains(after[i].GetInstanceID()) &&
                    after[i].gameObject.activeInHierarchy)
                    return after[i];
            }
            return null;
        }

        private static bool Evaluate(Func<bool> predicate)
        {
            if (predicate == null)
                return false;
            try { return predicate(); }
            catch { return false; }
        }

        public void Dispose()
        {
            nativeCache.Dispose();
            activeSpawns.Clear();
        }
    }
}
