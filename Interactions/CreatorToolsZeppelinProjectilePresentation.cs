using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsZeppelinProjectilePresentation
    {
        internal static void InstallPatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;

            var prefix = AccessTools.Method(
                typeof(CreatorToolsZeppelinProjectilePresentation),
                "CaptureExistingProjectilesPrefix");
            var postfix = AccessTools.Method(
                typeof(CreatorToolsZeppelinProjectilePresentation),
                "PrepareNewProjectilesPostfix");
            var fireMethods = new[]
            {
                AccessTools.Method(
                    typeof(FlyingBlimpLevelEnemy), "FireSingle"),
                AccessTools.Method(
                    typeof(FlyingBlimpLevelEnemy), "FireSpreadshot")
            };

            for (var i = 0; i < fireMethods.Length; i++)
            {
                if (fireMethods[i] == null || prefix == null ||
                    postfix == null)
                {
                    Warn(logWarning,
                        "Could not install the catalog zeppelin projectile " +
                        "presentation patch.");
                    continue;
                }
                harmony.Patch(
                    fireMethods[i],
                    prefix: new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix));
            }
        }

        private static void CaptureExistingProjectilesPrefix(
            FlyingBlimpLevelEnemy __instance,
            out HashSet<int> __state)
        {
            __state = null;
            if (__instance == null ||
                __instance.GetComponent<
                    CreatorToolsInteractionRenderPriority>() == null)
                return;

            __state = new HashSet<int>();
            var projectiles = Resources.FindObjectsOfTypeAll<
                FlyingBlimpLevelEnemyProjectile>();
            for (var i = 0; i < projectiles.Length; i++)
                if (projectiles[i] != null)
                    __state.Add(projectiles[i].GetInstanceID());
        }

        private static void PrepareNewProjectilesPostfix(HashSet<int> __state)
        {
            if (__state == null)
                return;

            var projectiles = Resources.FindObjectsOfTypeAll<
                FlyingBlimpLevelEnemyProjectile>();
            for (var i = 0; i < projectiles.Length; i++)
            {
                var projectile = projectiles[i];
                if (projectile == null ||
                    __state.Contains(projectile.GetInstanceID()) ||
                    !projectile.gameObject.activeInHierarchy)
                    continue;
                CreatorToolsInteractionPresentation.BringActorToFront(
                    projectile.gameObject);
            }
        }

        private static void Warn(Action<string> logWarning, string message)
        {
            if (logWarning != null)
                logWarning(message);
        }
    }
}
