using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class BaronessHeadTossInteractionState : MonoBehaviour
    {
        private const float NativeChaseCycleSeconds = 21f / 24f;
        private const float NativeHeadReleaseSeconds =
            NativeChaseCycleSeconds + 19f / 24f;
        private const float NativeTossEndSeconds =
            NativeChaseCycleSeconds + 42f / 24f;
        private const float EntranceDurationSeconds = 20f / 24f;
        private const float ExitStartSeconds = NativeTossEndSeconds;
        private const float ExitDurationSeconds = 20f / 24f;
        private const float MaximumLifetimeSeconds = 24f;
        private const float OffscreenCleanupMarginPixels = 220f;
        private const string TossParameterName = "Toss";

        private GameObject baronessRoot;
        private BaronessLevelCastle inertParent;
        private Animator animator;
        private SpriteRenderer[] bodyRenderers;
        private BaronessLevelFollowingProjectile headTemplate;
        private LevelProperties.Baroness properties;
        private Transform tossPoint;
        private Vector3 tossLocalPosition;
        private Vector3 offscreenPosition;
        private Vector3 attackPosition;
        private Vector3 initialCameraPosition;
        private CreatorToolsDonorLabel donorLabel;
        private BaronessLevelFollowingProjectile head;
        private Action<string> logWarning;
        private string donor;
        private string giftImagePath = string.Empty;
        private float cameraScale = 1f;
        private float elapsed;
        private float offscreenElapsed;
        private bool headReleased;
        private bool bodyHidden;
        private bool cleaningUp;

        internal void Initialize(
            GameObject baronessRoot,
            BaronessLevelCastle inertParent,
            Animator animator,
            SpriteRenderer[] bodyRenderers,
            BaronessLevelFollowingProjectile headTemplate,
            LevelProperties.Baroness properties,
            Transform tossPoint,
            Vector3 tossLocalPosition,
            Vector3 offscreenPosition,
            Vector3 attackPosition,
            float cameraScale,
            string donor,
            string giftImagePath,
            Action<string> logWarning)
        {
            this.baronessRoot = baronessRoot;
            this.inertParent = inertParent;
            this.animator = animator;
            this.bodyRenderers = bodyRenderers;
            this.headTemplate = headTemplate;
            this.properties = properties;
            this.tossPoint = tossPoint;
            this.tossLocalPosition = tossLocalPosition;
            this.offscreenPosition = offscreenPosition;
            this.attackPosition = attackPosition;
            this.cameraScale = Mathf.Max(0.01f, cameraScale);
            this.donor = donor;
            this.giftImagePath = giftImagePath ?? string.Empty;
            this.logWarning = logWarning;
            donorLabel = baronessRoot == null
                ? null
                : baronessRoot.GetComponent<CreatorToolsDonorLabel>();
            var camera = FindGameplayCamera();
            initialCameraPosition = camera == null
                ? Vector3.zero
                : camera.transform.position;
            if (baronessRoot != null)
                baronessRoot.transform.position = offscreenPosition;
            ApplyBodyVisibility();
        }

        internal bool TryGetActorPosition(out Vector2 position)
        {
            if (head != null)
            {
                position = head.transform.position;
                return true;
            }
            if (baronessRoot != null)
            {
                position = baronessRoot.transform.position;
                return true;
            }
            position = Vector2.zero;
            return false;
        }

        private void Update()
        {
            if (cleaningUp)
                return;

            var speed = Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (animator != null && !bodyHidden)
                animator.speed = speed;
            if (speed <= 0f)
                return;
            elapsed += Time.unscaledDeltaTime * speed;

            UpdateBodyMotion();
            if (!headReleased && elapsed >= NativeHeadReleaseSeconds)
                ReleaseHead();
            if (!bodyHidden && elapsed >=
                    ExitStartSeconds + ExitDurationSeconds)
                HideBaroness();

            if (headReleased && head == null && bodyHidden)
            {
                Destroy(gameObject);
                return;
            }

            if (head != null && bodyHidden)
            {
                if (IsFullyOutsideGameplayView(
                        head.gameObject,
                        OffscreenCleanupMarginPixels * cameraScale))
                    offscreenElapsed += Time.unscaledDeltaTime * speed;
                else
                    offscreenElapsed = 0f;

                if (offscreenElapsed >= 0.6f)
                {
                    Destroy(head.gameObject);
                    Destroy(gameObject);
                    return;
                }
            }

            if (elapsed >= MaximumLifetimeSeconds)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            ApplyBodyVisibility();
        }

        private void UpdateBodyMotion()
        {
            if (baronessRoot == null || bodyHidden)
                return;

            var camera = FindGameplayCamera();
            var cameraShift = camera == null
                ? Vector3.zero
                : camera.transform.position - initialCameraPosition;
            cameraShift.z = 0f;
            var hidden = offscreenPosition + cameraShift;
            var attack = attackPosition + cameraShift;

            if (elapsed < EntranceDurationSeconds)
            {
                var progress = Mathf.Clamp01(
                    elapsed / EntranceDurationSeconds);
                progress = Mathf.SmoothStep(0f, 1f, progress);
                baronessRoot.transform.position = Vector3.Lerp(
                    hidden, attack, progress);
                return;
            }
            if (elapsed <= ExitStartSeconds)
            {
                baronessRoot.transform.position = attack;
                return;
            }

            var exitProgress = Mathf.Clamp01(
                (elapsed - ExitStartSeconds) / ExitDurationSeconds);
            exitProgress = Mathf.SmoothStep(0f, 1f, exitProgress);
            baronessRoot.transform.position = Vector3.Lerp(
                attack, hidden, exitProgress);
        }

        private void ReleaseHead()
        {
            headReleased = true;
            try
            {
                if (baronessRoot == null || inertParent == null ||
                    headTemplate == null || properties == null)
                    throw new InvalidOperationException(
                        "The native Baroness head toss is incomplete.");

                var player = PlayerManager.GetNext();
                if (player == null)
                    throw new InvalidOperationException(
                        "No active player can be targeted by the Baroness head.");

                head = UnityEngine.Object.Instantiate(headTemplate);
                if (head == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native Baroness head.");

                head.gameObject.name =
                    "CreatorTools_NativeBaronessFollowingHead";
                head.gameObject.SetActive(true);
                var releasePosition = tossPoint == null
                    ? baronessRoot.transform.TransformPoint(tossLocalPosition)
                    : tossPoint.position;
                head.Init(
                    releasePosition,
                    player.transform.position,
                    properties.CurrentState.baronessVonBonbon,
                    player,
                    inertParent);
                if (animator != null)
                    animator.SetBool(TossParameterName, false);

                CreatorToolsInteractionPresentation.
                    MatchGameplayCameraScale(head.gameObject, logWarning);
                CreatorToolsInteractionPresentation.BringActorToFront(
                    head.gameObject);
                TransferPresentationToHead();
            }
            catch (Exception exception)
            {
                Warn("Could not release the Baroness's native head: ",
                    exception);
                if (head != null)
                    Destroy(head.gameObject);
                head = null;
                Destroy(gameObject);
            }
        }

        private void TransferPresentationToHead()
        {
            if (head == null)
                return;
            var anchor = FindLabelAnchor(head.gameObject);
            if (donorLabel != null && donorLabel.RebindTo(
                    head.gameObject, anchor, 0.2f))
                return;

            CreatorToolsInteractionPresentation.PrepareActor(
                head.gameObject, anchor, donor, logWarning);
            var label = head.gameObject.GetComponent<
                CreatorToolsDonorLabel>();
            if (label != null && !string.IsNullOrEmpty(giftImagePath))
                label.SetGiftImage(giftImagePath);
        }

        private void HideBaroness()
        {
            bodyHidden = true;
            if (animator != null)
                animator.enabled = false;
            ApplyBodyVisibility();
        }

        private void ApplyBodyVisibility()
        {
            if (bodyRenderers == null)
                return;
            for (var i = 0; i < bodyRenderers.Length; i++)
            {
                var renderer = bodyRenderers[i];
                if (renderer != null)
                    renderer.enabled = !bodyHidden &&
                        IsBaronessRenderer(renderer);
            }
        }

        private static bool IsBaronessRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.gameObject == null)
                return false;
            var name = renderer.gameObject.name;
            return string.Equals(
                    name, "BaronessPhase2", StringComparison.Ordinal) ||
                string.Equals(
                    name, "BaronessPhase2Top", StringComparison.Ordinal);
        }

        private static SpriteRenderer FindLabelAnchor(GameObject actor)
        {
            if (actor == null)
                return null;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer fallback = null;
            SpriteRenderer best = null;
            var bestArea = -1f;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;
                if (fallback == null ||
                    (renderer.enabled && renderer.gameObject.activeInHierarchy))
                    fallback = renderer;
                if (renderer.sprite == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                    continue;
                var size = renderer.sprite.bounds.size;
                var area = Mathf.Abs(size.x * size.y);
                if (area <= bestArea)
                    continue;
                best = renderer;
                bestArea = area;
            }
            return best == null ? fallback : best;
        }

        private static bool IsFullyOutsideGameplayView(
            GameObject actor, float margin)
        {
            var camera = FindGameplayCamera();
            if (actor == null || camera == null)
                return false;
            var distance = Mathf.Abs(
                camera.transform.position.z - actor.transform.position.z);
            var bottomLeft = camera.ViewportToWorldPoint(
                new Vector3(0f, 0f, distance));
            var topRight = camera.ViewportToWorldPoint(
                new Vector3(1f, 1f, distance));
            var bounds = VisibleBounds(actor);
            if (!bounds.HasValue)
                return false;
            var value = bounds.Value;
            return value.max.x < bottomLeft.x - margin ||
                value.min.x > topRight.x + margin ||
                value.max.y < bottomLeft.y - margin ||
                value.min.y > topRight.y + margin;
        }

        internal static Bounds? VisibleBounds(GameObject actor)
        {
            if (actor == null)
                return null;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            var found = false;
            var bounds = new Bounds();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                var visible = VisibleSpriteBounds(renderer);
                if (!found)
                {
                    bounds = visible;
                    found = true;
                }
                else
                    bounds.Encapsulate(visible);
            }
            return found ? (Bounds?)bounds : null;
        }

        private static Bounds VisibleSpriteBounds(SpriteRenderer renderer)
        {
            try
            {
                var sprite = renderer.sprite;
                var pixels = Mathf.Max(0.01f, sprite.pixelsPerUnit);
                var offset = sprite.textureRectOffset;
                var size = sprite.textureRect.size;
                var pivot = sprite.pivot;
                var min = new Vector2(
                    (offset.x - pivot.x) / pixels,
                    (offset.y - pivot.y) / pixels);
                var max = new Vector2(
                    (offset.x + size.x - pivot.x) / pixels,
                    (offset.y + size.y - pivot.y) / pixels);
                if (renderer.flipX)
                {
                    var oldMin = min.x;
                    min.x = -max.x;
                    max.x = -oldMin;
                }
                if (renderer.flipY)
                {
                    var oldMin = min.y;
                    min.y = -max.y;
                    max.y = -oldMin;
                }

                var first = renderer.transform.TransformPoint(
                    new Vector3(min.x, min.y, 0f));
                var bounds = new Bounds(first, Vector3.zero);
                bounds.Encapsulate(renderer.transform.TransformPoint(
                    new Vector3(min.x, max.y, 0f)));
                bounds.Encapsulate(renderer.transform.TransformPoint(
                    new Vector3(max.x, min.y, 0f)));
                bounds.Encapsulate(renderer.transform.TransformPoint(
                    new Vector3(max.x, max.y, 0f)));
                return bounds;
            }
            catch
            {
                return renderer.bounds;
            }
        }

        internal static Camera FindGameplayCamera()
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

        private void Warn(string prefix, Exception exception)
        {
            if (logWarning != null)
                logWarning(prefix + exception);
        }

        private void OnDestroy()
        {
            if (cleaningUp)
                return;
            cleaningUp = true;
            if (head != null)
                Destroy(head.gameObject);
            head = null;
            baronessRoot = null;
            inertParent = null;
            animator = null;
            bodyRenderers = null;
            headTemplate = null;
            properties = null;
            tossPoint = null;
            donorLabel = null;
        }
    }

    internal sealed class CreatorToolsBaronessHeadTossMarker : MonoBehaviour
    {
    }
}
