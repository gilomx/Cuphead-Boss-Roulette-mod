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
        private static readonly Color32 DefaultTextColor =
            new Color32(255, 240, 194, 255);
        private static readonly Color32 AlternateTextColor =
            new Color32(24, 20, 17, 255);
        // Add Levels values here after the alternate-color boss list is
        // approved. An empty set deliberately preserves today's presentation.
        private static readonly HashSet<Levels> AlternateTextColorLevels =
            new HashSet<Levels>();
        private CreatorToolsDonorLabelFollower follower;
        private Renderer labelRenderer;

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

                var labelText = labelObject.AddComponent<TextMeshPro>();
                // TextMeshPro replaces the GameObject's Transform with a
                // RectTransform in this Unity version. Resolve it afterwards.
                var labelTransform = labelText.rectTransform;
                labelText.font = FindGameFont();
                labelText.text = donor;
                labelText.fontSize = 22f;
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
                labelTransform.localScale = AbsoluteScale(
                    transform.lossyScale);
                labelTransform.rotation = Quaternion.identity;

                follower = labelObject.AddComponent<
                    CreatorToolsDonorLabelFollower>();
                var cameraScale = GetComponent<
                    CreatorToolsInteractionCameraScale>();
                var scaleFactor = cameraScale == null
                    ? 1f
                    : Mathf.Max(0.01f, cameraScale.Factor);
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

        internal bool RebindTo(
            GameObject actor,
            SpriteRenderer anchorRenderer,
            float dynamicAnchorSeconds)
        {
            if (actor == null || follower == null)
                return false;

            var cameraScale = actor.GetComponent<
                CreatorToolsInteractionCameraScale>();
            var scaleFactor = cameraScale == null
                ? 1f
                : Mathf.Max(0.01f, cameraScale.Factor);
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
            if (parent == null || labelRenderer == null)
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
            source.enabled = false;
            return true;
        }

        private static Vector3 AbsoluteScale(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
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
                priority.RegisterLabel(labelRenderer);
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
        private const float FadeDuration = 0.6f;

        private Transform actorTransform;
        private SpriteRenderer actorRenderer;
        private TextMeshPro text;
        private float fallbackVerticalOffset;
        private float visualGap;
        private float additionalVerticalOffset;
        private float fadeElapsed;
        private Color originalColor;
        private Color32 originalOutlineColor;
        private Vector3 actorOffset;
        private bool positioned;
        private bool rendererAnchorCaptured;
        private float dynamicAnchorRemaining;
        private float currentOpacity = 1f;
        private float fadeInDuration;
        private float fadeInElapsed;
        private float fadeOutStartOpacity = 1f;
        private bool waitingForActorVisibility;
        private bool fadingIn;
        private bool fadeOutStarted;

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
            fallbackVerticalOffset = newFallbackVerticalOffset;
            visualGap = newVisualGap;
            additionalVerticalOffset = 0f;
            actorOffset = Vector3.zero;
            positioned = false;
            rendererAnchorCaptured = false;
            dynamicAnchorRemaining = Mathf.Max(
                0f, dynamicAnchorSeconds);
            fadeOutStarted = false;
            fadeElapsed = 0f;
            if (text != null && actorTransform != null)
                text.rectTransform.localScale = new Vector3(
                    Mathf.Abs(actorTransform.lossyScale.x),
                    Mathf.Abs(actorTransform.lossyScale.y),
                    Mathf.Abs(actorTransform.lossyScale.z));
            UpdatePosition();
        }

        internal void SetVerticalOffsetPixels(float offsetPixels)
        {
            var cameraScale = actorTransform == null
                ? null
                : actorTransform.GetComponent<
                    CreatorToolsInteractionCameraScale>();
            var scaleFactor = cameraScale == null
                ? 1f
                : Mathf.Max(0.01f, cameraScale.Factor);
            var scaledOffset = offsetPixels * scaleFactor;
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
            if (actorTransform != null)
            {
                UpdatePosition();
                UpdateFadeIn();
                return;
            }
            UpdateFade();
        }

        private void UpdatePosition()
        {
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

        private void UpdateFade()
        {
            if (text == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!fadeOutStarted)
            {
                fadeOutStarted = true;
                fadeElapsed = 0f;
                fadeOutStartOpacity = currentOpacity;
                waitingForActorVisibility = false;
                fadingIn = false;
            }

            var speed = Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (speed <= 0f)
                return;
            fadeElapsed += Time.unscaledDeltaTime * speed;
            currentOpacity = fadeOutStartOpacity *
                (1f - Mathf.Clamp01(fadeElapsed / FadeDuration));
            ApplyOpacity(currentOpacity);

            if (currentOpacity <= 0f)
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
            ApplyOpacity(currentOpacity);
            if (currentOpacity >= 1f)
                fadingIn = false;
        }

        private bool ActorIsVisible()
        {
            if (actorTransform == null)
                return false;
            var camera = Camera.main;
            if (camera == null || !camera.enabled)
                return false;
            if (actorRenderer == null || actorRenderer.sprite == null ||
                !actorRenderer.enabled ||
                !actorRenderer.gameObject.activeInHierarchy)
            {
                var point = camera.WorldToViewportPoint(
                    actorTransform.position);
                return point.z >= 0f && point.x >= 0f && point.x <= 1f &&
                    point.y >= 0f && point.y <= 1f;
            }

            var bounds = actorRenderer.bounds;
            var minimum = camera.WorldToViewportPoint(bounds.min);
            var maximum = camera.WorldToViewportPoint(bounds.max);
            return maximum.z >= 0f && maximum.x >= 0f &&
                minimum.x <= 1f && maximum.y >= 0f && minimum.y <= 1f;
        }

        private void ApplyOpacity(float opacity)
        {
            if (text == null)
                return;
            var normalized = Mathf.Clamp01(opacity);
            var color = originalColor;
            color.a *= normalized;
            text.color = color;

            var outline = originalOutlineColor;
            outline.a = (byte)Mathf.RoundToInt(
                originalOutlineColor.a * normalized);
            text.outlineColor = outline;
        }
    }
}
