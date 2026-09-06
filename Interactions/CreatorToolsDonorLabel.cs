using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsDonorLabel : MonoBehaviour
    {
        private const float FallbackVerticalOffset = 350f;
        private const float VisualGap = 14f;
        private const float LabelWidth = 320f;
        private const float LabelHeight = 48f;
        private const float GiftImageGap = 5f;
        private static readonly Color32 DefaultTextColor =
            new Color32(255, 240, 194, 255);
        private static readonly Color32 AlternateTextColor =
            new Color32(24, 20, 17, 255);
        // Add Levels values here after the alternate-color boss list is
        // approved. An empty set deliberately preserves today's presentation.
        private static readonly HashSet<Levels> AlternateTextColorLevels =
            new HashSet<Levels>();
        private static bool giftImagesVisible = true;
        private CreatorToolsDonorLabelFollower follower;
        private TextMeshPro labelText;
        private Renderer labelRenderer;
        private SpriteRenderer giftRenderer;

        internal static void SetGiftImagesVisible(bool visible)
        {
            giftImagesVisible = visible;
            var followers = UnityEngine.Object.FindObjectsOfType<
                CreatorToolsDonorLabelFollower>();
            for (var i = 0; i < followers.Length; i++)
                if (followers[i] != null)
                    followers[i].SetGiftImagePreferenceVisible(visible);
        }

        internal void Initialize(string value)
        {
            Initialize(value, null);
        }

        internal void Initialize(
            string value,
            SpriteRenderer anchorRenderer)
        {
            var donor = string.IsNullOrEmpty(value)
                ? string.Empty
                : value.ToUpperInvariant();
            if (donor.Length == 0)
                return;

            GameObject labelObject = null;
            try
            {
                labelObject = new GameObject(
                    "CreatorTools_DonorLabel");
                labelObject.layer = gameObject.layer;

                labelText = labelObject.AddComponent<TextMeshPro>();
                // TextMeshPro replaces the GameObject's Transform with a
                // RectTransform in this Unity version. Resolve it afterwards.
                var labelTransform = labelText.rectTransform;
                labelText.font = FindGameFont();
                labelText.text = donor;
                labelText.fontSize = 28f;
                labelText.fontStyle = FontStyles.Bold;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.enableWordWrapping = false;
                labelText.richText = false;
                labelText.isOrthographic = true;
                labelText.color = ResolveTextColor();
                labelText.outlineColor = new Color32(20, 15, 10, 235);
                labelText.outlineWidth = 0.18f;
                labelText.rectTransform.sizeDelta = new Vector2(
                    LabelWidth, LabelHeight);
                labelText.rectTransform.pivot = new Vector2(0.5f, 1f);

                var actorRenderer = anchorRenderer == null
                    ? GetComponent<SpriteRenderer>()
                    : anchorRenderer;
                labelRenderer = labelText.GetComponent<Renderer>();
                MatchActorSorting(labelRenderer);
                labelTransform.rotation = Quaternion.identity;

                follower = labelObject.AddComponent<
                    CreatorToolsDonorLabelFollower>();
                var scaleFactor = GetLabelCameraScale();
                follower.Initialize(
                    transform,
                    actorRenderer,
                    labelText,
                    FallbackVerticalOffset,
                    VisualGap * scaleFactor);
                RegisterWithRenderPriority(gameObject);
            }
            catch
            {
                if (labelObject != null)
                    Destroy(labelObject);
                throw;
            }
        }

        internal void SetGiftImage(string imagePath)
        {
            if (labelText == null || labelRenderer == null ||
                string.IsNullOrEmpty(imagePath))
                return;
            string loadError;
            var sprite = CreatorToolsGiftImageCache.TryGet(
                imagePath, out loadError);
            if (sprite == null)
            {
                // The presentation boundary catches and logs this once. The
                // cache suppresses repeats for a path that already failed so
                // a batch of redeems cannot flood BepInEx's log.
                if (!string.IsNullOrEmpty(loadError))
                    throw new InvalidOperationException(loadError);
                return;
            }

            if (giftRenderer == null)
            {
                var giftObject = new GameObject(
                    "CreatorTools_DonorGift");
                giftObject.layer = labelText.gameObject.layer;
                giftObject.transform.SetParent(
                    labelText.rectTransform, false);
                giftRenderer = giftObject.AddComponent<SpriteRenderer>();
            }

            labelText.margin = Vector4.zero;
            var availableTextWidth = Mathf.Max(
                1f, LabelWidth - sprite.bounds.size.x - GiftImageGap);
            var textWidth = Mathf.Min(
                availableTextWidth,
                Mathf.Max(1f, labelText.preferredWidth));
            labelText.margin = new Vector4(
                sprite.bounds.size.x + GiftImageGap, 0f, 0f, 0f);

            giftRenderer.sprite = sprite;
            giftRenderer.color = Color.white;
            giftRenderer.sortingLayerID = labelRenderer.sortingLayerID;
            giftRenderer.sortingOrder = labelRenderer.sortingOrder;
            giftRenderer.transform.localPosition = new Vector3(
                -(textWidth + GiftImageGap) * 0.5f,
                -LabelHeight * 0.5f,
                0f);
            giftRenderer.transform.localRotation = Quaternion.identity;
            giftRenderer.transform.localScale = Vector3.one;

            if (follower != null)
            {
                follower.SetGiftRenderer(giftRenderer);
                follower.SetGiftImagePreferenceVisible(giftImagesVisible);
            }
            RegisterWithRenderPriority(gameObject);
        }

        internal bool RebindTo(
            GameObject actor,
            SpriteRenderer anchorRenderer,
            float dynamicAnchorSeconds)
        {
            if (actor == null || follower == null)
                return false;

            var scaleFactor = GetLabelCameraScale();
            follower.Rebind(
                actor.transform,
                anchorRenderer,
                FallbackVerticalOffset,
                VisualGap * scaleFactor,
                dynamicAnchorSeconds);
            RegisterWithRenderPriority(actor);
            return true;
        }

        internal void SetVerticalOffsetPixels(float offsetPixels)
        {
            if (follower != null)
                follower.SetVerticalOffsetPixels(offsetPixels);
        }

        internal void FollowAnimatedBody(
            SpriteRenderer primary, SpriteRenderer secondary, bool includeSecondary,
            Action prepareBody)
        {
            if (follower != null)
                follower.FollowAnimatedBody(primary, secondary, includeSecondary, prepareBody);
        }

        internal void Hide()
        {
            if (follower != null)
                follower.Hide();
        }

        internal void FadeInWhenActorVisible(float duration)
        {
            if (follower != null)
                follower.FadeInWhenActorVisible(duration);
        }

        internal bool CreateLevelEndSnapshot(Transform parent)
        {
            if (parent == null || labelRenderer == null ||
                follower == null || !follower.HasVisibleActor)
                return false;
            var source = labelRenderer.GetComponent<TextMeshPro>();
            if (source == null || !source.enabled ||
                !source.gameObject.activeInHierarchy ||
                source.color.a <= 0.01f)
                return false;

            var frozenObject = new GameObject(
                source.gameObject.name + "_Frozen");
            frozenObject.layer = source.gameObject.layer;
            var frozen = frozenObject.AddComponent<TextMeshPro>();
            frozen.text = source.text;
            frozen.font = source.font;
            frozen.fontSharedMaterial = source.fontSharedMaterial;
            frozen.fontSize = source.fontSize;
            frozen.fontStyle = source.fontStyle;
            frozen.alignment = source.alignment;
            frozen.enableWordWrapping = source.enableWordWrapping;
            frozen.richText = source.richText;
            frozen.isOrthographic = source.isOrthographic;
            frozen.margin = source.margin;
            frozen.color = source.color;
            frozen.outlineColor = source.outlineColor;
            frozen.outlineWidth = source.outlineWidth;
            frozen.rectTransform.sizeDelta =
                source.rectTransform.sizeDelta;
            frozen.rectTransform.pivot = source.rectTransform.pivot;
            frozen.rectTransform.SetParent(parent, false);
            frozen.rectTransform.position =
                source.rectTransform.position;
            frozen.rectTransform.rotation =
                source.rectTransform.rotation;
            frozen.rectTransform.localScale =
                source.rectTransform.lossyScale;

            var frozenRenderer = frozen.GetComponent<Renderer>();
            if (frozenRenderer != null)
            {
                frozenRenderer.sortingLayerID =
                    labelRenderer.sortingLayerID;
                frozenRenderer.sortingOrder =
                    labelRenderer.sortingOrder;
            }
            if (giftRenderer != null && giftRenderer.enabled &&
                giftRenderer.sprite != null &&
                giftRenderer.color.a > 0.01f)
            {
                var frozenGiftObject = new GameObject(
                    giftRenderer.gameObject.name + "_Frozen");
                frozenGiftObject.layer = giftRenderer.gameObject.layer;
                var frozenGift = frozenGiftObject.AddComponent<
                    SpriteRenderer>();
                frozenGift.sprite = giftRenderer.sprite;
                frozenGift.color = giftRenderer.color;
                frozenGift.flipX = giftRenderer.flipX;
                frozenGift.flipY = giftRenderer.flipY;
                frozenGift.sortingLayerID = giftRenderer.sortingLayerID;
                frozenGift.sortingOrder = giftRenderer.sortingOrder;
                frozenGift.transform.SetParent(parent, false);
                frozenGift.transform.position =
                    giftRenderer.transform.position;
                frozenGift.transform.rotation =
                    giftRenderer.transform.rotation;
                frozenGift.transform.localScale =
                    giftRenderer.transform.lossyScale;
            }
            if (follower != null)
                follower.SuppressGiftImageForLevelEnd();
            else if (giftRenderer != null)
                giftRenderer.enabled = false;
            source.enabled = false;
            return true;
        }

        internal static float GetLabelCameraScale()
        {
            // Names and gifts have one readable screen size across the catalog.
            // Actor scale may include a native prefab size or the aircraft
            // reduction; neither should shrink the label, including on handoff.
            var camera = CreatorToolsInteractionPresentation.FindGameplayCamera();
            return camera == null ? 1f : Mathf.Max(0.01f, camera.orthographicSize / 360f);
        }

        private static Color ResolveTextColor()
        {
            var level = Level.Current;
            return level != null &&
                AlternateTextColorLevels.Contains(level.CurrentLevel)
                    ? AlternateTextColor
                    : DefaultTextColor;
        }

        private void RegisterWithRenderPriority(GameObject actor)
        {
            if (actor == null || labelRenderer == null)
                return;
            var priority = actor.GetComponent<
                CreatorToolsInteractionRenderPriority>();
            if (priority != null)
            {
                priority.RegisterLabel(labelRenderer);
                if (giftRenderer != null)
                    priority.RegisterLabel(giftRenderer);
            }
        }

        private void MatchActorSorting(Renderer donorLabelRenderer)
        {
            if (donorLabelRenderer == null)
                return;

            Renderer reference = null;
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (candidate == null ||
                    candidate == donorLabelRenderer)
                    continue;
                if (reference == null ||
                    candidate.sortingOrder > reference.sortingOrder)
                    reference = candidate;
            }

            if (reference == null)
                return;
            donorLabelRenderer.sortingLayerID =
                reference.sortingLayerID;
            donorLabelRenderer.sortingOrder =
                reference.sortingOrder + 1;
        }

        private static TMP_FontAsset FindGameFont()
        {
            try
            {
                var font = FontLoader.GetTMPFont(
                    FontLoader.TMPFontType.
                        CupheadMemphis_Medium_merged__SDF);
                if (font != null)
                    return font;
            }
            catch
            {
            }

            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (var i = 0; i < fonts.Length; i++)
                if (fonts[i] != null && fonts[i].name.IndexOf(
                    "Memphis", StringComparison.OrdinalIgnoreCase) >= 0)
                    return fonts[i];
            return TMP_FontAsset.defaultFontAsset;
        }
    }

    internal sealed class CreatorToolsDonorLabelFollower : MonoBehaviour
    {
        private readonly CreatorToolsDonorLabelLifetime lifetime =
            new CreatorToolsDonorLabelLifetime();
        private Transform actorTransform;
        private SpriteRenderer actorRenderer;
        private SpriteRenderer[] actorRenderers;
        private SpriteRenderer secondaryBodyRenderer;
        private readonly Dictionary<Sprite, Vector2[]> spriteVertices =
            new Dictionary<Sprite, Vector2[]>();
        private bool followAnimatedBody;
        private bool includeSecondaryBody;
        private Action prepareBodyForAnchor;
        private float textBottomOffset;
        private float presentationScale = 1f;
        private TextMeshPro text;
        private SpriteRenderer giftRenderer;
        private float fallbackVerticalOffset;
        private float visualGap;
        private float additionalVerticalOffset;
        private Color originalColor;
        private Color originalGiftColor;
        private Color32 originalOutlineColor;
        private Vector4 giftVisibleTextMargin;
        private Vector3 actorOffset;
        private bool positioned;
        private bool rendererAnchorCaptured;
        private float dynamicAnchorRemaining;
        private float currentOpacity = 1f;
        private float fadeInDuration;
        private float fadeInElapsed;
        private bool waitingForActorVisibility;
        private bool fadingIn;
        private bool giftImagePreferenceVisible = true;
        private bool giftImageLifecycleSuppressed;

        internal bool HasVisibleActor
        {
            get { return lifetime.Opacity > 0.01f && ActorIsVisible(); }
        }

        internal void Initialize(
            Transform actorTransform,
            SpriteRenderer actorRenderer,
            TextMeshPro text,
            float fallbackVerticalOffset,
            float visualGap)
        {
            this.text = text;
            originalColor = text == null ? Color.white : text.color;
            originalOutlineColor = text == null
                ? new Color32(0, 0, 0, 0)
                : text.outlineColor;
            Rebind(
                actorTransform,
                actorRenderer,
                fallbackVerticalOffset,
                visualGap,
                0f);
        }

        internal void Rebind(
            Transform newActorTransform,
            SpriteRenderer newActorRenderer,
            float newFallbackVerticalOffset,
            float newVisualGap,
            float dynamicAnchorSeconds)
        {
            actorTransform = newActorTransform;
            actorRenderer = newActorRenderer;
            actorRenderers = newActorTransform == null ? null :
                newActorTransform.GetComponentsInChildren<SpriteRenderer>(true);
            followAnimatedBody = false;
            secondaryBodyRenderer = null;
            prepareBodyForAnchor = null;
            spriteVertices.Clear();
            fallbackVerticalOffset = newFallbackVerticalOffset;
            visualGap = newVisualGap;
            additionalVerticalOffset = 0f;
            actorOffset = Vector3.zero;
            positioned = false;
            rendererAnchorCaptured = false;
            dynamicAnchorRemaining = Mathf.Max(
                0f, dynamicAnchorSeconds);
            lifetime.Rebind();
            presentationScale = CreatorToolsDonorLabel.GetLabelCameraScale();
            ApplyPresentationScale();
            UpdatePosition();
            UpdateLifetime(0f);
        }

        internal void FollowAnimatedBody(
            SpriteRenderer primary, SpriteRenderer secondary, bool includeSecondary,
            Action prepareBody)
        {
            actorRenderer = primary;
            secondaryBodyRenderer = secondary;
            includeSecondaryBody = includeSecondary;
            prepareBodyForAnchor = prepareBody;
            followAnimatedBody = true;
            // Only the body can keep this name visible, never spawned bullets,
            // dust or pieces thrown far away from a multipart miniboss.
            actorRenderers = new[] { primary, secondary };
            positioned = false;
            MeasureTextBottomOffset();
            UpdatePosition();
            UpdateLifetime(0f);
        }

        internal void SetGiftRenderer(SpriteRenderer value)
        {
            giftRenderer = value;
            giftVisibleTextMargin = text == null
                ? Vector4.zero
                : text.margin;
            originalGiftColor = value == null
                ? Color.clear
                : value.color;
            ApplyOpacity(lifetime.Opacity);
            ApplyGiftRendererVisibility();
        }

        internal void SetGiftImagePreferenceVisible(bool visible)
        {
            giftImagePreferenceVisible = visible;
            ApplyGiftRendererVisibility();
        }

        internal void SuppressGiftImageForLevelEnd()
        {
            giftImageLifecycleSuppressed = true;
            ApplyGiftRendererVisibility();
        }

        internal void SetVerticalOffsetPixels(float offsetPixels)
        {
            UpdatePresentationScale();
            var scaledOffset = offsetPixels * presentationScale;
            if (positioned)
                actorOffset.y +=
                    scaledOffset - additionalVerticalOffset;
            additionalVerticalOffset = scaledOffset;
            rendererAnchorCaptured = false;
        }

        internal void Hide()
        {
            waitingForActorVisibility = false;
            fadingIn = false;
            fadeInElapsed = 0f;
            currentOpacity = 0f;
            ApplyOpacity(currentOpacity);
        }

        internal void FadeInWhenActorVisible(float duration)
        {
            fadeInDuration = Mathf.Max(0.01f, duration);
            fadeInElapsed = 0f;
            currentOpacity = 0f;
            waitingForActorVisibility = true;
            fadingIn = false;
            ApplyOpacity(currentOpacity);
        }

        private void LateUpdate()
        {
            if (text == null)
            {
                Destroy(gameObject);
                return;
            }
            if (actorTransform != null)
            {
                UpdatePresentationScale();
                UpdatePosition();
                UpdateFadeIn();
            }
            UpdateLifetime(Time.unscaledDeltaTime);
        }

        private void UpdatePosition()
        {
            if (followAnimatedBody)
            {
                UpdateAnimatedBodyPosition();
                return;
            }
            if ((!rendererAnchorCaptured ||
                 dynamicAnchorRemaining > 0f) &&
                actorRenderer != null &&
                actorRenderer.sprite != null && actorRenderer.enabled &&
                actorRenderer.gameObject.activeInHierarchy)
            {
                var bounds = actorRenderer.bounds;
                var anchor = new Vector3(
                    bounds.center.x,
                    bounds.max.y + visualGap +
                        additionalVerticalOffset,
                    bounds.center.z);
                actorOffset = anchor - actorTransform.position;
                positioned = true;
                if (dynamicAnchorRemaining <= 0f)
                    rendererAnchorCaptured = true;
            }
            else if (!positioned && actorTransform != null)
            {
                var anchor = actorTransform.TransformPoint(
                    new Vector3(0f, fallbackVerticalOffset, 0f));
                anchor.y += additionalVerticalOffset;
                actorOffset = anchor - actorTransform.position;
                positioned = true;
            }
            if (positioned && actorTransform != null)
                transform.position = actorTransform.position + actorOffset;
            transform.rotation = Quaternion.identity;

            if (dynamicAnchorRemaining <= 0f)
                return;
            var speed = Mathf.Max(0f, CupheadTime.GlobalSpeed);
            dynamicAnchorRemaining = Mathf.Max(
                0f,
                dynamicAnchorRemaining -
                Time.unscaledDeltaTime * speed);
            if (dynamicAnchorRemaining <= 0f && positioned)
                rendererAnchorCaptured = true;
        }

        private void UpdatePresentationScale()
        {
            var scale = CreatorToolsDonorLabel.GetLabelCameraScale();
            if (Mathf.Approximately(scale, presentationScale))
                return;
            var ratio = scale / presentationScale;
            var previousGap = visualGap + additionalVerticalOffset;
            visualGap *= ratio;
            additionalVerticalOffset *= ratio;
            if (positioned)
                actorOffset.y += visualGap + additionalVerticalOffset - previousGap;
            presentationScale = scale;
            ApplyPresentationScale();
        }

        private void ApplyPresentationScale()
        {
            if (text != null)
                text.rectTransform.localScale = new Vector3(
                    presentationScale, presentationScale, 1f);
        }

        private void UpdateAnimatedBodyPosition()
        {
            // Native turns can reset the body's scale in the same frame.
            // Restore it before measuring, regardless of LateUpdate order.
            if (prepareBodyForAnchor != null)
                prepareBodyForAnchor();
            Bounds bounds;
            var hasBody = TryGetVisualBounds(actorRenderer, out bounds);
            if (includeSecondaryBody || !hasBody)
            {
                Bounds secondaryBounds;
                if (TryGetVisualBounds(secondaryBodyRenderer, out secondaryBounds))
                {
                    if (hasBody)
                        bounds.Encapsulate(secondaryBounds);
                    else
                        bounds = secondaryBounds;
                    hasBody = true;
                }
            }
            if (!hasBody)
                return;

            // The TMP rect has a top pivot. Offset by the actual text bottom
            // so the readable gap is above the drawing, not inside the rect.
            var bottomOffset = textBottomOffset;
            if (giftRenderer != null && giftRenderer.enabled && giftRenderer.sprite != null)
                bottomOffset = Mathf.Max(bottomOffset,
                    -giftRenderer.transform.localPosition.y - giftRenderer.sprite.bounds.min.y);
            transform.position = new Vector3(bounds.center.x,
                bounds.max.y + visualGap + additionalVerticalOffset +
                    bottomOffset * presentationScale, bounds.center.z);
            transform.rotation = Quaternion.identity;
            positioned = true;
        }

        private void MeasureTextBottomOffset()
        {
            if (text == null || !followAnimatedBody)
                return;
            text.ForceMeshUpdate();
            textBottomOffset = 0f;
            // Include characters rendered by fallback font submeshes too.
            var info = text.textInfo;
            for (var i = 0; i < info.characterCount; i++)
                if (info.characterInfo[i].isVisible)
                    textBottomOffset = Mathf.Max(textBottomOffset,
                        -info.characterInfo[i].vertex_BL.position.y);
        }

        private bool TryGetVisualBounds(SpriteRenderer renderer, out Bounds bounds)
        {
            bounds = new Bounds();
            if (!RendererHasVisibleSprite(renderer))
                return false;
            var sprite = renderer.sprite;
            Vector2[] vertices;
            if (!spriteVertices.TryGetValue(sprite, out vertices))
            {
                // Cupcake frames have different pivots and a large transparent
                // canvas. Cache each trimmed mesh once, not its moving bounds.
                vertices = sprite.vertices;
                spriteVertices.Add(sprite, vertices);
            }
            if (vertices.Length == 0)
                return false;
            var spriteTransform = renderer.transform;
            var flipX = renderer.flipX ? -1f : 1f;
            var flipY = renderer.flipY ? -1f : 1f;
            for (var i = 0; i < vertices.Length; i++)
            {
                var point = spriteTransform.TransformPoint(new Vector3(
                    vertices[i].x * flipX, vertices[i].y * flipY, 0f));
                if (i == 0)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);
            }
            return true;
        }

        private void UpdateLifetime(float unscaledDeltaTime)
        {
            lifetime.Advance(actorTransform != null, ActorIsVisible(),
                currentOpacity, unscaledDeltaTime);
            ApplyOpacity(lifetime.Opacity);
            if (lifetime.Finished)
                Destroy(gameObject);
        }

        private void UpdateFadeIn()
        {
            if (waitingForActorVisibility)
            {
                if (!ActorIsVisible())
                    return;
                waitingForActorVisibility = false;
                fadingIn = true;
            }
            if (!fadingIn)
                return;

            var speed = Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (speed <= 0f)
                return;
            fadeInElapsed += Time.unscaledDeltaTime * speed;
            currentOpacity = Mathf.Clamp01(
                fadeInElapsed / fadeInDuration);
            if (currentOpacity >= 1f)
                fadingIn = false;
        }

        private bool ActorIsVisible()
        {
            if (actorTransform == null ||
                !actorTransform.gameObject.activeInHierarchy)
                return false;
            var camera = CreatorToolsInteractionPresentation.FindGameplayCamera();
            if (camera == null)
                return false;
            // Multi-part native actors can have an empty/disabled root sprite.
            // Test their actual visuals, never their still-existing root point.
            for (var i = 0; actorRenderers != null && i < actorRenderers.Length; i++)
            {
                var renderer = actorRenderers[i];
                if (followAnimatedBody)
                {
                    Bounds bounds;
                    if (TryGetVisualBounds(renderer, out bounds) &&
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 &&
                        BoundsAreVisible(bounds, camera))
                        return true;
                }
                else if (RendererIsVisible(renderer, camera))
                    return true;
            }
            return false;
        }

        private static bool RendererIsVisible(SpriteRenderer renderer, Camera camera)
        {
            if (!RendererHasVisibleSprite(renderer) ||
                (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                return false;
            return BoundsAreVisible(renderer.bounds, camera);
        }

        private static bool BoundsAreVisible(Bounds bounds, Camera camera)
        {
            var minimum = camera.WorldToViewportPoint(bounds.min);
            var maximum = camera.WorldToViewportPoint(bounds.max);
            return maximum.z >= 0f && maximum.x >= 0f &&
                minimum.x <= 1f && maximum.y >= 0f && minimum.y <= 1f;
        }

        private static bool RendererHasVisibleSprite(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy || renderer.color.a <= 0.01f)
                return false;
            // Native death fades can change the material alpha instead of the
            // SpriteRenderer tint. Read the current material without instancing it.
            var material = renderer.sharedMaterial;
            if (material != null && material.HasProperty("_Color") &&
                material.color.a <= 0.01f)
                return false;
            return true;
        }

        private void ApplyOpacity(float opacity)
        {
            var normalized = Mathf.Clamp01(opacity);
            if (text != null)
            {
                var color = originalColor;
                color.a *= normalized;
                text.color = color;

                var outline = originalOutlineColor;
                outline.a = (byte)Mathf.RoundToInt(
                    originalOutlineColor.a * normalized);
                text.outlineColor = outline;
            }
            if (giftRenderer != null)
            {
                var giftColor = originalGiftColor;
                giftColor.a *= normalized;
                giftRenderer.color = giftColor;
            }
        }

        private void ApplyGiftRendererVisibility()
        {
            var visible = giftRenderer != null &&
                giftImagePreferenceVisible &&
                !giftImageLifecycleSuppressed;
            if (giftRenderer != null)
                giftRenderer.enabled = visible;
            if (text != null)
                text.margin = visible
                    ? giftVisibleTextMargin
                    : Vector4.zero;
            MeasureTextBottomOffset();
        }
    }
}
