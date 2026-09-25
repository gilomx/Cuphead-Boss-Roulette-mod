using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<PlayerStatsManager, TimedHpOneHealthState>
            timedHpOnePlayers =
                new Dictionary<PlayerStatsManager, TimedHpOneHealthState>();
        private readonly Dictionary<PlayerStatsManager,
            TimedHpOneSuspendedHeartEffect> timedHpOneHearts =
                new Dictionary<PlayerStatsManager,
                    TimedHpOneSuspendedHeartEffect>();
        private bool timedHpOneRuntimeActive;
        private int timedHpOneLevelInstanceId = -1;

        private bool IsTimedHpOneHealthLockActive()
        {
            return timedHpOneRuntimeActive &&
                ExperimentalFeatures.EnableHpOneChallenge &&
                timedChallengeInteractions != null &&
                timedChallengeInteractions.IsActive(
                    CreatorToolsTimedChallenge.HpOne);
        }

        private void SyncTimedHpOneEffect(CreatorToolsTimedChallenge timer)
        {
            var active = timer != null &&
                timer.IsActive(CreatorToolsTimedChallenge.HpOne);
            if (!active)
            {
                if (timedHpOneRuntimeActive)
                    EndTimedHpOneRuntime();
                return;
            }

            if (!timedHpOneRuntimeActive)
                BeginTimedHpOneRuntime();
            UpdateTimedHpOnePlayers();
        }

        private void BeginTimedHpOneRuntime()
        {
            timedHpOnePlayers.Clear();
            timedHpOneHearts.Clear();
            timedHpOneLevelInstanceId = -1;
            try
            {
                var level = Level.Current;
                if (level != null)
                    timedHpOneLevelInstanceId = level.GetInstanceID();
            }
            catch
            {
            }
            timedHpOneRuntimeActive = true;
        }

        private TimedHpOneHealthState TimedHpOneState(
            PlayerStatsManager stats)
        {
            TimedHpOneHealthState state;
            if (!timedHpOnePlayers.TryGetValue(stats, out state))
            {
                state = new TimedHpOneHealthState();
                timedHpOnePlayers.Add(stats, state);
            }
            return state;
        }

        private void ObserveTimedHpOneHealthWrite(
            PlayerStatsManager stats, int value)
        {
            if (!timedHpOneRuntimeActive || stats == null)
                return;
            TimedHpOneState(stats).ObserveProposedHealth(value);
        }

        private void ObserveTimedHpOneHealthMaxWrite(
            PlayerStatsManager stats, int value)
        {
            if (!timedHpOneRuntimeActive || stats == null)
                return;
            TimedHpOneState(stats).ObserveProposedHealthMax(value);
        }

        private void SuspendTimedHpOneChaliceShield(
            PlayerStatsManager stats)
        {
            if (!timedHpOneRuntimeActive || stats == null)
                return;
            TimedHpOneState(stats).SuspendChaliceShield();
        }

        private bool HasTimedHpOneSuspendedChaliceShield(
            PlayerStatsManager stats)
        {
            TimedHpOneHealthState state;
            return stats != null &&
                timedHpOnePlayers.TryGetValue(stats, out state) &&
                state != null && state.PendingChaliceShield;
        }

        private void UpdateTimedHpOnePlayers()
        {
            foreach (var player in PlayerManager.GetAllPlayers())
            {
                if (player == null || player.stats == null ||
                    !player.gameObject.activeInHierarchy)
                    continue;

                var stats = player.stats;
                var state = TimedHpOneState(stats);
                if (player.IsDead || stats.Health <= 0)
                {
                    state.MarkDefeated();
                    DiscardTimedHpOneHeart(stats);
                    continue;
                }

                state.Capture(stats.Health, stats.HealthMax);
                if (stats.ChaliceShieldOn)
                {
                    state.SuspendChaliceShield();
                    stats.SetChaliceShield(false);
                }
                AttachExistingTimedHpOneHeart(player, stats);

                if (stats.Health != 1 || stats.HealthMax != 1)
                    stats.SetHealth(1);
            }
        }

        private void AttachExistingTimedHpOneHeart(
            AbstractPlayerController player, PlayerStatsManager stats)
        {
            TimedHpOneSuspendedHeartEffect current;
            if (timedHpOneHearts.TryGetValue(stats, out current) &&
                current != null)
                return;

            var groundPlayer = player as LevelPlayerController;
            if (groundPlayer == null)
                return;

            var hearts = UnityEngine.Object
                .FindObjectsOfType<PlayerSuperChaliceShieldHeart>();
            for (var i = 0; i < hearts.Length; i++)
            {
                var heart = hearts[i];
                if (heart == null)
                    continue;
                try
                {
                    var owner = Traverse.Create(heart)
                        .Field("player").GetValue<Transform>();
                    if (owner != groundPlayer.transform)
                        continue;
                    AttachTimedHpOneHeart(
                        stats, heart.gameObject, groundPlayer);
                    return;
                }
                catch
                {
                }
            }
        }

        private void AttachTimedHpOneHeart(
            PlayerStatsManager stats, GameObject heart,
            LevelPlayerController player)
        {
            if (!timedHpOneRuntimeActive || stats == null || heart == null)
                return;

            var state = TimedHpOneState(stats);
            state.SuspendChaliceShield();

            TimedHpOneSuspendedHeartEffect current;
            if (timedHpOneHearts.TryGetValue(stats, out current) &&
                current != null && current.gameObject != heart)
            {
                // SetChaliceShield(true) is forced back to false during the
                // challenge, so another Super II would otherwise believe no
                // shield exists and leave a second heart behind. Preserve the
                // first suspended heart as the one native shield and discard
                // only the newly-created duplicate.
                heart.SetActive(false);
                UnityEngine.Object.Destroy(heart);
                stats.SetChaliceShield(false);
                if (player != null && player.damageReceiver != null)
                    player.damageReceiver.Vulnerable();
                return;
            }

            var effect = heart.GetComponent<TimedHpOneSuspendedHeartEffect>();
            if (effect == null)
                effect = heart.AddComponent<TimedHpOneSuspendedHeartEffect>();
            effect.Initialize(hpOneRejectedHeartShader, player);
            timedHpOneHearts[stats] = effect;
            stats.SetChaliceShield(false);
            if (player != null && player.damageReceiver != null)
                player.damageReceiver.Vulnerable();
        }

        private void MarkTimedHpOneDefeated(PlayerStatsManager stats)
        {
            if (!timedHpOneRuntimeActive || stats == null)
                return;
            var state = TimedHpOneState(stats);
            // A late cooperative player can write its default zero HP before
            // LevelInit supplies the real value. Only an already-captured
            // player can have died through a setter write.
            if (!state.Captured)
                return;
            state.MarkDefeated();
            DiscardTimedHpOneHeart(stats);
        }

        private void DiscardTimedHpOneHeart(PlayerStatsManager stats)
        {
            TimedHpOneSuspendedHeartEffect effect;
            if (!timedHpOneHearts.TryGetValue(stats, out effect))
                return;
            timedHpOneHearts.Remove(stats);
            if (effect != null)
            {
                var target = effect.gameObject;
                target.SetActive(false);
                UnityEngine.Object.Destroy(target);
            }
            if (stats != null)
                stats.SetChaliceShield(false);
        }

        private void EndTimedHpOneRuntime()
        {
            timedHpOneRuntimeActive = false;
            var sameLevel = false;
            try
            {
                var level = Level.Current;
                sameLevel = !SceneLoader.CurrentlyLoading && level != null &&
                    level.GetInstanceID() == timedHpOneLevelInstanceId &&
                    !Level.Won;
            }
            catch
            {
                sameLevel = false;
            }

            foreach (var entry in timedHpOnePlayers)
            {
                var stats = entry.Key;
                var state = entry.Value;
                if (stats == null || state == null || !state.Captured)
                    continue;

                var player = stats.basePlayer;
                var alive = sameLevel && player != null &&
                    !player.IsDead && player.gameObject.activeInHierarchy &&
                    stats.Health > 0;

                if (sameLevel)
                {
                    // Restoring max HP is safe even for a ghost. Current HP is
                    // refunded only when this player never died.
                    RestoreTimedHpOneHealthMax(
                        stats, state.OriginalHealthMax);
                    if (state.ShouldRefundCurrentHealth(alive))
                        stats.SetHealth(
                            state.RestoredCurrentHealth(stats.Health));
                    else if (alive)
                        stats.SetHealth(stats.Health);
                    RestoreTimedHpOneHealthMax(
                        stats, state.OriginalHealthMax);
                }

                TimedHpOneSuspendedHeartEffect effect;
                timedHpOneHearts.TryGetValue(stats, out effect);
                if (sameLevel &&
                    state.ShouldRestoreChaliceShield(alive) &&
                    effect != null)
                {
                    effect.Restore();
                    stats.SetChaliceShield(true);
                }
                else if (effect != null)
                {
                    var target = effect.gameObject;
                    target.SetActive(false);
                    UnityEngine.Object.Destroy(target);
                }
            }

            timedHpOnePlayers.Clear();
            timedHpOneHearts.Clear();
            timedHpOneLevelInstanceId = -1;
        }

        private static void RestoreTimedHpOneHealthMax(
            PlayerStatsManager stats, int value)
        {
            // The game exposes HealthMax publicly but keeps its setter private.
            // Traverse invokes that native setter so its normal side effects
            // and every installed Harmony observer remain intact.
            Traverse.Create(stats).Property("HealthMax").SetValue(value);
        }
    }
}
