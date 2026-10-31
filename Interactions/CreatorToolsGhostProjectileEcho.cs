using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsGhostProjectileEcho : MonoBehaviour
    {
        private const float CatchupSeconds = 0.22f;
        private const float Opacity = 0.5f;

        private AbstractProjectile projectile;
        private SpriteRenderer[] sourceRenderers;
        private SpriteRenderer[] echoRenderers;
        private Vector3 initialOffset;
        private float elapsed;

        internal bool IsGhostShot { get; private set; }

        internal void Initialize(
            AbstractProjectile source,
            bool isGhostShot,
            Vector3 offset)
        {
            // Native weapons can reuse a projectile. Retire its detached
            // visuals before replacing the damage snapshot for the next shot.
            DestroyVisuals();
            projectile = source;
            IsGhostShot = isGhostShot;
            initialOffset = offset;
            elapsed = 0f;
            if (!IsGhostShot || projectile == null)
                return;

            sourceRenderers = projectile.GetComponentsInChildren<
                SpriteRenderer>(true);
            echoRenderers = new SpriteRenderer[sourceRenderers.Length];
            for (var i = 0; i < sourceRenderers.Length; i++)
            {
                var sourceRenderer = sourceRenderers[i];
                if (sourceRenderer == null)
                    continue;
                var echoObject = new GameObject(
                    "CreatorTools_GhostProjectileVisual");
                echoObject.layer = sourceRenderer.gameObject.layer;
                echoRenderers[i] = echoObject.AddComponent<SpriteRenderer>();
            }
            CopyVisuals(initialOffset);
            enabled = true;
        }

        private void Update()
        {
            if (!IsGhostShot || echoRenderers == null)
                return;
            elapsed += Time.unscaledDeltaTime *
                Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (elapsed >= CatchupSeconds)
                DestroyVisuals();
        }

        private void LateUpdate()
        {
            if (!IsGhostShot || projectile == null ||
                echoRenderers == null)
                return;
            var progress = Mathf.Clamp01(elapsed / CatchupSeconds);
            progress = progress * progress * (3f - 2f * progress);
            CopyVisuals(Vector3.Lerp(
                initialOffset, Vector3.zero, progress));
        }

        private void CopyVisuals(Vector3 offset)
        {
            for (var i = 0; sourceRenderers != null &&
                 echoRenderers != null &&
                 i < sourceRenderers.Length &&
                 i < echoRenderers.Length; i++)
            {
                var source = sourceRenderers[i];
                var echo = echoRenderers[i];
                if (source == null || echo == null)
                    continue;

                echo.sprite = source.sprite;
                echo.sharedMaterial = source.sharedMaterial;
                echo.flipX = source.flipX;
                echo.flipY = source.flipY;
                echo.sortingLayerID = source.sortingLayerID;
                echo.sortingOrder = source.sortingOrder - 1;
                echo.enabled = source.enabled &&
                    source.gameObject.activeInHierarchy;
                echo.transform.position = source.transform.position + offset;
                echo.transform.rotation = source.transform.rotation;
                echo.transform.localScale = source.transform.lossyScale;

                var sourceColor = source.color;
                var tint = Color.Lerp(
                    sourceColor,
                    new Color(0.48f, 0.9f, 1f, sourceColor.a),
                    0.55f);
                tint.a = sourceColor.a * Opacity;
                echo.color = tint;
            }
        }

        private void OnDestroy()
        {
            DestroyVisuals();
        }

        private void OnDisable()
        {
            // Pooled projectiles can disappear before the catch-up finishes.
            // Their unparented echo must not survive into a later reuse.
            DestroyVisuals();
        }

        private void DestroyVisuals()
        {
            if (echoRenderers != null)
            {
                for (var i = 0; i < echoRenderers.Length; i++)
                    if (echoRenderers[i] != null)
                        Destroy(echoRenderers[i].gameObject);
            }
            echoRenderers = null;
            sourceRenderers = null;
            // Keep IsGhostShot available to the damage patch after the visual
            // expires, without running two Unity callbacks on every bullet.
            enabled = false;
        }
    }

    public sealed partial class Plugin
    {
        private static void GhostProjectileFirePostfix(
            AbstractProjectile __result)
        {
            if (__result == null)
                return;

            var plugin = activeInstance;
            var offset = Vector3.zero;
            var isGhostShot = plugin != null &&
                plugin.creatorToolsInteractions != null &&
                plugin.creatorToolsInteractions.TryGetGhostProjectileOffset(
                    __result.PlayerId, out offset);
            var marker = __result.gameObject.GetComponent<
                CreatorToolsGhostProjectileEcho>();
            if (marker == null)
                marker = __result.gameObject.AddComponent<
                    CreatorToolsGhostProjectileEcho>();
            marker.Initialize(__result, isGhostShot, offset);
        }
    }
}
