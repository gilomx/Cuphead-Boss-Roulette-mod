using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private void InstallCreatorToolsTapFarmingDamagePatches()
        {
            CreatorToolsTapFarmingDamagePatch.RobotLocalDamageEnabled = false;
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
            var progressionPrefix = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "NativeProgressionPrefix");
            var progressionPostfix = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "NativeProgressionPostfix");
            var progressionFinalizer = AccessTools.Method(
                typeof(CreatorToolsTapFarmingDamagePatch),
                "NativeProgressionFinalizer");
            if (bossPrefix == null || bossPostfix == null ||
                bossFinalizer == null || progressionPrefix == null ||
                progressionPostfix == null || progressionFinalizer == null)
            {
                Logger.LogWarning(
                    "Could not install the Tap Farming boss-damage bridge.");
                return;
            }

            var patched = new HashSet<MethodBase>();
            var failures = 0;
            var robotBridgeReady = false;
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
                    var advance = baseType.GetMethod(
                        "DealDamageToNextNamedState",
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null, Type.EmptyTypes, null);
                    if (advance == null)
                        throw new MissingMethodException(
                            "Native boss phase progression was not found.");
                    // Install this guard first. If native progression cannot
                    // be protected, leave this boss's damage untouched rather
                    // than risk absorbing an irreversible phase transition.
                    harmony.Patch(advance,
                        prefix: new HarmonyMethod(progressionPrefix),
                        postfix: new HarmonyMethod(progressionPostfix),
                        finalizer: new HarmonyMethod(progressionFinalizer));
                    harmony.Patch(dealDamage,
                        prefix: new HarmonyMethod(bossPrefix),
                        postfix: new HarmonyMethod(bossPostfix),
                        finalizer: new HarmonyMethod(bossFinalizer));
                    if (propertyTypes[i] == typeof(LevelProperties.Robot))
                        robotBridgeReady = true;
                }
                catch (Exception exception)
                {
                    failures++;
                    Logger.LogWarning(
                        "Could not attach Tap Farming to " +
                        baseType.FullName + ": " + exception.Message);
                }
            }

            if (robotBridgeReady)
            {
                try
                {
                    var transpiler = AccessTools.Method(
                        typeof(CreatorToolsTapFarmingDamagePatch), "RobotLocalDamageTranspiler");
                    var signature = new[] { typeof(DamageDealer.DamageInfo) };
                    var body = AccessTools.Method(typeof(RobotLevelRobotBodyPart), "OnDamageTaken", signature);
                    var chest = AccessTools.Method(typeof(RobotLevelRobotChest), "OnDamageTaken", signature);
                    if (transpiler == null || body == null || chest == null)
                        throw new MissingMethodException("Robot local health callbacks were not found.");
                    harmony.Patch(body, transpiler: new HarmonyMethod(transpiler));
                    harmony.Patch(chest, transpiler: new HarmonyMethod(transpiler));
                    // A partial installation stays inert, preserving native
                    // local HP if either callback no longer matches.
                    CreatorToolsTapFarmingDamagePatch.RobotLocalDamageEnabled = true;
                }
                catch (Exception exception)
                {
                    Logger.LogWarning("Could not attach Tap Farming to Robot local health: " + exception.Message);
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
            internal static bool RobotLocalDamageEnabled;
            [ThreadStatic]
            private static int playerDamageDepth;
            [ThreadStatic]
            private static int bossDamageDepth;
            [ThreadStatic]
            private static int nativeProgressionDepth;

            private static IEnumerable<CodeInstruction> RobotLocalDamageTranspiler(
                IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            {
                var body = __originalMethod.GetMethodBody();
                var parameters = __originalMethod.GetParameters();
                var owner = __originalMethod.DeclaringType;
                if ((owner != typeof(RobotLevelRobotBodyPart) && owner != typeof(RobotLevelRobotChest)) ||
                    __originalMethod.Name != "OnDamageTaken" || parameters.Length != 1 ||
                    parameters[0].ParameterType != typeof(DamageDealer.DamageInfo) || body == null ||
                    body.LocalVariables.Count == 0 || body.LocalVariables[0].LocalType != typeof(float) ||
                    body.ExceptionHandlingClauses.Count != 0)
                    throw new InvalidOperationException("Robot local-health callback shape changed.");
                var getter = typeof(DamageDealer.DamageInfo).GetProperty("damage").GetGetMethod();
                var properties = AccessTools.Field(typeof(RobotLevelRobotBodyPart), "properties");
                var helper = AccessTools.Method(typeof(CreatorToolsTapFarmingDamagePatch), "ReadRobotLocalDamage");
                if (properties == null || properties.FieldType != typeof(LevelProperties.Robot) || helper == null)
                    throw new InvalidOperationException("Robot local-health metadata changed.");
                var source = new List<CodeInstruction>(instructions);
                var replacement = -1;
                for (var i = 0; i < source.Count; i++)
                {
                    var called = source[i].operand as MethodInfo;
                    if (called != null && called.Name == "DealDamage" && called.DeclaringType.IsGenericType &&
                        called.DeclaringType.GetGenericTypeDefinition().Name == "AbstractLevelProperties`3")
                        throw new InvalidOperationException("Robot local damage now also forwards generic boss damage.");
                    if ((source[i].opcode != OpCodes.Call && source[i].opcode != OpCodes.Callvirt) ||
                        !Equals(source[i].operand, getter)) continue;
                    if (replacement >= 0 || i == 0 || i + 2 >= source.Count ||
                        source[i - 1].opcode != OpCodes.Ldarg_1 || source[i + 1].opcode != OpCodes.Sub ||
                        source[i + 2].opcode != OpCodes.Stind_R4 || source[i].blocks.Count != 0)
                        throw new InvalidOperationException("Robot local-health subtraction changed.");
                    replacement = i;
                }
                if (replacement < 0)
                    throw new InvalidOperationException("Robot local-health damage read was not found.");
                var load = new CodeInstruction(OpCodes.Ldarg_0);
                load.labels.AddRange(source[replacement].labels);
                source[replacement] = load;
                source.Insert(replacement + 1, new CodeInstruction(OpCodes.Ldfld, properties));
                source.Insert(replacement + 2, new CodeInstruction(OpCodes.Ldloc_0));
                source.Insert(replacement + 3, new CodeInstruction(OpCodes.Call, helper));
                return source;
            }

            private static float ReadRobotLocalDamage(DamageDealer.DamageInfo info,
                LevelProperties.Robot properties, float previousLocalHealth)
            {
                var damage = info.damage;
                if (!RobotLocalDamageEnabled || !(previousLocalHealth > 0f) || !(damage > 0f) ||
                    properties == null || playerDamageDepth <= 0 || bossDamageDepth > 0 ||
                    nativeProgressionDepth > 0)
                    return damage;
                var plugin = activeInstance;
                if (plugin == null || plugin.creatorToolsInteractions == null) return damage;
                // Same reserve path as generic health. Exactly one native
                // getter runs: body primary OR chest heart, never both.
                bossDamageDepth++;
                try
                {
                    plugin.creatorToolsInteractions.PrepareTapFarmingBossDamage(properties, ref damage);
                    return damage;
                }
                finally { bossDamageDepth--; }
            }

            private static void NativeProgressionPrefix(out bool __state)
            {
                // Destroying a Robot component (and several other native
                // mechanics) advances the boss with synthetic DealDamage(1)
                // calls. These are state-machine work, not player hits. The
                // game's bounded loop must finish without spending tap HP.
                nativeProgressionDepth++;
                __state = true;
            }

            private static void NativeProgressionPostfix(
                object __instance, ref bool __state)
            {
                if (!__state)
                    return;
                var plugin = activeInstance;
                if (plugin != null &&
                    plugin.creatorToolsInteractions != null)
                    plugin.creatorToolsInteractions
                        .ObserveTapFarmingBossDamage(__instance);
                EndNativeProgression(ref __state);
            }

            private static Exception NativeProgressionFinalizer(
                Exception __exception, ref bool __state)
            {
                EndNativeProgression(ref __state);
                return __exception;
            }

            private static void EndNativeProgression(ref bool state)
            {
                if (!state)
                    return;
                state = false;
                if (nativeProgressionDepth > 0)
                    nativeProgressionDepth--;
            }

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
                if (playerDamageDepth <= 0 || bossDamageDepth > 0 ||
                    nativeProgressionDepth > 0)
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
