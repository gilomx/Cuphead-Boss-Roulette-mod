using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsInteractionPresentation
    {
        // ForegroundEffects is Cuphead's last gameplay sorting layer. UI,
        // loader, top-level fades and achievement overlays remain above it.
        private const string FrontGameplaySortingLayer = "ForegroundEffects";
        private const string CoveredGameplaySortingLayer = "Enemies";
        private const int FrontActorSortingOrder = short.MaxValue - 64;
        private const float ReferenceViewportHeight = 720f;

        internal static float MatchGameplayCameraScale(
            GameObject actor,
            Action<string> logWarning)
        {
            if (actor == null)
                return 1f;
            try
            {
                var existing = actor.GetComponent<
                    CreatorToolsInteractionCameraScale>();
                if (existing != null)
                    return existing.Factor;

                var camera = FindGameplayCamera();
                if (camera == null)
                    return 1f;
                var factor = Mathf.Max(
                    0.01f,
                    camera.orthographicSize * 2f /
                    ReferenceViewportHeight);
                var nativeScale = actor.transform.localScale;
                actor.transform.localScale = new Vector3(
                    nativeScale.x * factor,
                    nativeScale.y * factor,
                    nativeScale.z);
                var marker = actor.AddComponent<
                    CreatorToolsInteractionCameraScale>();
                marker.Factor = factor;
                return factor;
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "Could not preserve the interaction actor screen size: ",
                    exception);
                return 1f;
            }
        }

        internal static void MarkInheritedGameplayCameraScale(
            GameObject actor,
            float factor)
        {
            if (actor == null)
                return;
            var marker = actor.GetComponent<
                CreatorToolsInteractionCameraScale>();
            if (marker == null)
                marker = actor.AddComponent<
                    CreatorToolsInteractionCameraScale>();
            marker.Factor = Mathf.Max(0.01f, factor);
        }

        internal static void BringActorToFront(GameObject actor)
        {
            if (actor == null)
                return;

            var renderers = actor.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            var priority = actor.GetComponent<
                CreatorToolsInteractionRenderPriority>();
            if (priority == null)
                priority = actor.AddComponent<
                    CreatorToolsInteractionRenderPriority>();
            priority.Initialize(
                renderers,
                FrontGameplaySortingLayer,
                CoveredGameplaySortingLayer,
                FrontActorSortingOrder);
        }

        internal static void PrepareActor(
            GameObject actor,
            string donor,
            Action<string> logWarning)
        {
            PrepareActor(actor, null, donor, logWarning);
        }

        internal static void PrepareActor(
            GameObject actor,
            SpriteRenderer labelAnchorRenderer,
            string donor,
            Action<string> logWarning)
        {
            if (actor == null)
                return;

            MatchGameplayCameraScale(actor, logWarning);

            try
            {
                BringActorToFront(actor);
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "Could not move the interaction actor forward: ",
                    exception);
            }

            try
            {
                var label = actor.AddComponent<CreatorToolsDonorLabel>();
                label.Initialize(donor, labelAnchorRenderer);
            }
            catch (Exception exception)
            {
                // Presentation must never invalidate an already-created
                // gameplay actor. Keep the enemy and report the label issue.
                Warn(logWarning,
                    "The interaction actor spawned without its donor label: ",
                    exception);
            }
        }

        private static void Warn(
            Action<string> logWarning,
            string prefix,
            Exception exception)
        {
            if (logWarning != null)
                logWarning(prefix + exception);
        }

        private static Camera FindGameplayCamera()
        {
            var main = Camera.main;
            if (main != null && main.enabled && main.orthographic)
                return main;
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (var i = 0; i < cameras.Length; i++)
                if (cameras[i] != null && cameras[i].enabled &&
                    cameras[i].orthographic)
                    return cameras[i];
            return null;
        }
    }

    internal sealed class CreatorToolsInteractionCameraScale : MonoBehaviour
    {
        internal float Factor = 1f;
    }

    internal sealed class CreatorToolsInteractionRenderPriority : MonoBehaviour
    {
        private static int screenCoverFrame = -1;
        private static bool screenCoverActive;

        private Renderer[] actorRenderers;
        private int[] relativeOrders;
        private Renderer labelRenderer;
        private string frontLayerName;
        private string coveredLayerName;
        private int maximumActorOrder;

        internal void Initialize(
            Renderer[] renderers,
            string frontLayer,
            string coveredLayer,
            int maximumOrder)
        {
            actorRenderers = renderers;
            frontLayerName = frontLayer;
            coveredLayerName = coveredLayer;
            maximumActorOrder = maximumOrder;

            var nativeMaximum = int.MinValue;
            for (var i = 0; i < actorRenderers.Length; i++)
                if (actorRenderers[i] != null)
                    nativeMaximum = Mathf.Max(
                        nativeMaximum,
                        actorRenderers[i].sortingOrder);
            if (nativeMaximum == int.MinValue)
                nativeMaximum = 0;

            relativeOrders = new int[actorRenderers.Length];
            for (var i = 0; i < actorRenderers.Length; i++)
                relativeOrders[i] = actorRenderers[i] == null
                    ? 0
                    : actorRenderers[i].sortingOrder - nativeMaximum;
            ApplyPriority();
        }

        internal void RegisterLabel(Renderer renderer)
        {
            labelRenderer = renderer;
            ApplyPriority();
        }

        private void LateUpdate()
        {
            ApplyPriority();
        }

        private void ApplyPriority()
        {
            if (actorRenderers == null || relativeOrders == null)
                return;
            var layerName = IsScreenCoverActive()
                ? coveredLayerName
                : frontLayerName;
            for (var i = 0; i < actorRenderers.Length; i++)
            {
                var renderer = actorRenderers[i];
                if (renderer == null)
                    continue;
                renderer.sortingLayerName = layerName;
                renderer.sortingOrder = Mathf.Clamp(
                    maximumActorOrder + relativeOrders[i],
                    short.MinValue,
                    maximumActorOrder);
            }
            if (labelRenderer == null)
                return;
            labelRenderer.sortingLayerName = layerName;
            labelRenderer.sortingOrder = Mathf.Min(
                short.MaxValue,
                maximumActorOrder + 1);
        }

        private static bool IsScreenCoverActive()
        {
            if (screenCoverFrame == Time.frameCount)
                return screenCoverActive;
            screenCoverFrame = Time.frameCount;
            screenCoverActive = false;

            // Brineybeard's native squid ink is not implemented through a
            // PlayerScreenEffectController. Its renderer is enabled for the
            // complete hit/fade cycle and disabled again when the ink clears.
            // Treat the enabled state as the cover boundary so catalog actors
            // cannot flash over the first splat while its alpha ramps up.
            var pirateInkOverlay = PirateLevelSquidInkOverlay.Current;
            if (pirateInkOverlay != null)
            {
                var pirateInkRenderer = pirateInkOverlay.GetComponent<
                    SpriteRenderer>();
                if (pirateInkRenderer != null &&
                    pirateInkRenderer.enabled &&
                    pirateInkRenderer.gameObject.activeInHierarchy)
                {
                    screenCoverActive = true;
                    return true;
                }
            }

            var controllers = UnityEngine.Object.FindObjectsOfType<
                PlayerScreenEffectController>();
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null ||
                    !controller.gameObject.activeInHierarchy)
                    continue;
                var sprites = controller.GetComponentsInChildren<
                    SpriteRenderer>(true);
                for (var j = 0; j < sprites.Length; j++)
                {
                    var sprite = sprites[j];
                    if (sprite == null || !sprite.enabled ||
                        !sprite.gameObject.activeInHierarchy ||
                        sprite.sprite == null || sprite.color.a <= 0.01f)
                        continue;
                    screenCoverActive = true;
                    return true;
                }
            }
            return false;
        }
    }
}
