using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private GameObject nativeRoulettePrompt;
        private Text nativeRoulettePromptAction;
        private Text nativeRoulettePromptKey;
        private Image nativeRoulettePromptKeyBackground;
        private Image nativeRouletteDimOverlay;
        private RectTransform nativeRoulettePromptActionRect;
        private RectTransform nativeRoulettePromptKeyRect;
        private RectTransform nativeRoulettePromptKeyTextRect;
        private RectTransform nativeRoulettePromptKeyBackgroundRect;
        private GameObject nativeChallengeCanvas;
        private GameObject nativeChallengePrompt;
        private Text nativeChallengePromptAction;
        private RectTransform nativeChallengePromptActionRect;

        private void LateUpdate()
        {
            UpdateNativeChallengePrompt();
            UpdateNativeRoulettePrompt();
        }

        private void UpdateNativeRoulettePrompt()
        {
            var canUse = CanUseRouletteOnMap();
            var showOpen = canUse && !visible && cardVisibility <= 0.001f;
            var showReroll = canUse && visible && !autoLoad.Value && resultReady &&
                             !running && !pendingLoad;
            var shouldShow = showOpen || showReroll;
            var needsNativeLayer = canUse &&
                                   (shouldShow || cardVisibility > 0.001f);

            if (nativeRoulettePrompt == null)
            {
                if (!needsNativeLayer || !TryCreateNativeRoulettePrompt())
                    return;
            }

            UpdateNativeRouletteDimOverlay(
                canUse && cardVisibility > 0.001f);

            if (!shouldShow)
            {
                if (nativeRoulettePrompt.activeSelf)
                    nativeRoulettePrompt.SetActive(false);
                return;
            }

            var action = showReroll ? "VOLVER A GIRAR" : "ABRIR RULETA";
            var key = showReroll ? "F7" : "F6";
            if (!nativeRoulettePrompt.activeSelf)
                nativeRoulettePrompt.SetActive(true);
            ApplyNativeRoulettePromptText(action, key, true);
        }

        private void UpdateNativeRouletteDimOverlay(bool visibleNow)
        {
            if (nativeRouletteDimOverlay == null)
                return;
            if (nativeRouletteDimOverlay.gameObject.activeSelf != visibleNow)
                nativeRouletteDimOverlay.gameObject.SetActive(visibleNow);
            if (visibleNow)
                nativeRouletteDimOverlay.color =
                    new Color(0f, 0f, 0f, 0.66f * Mathf.Clamp01(cardVisibility));
        }

        private bool TryCreateNativeRoulettePrompt()
        {
            var glyphs = Resources.FindObjectsOfTypeAll<CupheadGlyph>();
            CupheadGlyph templateGlyph = null;
            for (var i = 0; i < glyphs.Length; i++)
            {
                var glyph = glyphs[i];
                if (glyph == null || !glyph.gameObject.scene.IsValid())
                    continue;

                var glyphTransform = glyph.transform;
                var help = glyphTransform.parent;
                var background = help == null ? null : help.parent;
                var pauseGui = background == null ? null : background.parent;
                if (glyphTransform.name == "Glyph (2)" &&
                    help != null && help.name == "Help (2)" &&
                    background != null && background.name == "Background" &&
                    pauseGui != null && pauseGui.name == "PauseGUI")
                {
                    templateGlyph = glyph;
                    break;
                }
            }

            if (templateGlyph == null)
                return false;

            var templateRoot = templateGlyph.transform.parent;
            var screenCanvas = templateRoot.parent.parent.parent;

            var dimObject = new GameObject("Gilomx Roulette Dim Overlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimObject.transform.SetParent(screenCanvas, false);
            var dimRect = dimObject.transform as RectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            nativeRouletteDimOverlay = dimObject.GetComponent<Image>();
            nativeRouletteDimOverlay.raycastTarget = false;
            nativeRouletteDimOverlay.color = Color.clear;
            dimObject.transform.SetAsLastSibling();

            nativeRoulettePrompt = Instantiate(templateRoot.gameObject);
            nativeRoulettePrompt.name = "Gilomx Roulette Native Prompt";
            nativeRoulettePrompt.transform.SetParent(screenCanvas, false);
            nativeRoulettePrompt.transform.SetAsLastSibling();

            SetDirectChildActive(nativeRoulettePrompt.transform, "Text", false);
            SetDirectChildActive(nativeRoulettePrompt.transform, "Glyph (1)", false);

            var actionTransform = FindDirectChild(nativeRoulettePrompt.transform, "Text (1)");
            var keyTransform = FindDirectChild(nativeRoulettePrompt.transform, "Glyph (2)");
            if (actionTransform == null || keyTransform == null)
            {
                DestroyNativeRoulettePrompt();
                return false;
            }

            var glyphComponents = keyTransform.GetComponentsInChildren<CupheadGlyph>(true);
            for (var i = 0; i < glyphComponents.Length; i++)
                glyphComponents[i].enabled = false;

            nativeRoulettePromptAction = actionTransform.GetComponent<Text>();
            nativeRoulettePromptActionRect = actionTransform as RectTransform;
            nativeRoulettePromptKeyRect = keyTransform as RectTransform;

            var keyBackgroundTransform = FindDirectChild(keyTransform, "BGText");
            var keyTextTransform = FindDirectChild(keyTransform, "Text");
            var keyCharBackgroundTransform = FindDirectChild(keyTransform, "BGChar");
            var keyCharTransform = FindDirectChild(keyTransform, "Char");
            if (keyBackgroundTransform == null || keyTextTransform == null ||
                nativeRoulettePromptAction == null || nativeRoulettePromptActionRect == null ||
                nativeRoulettePromptKeyRect == null)
            {
                DestroyNativeRoulettePrompt();
                return false;
            }

            if (keyCharBackgroundTransform != null)
                keyCharBackgroundTransform.gameObject.SetActive(false);
            if (keyCharTransform != null)
                keyCharTransform.gameObject.SetActive(false);
            keyBackgroundTransform.gameObject.SetActive(true);
            keyTextTransform.gameObject.SetActive(true);

            nativeRoulettePromptKeyBackground = keyBackgroundTransform.GetComponent<Image>();
            nativeRoulettePromptKey = keyTextTransform.GetComponent<Text>();
            nativeRoulettePromptKeyBackgroundRect =
                keyBackgroundTransform as RectTransform;
            nativeRoulettePromptKeyTextRect = keyTextTransform as RectTransform;
            if (nativeRoulettePromptKeyBackground == null ||
                nativeRoulettePromptKey == null ||
                nativeRoulettePromptKeyBackgroundRect == null ||
                nativeRoulettePromptKeyTextRect == null)
            {
                DestroyNativeRoulettePrompt();
                return false;
            }

            nativeRoulettePromptKeyBackground.type = Image.Type.Sliced;
            nativeRoulettePrompt.SetActive(false);
            return true;
        }

        private void ApplyNativeRoulettePromptText(string action, string key, bool showKey)
        {
            nativeRoulettePromptAction.text = action;
            nativeRoulettePromptKey.text = key;
            if (nativeRoulettePromptKeyRect.gameObject.activeSelf != showKey)
                nativeRoulettePromptKeyRect.gameObject.SetActive(showKey);

            Canvas.ForceUpdateCanvases();
            const float keyRight = 1290f;
            var actionPosition = nativeRoulettePromptActionRect.anchoredPosition;
            if (!showKey)
            {
                actionPosition.x = keyRight;
                nativeRoulettePromptActionRect.anchoredPosition = actionPosition;
                nativeRoulettePromptActionRect.sizeDelta =
                    new Vector2(Mathf.Ceil(nativeRoulettePromptAction.preferredWidth + 6f),
                        nativeRoulettePromptActionRect.sizeDelta.y);
                return;
            }

            const float minimumKeyWidth = 30f;
            const float keyPadding = 2.5f;
            const float textToKeyGap = 4.5f;
            var keyWidth = Mathf.Max(minimumKeyWidth,
                Mathf.Ceil(nativeRoulettePromptKey.preferredWidth * 1.1f + keyPadding));
            nativeRoulettePromptKeyTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            nativeRoulettePromptKeyTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            nativeRoulettePromptKeyTextRect.pivot = new Vector2(0.5f, 0.5f);
            nativeRoulettePromptKeyTextRect.anchoredPosition =
                new Vector2(-10f, -0.3f);
            var keyPosition = nativeRoulettePromptKeyRect.anchoredPosition;
            keyPosition.x = keyRight - keyWidth;
            nativeRoulettePromptKeyRect.anchoredPosition = keyPosition;
            nativeRoulettePromptKeyRect.sizeDelta =
                new Vector2(keyWidth, nativeRoulettePromptKeyRect.sizeDelta.y);
            nativeRoulettePromptKeyBackgroundRect.sizeDelta =
                new Vector2(keyWidth, nativeRoulettePromptKeyBackgroundRect.sizeDelta.y);
            nativeRoulettePromptKeyTextRect.sizeDelta =
                new Vector2(keyWidth, nativeRoulettePromptKeyTextRect.sizeDelta.y);

            actionPosition.x = keyPosition.x - textToKeyGap;
            nativeRoulettePromptActionRect.anchoredPosition = actionPosition;
            nativeRoulettePromptActionRect.sizeDelta =
                new Vector2(Mathf.Ceil(nativeRoulettePromptAction.preferredWidth + 6f),
                    nativeRoulettePromptActionRect.sizeDelta.y);
        }

        private bool PrepareNativeChallengePrompt()
        {
            if (nativeChallengePrompt != null)
                return true;
            if (string.IsNullOrEmpty(activeChallenge))
                return false;
            if (nativeRoulettePrompt == null && !TryCreateNativeRoulettePrompt())
                return false;

            var sourceCanvas = nativeRoulettePrompt.GetComponentInParent<Canvas>();
            var sourceScaler = sourceCanvas == null
                ? null
                : sourceCanvas.GetComponent<CanvasScaler>();

            nativeChallengeCanvas = new GameObject(
                "Gilomx Persistent Challenge Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = nativeChallengeCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sourceCanvas == null
                ? 100
                : sourceCanvas.sortingOrder + 100;
            if (sourceCanvas != null)
            {
                canvas.pixelPerfect = sourceCanvas.pixelPerfect;
                canvas.targetDisplay = sourceCanvas.targetDisplay;
            }

            var scaler = nativeChallengeCanvas.GetComponent<CanvasScaler>();
            if (sourceScaler != null)
            {
                scaler.uiScaleMode = sourceScaler.uiScaleMode;
                scaler.referenceResolution = sourceScaler.referenceResolution;
                scaler.screenMatchMode = sourceScaler.screenMatchMode;
                scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
                scaler.scaleFactor = sourceScaler.scaleFactor;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1366f, 768f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
            }

            UnityEngine.Object.DontDestroyOnLoad(nativeChallengeCanvas);
            nativeChallengePrompt = Instantiate(nativeRoulettePrompt);
            nativeChallengePrompt.name = "Gilomx Persistent Challenge Prompt";
            nativeChallengePrompt.transform.SetParent(nativeChallengeCanvas.transform, false);
            nativeChallengePrompt.transform.SetAsLastSibling();

            SetDirectChildActive(nativeChallengePrompt.transform, "Text", false);
            SetDirectChildActive(nativeChallengePrompt.transform, "Glyph (1)", false);
            SetDirectChildActive(nativeChallengePrompt.transform, "Glyph (2)", false);
            var glyphs = nativeChallengePrompt.GetComponentsInChildren<CupheadGlyph>(true);
            for (var i = 0; i < glyphs.Length; i++)
                glyphs[i].enabled = false;

            var actionTransform =
                FindDirectChild(nativeChallengePrompt.transform, "Text (1)");
            if (actionTransform == null)
            {
                DestroyNativeChallengePrompt();
                return false;
            }

            nativeChallengePromptAction = actionTransform.GetComponent<Text>();
            nativeChallengePromptActionRect = actionTransform as RectTransform;
            if (nativeChallengePromptAction == null ||
                nativeChallengePromptActionRect == null)
            {
                DestroyNativeChallengePrompt();
                return false;
            }

            nativeChallengePrompt.SetActive(false);
            return true;
        }

        private void UpdateNativeChallengePrompt()
        {
            var shouldShow = ShouldShowActiveChallenge();
            if (!shouldShow || nativeChallengePrompt == null)
            {
                SetNativeChallengePromptVisible(false);
                return;
            }

            SetNativeChallengePromptVisible(true);
            ApplyNativeChallengePromptText(
                "RETO: " + activeChallenge.ToUpperInvariant());
        }

        private void ApplyNativeChallengePromptText(string text)
        {
            nativeChallengePromptAction.text = text;
            Canvas.ForceUpdateCanvases();
            const float promptRight = 1290f;
            var actionPosition =
                nativeChallengePromptActionRect.anchoredPosition;
            actionPosition.x = promptRight;
            nativeChallengePromptActionRect.anchoredPosition = actionPosition;
            nativeChallengePromptActionRect.sizeDelta =
                new Vector2(
                    Mathf.Ceil(nativeChallengePromptAction.preferredWidth + 6f),
                    nativeChallengePromptActionRect.sizeDelta.y);
        }

        private void SetNativeChallengePromptVisible(bool visibleNow)
        {
            if (nativeChallengePrompt != null &&
                nativeChallengePrompt.activeSelf != visibleNow)
                nativeChallengePrompt.SetActive(visibleNow);
        }

        private void DestroyNativeChallengePrompt()
        {
            if (nativeChallengeCanvas != null)
                Destroy(nativeChallengeCanvas);
            nativeChallengeCanvas = null;
            nativeChallengePrompt = null;
            nativeChallengePromptAction = null;
            nativeChallengePromptActionRect = null;
        }
        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }
            return null;
        }

        private static void SetDirectChildActive(Transform parent, string name, bool active)
        {
            var child = FindDirectChild(parent, name);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        private void DestroyNativeRoulettePrompt()
        {
            if (nativeRoulettePrompt != null)
                Destroy(nativeRoulettePrompt);
            if (nativeRouletteDimOverlay != null)
                Destroy(nativeRouletteDimOverlay.gameObject);
            nativeRoulettePrompt = null;
            nativeRoulettePromptAction = null;
            nativeRoulettePromptKey = null;
            nativeRoulettePromptKeyBackground = null;
            nativeRouletteDimOverlay = null;
            nativeRoulettePromptActionRect = null;
            nativeRoulettePromptKeyRect = null;
            nativeRoulettePromptKeyTextRect = null;
            nativeRoulettePromptKeyBackgroundRect = null;
        }
    }
}
