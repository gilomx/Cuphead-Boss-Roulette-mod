using System;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private static int rejectedHeartReviveReceiverId = -1;
        private static int rejectedHeartReviveFrame = -1;
        private static int rejectedHealerParrySoundFrame = -1;

        private void InstallHpOneChallengePatches()
        {
            var healthSetter = AccessTools.PropertySetter(
                typeof(PlayerStatsManager), "Health");
            var healthMaxSetter = AccessTools.PropertySetter(
                typeof(PlayerStatsManager), "HealthMax");
            var clampHealthPrefix = AccessTools.Method(
                typeof(Plugin), "ClampHpOneHealthPrefix");
            var forceHealthMaxPrefix = AccessTools.Method(
                typeof(Plugin), "ForceHpOneHealthMaxPrefix");
            if (healthSetter != null && healthMaxSetter != null &&
                clampHealthPrefix != null && forceHealthMaxPrefix != null)
            {
                harmony.Patch(healthSetter,
                    prefix: new HarmonyMethod(clampHealthPrefix));
                harmony.Patch(healthMaxSetter,
                    prefix: new HarmonyMethod(forceHealthMaxPrefix));
            }
            else
                Logger.LogWarning(
                    "Could not install the HP.1 health clamps.");

            var partnerCanSteal = AccessTools.PropertyGetter(
                typeof(PlayerStatsManager), "PartnerCanSteal");
            var allowPartnerJoinPostfix = AccessTools.Method(
                typeof(Plugin), "AllowHpOnePartnerJoinPostfix");
            var partnerStealHealth = AccessTools.Method(
                typeof(PlayerStatsManager), "OnPartnerStealHealth");
            var preservePartnerHealthPrefix = AccessTools.Method(
                typeof(Plugin), "PreserveHpOnePartnerHealthPrefix");
            if (partnerCanSteal != null && allowPartnerJoinPostfix != null &&
                partnerStealHealth != null &&
                preservePartnerHealthPrefix != null)
            {
                harmony.Patch(partnerCanSteal,
                    postfix: new HarmonyMethod(allowPartnerJoinPostfix));
                harmony.Patch(partnerStealHealth,
                    prefix: new HarmonyMethod(preservePartnerHealthPrefix));
            }
            else
                Logger.LogWarning(
                    "Could not install the HP.1 cooperative join guards.");

            var setChaliceShield = AccessTools.Method(
                typeof(PlayerStatsManager), "SetChaliceShield",
                new[] { typeof(bool) });
            var rejectChaliceShieldPrefix = AccessTools.Method(
                typeof(Plugin), "RejectHpOneChaliceShieldPrefix");
            var createChaliceHeart = AccessTools.Method(
                typeof(PlayerSuperChaliceShield), "CreateHeart");
            var decorateChaliceHeartPostfix = AccessTools.Method(
                typeof(Plugin), "DecorateRejectedChaliceHeartPostfix");
            var destroyChaliceHeart = AccessTools.Method(
                typeof(PlayerSuperChaliceShieldHeart), "Destroy");
            var keepRejectedHeartPrefix = AccessTools.Method(
                typeof(Plugin), "KeepRejectedChaliceHeartForGlitchPrefix");
            var damageReceiverRevive = AccessTools.Method(
                typeof(PlayerDamageReceiver), "OnRevive",
                new[] { typeof(Vector3) });
            var suppressRejectedHeartRevivePrefix = AccessTools.Method(
                typeof(Plugin), "SuppressRejectedHeartRevivePrefix");
            if (setChaliceShield != null &&
                rejectChaliceShieldPrefix != null &&
                createChaliceHeart != null &&
                decorateChaliceHeartPostfix != null &&
                destroyChaliceHeart != null &&
                keepRejectedHeartPrefix != null &&
                damageReceiverRevive != null &&
                suppressRejectedHeartRevivePrefix != null)
            {
                harmony.Patch(setChaliceShield,
                    prefix: new HarmonyMethod(rejectChaliceShieldPrefix));
                harmony.Patch(createChaliceHeart,
                    postfix: new HarmonyMethod(
                        decorateChaliceHeartPostfix));
                harmony.Patch(destroyChaliceHeart,
                    prefix: new HarmonyMethod(keepRejectedHeartPrefix));
                harmony.Patch(damageReceiverRevive,
                    prefix: new HarmonyMethod(
                        suppressRejectedHeartRevivePrefix));
            }
            else
                Logger.LogWarning(
                    "Could not install the HP.1 Chalice shield rejection effect.");

            var effectCreate = AccessTools.Method(
                typeof(Effect), "Create",
                new[] { typeof(Vector3), typeof(Vector3) });
            var decorateRejectedHealRootPostfix = AccessTools.Method(
                typeof(Plugin), "DecorateRejectedHealRootPostfix");
            var healerParticleAwake = AccessTools.Method(
                typeof(HealerCharmParticleEffect), "Awake");
            var decorateRejectedHealParticleAwakePostfix = AccessTools.Method(
                typeof(Plugin),
                "DecorateRejectedHealParticleAwakePostfix");
            var healerStartPlayerFlash = AccessTools.Method(
                typeof(HealerCharmSparkEffect), "StartPlayerFlash");
            var decorateRejectedPlayerFlashPostfix = AccessTools.Method(
                typeof(Plugin),
                "DecorateRejectedPlayerFlashPostfix");
            if (effectCreate != null &&
                decorateRejectedHealRootPostfix != null &&
                healerParticleAwake != null &&
                decorateRejectedHealParticleAwakePostfix != null &&
                healerStartPlayerFlash != null &&
                decorateRejectedPlayerFlashPostfix != null)
            {
                harmony.Patch(effectCreate,
                    postfix: new HarmonyMethod(
                        decorateRejectedHealRootPostfix));
                harmony.Patch(healerParticleAwake,
                    postfix: new HarmonyMethod(
                        decorateRejectedHealParticleAwakePostfix));
                harmony.Patch(healerStartPlayerFlash,
                    postfix: new HarmonyMethod(
                        decorateRejectedPlayerFlashPostfix));
            }
            else
                Logger.LogWarning(
                    "Could not install the HP.1 healer rejection effect.");

            var healerCharm = AccessTools.Method(
                typeof(PlayerStatsManager), "HealerCharm");
            var trackHealerCharmPrefix = AccessTools.Method(
                typeof(Plugin), "TrackHpOneHealerCharmPrefix");
            var trackHealerCharmPostfix = AccessTools.Method(
                typeof(Plugin), "TrackHpOneHealerCharmPostfix");
            var audioPlay = AccessTools.Method(
                typeof(AudioManager), "Play", new[] { typeof(string) });
            var replaceRejectedParrySoundPrefix = AccessTools.Method(
                typeof(Plugin), "ReplaceHpOneRejectedParrySoundPrefix");
            if (healerCharm != null &&
                trackHealerCharmPrefix != null &&
                trackHealerCharmPostfix != null &&
                audioPlay != null &&
                replaceRejectedParrySoundPrefix != null)
            {
                harmony.Patch(healerCharm,
                    prefix: new HarmonyMethod(trackHealerCharmPrefix),
                    postfix: new HarmonyMethod(trackHealerCharmPostfix));
                harmony.Patch(audioPlay,
                    prefix: new HarmonyMethod(
                        replaceRejectedParrySoundPrefix));
            }
            else
                Logger.LogWarning(
                    "Could not install the HP.1 rejected parry sound replacement.");
        }

        private static bool IsHpOneRuntimeActive()
        {
            var plugin = activeInstance;
            return plugin != null && plugin.ShouldApplyHpOneHealthLock();
        }

        private bool ShouldApplyHpOneHealthLock()
        {
            if (!ExperimentalFeatures.EnableHpOneChallenge ||
                activeChallenge != ModifierId.HpOne ||
                !activeChallengeTargetAssigned)
                return false;

            try
            {
                var level = Level.Current;
                if (level != null)
                    return level.LevelType == Level.Type.Battle &&
                           ActiveChallengeMatches(level);
            }
            catch
            {
                // Level.Current can be unavailable while LevelInit builds the
                // battle. The HUD session below is already tied to the result.
            }

            return activeChallengeFromManualEquipment ||
                   (battleHudPresentationActive && loanedLoadoutsActive);
        }

        private static void ClampHpOneHealthPrefix(ref int __0)
        {
            if (IsHpOneRuntimeActive() && __0 > 1)
                __0 = 1;
        }

        private static void ForceHpOneHealthMaxPrefix(ref int __0)
        {
            if (IsHpOneRuntimeActive())
                __0 = 1;
        }

        private static void AllowHpOnePartnerJoinPostfix(ref bool __result)
        {
            if (IsHpOneRuntimeActive())
                __result = true;
        }

        private static bool PreserveHpOnePartnerHealthPrefix()
        {
            return !IsHpOneRuntimeActive();
        }

        private static void RejectHpOneChaliceShieldPrefix(ref bool __0)
        {
            if (IsHpOneRuntimeActive())
                __0 = false;
        }

        private static void DecorateRejectedChaliceHeartPostfix(
            PlayerSuperChaliceShield __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                !plugin.ShouldApplyHpOneHealthLock())
                return;

            try
            {
                var heart = Traverse.Create(__instance)
                    .Field("shieldHeart").GetValue<GameObject>();
                var player = Traverse.Create(__instance)
                    .Field("player").GetValue<LevelPlayerController>();
                if (heart == null)
                    return;

                var effect = heart.GetComponent<HpOneRejectedHeartEffect>();
                if (effect == null)
                    effect = heart.AddComponent<HpOneRejectedHeartEffect>();
                effect.Initialize(plugin.hpOneRejectedHeartShader, player);

                if (player != null && player.damageReceiver != null)
                    player.damageReceiver.Vulnerable();
            }
            catch (Exception exception)
            {
                plugin.Logger.LogWarning(
                    "Could not decorate the rejected Chalice heart: " +
                    exception.Message);
            }
        }

        private static void DecorateRejectedHealRootPostfix(
            Effect __instance,
            ref Effect __result)
        {
            if (!(__instance is HealerCharmSparkEffect) || __result == null)
                return;

            var plugin = activeInstance;
            if (plugin == null ||
                !plugin.ShouldApplyHpOneHealthLock())
                return;

            plugin.DecorateRejectedHealObject(__result.gameObject, null,
                "healer root");
        }

        private static void DecorateRejectedHealParticleAwakePostfix(
            HealerCharmParticleEffect __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                !plugin.ShouldApplyHpOneHealthLock())
                return;

            plugin.DecorateRejectedHealObject(__instance.gameObject, null,
                "healer particle");
        }

        private static void DecorateRejectedPlayerFlashPostfix(
            HealerCharmSparkEffect __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                !plugin.ShouldApplyHpOneHealthLock())
                return;

            try
            {
                var startedFlash = Traverse.Create(__instance)
                    .Field("startedFlash").GetValue<int>();
                if (startedFlash >= 0)
                    return;

                var target = Traverse.Create(__instance)
                    .Field("target").GetValue<AbstractPlayerController>();
                if (target == null)
                    return;

                var effect = target.gameObject
                    .GetComponent<HpOneRejectedPlayerFlashEffect>();
                if (effect == null)
                    effect = target.gameObject
                        .AddComponent<HpOneRejectedPlayerFlashEffect>();
                effect.Initialize(target);
            }
            catch (Exception exception)
            {
                plugin.Logger.LogWarning(
                    "Could not grayscale rejected healer player flash: " +
                    exception.Message);
            }
        }
        private static void TrackHpOneHealerCharmPrefix(
            PlayerStatsManager __instance,
            out int __state)
        {
            __state = -1;
            if (__instance == null || !IsHpOneRuntimeActive())
                return;

            try
            {
                __state = Traverse.Create(__instance)
                    .Property("HealerHPReceived").GetValue<int>();
            }
            catch
            {
                __state = -1;
            }
        }

        private static void TrackHpOneHealerCharmPostfix(
            PlayerStatsManager __instance,
            int __state)
        {
            if (__instance == null || __state < 0 ||
                !IsHpOneRuntimeActive())
                return;

            try
            {
                var received = Traverse.Create(__instance)
                    .Property("HealerHPReceived").GetValue<int>();
                if (received > __state)
                    rejectedHealerParrySoundFrame = Time.frameCount;
            }
            catch
            {
                rejectedHealerParrySoundFrame = -1;
            }
        }

        private static bool ReplaceHpOneRejectedParrySoundPrefix(string key)
        {
            if (rejectedHealerParrySoundFrame != Time.frameCount ||
                string.IsNullOrEmpty(key))
                return true;

            var normalized = key.ToLowerInvariant();
            var isRegular = normalized == "player_parry_power_up";
            var isFull = normalized == "player_parry_power_up_full";
            if (!isRegular && !isFull)
                return true;

            rejectedHealerParrySoundFrame = -1;
            var plugin = activeInstance;
            if (plugin == null || plugin.hpOneRejectedParryClip == null)
                return true;

            plugin.PlayOneShot(plugin.hpOneRejectedParryClip, 1f);

            // Keep the native full-meter cue as an additional layer. For an
            // ordinary parry, replace only the randomized hit_01/hit_02 group.
            return isFull;
        }

        private void DecorateRejectedHealObject(
            GameObject target,
            LevelPlayerController player,
            string label)
        {
            if (target == null)
                return;

            try
            {
                var effect = target.GetComponent<HpOneRejectedHeartEffect>();
                if (effect == null)
                    effect = target.AddComponent<HpOneRejectedHeartEffect>();
                effect.Initialize(hpOneRejectedHeartShader, player,
                    label != "healer root");
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Could not decorate rejected " + label +
                    ": " + exception.Message);
            }
        }

        private static bool KeepRejectedChaliceHeartForGlitchPrefix(
            PlayerSuperChaliceShieldHeart __instance)
        {
            if (!IsHpOneRuntimeActive() || __instance == null)
                return true;

            var effect = __instance.GetComponent<HpOneRejectedHeartEffect>();
            if (effect == null)
                return true;

            var receiver = effect.Receiver;
            if (receiver != null)
            {
                rejectedHeartReviveReceiverId = receiver.GetInstanceID();
                rejectedHeartReviveFrame = Time.frameCount;
            }
            return false;
        }

        private static bool SuppressRejectedHeartRevivePrefix(
            PlayerDamageReceiver __instance)
        {
            if (__instance == null || rejectedHeartReviveFrame != Time.frameCount ||
                rejectedHeartReviveReceiverId != __instance.GetInstanceID())
                return true;

            rejectedHeartReviveReceiverId = -1;
            rejectedHeartReviveFrame = -1;
            return false;
        }
    }
}
