using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsZeppelinProjectilePresentation
    {
        private static readonly FieldInfo ProjectilePrefabField =
            AccessTools.Field(typeof(FlyingBlimpLevelEnemy), "projectilePrefab");
        private static readonly FieldInfo ParryablePrefabField =
            AccessTools.Field(typeof(FlyingBlimpLevelEnemy), "parryablePrefab");

        [ThreadStatic]
        private static FireContext currentFire;

        internal static void InstallPatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;

            var prefix = AccessTools.Method(
                typeof(CreatorToolsZeppelinProjectilePresentation),
                "BeginFirePrefix");
            var postfix = AccessTools.Method(
                typeof(CreatorToolsZeppelinProjectilePresentation),
                "EndFirePostfix");
            var finalizer = AccessTools.Method(
                typeof(CreatorToolsZeppelinProjectilePresentation),
                "EndFireFinalizer");
            var created = AccessTools.Method(
                typeof(CreatorToolsZeppelinProjectilePresentation),
                "PrepareCreatedProjectilePostfix");
            var create = AccessTools.Method(typeof(BasicProjectile), "Create",
                new[] { typeof(Vector2), typeof(float), typeof(float) });
            if (create == null || prefix == null || postfix == null ||
                finalizer == null || created == null ||
                ProjectilePrefabField == null || ParryablePrefabField == null)
            {
                Warn(logWarning,
                    "Could not install the catalog zeppelin projectile " +
                    "presentation patch.");
                return;
            }
            harmony.Patch(create, postfix: new HarmonyMethod(created));
            var fireMethods = new[]
            {
                AccessTools.Method(
                    typeof(FlyingBlimpLevelEnemy), "FireSingle"),
                AccessTools.Method(
                    typeof(FlyingBlimpLevelEnemy), "FireSpreadshot")
            };

            for (var i = 0; i < fireMethods.Length; i++)
            {
                if (fireMethods[i] == null)
                {
                    Warn(logWarning,
                        "Could not install the catalog zeppelin projectile " +
                        "presentation patch.");
                    continue;
                }
                harmony.Patch(
                    fireMethods[i],
                    prefix: new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix),
                    finalizer: new HarmonyMethod(finalizer));
            }
        }

        private static void BeginFirePrefix(
            FlyingBlimpLevelEnemy __instance,
            out FireScope __state)
        {
            __state = new FireScope { Previous = currentFire, Entered = true };
            // An unmarked nested native fire must not inherit another actor's
            // catalog scope. The previous scope is restored on every exit.
            currentFire = new FireContext();
            if (__instance == null ||
                __instance.GetComponent<
                    CreatorToolsInteractionRenderPriority>() == null)
                return;

            currentFire.ProjectilePrefab =
                ProjectilePrefabField.GetValue(__instance) as BasicProjectile;
            currentFire.ParryablePrefab =
                ParryablePrefabField.GetValue(__instance) as BasicProjectile;
        }

        private static void PrepareCreatedProjectilePostfix(
            BasicProjectile __instance,
            BasicProjectile __result)
        {
            if (__instance == null ||
                (!ReferenceEquals(__instance, currentFire.ProjectilePrefab) &&
                 !ReferenceEquals(__instance, currentFire.ParryablePrefab)))
                return;

            var projectile = __result as FlyingBlimpLevelEnemyProjectile;
            if (projectile == null || !projectile.gameObject.activeInHierarchy)
                return;
            // Capture the exact native Create result, including parryable and
            // spread bullets, instead of scanning every loaded projectile
            // twice for each fire. Native aim, speed and animation stay native.
            CreatorToolsInteractionPresentation.MatchGameplayBodyScale(
                projectile.gameObject, null);
            CreatorToolsInteractionPresentation.BringActorToFront(
                projectile.gameObject);
        }

        private static void EndFirePostfix(ref FireScope __state)
        {
            EndFire(ref __state);
        }

        private static Exception EndFireFinalizer(
            Exception __exception, ref FireScope __state)
        {
            EndFire(ref __state);
            return __exception;
        }

        private static void EndFire(ref FireScope state)
        {
            if (!state.Entered)
                return;
            currentFire = state.Previous;
            state.Entered = false;
        }

        private struct FireContext
        {
            internal BasicProjectile ProjectilePrefab;
            internal BasicProjectile ParryablePrefab;
        }

        private struct FireScope
        {
            internal FireContext Previous;
            internal bool Entered;
        }

        private static void Warn(Action<string> logWarning, string message)
        {
            if (logWarning != null)
                logWarning(message);
        }
    }
}
