using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private void InstallCreatorToolsTapFarmingDamagePatches()
        {
            var playerDamage = AccessTools.Method(
                typeof(DamageDealer), "DealDamage",
                new[] { typeof(GameObject) });
            var playerPrefix = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "PlayerDamagePrefix");
            var playerPostfix = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "PlayerDamagePostfix");
            var playerFinalizer = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "PlayerDamageFinalizer");
            if (playerDamage == null || playerPrefix == null ||
                playerPostfix == null || playerFinalizer == null)
            {
                Logger.LogWarning(
                    "Could not install the Tap Farming player-damage " +
                    "context.");
                return;
            }

            harmony.Patch(playerDamage,
                prefix: new HarmonyMethod(playerPrefix),
                postfix: new HarmonyMethod(playerPostfix),
                finalizer: new HarmonyMethod(playerFinalizer));

            var bossPrefix = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "BossDamagePrefix");
            var bossPostfix = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "BossDamagePostfix");
            var bossFinalizer = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "BossDamageFinalizer");
            if (bossPrefix == null || bossPostfix == null ||
                bossFinalizer == null)
            {
                Logger.LogWarning(
                    "Could not install the Tap Farming boss-damage bridge.");
                return;
            }

            var patched = new HashSet<MethodBase>();
            var failures = 0;
            var propertyTypes = typeof(LevelProperties).GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < propertyTypes.Length; i++)
            {
                var baseType = propertyTypes[i].BaseType;
                if (baseType == null || !baseType.IsGenericType ||
                    baseType.GetGenericTypeDefinition().Name !=
                        "AbstractLevelProperties`3")
                    continue;
                var dealDamage = baseType.GetMethod(
                    "DealDamage",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(float) }, null);
                if (dealDamage == null || !patched.Add(dealDamage))
                    continue;
                try
                {
                    harmony.Patch(dealDamage,
                        prefix: new HarmonyMethod(bossPrefix),
                        postfix: new HarmonyMethod(bossPostfix),
                        finalizer: new HarmonyMethod(bossFinalizer));
                }
                catch (Exception exception)
                {
                    failures++;
                    Logger.LogWarning(
                        "Could not attach Tap Farming to " +
                        baseType.FullName + ": " + exception.Message);
                }
            }

            if (patched.Count == 0)
                Logger.LogWarning(
                    "Tap Farming found no native boss health methods.");
            else
                Logger.LogInfo(
                    "Tap Farming damage bridge installed for " +
                    (patched.Count - failures) + " boss property types" +
                    (failures == 0 ? "." : " (" + failures +
                        " could not be patched)."));
        }

        private static class CreatorToolsTapFarmingDamagePatch
        {
            [ThreadStatic]
            private static int playerDamageDepth;
            [ThreadStatic]
            private static int bossDamageDepth;

            private static void PlayerDamagePrefix(
                GameObject hit,
                PlayerId ___playerId,
                out bool __state)
            {
                __state = false;
                var plugin = activeInstance;
                if (plugin == null ||
                    plugin.creatorToolsInteractions == null ||
                    (int)___playerId == int.MaxValue ||
                    !IsPlayerOffensiveDamageTarget(hit))
                    return;
                playerDamageDepth++;
                __state = true;
            }

            private static void PlayerDamagePostfix(ref bool __state)
            {
                EndPlayerDamage(ref __state);
            }

            private static Exception PlayerDamageFinalizer(
                Exception __exception, ref bool __state)
            {
                EndPlayerDamage(ref __state);
                return __exception;
            }

            private static void EndPlayerDamage(ref bool state)
            {
                if (!state)
                    return;
                state = false;
                if (playerDamageDepth > 0)
                    playerDamageDepth--;
            }

            private static bool BossDamagePrefix(
                object __instance,
                ref float damage,
                out bool __state)
            {
                __state = false;
                if (playerDamageDepth <= 0 || bossDamageDepth > 0)
                    return true;
                var plugin = activeInstance;
                if (plugin == null ||
                    plugin.creatorToolsInteractions == null)
                    return true;

                bossDamageDepth++;
                __state = true;
                return plugin.creatorToolsInteractions
                    .PrepareTapFarmingBossDamage(
                        __instance, ref damage);
            }

            private static void BossDamagePostfix(
                object __instance, ref bool __state)
            {
                if (!__state)
                    return;
                var plugin = activeInstance;
                if (plugin != null &&
                    plugin.creatorToolsInteractions != null)
                    plugin.creatorToolsInteractions
                        .ObserveTapFarmingBossDamage(__instance);
                EndBossDamage(ref __state);
            }

            private static Exception BossDamageFinalizer(
                Exception __exception, ref bool __state)
            {
                EndBossDamage(ref __state);
                return __exception;
            }

            private static void EndBossDamage(ref bool state)
            {
                if (!state)
                    return;
                state = false;
                if (bossDamageDepth > 0)
                    bossDamageDepth--;
            }
        }
    }
}
