using HarmonyLib;
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
        private CupheadGlyph nativeRoulettePromptKeyGlyph;
        private RectTransform nativeRoulettePromptModifierRect;
        private Text nativeRoulettePromptModifier;
        private RectTransform nativeRoulettePromptModifierTextRect;
        private RectTransform nativeRoulettePromptModifierBackgroundRect;
        private Text nativeRoulettePromptComboSeparator;
        private RectTransform nativeRoulettePromptComboSeparatorRect;
        private string nativeRoulettePromptLayoutToken;
        private GameObject nativeChallengeCanvas;
        private GameObject nativeChallengePrompt;
        private Text nativeChallengePromptAction;
        private RectTransform nativeChallengePromptActionRect;

        private void LateUpdate()
        {
            UpdateBattleResultHud();
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

            var action = showReroll
                ? L(ModText.ActionSpinAgain)
                : L(ModText.ActionOpenRoulette);
            var key = showReroll ? "F7" : "F6";
            int rewiredPlayerId;
            string leftTrigger;
            string rightTrigger;
            var controllerMode = TryGetControllerPromptInfo(
                out rewiredPlayerId, out leftTrigger, out rightTrigger);
            if (!nativeRoulettePrompt.activeSelf)
                nativeRoulettePrompt.SetActive(true);
            ApplyNativeRoulettePrompt(
                action, key, showReroll, controllerMode, rewiredPlayerId,
                leftTrigger, rightTrigger);
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
            var separatorTransform = FindDirectChild(nativeRoulettePrompt.transform, "Text");
            var modifierTransform = FindDirectChild(nativeRoulettePrompt.transform, "Glyph (1)");
            if (actionTransform == null || keyTransform == null ||
                separatorTransform == null || modifierTransform == null)
            {
                DestroyNativeRoulettePrompt();
                return false;
            }

            var glyphComponents = keyTransform.GetComponentsInChildren<CupheadGlyph>(true);
            for (var i = 0; i < glyphComponents.Length; i++)
                glyphComponents[i].enabled = false;
            nativeRoulettePromptKeyGlyph = glyphComponents.Length > 0
                ? glyphComponents[0]
                : null;
            var modifierGlyphs =
                modifierTransform.GetComponentsInChildren<CupheadGlyph>(true);
            for (var i = 0; i < modifierGlyphs.Length; i++)
                modifierGlyphs[i].enabled = false;

            nativeRoulettePromptAction = actionTransform.GetComponent<Text>();
            nativeRoulettePromptActionRect = actionTransform as RectTransform;
            var actionLocalization =
                actionTransform.GetComponents<LocalizationHelper>();
            for (var i = 0; i < actionLocalization.Length; i++)
                actionLocalization[i].enabled = false;
            nativeRoulettePromptKeyRect = keyTransform as RectTransform;
            nativeRoulettePromptComboSeparator =
                separatorTransform.GetComponent<Text>();
            nativeRoulettePromptComboSeparatorRect =
                separatorTransform as RectTransform;
            nativeRoulettePromptModifierRect =
                modifierTransform as RectTransform;

            // The cloned Help row normally orders these as
            // CONFIRM + glyph, then action + glyph. Keep its native layout,
            // but reorder the children into action + modifier + separator + glyph.
            actionTransform.SetSiblingIndex(0);
            modifierTransform.SetSiblingIndex(1);
            separatorTransform.SetSiblingIndex(2);
            keyTransform.SetSiblingIndex(3);
            var separatorBehaviours =
                separatorTransform.GetComponents<MonoBehaviour>();
            for (var i = 0; i < separatorBehaviours.Length; i++)
                if (!(separatorBehaviours[i] is Text))
                    separatorBehaviours[i].enabled = false;

            var keyBackgroundTransform = FindDirectChild(keyTransform, "BGText");
            var keyTextTransform = FindDirectChild(keyTransform, "Text");
            var keyCharBackgroundTransform = FindDirectChild(keyTransform, "BGChar");
            var keyCharTransform = FindDirectChild(keyTransform, "Char");
            var modifierBackgroundTransform =
                FindDirectChild(modifierTransform, "BGText");
            var modifierTextTransform =
                FindDirectChild(modifierTransform, "Text");
            var modifierCharBackgroundTransform =
                FindDirectChild(modifierTransform, "BGChar");
            var modifierCharTransform =
                FindDirectChild(modifierTransform, "Char");
            if (keyBackgroundTransform == null || keyTextTransform == null ||
                modifierBackgroundTransform == null || modifierTextTransform == null ||
                nativeRoulettePromptAction == null || nativeRoulettePromptActionRect == null ||
                nativeRoulettePromptKeyRect == null ||
                nativeRoulettePromptComboSeparator == null ||
                nativeRoulettePromptComboSeparatorRect == null ||
                nativeRoulettePromptModifierRect == null)
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
            if (modifierCharBackgroundTransform != null)
                modifierCharBackgroundTransform.gameObject.SetActive(false);
            if (modifierCharTransform != null)
                modifierCharTransform.gameObject.SetActive(false);
            modifierBackgroundTransform.gameObject.SetActive(true);
            modifierTextTransform.gameObject.SetActive(true);

            nativeRoulettePromptKeyBackground = keyBackgroundTransform.GetComponent<Image>();
            nativeRoulettePromptKey = keyTextTransform.GetComponent<Text>();
            nativeRoulettePromptKeyBackgroundRect =
                keyBackgroundTransform as RectTransform;
            nativeRoulettePromptKeyTextRect = keyTextTransform as RectTransform;
            nativeRoulettePromptModifier =
                modifierTextTransform.GetComponent<Text>();
            nativeRoulettePromptModifierBackgroundRect =
                modifierBackgroundTransform as RectTransform;
            nativeRoulettePromptModifierTextRect =
                modifierTextTransform as RectTransform;
            if (nativeRoulettePromptKeyBackground == null ||
                nativeRoulettePromptKey == null ||
                nativeRoulettePromptKeyBackgroundRect == null ||
                nativeRoulettePromptKeyTextRect == null ||
                nativeRoulettePromptModifier == null ||
                nativeRoulettePromptModifierBackgroundRect == null ||
                nativeRoulettePromptModifierTextRect == null)
            {
                DestroyNativeRoulettePrompt();
                return false;
            }

            nativeRoulettePromptKeyBackground.type = Image.Type.Sliced;
            var modifierBackground =
                modifierBackgroundTransform.GetComponent<Image>();
            if (modifierBackground != null)
                modifierBackground.type = Image.Type.Sliced;
            nativeRoulettePromptAction.alignment = TextAnchor.MiddleRight;
            nativeRoulettePromptComboSeparator.alignment = TextAnchor.MiddleCenter;
            nativeRoulettePrompt.SetActive(false);
            return true;
        }

        private void ApplyNativeRoulettePrompt(
            string action,
            string keyboardKey,
            bool reroll,
            bool controllerMode,
            int rewiredPlayerId,
            string leftTrigger,
            string rightTrigger)
        {
            var layoutToken = action + "|" + keyboardKey + "|" +
                              controllerMode + "|" + rewiredPlayerId + "|" +
                              leftTrigger + "|" + rightTrigger;
            if (nativeRoulettePromptLayoutToken == layoutToken)
            {
                // The row is cloned from PauseGUI, whose localization events
                // can restore VOLVER after our initial layout pass. Reassert
                // the owned label even when the layout token did not change.
                if (nativeRoulettePromptAction.text != action)
                {
                    nativeRoulettePromptAction.text = action;
                    Canvas.ForceUpdateCanvases();
                    nativeRoulettePromptActionRect.sizeDelta = new Vector2(
                        Mathf.Ceil(nativeRoulettePromptAction.preferredWidth + 6f),
                        nativeRoulettePromptActionRect.sizeDelta.y);
                }
                if (controllerMode && !reroll &&
                    nativeRoulettePromptComboSeparator.text != "+")
                    nativeRoulettePromptComboSeparator.text = "+";
                else if (!controllerMode || reroll)
                {
                    var expectedKey = controllerMode
                        ? rightTrigger
                        : keyboardKey;
                    if (nativeRoulettePromptKey.text != expectedKey)
                    {
                        if (nativeRoulettePromptKeyGlyph != null)
                            nativeRoulettePromptKeyGlyph.enabled = false;
                        ConfigureManualPromptGlyph(
                            nativeRoulettePromptKeyRect,
                            nativeRoulettePromptKey,
                            nativeRoulettePromptKeyTextRect,
                            nativeRoulettePromptKeyBackgroundRect,
                            expectedKey,
                            true,
                            35f);
                    }
                }
                return;
            }
            nativeRoulettePromptLayoutToken = layoutToken;

            nativeRoulettePromptAction.text = action;
            nativeRoulettePromptKeyRect.gameObject.SetActive(true);
            nativeRoulettePromptModifierRect.gameObject.SetActive(false);
            nativeRoulettePromptComboSeparatorRect.gameObject.SetActive(false);

            var keyWidth = 30f;
            if (controllerMode && !reroll)
            {
                if (!ConfigureNativePromptGlyph(
                        nativeRoulettePromptKeyGlyph,
                        CupheadButton.EquipMenu,
                        rewiredPlayerId))
                    keyWidth = ConfigureManualPromptGlyph(
                        nativeRoulettePromptKeyRect,
                        nativeRoulettePromptKey,
                        nativeRoulettePromptKeyTextRect,
                        nativeRoulettePromptKeyBackgroundRect,
                        "EQUIP",
                        true,
                        35f);
                else
                {
                    Canvas.ForceUpdateCanvases();
                    keyWidth = Mathf.Max(30f,
                        Mathf.Ceil(nativeRoulettePromptKeyGlyph.preferredWidth));
                    SetRectWidth(nativeRoulettePromptKeyRect, keyWidth);
                }
            }
            else
            {
                if (nativeRoulettePromptKeyGlyph != null)
                    nativeRoulettePromptKeyGlyph.enabled = false;
                keyWidth = ConfigureManualPromptGlyph(
                    nativeRoulettePromptKeyRect,
                    nativeRoulettePromptKey,
                    nativeRoulettePromptKeyTextRect,
                    nativeRoulettePromptKeyBackgroundRect,
                    controllerMode ? rightTrigger : keyboardKey,
                    true,
                    35f);
            }

            Canvas.ForceUpdateCanvases();
            const float keyRight = 1290f;
            const float textToKeyGap = 4.5f;
            nativeRoulettePromptKeyRect.pivot = new Vector2(
                0f, nativeRoulettePromptKeyRect.pivot.y);
            var keyPosition = nativeRoulettePromptKeyRect.anchoredPosition;
            keyPosition.x = keyRight - keyWidth;
            nativeRoulettePromptKeyRect.anchoredPosition = keyPosition;

            var actionRight = keyPosition.x - textToKeyGap;
            if (controllerMode && !reroll)
            {
                const float comboGap = 3f;
                const float separatorWidth = 11f;
                var modifierWidth = ConfigureManualPromptGlyph(
                    nativeRoulettePromptModifierRect,
                    nativeRoulettePromptModifier,
                    nativeRoulettePromptModifierTextRect,
                    nativeRoulettePromptModifierBackgroundRect,
                    leftTrigger);
                nativeRoulettePromptModifierRect.gameObject.SetActive(true);
                nativeRoulettePromptComboSeparator.text = "+";
                nativeRoulettePromptComboSeparatorRect.gameObject.SetActive(true);
                SetRectWidth(nativeRoulettePromptComboSeparatorRect,
                    separatorWidth);
                nativeRoulettePromptComboSeparatorRect.pivot = new Vector2(
                    0f, nativeRoulettePromptComboSeparatorRect.pivot.y);

                var separatorPosition =
                    nativeRoulettePromptComboSeparatorRect.anchoredPosition;
                separatorPosition.x = keyPosition.x - comboGap - separatorWidth;
                nativeRoulettePromptComboSeparatorRect.anchoredPosition =
                    separatorPosition;

                nativeRoulettePromptModifierRect.pivot = new Vector2(
                    0f, nativeRoulettePromptModifierRect.pivot.y);
                var modifierPosition =
                    nativeRoulettePromptModifierRect.anchoredPosition;
                modifierPosition.x = separatorPosition.x - comboGap - modifierWidth;
                nativeRoulettePromptModifierRect.anchoredPosition =
                    modifierPosition;
                actionRight = modifierPosition.x - textToKeyGap;
            }

            var actionPosition = nativeRoulettePromptActionRect.anchoredPosition;
            actionPosition.x = actionRight;
            nativeRoulettePromptActionRect.anchoredPosition = actionPosition;
            nativeRoulettePromptActionRect.sizeDelta =
                new Vector2(Mathf.Ceil(nativeRoulettePromptAction.preferredWidth + 6f),
                    nativeRoulettePromptActionRect.sizeDelta.y);
        }

        private static float ConfigureManualPromptGlyph(
            RectTransform root,
            Text text,
            RectTransform textRect,
            RectTransform backgroundRect,
            string value,
            bool restoreNativeTextLayout = false,
            float minimumWidth = 31f)
        {
            if (restoreNativeTextLayout)
            {
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                textRect.localScale = Vector3.one;
            }

            var charBackground = FindDirectChild(root, "BGChar");
            var character = FindDirectChild(root, "Char");
            if (charBackground != null)
                charBackground.gameObject.SetActive(false);
            if (character != null)
                character.gameObject.SetActive(false);
            backgroundRect.gameObject.SetActive(true);
            textRect.gameObject.SetActive(true);
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            Canvas.ForceUpdateCanvases();

            var width = Mathf.Max(minimumWidth,
                Mathf.Ceil(text.preferredWidth + 8f));
            SetRectWidth(root, width);
            SetRectWidth(backgroundRect, width);
            var layoutElement = root.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minWidth = width;
                layoutElement.preferredWidth = width;
                layoutElement.flexibleWidth = 0f;
            }
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, -0.3f);
            SetRectWidth(textRect, width);
            return width;
        }

        private static void SetRectWidth(RectTransform rect, float width)
        {
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static bool ConfigureNativePromptGlyph(
            CupheadGlyph glyph,
            CupheadButton button,
            int rewiredPlayerId)
        {
            if (glyph == null)
                return false;
            try
            {
                AccessTools.Field(typeof(CupheadGlyph), "button")
                    .SetValue(glyph, button);
                AccessTools.Field(typeof(CupheadGlyph), "rewiredPlayerId")
                    .SetValue(glyph, rewiredPlayerId);
                glyph.enabled = true;
                AccessTools.Method(typeof(CupheadGlyph), "Init")
                    .Invoke(glyph, null);
                return true;
            }
            catch
            {
                glyph.enabled = false;
                return false;
            }
        }

        private static bool TryGetControllerPromptInfo(
            out int rewiredPlayerId,
            out string leftTrigger,
            out string rightTrigger)
        {
            rewiredPlayerId = 0;
            leftTrigger = "LT";
            rightTrigger = "RT";
            for (var playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                try
                {
                    var playerId = playerIndex == 0
                        ? PlayerId.PlayerOne
                        : PlayerId.PlayerTwo;
                    var player = PlayerManager.GetPlayerInput(playerId);
                    var controller = player == null || player.controllers == null
                        ? null
                        : player.controllers.GetLastActiveController();
                    if (controller == null ||
                        controller.type != Rewired.ControllerType.Joystick)
                        continue;

                    rewiredPlayerId = playerIndex;
                    GetControllerTriggerLabels(
                        controller, out leftTrigger, out rightTrigger);
                    return true;
                }
                catch
                {
                    // A player slot may not exist yet while the map UI starts.
                }
            }
            return false;
        }

        private static void GetControllerTriggerLabels(
            Rewired.Controller controller,
            out string leftTrigger,
            out string rightTrigger)
        {
            var identity = ((controller.name ?? string.Empty) + " " +
                            (controller.hardwareName ?? string.Empty) + " " +
                            (controller.hardwareIdentifier ?? string.Empty))
                .ToLowerInvariant();
            if (identity.Contains("nintendo") || identity.Contains("switch") ||
                identity.Contains("joy-con") || identity.Contains("joycon"))
            {
                leftTrigger = "ZL";
                rightTrigger = "ZR";
                return;
            }
            if (identity.Contains("playstation") || identity.Contains("dualshock") ||
                identity.Contains("dualsense") || identity.Contains("sony") ||
                identity.Contains("ps3") || identity.Contains("ps4") ||
                identity.Contains("ps5"))
            {
                leftTrigger = "L2";
                rightTrigger = "R2";
                return;
            }
            leftTrigger = "LT";
            rightTrigger = "RT";
        }

        private bool PrepareNativeChallengePrompt()
        {
            if (nativeChallengePrompt != null)
                return true;
            if (activeChallenge == ModifierId.None)
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
                LocalizedChallengeLabel(activeChallenge).ToUpperInvariant());
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
            nativeRoulettePromptKeyGlyph = null;
            nativeRoulettePromptModifierRect = null;
            nativeRoulettePromptModifier = null;
            nativeRoulettePromptModifierTextRect = null;
            nativeRoulettePromptModifierBackgroundRect = null;
            nativeRoulettePromptComboSeparator = null;
            nativeRoulettePromptComboSeparatorRect = null;
            nativeRoulettePromptLayoutToken = null;
        }
    }
}
