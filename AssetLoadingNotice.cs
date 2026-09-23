using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private Image assetLoadingClock;
        private Text assetLoadingNotice;
        private bool preparingInteractionAssets;
        private bool preparingTimedVisualAssets;

        private void UpdateAssetLoadingNotice()
        {
            var show = SceneLoader.CurrentlyLoading &&
                (preparingInteractionAssets || preparingTimedVisualAssets) &&
                assetLoadingClock != null && assetLoadingClock.isActiveAndEnabled &&
                assetLoadingClock.color.a > 0.001f;
            if (!show)
            {
                if (assetLoadingNotice != null && assetLoadingNotice.gameObject.activeSelf)
                    assetLoadingNotice.gameObject.SetActive(false);
                return;
            }

            if (assetLoadingNotice != null && assetLoadingNotice.transform.parent != assetLoadingClock.transform)
                DestroyAssetLoadingNotice();
            if (assetLoadingNotice == null)
            {
                // Reuse an already loaded game font. Creating this label must
                // not trigger another asset scan or load during preparation.
                if (theme == null || theme.BodyFont == null) return;
                var obj = new GameObject("PichiAssetLoadingNotice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                obj.layer = assetLoadingClock.gameObject.layer;
                obj.transform.SetParent(assetLoadingClock.transform, false);
                assetLoadingNotice = obj.GetComponent<Text>();
                assetLoadingNotice.font = theme.BodyFont;
                assetLoadingNotice.fontSize = 22;
                assetLoadingNotice.fontStyle = FontStyle.Normal;
                assetLoadingNotice.alignment = TextAnchor.LowerRight;
                assetLoadingNotice.supportRichText = false;
                assetLoadingNotice.raycastTarget = false;
                assetLoadingNotice.resizeTextForBestFit = false;
                assetLoadingNotice.horizontalOverflow = HorizontalWrapMode.Wrap;
                assetLoadingNotice.verticalOverflow = VerticalWrapMode.Truncate;
                // Anchor to the actual clock, not screen pixels or a gameplay
                // canvas: the native loader owns its scale, camera and layer.
                // Keep the text's lower edge at the clock's lower edge.
                var rect = assetLoadingNotice.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-18f, 0f);
                rect.sizeDelta = new Vector2(380f, 70f);
            }

            var message = L(ModText.LoadingInteractions);
            if (assetLoadingNotice.text != message) assetLoadingNotice.text = message;
            // Image alpha does not propagate to children. Match the native
            // clock explicitly so the notice cannot precede its fade-in.
            assetLoadingNotice.color = new Color(1f, 1f, 1f, assetLoadingClock.color.a);
            if (!assetLoadingNotice.gameObject.activeSelf) assetLoadingNotice.gameObject.SetActive(true);
        }

        private void DestroyAssetLoadingNotice()
        {
            if (assetLoadingNotice != null) Destroy(assetLoadingNotice.gameObject);
            assetLoadingNotice = null;
        }
    }
}
