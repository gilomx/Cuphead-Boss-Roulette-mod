using System;
using System.Reflection;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsGhostActor : MonoBehaviour
    {
        private static readonly FieldInfo PlaneSuperPlayer =
            typeof(AbstractPlaneSuper).GetField(
                "player",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
        private static readonly FieldInfo PlaneSuperSpriteRenderer =
            typeof(AbstractPlaneSuper).GetField(
                "spriteRenderer",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);

        internal const float LifetimeSeconds = 10f;

        private const float SideSeparation = 140f;
        private const float HoverWidth = 8f;
        private const float HoverHeight = 5f;
        private const float HoverSpeed = 2.4f;
        private const float FollowSmoothTime = 0.12f;
        private const float SnapDistance = 160f;
        private const float EntranceDuration = 0.22f;
        private const float ExitDuration = 0.24f;

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
            // Ghost Help continues moving during the K.O. presentation, so
            // its name must follow the actor without a frozen duplicate.
            donorLabel.KeepLiveAtLevelEnd();
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
            var effectAdvancing = Evaluate(canAdvance);
            var winningLevelEnd = CreatorToolsInteractionPresentation
                .IsWinningLevelEnd();
            if (!effectAdvancing && !winningLevelEnd)
                return;

            var effectDelta = Time.unscaledDeltaTime *
                Mathf.Max(0f, CupheadTime.GlobalSpeed);
            // K.O. slows or stops the gameplay clock. Preserve the ghost's
            // normal visual float and aircraft follow during the victory
            // presentation without spending the remaining help duration.
            gameplayDelta = winningLevelEnd
                ? Time.unscaledDeltaTime
                : effectDelta;
            if (effectAdvancing)
                elapsed += effectDelta;
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
            RefreshSourceRenderer();
            if (sourceRenderer == null)
            {
                ApplyCachedFrameVisibility(ResolveOpacity(), false);
                return;
            }

            UpdateDisplayedSide();
            var target = ResolveTargetPosition();
            var entering = elapsed < EntranceDuration;
            if (groundPresentation || entering || exitStarted || !positioned ||
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
                var remaining = LifetimeSeconds - elapsed;
                return origin + exitStartOffset *
                    CreatorToolsGhostMotionPolicy.ResolveExitOffsetFactor(
                        remaining, ExitDuration);
            }
            var orbitTarget = new Vector3(
                origin.x - displayedSide * SideSeparation +
                    Mathf.Cos(hoverTime) * HoverWidth,
                origin.y + Mathf.Sin(hoverTime) * HoverHeight,
                origin.z);
            if (elapsed >= EntranceDuration)
                return orbitTarget;
            return origin + (orbitTarget - origin) *
                CreatorToolsGhostMotionPolicy.ResolveEntranceOffsetFactor(
                    elapsed, EntranceDuration);
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
            var scale = sourceRenderer.transform.lossyScale;
            if (Mathf.Abs(scale.x) <= 0.001f ||
                Mathf.Abs(scale.y) <= 0.001f)
                return;
            transform.localScale = scale;
        }

        private float ResolveOpacity()
        {
            return CreatorToolsGhostMotionPolicy.ResolveOpacity(
                elapsed, LifetimeSeconds, EntranceDuration, ExitDuration);
        }

        private void CopyFrame(float opacity)
        {
            if (sourceRenderer == null || ghostRenderer == null)
                return;
            if (sourceRenderer.sprite != null)
            {
                ghostRenderer.sprite = sourceRenderer.sprite;
                if (sourceRenderer.sharedMaterial != null)
                    ghostRenderer.sharedMaterial =
                        sourceRenderer.sharedMaterial;
                ghostRenderer.flipX = sourceRenderer.flipX;
                ghostRenderer.flipY = sourceRenderer.flipY;
                ghostRenderer.sortingLayerID =
                    sourceRenderer.sortingLayerID;
                ghostRenderer.sortingOrder =
                    sourceRenderer.sortingOrder - 1;
            }

            var sourceColor = sourceRenderer.color;
            var tint = Color.Lerp(
                sourceColor,
                new Color(0.48f, 0.9f, 1f, sourceColor.a),
                0.55f);
            tint.a = CreatorToolsGhostFramePolicy.ResolveGhostAlpha(
                sourceColor.a, opacity);
            ghostRenderer.color = tint;
            ApplyCachedFrameVisibility(
                opacity, RendererCanProvideVisibleFrame(sourceRenderer));
        }

        private void ApplyCachedFrameVisibility(
            float opacity, bool sourceVisible)
        {
            if (ghostRenderer == null)
                return;
            ghostRenderer.enabled =
                CreatorToolsGhostFramePolicy.ShouldDisplayCachedFrame(
                    ghostRenderer.sprite != null,
                    sourceVisible,
                    opacity);
        }

        private void RefreshSourceRenderer()
        {
            if (CreatorToolsGhostFramePolicy.MayUseAlternateRenderer(
                    groundPresentation))
            {
                var superRenderer = FindPlaneSuperRenderer(owner);
                if (RendererCanProvideVisibleFrame(superRenderer))
                {
                    sourceRenderer = superRenderer;
                    return;
                }
            }

            var preferred = FindPlayerRenderer(owner);
            if (RendererCanProvideVisibleFrame(preferred))
            {
                sourceRenderer = preferred;
                return;
            }
            if (RendererCanProvideVisibleFrame(sourceRenderer))
                return;

            // Chalice's first ground jump exposes auxiliary renderers for
            // individual body pieces. Mirroring only one would make the ghost
            // look incomplete, so keep the last complete primary frame until
            // the normal renderer returns. Aircraft may safely follow their
            // separate bomb/transformation renderers.
            if (!CreatorToolsGhostFramePolicy.MayUseAlternateRenderer(
                    groundPresentation))
            {
                if (sourceRenderer == null)
                    sourceRenderer = preferred;
                return;
            }

            var renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer best = null;
            for (var index = 0; index < renderers.Length; index++)
            {
                var candidate = renderers[index];
                if (!RendererCanProvideVisibleFrame(candidate))
                    continue;
                if (best == null ||
                    candidate.sortingOrder > best.sortingOrder)
                    best = candidate;
            }
            if (best != null)
                sourceRenderer = best;
            else if (sourceRenderer == null)
                sourceRenderer = preferred;
        }

        private static bool RendererCanProvideVisibleFrame(
            SpriteRenderer renderer)
        {
            return renderer != null &&
                CreatorToolsGhostFramePolicy.CanAdoptSourceFrame(
                    renderer.gameObject.activeInHierarchy,
                    renderer.enabled,
                    renderer.sprite != null,
                    renderer.color.a);
        }

        private static SpriteRenderer FindPlaneSuperRenderer(
            AbstractPlayerController player)
        {
            if (player == null || PlaneSuperPlayer == null)
                return null;
            try
            {
                var supers = UnityEngine.Object
                    .FindObjectsOfType<AbstractPlaneSuper>();
                for (var index = 0; index < supers.Length; index++)
                {
                    var planeSuper = supers[index];
                    var superPlayer = planeSuper == null
                        ? null
                        : PlaneSuperPlayer.GetValue(planeSuper)
                            as PlanePlayerController;
                    if (planeSuper == null || superPlayer != player)
                        continue;

                    var preferred = PlaneSuperSpriteRenderer == null
                        ? null
                        : PlaneSuperSpriteRenderer.GetValue(planeSuper)
                            as SpriteRenderer;
                    if (RendererCanProvideVisibleFrame(preferred))
                        return preferred;

                    var renderers = planeSuper
                        .GetComponentsInChildren<SpriteRenderer>(true);
                    SpriteRenderer best = null;
                    for (var rendererIndex = 0;
                        rendererIndex < renderers.Length;
                        rendererIndex++)
                    {
                        var candidate = renderers[rendererIndex];
                        if (!RendererCanProvideVisibleFrame(candidate))
                            continue;
                        if (best == null ||
                            candidate.sortingOrder > best.sortingOrder)
                            best = candidate;
                    }
                    if (best != null)
                        return best;
                }
            }
            catch
            {
            }
            return null;
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
