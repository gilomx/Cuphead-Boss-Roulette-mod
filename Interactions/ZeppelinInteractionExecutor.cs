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
        private int purpleSpawnCounter;

        internal ZeppelinInteractionExecutor(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
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
                return FindBlimpLady() != null ||
                    (nativeCache.CanSpawn(NativeZeppelinVariant.Purple) &&
                    nativeCache.CanSpawn(NativeZeppelinVariant.Green));
            }
        }

        internal void Update()
        {
            nativeCache.Update();
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

            NativeZeppelinSpawnParameters parameters;
            if (!NativeZeppelinSpawnPattern.TryCreate(
                variant,
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
                return true;
            }
            return TrySpawnNative(
                lady,
                variant,
                parameters,
                donor,
                out spawned,
                out feedbackCode,
                out error);
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

                var label = spawned.gameObject.AddComponent<
                    CreatorToolsDonorLabel>();
                label.Initialize(donor);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
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

        public void Dispose()
        {
            nativeCache.Dispose();
        }
    }
}
