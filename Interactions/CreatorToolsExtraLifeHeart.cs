using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsExtraLifeHeart : MonoBehaviour
    {
        private const float EntranceDelay = 0.12f;
        private const float EntranceDuration = 0.46f;
        private const float FallbackHoverWidth = 100f;
        private const float PlaneFallbackHoverWidth = 60f;
        private const float FallbackPopDuration = 0.2f;

        private AbstractPlayerController player;
        private PlayerSuperChaliceShieldHeart nativeHeart;
        private CreatorToolsDonorLabel donorLabel;
        private SpriteRenderer[] entranceRenderers;
        private Color[] entranceColors;
        private float entranceElapsed;
        private float entranceScale;
        private float presentationScale = 1f;
        private float fallbackHoverTime;
        private float fallbackLerpSpeed;
        private float fallbackPopElapsed;
        private float fallbackPopScale = 1f;
        private bool fallbackVisual;
        private bool entering;
        private bool popping;

        internal int CreditId { get; private set; }

        internal void Initialize(
            int creditId,
            string donor,
            string giftImagePath,
            AbstractPlayerController owner)
        {
            CreditId = creditId;
            player = owner;
            nativeHeart = GetComponent<PlayerSuperChaliceShieldHeart>();
            fallbackVisual = nativeHeart == null;

            if (nativeHeart != null && player != null)
            {
                var native = Traverse.Create(nativeHeart);
                native.Field("player").SetValue(player.transform);
                // Keep the exact native orbit and follow behavior, but start
                // at another point on that same path. This keeps the redeemed
                // heart visible when Ms. Chalice also owns her Super II heart.
                native.Field("hoverTime").SetValue(0f);
            }
            else
            {
                fallbackHoverTime = 0f;
                fallbackLerpSpeed = 0f;
            }

            UpdatePresentationScale();

            var anchor = FindAnchorRenderer();
            donorLabel = gameObject.AddComponent<CreatorToolsDonorLabel>();
            donorLabel.Initialize(donor, anchor);
            donorLabel.SetGiftImage(giftImagePath);
            // The prefab begins with a larger arrival frame. Re-measure while
            // it settles so the label is anchored to the heart, not that first
            // frame. The calibrated offset remains in presentation units, so
            // the follower scales it with the live camera zoom.
            donorLabel.RebindTo(gameObject, anchor, 1.5f);
            donorLabel.SetVerticalOffsetPixels(-125f);
            donorLabel.Hide();
            BeginEntrance();
        }

        internal void SetSuspended(bool suspended, Shader shader)
        {
            var effect = GetComponent<TimedHpOneSuspendedHeartEffect>();
            if (suspended)
            {
                if (effect == null)
                    effect = gameObject.AddComponent<
                        TimedHpOneSuspendedHeartEffect>();
                effect.Initialize(shader, player);
            }
            else if (effect != null)
                effect.Restore();
        }

        internal void Pop()
        {
            if (popping)
                return;
            popping = true;
            FinishEntrance(false);
            if (nativeHeart != null)
                nativeHeart.Destroy();
            else
            {
                fallbackPopElapsed = 0f;
                fallbackPopScale = 1f;
                try { AudioManager.Play("player_super_chalice_shield_end"); }
                catch { }
            }
        }

        private void Update()
        {
            if (popping)
            {
                if (fallbackVisual)
                    UpdateFallbackPop();
                return;
            }
            if (player == null)
            {
                Destroy(gameObject);
                return;
            }
            if (!player.gameObject.activeInHierarchy)
                return;
            UpdateEntrance();
        }

        private void FixedUpdate()
        {
            if (!fallbackVisual || popping || player == null)
                return;

            // PlayerSuperChaliceShieldHeart.FixedUpdate expressed directly so
            // aircraft levels can use the exact native orbit even though they
            // do not load the terrestrial Super II prefab.
            var sine = Mathf.Sin(fallbackHoverTime);
            var cosine = Mathf.Cos(fallbackHoverTime);
            var denominator = 1f + sine * sine;
            var hoverWidth = player is PlanePlayerController
                ? PlaneFallbackHoverWidth
                : FallbackHoverWidth;
            var offset = new Vector3(
                hoverWidth * cosine / denominator,
                hoverWidth * sine * cosine / denominator,
                0f);
            fallbackHoverTime += CupheadTime.FixedDelta * 2f;
            fallbackLerpSpeed = Mathf.Min(
                fallbackLerpSpeed + CupheadTime.FixedDelta, 3f);
            transform.position = Vector3.Lerp(
                transform.position,
                player.transform.position + offset,
                CupheadTime.FixedDelta * fallbackLerpSpeed);
        }

        private void BeginEntrance()
        {
            entranceRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            entranceColors = new Color[entranceRenderers.Length];
            for (var i = 0; i < entranceRenderers.Length; i++)
            {
                var renderer = entranceRenderers[i];
                if (renderer == null)
                    continue;
                entranceColors[i] = renderer.color;
                var hidden = renderer.color;
                hidden.a = 0f;
                renderer.color = hidden;
            }
            entranceElapsed = 0f;
            entranceScale = 0f;
            entering = true;
            ApplyPresentationScale(entranceScale);
        }

        private void UpdateEntrance()
        {
            if (!entering)
                return;
            entranceElapsed += Time.deltaTime;
            var linearProgress = Mathf.Clamp01(
                (entranceElapsed - EntranceDelay) / EntranceDuration);
            // SmoothStep gives the same soft handoff perceived in the native
            // Super II while the native heart component flies into its orbit.
            var opacityProgress = linearProgress * linearProgress *
                (3f - 2f * linearProgress);
            ApplyEntranceOpacity(opacityProgress);
            entranceScale = EaseOutBack(linearProgress);
            if (linearProgress >= 1f)
                FinishEntrance(true);
        }

        private void LateUpdate()
        {
            // The native heart copies an unscaled value from the player on
            // every fixed frame. Reapply the same per-level body compensation
            // used by the rest of the interaction catalog afterwards. Read
            // the current camera every frame so a live zoom moves the heart
            // and its independently scaled donor label together.
            UpdatePresentationScale();
            ApplyPresentationScale(
                popping && fallbackVisual
                    ? fallbackPopScale
                    : entering ? entranceScale : 1f);
        }

        private void UpdateFallbackPop()
        {
            fallbackPopElapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(
                fallbackPopElapsed / FallbackPopDuration);
            fallbackPopScale = 1f - progress;
            ApplyEntranceOpacity(1f - progress);
            if (progress >= 1f)
                Destroy(gameObject);
        }

        private void UpdatePresentationScale()
        {
            var current = CreatorToolsInteractionPresentation
                .GetGameplayBodyScale(
                    CreatorToolsInteractionPresentation
                        .GetGameplayCameraScale());
            if (Mathf.Approximately(current, presentationScale) &&
                GetComponent<CreatorToolsInteractionCameraScale>() != null)
                return;
            presentationScale = current;
            CreatorToolsInteractionPresentation
                .MarkInheritedGameplayCameraScale(
                    gameObject, presentationScale);
            if (donorLabel != null)
                donorLabel.RefreshAnchor();
        }

        private void ApplyEntranceOpacity(float opacity)
        {
            for (var i = 0; entranceRenderers != null &&
                 i < entranceRenderers.Length; i++)
            {
                var renderer = entranceRenderers[i];
                if (renderer == null)
                    continue;
                var color = entranceColors[i];
                color.a *= opacity;
                renderer.color = color;
            }
        }

        private void FinishEntrance(bool revealDonor)
        {
            if (!entering)
                return;
            entering = false;
            ApplyEntranceOpacity(1f);
            ApplyPresentationScale(1f);
            if (revealDonor && donorLabel != null)
                donorLabel.FadeInWhenActorVisible(0.22f);
        }

        private void ApplyPresentationScale(float entranceMultiplier)
        {
            if (player == null)
                return;
            // PlayerSuperChaliceShieldHeart normally copies the owner's X
            // direction every fixed frame. Preserve that rule and animate
            // only the temporary reveal multiplier.
            var ownerScale = player.transform.localScale;
            var scale = presentationScale * entranceMultiplier;
            transform.localScale = new Vector3(
                ownerScale.x * scale, scale, 1f);
        }

        private static float EaseOutBack(float progress)
        {
            // A gentler back ease than the usual UI curve: roughly a five
            // percent overshoot, enough to read without looking elastic.
            const float overshoot = 0.9f;
            var shifted = Mathf.Clamp01(progress) - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                overshoot * shifted * shifted;
        }

        private SpriteRenderer FindAnchorRenderer()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    return renderers[i];
            return null;
        }
    }
}
