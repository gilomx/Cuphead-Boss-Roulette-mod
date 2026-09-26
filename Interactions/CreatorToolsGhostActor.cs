using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsGhostActor : MonoBehaviour
    {
        internal const float LifetimeSeconds = 10f;

        private const float SideSeparation = 140f;
        private const float HoverWidth = 8f;
        private const float HoverHeight = 5f;
        private const float HoverSpeed = 2.4f;
        private const float FollowSmoothTime = 0.12f;
        private const float SnapDistance = 160f;
        private const float EntranceDuration = 0.22f;
        private const float ExitDuration = 0.24f;
        private const float MaximumOpacity = 0.52f;

        private AbstractPlayerController owner;
        private SpriteRenderer sourceRenderer;
        private SpriteRenderer ghostRenderer;
        private CreatorToolsDonorLabel donorLabel;
        private Func<bool> canAdvance;
        private Vector3 followVelocity;
        private float elapsed;
        private float hoverTime;
        private float gameplayDelta;
        private float displayedSide;
        private bool groundPresentation;
        private bool exitStarted;
        private Vector3 exitStartOffset;
        private bool positioned;

        internal bool EffectActive
        {
            get
            {
                return owner != null && ghostRenderer != null &&
                    elapsed < LifetimeSeconds && !owner.IsDead &&
                    owner.gameObject.activeInHierarchy;
            }
        }

        internal void Initialize(
            AbstractPlayerController player,
            string donor,
            string giftImagePath,
            Func<bool> canAdvance)
        {
            owner = player;
            this.canAdvance = canAdvance;
            sourceRenderer = FindPlayerRenderer(owner);
            if (sourceRenderer == null)
                throw new InvalidOperationException(
                    "Player 1 has no animated renderer for Ghost Help.");
            groundPresentation = owner is LevelPlayerController;
            displayedSide = ResolveFacing();

            gameObject.layer = sourceRenderer.gameObject.layer;
            ghostRenderer = gameObject.AddComponent<SpriteRenderer>();
            CopyFrame(0f);
            PositionImmediately();

            donorLabel = gameObject.AddComponent<CreatorToolsDonorLabel>();
            donorLabel.Initialize(donor, ghostRenderer);
            donorLabel.SetGiftImage(giftImagePath);
            donorLabel.FollowAnimatedBody(
                ghostRenderer, null, false, null);
            donorLabel.Hide();
            donorLabel.FadeInWhenActorVisible(EntranceDuration);
        }

        internal bool TryGetProjectileOffset(out Vector3 offset)
        {
            offset = Vector3.zero;
            if (!EffectActive)
                return false;
            if (sourceRenderer == null)
                sourceRenderer = FindPlayerRenderer(owner);
            if (sourceRenderer == null)
                return false;
            offset = transform.position - sourceRenderer.transform.position;
            return true;
        }

        private void Update()
        {
            gameplayDelta = 0f;
            if (owner == null || owner.IsDead)
            {
                Destroy(gameObject);
                return;
            }
            if (!owner.gameObject.activeInHierarchy)
                return;
            if (!Evaluate(canAdvance))
                return;

            gameplayDelta = Time.unscaledDeltaTime *
                Mathf.Max(0f, CupheadTime.GlobalSpeed);
            elapsed += gameplayDelta;
            hoverTime += gameplayDelta * HoverSpeed;
            if (!exitStarted &&
                LifetimeSeconds - elapsed <= ExitDuration)
                BeginExit();
            if (elapsed >= LifetimeSeconds)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            if (owner == null || ghostRenderer == null)
                return;
            if (sourceRenderer == null)
                sourceRenderer = FindPlayerRenderer(owner);
            if (sourceRenderer == null)
            {
                ghostRenderer.enabled = false;
                return;
            }

            UpdateDisplayedSide();
            var target = ResolveTargetPosition();
            if (groundPresentation || exitStarted || !positioned ||
                Vector3.Distance(transform.position, target) > SnapDistance)
            {
                transform.position = target;
                followVelocity = Vector3.zero;
                positioned = true;
            }
            else if (gameplayDelta > 0f)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    target,
                    ref followVelocity,
                    FollowSmoothTime,
                    Mathf.Infinity,
                    gameplayDelta);
            }

            transform.rotation = sourceRenderer.transform.rotation;
            ApplySourceScale();
            CopyFrame(ResolveOpacity());
        }

        private void PositionImmediately()
        {
            transform.position = ResolveTargetPosition();
            transform.rotation = sourceRenderer.transform.rotation;
            ApplySourceScale();
            followVelocity = Vector3.zero;
            positioned = true;
        }

        private Vector3 ResolveTargetPosition()
        {
            var origin = sourceRenderer == null
                ? owner.transform.position
                : sourceRenderer.transform.position;
            if (exitStarted)
            {
                var progress = Mathf.Clamp01(
                    (elapsed - (LifetimeSeconds - ExitDuration)) /
                    ExitDuration);
                progress = progress * progress *
                    (3f - 2f * progress);
                return origin + exitStartOffset * (1f - progress);
            }
            return new Vector3(
                origin.x - displayedSide * SideSeparation +
                    Mathf.Cos(hoverTime) * HoverWidth,
                origin.y + Mathf.Sin(hoverTime) * HoverHeight,
                origin.z);
        }

        private void BeginExit()
        {
            exitStarted = true;
            var origin = sourceRenderer == null
                ? owner.transform.position
                : sourceRenderer.transform.position;
            exitStartOffset = transform.position - origin;
            if (donorLabel != null)
                donorLabel.FadeOut(ExitDuration);
        }

        private void UpdateDisplayedSide()
        {
            // On ground the ghost keeps the side where it appeared. Cuphead
            // can turn or aim in either direction without making the helper
            // cross through him. Aircraft keep following their facing side.
            if (groundPresentation)
                return;
            displayedSide = ResolveFacing();
        }

        private float ResolveFacing()
        {
            if (sourceRenderer == null)
                return 1f;
            var facing = sourceRenderer.transform.lossyScale.x < 0f
                ? -1f
                : 1f;
            if (sourceRenderer.flipX)
                facing *= -1f;
            return facing;
        }

        private void ApplySourceScale()
        {
            if (sourceRenderer == null)
                return;
            transform.localScale = sourceRenderer.transform.lossyScale;
        }

        private float ResolveOpacity()
        {
            var entrance = Mathf.Clamp01(elapsed / EntranceDuration);
            var remaining = LifetimeSeconds - elapsed;
            var exit = Mathf.Clamp01(remaining / ExitDuration);
            return Mathf.Min(entrance, exit) * MaximumOpacity;
        }

        private void CopyFrame(float opacity)
        {
            if (sourceRenderer == null || ghostRenderer == null)
                return;
            ghostRenderer.sprite = sourceRenderer.sprite;
            ghostRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            ghostRenderer.flipX = sourceRenderer.flipX;
            ghostRenderer.flipY = sourceRenderer.flipY;
            ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            ghostRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            ghostRenderer.enabled = sourceRenderer.enabled &&
                sourceRenderer.gameObject.activeInHierarchy;

            var sourceColor = sourceRenderer.color;
            var tint = Color.Lerp(
                sourceColor,
                new Color(0.48f, 0.9f, 1f, sourceColor.a),
                0.55f);
            tint.a = sourceColor.a * opacity;
            ghostRenderer.color = tint;
        }

        private static SpriteRenderer FindPlayerRenderer(
            AbstractPlayerController player)
        {
            if (player == null)
                return null;
            var ground = player as LevelPlayerController;
            if (ground != null && ground.animationController != null)
                return ground.animationController.GetSpriteRenderer();
            var plane = player as PlanePlayerController;
            if (plane != null && plane.animationController != null)
                return plane.animationController.GetSpriteRenderer();
            return player.GetComponentInChildren<SpriteRenderer>();
        }

        private static bool Evaluate(Func<bool> predicate)
        {
            if (predicate == null)
                return false;
            try { return predicate(); }
            catch { return false; }
        }
    }
}
