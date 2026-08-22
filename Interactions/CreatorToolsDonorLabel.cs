using System;
using TMPro;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsDonorLabel : MonoBehaviour
    {
        private const float VerticalOffset = 86f;
        private const float LabelWidth = 320f;
        private const float LabelHeight = 48f;

        private GameObject labelObject;
        private Transform labelTransform;

        internal void Initialize(string value)
        {
            var donor = string.IsNullOrEmpty(value)
                ? string.Empty
                : value.ToUpperInvariant();
            if (donor.Length == 0)
                return;

            labelObject = new GameObject("CreatorTools_DonorLabel");
            labelObject.layer = gameObject.layer;
            labelTransform = labelObject.transform;
            labelTransform.SetParent(transform, false);

            var text = labelObject.AddComponent<TextMeshPro>();
            text.font = FindGameFont();
            text.text = donor;
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;
            text.isOrthographic = true;
            text.color = new Color(1f, 0.94f, 0.76f, 1f);
            text.outlineColor = new Color32(20, 15, 10, 235);
            text.outlineWidth = 0.18f;
            text.rectTransform.sizeDelta = new Vector2(
                LabelWidth, LabelHeight);

            MatchActorSorting(text.GetComponent<Renderer>());
            UpdatePosition();
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void OnEnable()
        {
            if (labelObject != null)
                labelObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (labelObject != null)
                labelObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (labelObject != null)
                Destroy(labelObject);
            labelObject = null;
            labelTransform = null;
        }

        private void UpdatePosition()
        {
            if (labelTransform == null)
                return;
            labelTransform.localPosition = new Vector3(
                0f, VerticalOffset, 0f);
            labelTransform.localRotation = Quaternion.identity;
            labelTransform.localScale = Vector3.one;
        }

        private void MatchActorSorting(Renderer labelRenderer)
        {
            if (labelRenderer == null)
                return;

            Renderer reference = null;
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (candidate == null || candidate == labelRenderer)
                    continue;
                if (reference == null ||
                    candidate.sortingOrder > reference.sortingOrder)
                    reference = candidate;
            }

            if (reference == null)
                return;
            labelRenderer.sortingLayerID = reference.sortingLayerID;
            labelRenderer.sortingOrder = reference.sortingOrder + 1;
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
}
