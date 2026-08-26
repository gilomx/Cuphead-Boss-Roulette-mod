using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            if (actor.GetComponent<
                    CreatorToolsInteractionOwnedObject>() == null)
                actor.AddComponent<CreatorToolsInteractionOwnedObject>();

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

        internal static void SetGiftImage(
            GameObject actor,
            string giftImagePath,
            Action<string> logWarning)
        {
            if (actor == null || string.IsNullOrEmpty(giftImagePath))
                return;
            try
            {
                var label = actor.GetComponent<CreatorToolsDonorLabel>();
                if (label != null)
                    label.SetGiftImage(giftImagePath);
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "The interaction actor spawned without its gift image: ",
                    exception);
            }
        }

        internal static void SetGiftImage(
            CagneyHomingPlantInteractionState state,
            string giftImagePath,
            Action<string> logWarning)
        {
            if (state == null || string.IsNullOrEmpty(giftImagePath))
                return;
            try
            {
                state.SetGiftImage(giftImagePath);
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "The Cagney interaction spawned without its gift image: ",
                    exception);
            }
        }

        internal static void FreezeActorsForLevelEnd(
            Level level,
            Action<string> logWarning)
        {
            if (level == null || HasLevelEndSnapshot())
                return;

            GameObject snapshotRoot = null;
            try
            {
                snapshotRoot = new GameObject(
                    "CreatorTools_InteractionLevelEndSnapshot");
                snapshotRoot.AddComponent<
                    CreatorToolsInteractionLevelEndSnapshot>();

                var gameplayScene = level.gameObject.scene;
                if (gameplayScene.IsValid() && gameplayScene.isLoaded)
                    SceneManager.MoveGameObjectToScene(
                        snapshotRoot, gameplayScene);

                var capturedCount = 0;
                var priorities = UnityEngine.Object.FindObjectsOfType<
                    CreatorToolsInteractionRenderPriority>();
                for (var i = 0; i < priorities.Length; i++)
                {
                    var priority = priorities[i];
                    if (priority == null ||
                        !priority.gameObject.activeInHierarchy ||
                        HasPriorityAncestor(priority.transform))
                        continue;
                    capturedCount += CreateFrozenActor(
                        priority.gameObject,
                        snapshotRoot.transform);
                }

                var labels = UnityEngine.Object.FindObjectsOfType<
                    CreatorToolsDonorLabel>();
                for (var i = 0; i < labels.Length; i++)
                    if (labels[i] != null &&
                        labels[i].CreateLevelEndSnapshot(
                            snapshotRoot.transform))
                        capturedCount++;

                if (capturedCount == 0)
                    UnityEngine.Object.Destroy(snapshotRoot);
            }
            catch (Exception exception)
            {
                if (snapshotRoot != null)
                    UnityEngine.Object.Destroy(snapshotRoot);
                Warn(logWarning,
                    "Could not freeze the catalog actors at level end: ",
                    exception);
            }
        }

        internal static void ClearLevelEndSnapshots()
        {
            var snapshots = Resources.FindObjectsOfTypeAll<
                CreatorToolsInteractionLevelEndSnapshot>();
            for (var i = 0; i < snapshots.Length; i++)
                if (snapshots[i] != null)
                    UnityEngine.Object.Destroy(
                        snapshots[i].gameObject);
        }

        internal static int ClearActiveActorsForPhaseTransition()
        {
            var ownedObjects = UnityEngine.Object.FindObjectsOfType<
                CreatorToolsInteractionOwnedObject>();
            if (ownedObjects == null || ownedObjects.Length == 0)
                return 0;

            var roots = new List<GameObject>();
            for (var i = 0; i < ownedObjects.Length; i++)
            {
                var ownedObject = ownedObjects[i];
                if (ownedObject == null ||
                    !ownedObject.gameObject.activeInHierarchy ||
                    HasOwnedObjectAncestor(ownedObject.transform))
                    continue;
                roots.Add(ownedObject.gameObject);
            }

            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
            return roots.Count;
        }

        private static bool HasOwnedObjectAncestor(Transform transform)
        {
            var ancestor = transform == null
                ? null
                : transform.parent;
            while (ancestor != null)
            {
                if (ancestor.GetComponent<
                        CreatorToolsInteractionOwnedObject>() != null)
                    return true;
                ancestor = ancestor.parent;
            }
            return false;
        }

        private static bool HasLevelEndSnapshot()
        {
            var snapshots = Resources.FindObjectsOfTypeAll<
                CreatorToolsInteractionLevelEndSnapshot>();
            return snapshots != null && snapshots.Length > 0;
        }

        private static bool HasPriorityAncestor(Transform transform)
        {
            var ancestor = transform == null
                ? null
                : transform.parent;
            while (ancestor != null)
            {
                if (ancestor.GetComponent<
                        CreatorToolsInteractionRenderPriority>() != null)
                    return true;
                ancestor = ancestor.parent;
            }
            return false;
        }

        private static int CreateFrozenActor(
            GameObject source,
            Transform parent)
        {
            if (source == null || parent == null)
                return 0;

            var animators = new List<FrozenAnimatorPair>();
            var rendererCount = 0;
            var frozen = CloneAnimatedVisualHierarchy(
                source.transform,
                parent,
                true,
                animators,
                ref rendererCount);
            if (frozen == null || rendererCount == 0)
            {
                if (frozen != null)
                    UnityEngine.Object.Destroy(frozen.gameObject);
                return 0;
            }

            InitializeFrozenAnimators(animators);
            var anchor = frozen.gameObject.AddComponent<
                CreatorToolsFrozenAnimationAnchor>();
            anchor.Initialize(source.transform.position);
            return rendererCount;
        }

        private static Transform CloneAnimatedVisualHierarchy(
            Transform source,
            Transform parent,
            bool root,
            List<FrozenAnimatorPair> animators,
            ref int rendererCount)
        {
            if (source == null)
                return null;

            var frozenObject = new GameObject(
                root ? source.gameObject.name + "_Frozen" : source.name);
            frozenObject.layer = source.gameObject.layer;
            var frozen = frozenObject.transform;
            frozen.SetParent(parent, false);
            if (root)
            {
                frozen.position = source.position;
                frozen.rotation = source.rotation;
                frozen.localScale = source.lossyScale;
            }
            else
            {
                frozen.localPosition = source.localPosition;
                frozen.localRotation = source.localRotation;
                frozen.localScale = source.localScale;
            }

            var sourceRenderer = source.GetComponent<SpriteRenderer>();
            if (sourceRenderer != null)
            {
                CopySpriteRenderer(sourceRenderer, frozenObject);
                if (sourceRenderer.enabled)
                    sourceRenderer.enabled = false;
                rendererCount++;
            }

            var sourceAnimator = source.GetComponent<Animator>();
            if (sourceAnimator != null &&
                sourceAnimator.runtimeAnimatorController != null)
            {
                var frozenAnimator = frozenObject.AddComponent<Animator>();
                frozenAnimator.enabled = false;
                animators.Add(new FrozenAnimatorPair
                {
                    Source = sourceAnimator,
                    Frozen = frozenAnimator
                });
            }

            for (var i = 0; i < source.childCount; i++)
                CloneAnimatedVisualHierarchy(
                    source.GetChild(i),
                    frozen,
                    false,
                    animators,
                    ref rendererCount);

            frozenObject.SetActive(source.gameObject.activeSelf);
            return frozen;
        }

        private static void CopySpriteRenderer(
            SpriteRenderer source,
            GameObject targetObject)
        {
            var renderer = targetObject.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.color = source.color;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.enabled = source.enabled;

            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);
        }

        private static void InitializeFrozenAnimators(
            List<FrozenAnimatorPair> animators)
        {
            for (var i = 0; i < animators.Count; i++)
            {
                var pair = animators[i];
                if (pair == null || pair.Source == null ||
                    pair.Frozen == null)
                    continue;
                try
                {
                    pair.Frozen.runtimeAnimatorController =
                        pair.Source.runtimeAnimatorController;
                    pair.Frozen.applyRootMotion = false;
                    pair.Frozen.updateMode = pair.Source.updateMode;
                    pair.Frozen.cullingMode =
                        AnimatorCullingMode.AlwaysAnimate;
                    pair.Frozen.speed = Mathf.Approximately(
                            pair.Source.speed, 0f)
                        ? 1f
                        : pair.Source.speed;
                    pair.Frozen.Rebind();
                    CopyAnimatorParameters(pair.Source, pair.Frozen);
                    var layers = Math.Min(
                        pair.Source.layerCount,
                        pair.Frozen.layerCount);
                    for (var layer = 0; layer < layers; layer++)
                    {
                        var state = pair.Source.
                            GetCurrentAnimatorStateInfo(layer);
                        if (state.fullPathHash != 0)
                            pair.Frozen.Play(
                                state.fullPathHash,
                                layer,
                                state.normalizedTime);
                    }
                    pair.Frozen.enabled = pair.Source.enabled;
                    if (pair.Frozen.enabled &&
                        pair.Frozen.gameObject.activeInHierarchy)
                        pair.Frozen.Update(0f);
                }
                catch
                {
                    pair.Frozen.enabled = false;
                }
            }
        }

        private static void CopyAnimatorParameters(
            Animator source,
            Animator target)
        {
            var parameters = source.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Bool)
                    target.SetBool(
                        parameter.nameHash,
                        source.GetBool(parameter.nameHash));
                else if (parameter.type ==
                         AnimatorControllerParameterType.Int)
                    target.SetInteger(
                        parameter.nameHash,
                        source.GetInteger(parameter.nameHash));
                else if (parameter.type ==
                         AnimatorControllerParameterType.Float)
                    target.SetFloat(
                        parameter.nameHash,
                        source.GetFloat(parameter.nameHash));
            }
        }

        private sealed class FrozenAnimatorPair
        {
            internal Animator Source;
            internal Animator Frozen;
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

    internal sealed class CreatorToolsInteractionLevelEndSnapshot :
        MonoBehaviour
    {
    }

    internal sealed class CreatorToolsFrozenAnimationAnchor : MonoBehaviour
    {
        private Vector3 worldPosition;

        internal void Initialize(Vector3 position)
        {
            worldPosition = position;
            transform.position = worldPosition;
        }

        private void LateUpdate()
        {
            transform.position = worldPosition;
        }
    }

    internal sealed class CreatorToolsInteractionRenderPriority : MonoBehaviour
    {
        private static int screenCoverFrame = -1;
        private static bool screenCoverActive;

        private Renderer[] actorRenderers;
        private int[] relativeOrders;
        private readonly List<Renderer> labelRenderers =
            new List<Renderer>();
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
            if (renderer == null || labelRenderers.Contains(renderer))
                return;
            labelRenderers.Add(renderer);
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
            for (var i = labelRenderers.Count - 1; i >= 0; i--)
            {
                var labelRenderer = labelRenderers[i];
                if (labelRenderer == null)
                {
                    labelRenderers.RemoveAt(i);
                    continue;
                }
                labelRenderer.sortingLayerName = layerName;
                labelRenderer.sortingOrder = Mathf.Min(
                    short.MaxValue,
                    maximumActorOrder + 1);
            }
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

    internal sealed class CreatorToolsInteractionOwnedObject : MonoBehaviour
    {
    }
}
