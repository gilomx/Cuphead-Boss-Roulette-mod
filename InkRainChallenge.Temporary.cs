using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed partial class InkRainChallengeRuntime : ITimedInkRainRuntime
    {
        private bool temporarySession;
        private bool temporaryDraining;
        private bool temporaryDefeat;
        private float temporaryTime;
        private SpriteRenderer temporaryNativeInkRenderer;
        private float EffectTime { get { return temporarySession ? temporaryTime : Time.time; } }
        private bool CanInkPlayers
        {
            get { return !temporaryDefeat && !squidIntroActive && EffectTime >= inkEffectsEnabledAt; }
        }

        internal bool PrepareTemporary()
        {
            if (SceneLoader.CurrentlyLoading || !EnsureInkAssets()) return false;
            gameplayCamera = FindGameplayCamera();
            return gameplayCamera != null;
        }

        public bool HasTemporaryVisuals
        {
            get
            {
                return drops.Count > 0 || groundImpacts.Count > 0 || splats.Count > 0 ||
                    squidIntroActive || inkAlpha > 0.001f || targetInkAlpha > 0.001f ||
                    (!temporaryDefeat && temporaryNativeInkRenderer != null && temporaryNativeInkRenderer.enabled &&
                     temporaryNativeInkRenderer.gameObject.activeInHierarchy);
            }
        }

        public void StartTemporary()
        {
            var level = Level.Current;
            StartAttempt(level == null ? Level.Mode.Normal : level.mode, false,
                level != null && level.CurrentLevel == Levels.Pirate);
            temporarySession = true;
            temporaryTime = Time.time;
            // The warning already announces this effect. Start ordinary rain
            // on its first active tick, without the equipped challenge's intro.
            nextSpawnAt = EffectTime;
            inkEffectsEnabledAt = EffectTime;
        }

        public void StopTemporaryEmission()
        {
            // No Clear/Reset here: airborne drops can still hit the ground or
            // a player. The existing ink hold/fade and squid exit run to end.
            temporaryDraining = true;
            nextSpawnAt = float.PositiveInfinity;
            StopSquidAttackAudio();
        }

        public void AdvanceTemporary(float seconds)
        {
            if (!temporarySession || seconds <= 0f) return;
            temporaryTime += seconds;
            TickRain(seconds);
        }

        public void BeginTemporaryDefeat()
        {
            if (!temporarySession) return;
            temporaryDefeat = true;
            StopTemporaryEmission();
        }

        public void CancelTemporary()
        {
            if (temporarySession) EndImmediately();
        }
    }
}
