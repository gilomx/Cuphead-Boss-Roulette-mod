using System;
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
                labelText.color = new Color(1f, 0.94f, 0.76f, 1f);
                labelText.outlineColor = new Color32(20, 15, 10, 235);
                labelText.outlineWidth = 0.18f;
                labelText.rectTransform.sizeDelta = new Vector2(
                    LabelWidth, LabelHeight);
                labelText.rectTransform.pivot = new Vector2(0.5f, 1f);

                var actorRenderer = anchorRenderer == null
                    ? GetComponent<SpriteRenderer>()
                    : anchorRenderer;
                MatchActorSorting(labelText.GetComponent<Renderer>());
                labelTransform.localScale = transform.lossyScale;
                labelTransform.rotation = Quaternion.identity;

                var follower = labelObject.AddComponent<
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
            }
            catch
            {
                if (labelObject != null)
                    Destroy(labelObject);
                throw;
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
        private const float FadeDuration = 0.6f;

        private Transform actorTransform;
        private SpriteRenderer actorRenderer;
        private TextMeshPro text;
        private float fallbackVerticalOffset;
        private float visualGap;
        private float fadeElapsed;
        private Color originalColor;
        private Color32 originalOutlineColor;
        private Vector3 actorOffset;
        private bool positioned;
        private bool rendererAnchorCaptured;

        internal void Initialize(
            Transform actorTransform,
            SpriteRenderer actorRenderer,
            TextMeshPro text,
            float fallbackVerticalOffset,
            float visualGap)
        {
            this.actorTransform = actorTransform;
            this.actorRenderer = actorRenderer;
            this.text = text;
            this.fallbackVerticalOffset = fallbackVerticalOffset;
            this.visualGap = visualGap;
            originalColor = text == null ? Color.white : text.color;
            originalOutlineColor = text == null
                ? new Color32(0, 0, 0, 0)
                : text.outlineColor;
            UpdatePosition();
        }

        private void LateUpdate()
        {
            if (actorTransform != null)
            {
                UpdatePosition();
                return;
            }
            UpdateFade();
        }

        private void UpdatePosition()
        {
            if (!rendererAnchorCaptured && actorRenderer != null &&
                actorRenderer.sprite != null && actorRenderer.enabled &&
                actorRenderer.gameObject.activeInHierarchy)
            {
                var bounds = actorRenderer.bounds;
                var anchor = new Vector3(
                    bounds.center.x,
                    bounds.max.y + visualGap,
                    bounds.center.z);
                actorOffset = anchor - actorTransform.position;
                positioned = true;
                rendererAnchorCaptured = true;
            }
            else if (!positioned && actorTransform != null)
            {
                var anchor = actorTransform.TransformPoint(
                    new Vector3(0f, fallbackVerticalOffset, 0f));
                actorOffset = anchor - actorTransform.position;
                positioned = true;
            }
            if (positioned && actorTransform != null)
                transform.position = actorTransform.position + actorOffset;
            transform.rotation = Quaternion.identity;
        }

        private void UpdateFade()
        {
            if (text == null)
            {
                Destroy(gameObject);
                return;
            }

            var speed = Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (speed <= 0f)
                return;
            fadeElapsed += Time.unscaledDeltaTime * speed;
            var opacity = 1f - Mathf.Clamp01(
                fadeElapsed / FadeDuration);

            var color = originalColor;
            color.a *= opacity;
            text.color = color;

            var outline = originalOutlineColor;
            outline.a = (byte)Mathf.RoundToInt(
                originalOutlineColor.a * opacity);
            text.outlineColor = outline;

            if (opacity <= 0f)
                Destroy(gameObject);
        }
    }
}
