using System;
        using HarmonyLib;
        using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const float BattleHudAlpha = 0.70f;
        private const float BattleHudPauseAlphaMultiplier = 0.70f;
        private const float BattleHudResumeAlphaDuration = 0.30f;
        private const float BattleHudIconSize = 48f;
        private const float BattleHudIconGap = -2f;
        private const float BattleHudRightMargin = 26f;
        private const float BattleHudBottomMargin = 13f;
        private const float BattleHudPauseBottomMargin = 13f;
        private const float BattleHudTextGap = 10f;
        private const float BattleHudMaxTextWidth = 420f;
        private const float BattleHudMultiplayerSideGap = 18f;
        private const float BattleHudInitialRevealDelay = 1.1f;
        private const float BattleHudRevealStep = 0.28f;
        private const float BattleHudPulseDuration = 0.38f;
        private const float BattleHudTextRevealDuration = 0.28f;
        private const float BattleHudImpactVolume = 1f;
        private static readonly System.Reflection.FieldInfo
            LevelHudCupheadField = AccessTools.Field(typeof(LevelHUD), "cuphead");
        private static readonly System.Reflection.FieldInfo
            LevelHudMugmanField = AccessTools.Field(typeof(LevelHUD), "mugman");
        private static readonly System.Reflection.FieldInfo
            LevelHudPlayerHealthField = AccessTools.Field(
                typeof(LevelHUDPlayer), "health");
        private static readonly System.Reflection.FieldInfo
            LevelHudPlayerSuperField = AccessTools.Field(
                typeof(LevelHUDPlayer), "super");
        private static readonly System.Reflection.FieldInfo
            SceneLoaderCanvasField = AccessTools.Field(typeof(SceneLoader), "canvas");

        private GameObject battleHudCanvas;
        private GameObject battleHudRoot;
        private RawImage[] battleHudIcons;
        private Text battleHudChallengeText;
        private Material battleHudSaturationMaterial;
        private Texture2D battleHudWhiteEmptyTexture;
        private Material battleHudChallengeBaseMaterial;
        private bool battleHudUsingSaturationMaterial;
        private int battleHudTextBaseFontSize;
        private int battleHudVisibleIconCount = 5;
        private float battleHudRevealStartedAt = -1f;
        private bool battleHudWasVisible;
        private bool battleHudOnNativeCanvas;
        private bool battleHudOnPauseLayer;
        private bool battleHudPresentationActive;
        private bool battleHudFollowNativeVictoryLayer;
        private bool battleHudHoldOverlayThroughVictory;
        private RouletteResult battleHudResultSnapshot;
        private ModifierId battleHudChallengeSnapshot = ModifierId.None;
        private int battleHudImpactPlayedCount;

        private void UpdateBattleResultHud()
        {
            if (!ShouldShowBattleResultHud())
            {
                if (battleHudRoot != null && battleHudRoot.activeSelf)
                    battleHudRoot.SetActive(false);
                // Dice Palace loads a separate battle scene for every space.
                // Those internal loads belong to one roulette session, so do
                // not replay the entry sequence (or its sounds) each time.
                if (!BattleHudUsesDicePalaceChain())
                {
                    battleHudWasVisible = false;
                    battleHudRevealStartedAt = -1f;
                    battleHudImpactPlayedCount = 0;
                }
                battleHudOnNativeCanvas = false;
                return;
            }

            if (battleHudRoot == null && !PrepareBattleResultHud())
                return;

            if (!UpdateBattleResultHudLayer())
            {
                battleHudRoot.SetActive(false);
                // LevelHUD is temporarily disabled by some phase/iris
                // transitions. Preserve the reveal state so returning to the
                // same fight cannot replay the entry animation.
                return;
            }

            if (!battleHudRoot.activeSelf)
                battleHudRoot.SetActive(true);
            if (!battleHudWasVisible)
            {
                battleHudWasVisible = true;
                battleHudRevealStartedAt = Time.realtimeSinceStartup;
            }

            UpdateBattleResultHudContents();
            UpdateBattleResultHudSaturation();
            UpdateBattleResultHudReveal();
        }

        private bool ShouldShowBattleResultHud()
        {
            if (!battleHudPresentationActive)
                return false;
            if (SceneLoader.CurrentlyLoading)
            {
                var keepThroughSceneLoad =
                    battleHudFollowNativeVictoryLayer ||
                    battleHudHoldOverlayThroughVictory ||
                    BattleHudUsesDicePalaceChain();
                if (!keepThroughSceneLoad || battleHudRoot == null)
                    return false;
            }

            try
            {
                var level = Level.Current;
                return level != null && level.LevelType == Level.Type.Battle;
            }
            catch
            {
                return false;
            }
        }

        private void BeginBattleResultHudSession()
        {
            returnToMapAfterRouletteFinalBossWin = false;
            battleHudPresentationActive = true;
            battleHudFollowNativeVictoryLayer = false;
            battleHudHoldOverlayThroughVictory = false;
            battleHudChallengeSnapshot = activeChallenge;
            battleHudResultSnapshot = new RouletteResult
            {
                Boss = result.Boss,
                Weapon1 = result.Weapon1,
                Weapon2 = result.Weapon2,
                Super = result.Super,
                Charm = result.Charm,
                Modifier = result.Modifier
            };
            battleHudWasVisible = false;
            battleHudRevealStartedAt = -1f;
            battleHudImpactPlayedCount = 0;
        }

        private void KeepBattleResultHudThroughVictory(
            bool holdUntilSceneChange)
        {
            if (!battleHudPresentationActive)
                return;

            battleHudHoldOverlayThroughVictory =
                holdUntilSceneChange;
            battleHudFollowNativeVictoryLayer =
                !holdUntilSceneChange;
            if (holdUntilSceneChange)
            {
                if (!PlaceBattleHudOnSceneTransitionLayer())
                    PlaceBattleHudOnPersistentOverlay();
                return;
            }
            Canvas nativeCanvas;
            if (TryGetNativeBattleHudCanvas(out nativeCanvas))
                TrySwapBattleHudToNativeVictoryLayer(nativeCanvas);
        }

        private void EndBattleResultHudSession()
        {
            returnToMapAfterRouletteFinalBossWin = false;
            battleHudPresentationActive = false;
            battleHudFollowNativeVictoryLayer = false;
            battleHudHoldOverlayThroughVictory = false;
            battleHudResultSnapshot = null;
            battleHudChallengeSnapshot = ModifierId.None;
            battleHudWasVisible = false;
            battleHudRevealStartedAt = -1f;
            battleHudImpactPlayedCount = 0;
            battleHudOnNativeCanvas = false;
            if (battleHudRoot != null && battleHudRoot.activeSelf)
                battleHudRoot.SetActive(false);
        }

        private bool PrepareBattleResultHud()
        {
            if (battleHudCanvas != null && battleHudRoot != null)
                return true;

            Transform screenCanvas;
            Transform pauseBackground;
            Text textTemplate;
            TryFindBattleHudNativeLayers(out screenCanvas,
                out pauseBackground, out textTemplate);

            if (battleHudCanvas == null)
            {
                if (screenCanvas == null)
                    return false;
                CreatePersistentBattleHudCanvas(screenCanvas);
            }

            if (battleHudRoot == null)
                CreateBattleHudRoot(textTemplate);
            return battleHudRoot != null;
        }

        private void CreatePersistentBattleHudCanvas(Transform screenCanvas)
        {
            var sourceCanvas = screenCanvas.GetComponent<Canvas>();
            var sourceScaler = screenCanvas.GetComponent<CanvasScaler>();
            battleHudCanvas = new GameObject("Gilomx Roulette Battle HUD Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

            var canvas = battleHudCanvas.GetComponent<Canvas>();
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

            var scaler = battleHudCanvas.GetComponent<CanvasScaler>();
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
                scaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
            }

            UnityEngine.Object.DontDestroyOnLoad(battleHudCanvas);
        }

        private void CreateBattleHudRoot(Text textTemplate)
        {
            battleHudRoot = new GameObject("Gilomx Roulette Battle HUD",
                typeof(RectTransform), typeof(CanvasGroup));
            battleHudRoot.transform.SetParent(battleHudCanvas.transform, false);
            var rootRect = battleHudRoot.transform as RectTransform;
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(
                -BattleHudRightMargin, BattleHudBottomMargin);
            rootRect.sizeDelta = new Vector2(
                BattleHudIconsWidth(5), BattleHudIconSize);

            EnsureBattleHudSaturationMaterial();

            battleHudIcons = new RawImage[5];
            for (var i = 0; i < battleHudIcons.Length; i++)
                battleHudIcons[i] = CreateBattleHudIcon(rootRect, i);

            if (textTemplate != null)
            {
                battleHudChallengeText = Instantiate(textTemplate);
                battleHudChallengeText.name =
                    "Gilomx Roulette Battle HUD Challenge";
            }
            else
            {
                var textObject = new GameObject(
                    "Gilomx Roulette Battle HUD Challenge",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                battleHudChallengeText = textObject.GetComponent<Text>();
                battleHudChallengeText.font = theme.BodyFont;
                battleHudChallengeText.fontSize = 22;
                battleHudChallengeText.fontStyle = FontStyle.Normal;
                battleHudChallengeText.color = Color.white;
            }

            battleHudChallengeText.transform.SetParent(rootRect, false);
            battleHudChallengeBaseMaterial = battleHudChallengeText.material;
            battleHudUsingSaturationMaterial = false;
            battleHudChallengeText.raycastTarget = false;
            battleHudChallengeText.alignment = TextAnchor.MiddleLeft;
            battleHudChallengeText.resizeTextForBestFit = false;
            battleHudChallengeText.horizontalOverflow =
                HorizontalWrapMode.Overflow;
            battleHudChallengeText.verticalOverflow = VerticalWrapMode.Overflow;
            var textColor = battleHudChallengeText.color;
            textColor.a = BattleHudAlpha;
            battleHudChallengeText.color = textColor;
            battleHudTextBaseFontSize = Mathf.Max(1,
                battleHudChallengeText.fontSize);

            var textRect = battleHudChallengeText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.zero;
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(
                BattleHudIconsWidth(5) + BattleHudTextGap,
                BattleHudIconSize * 0.5f);
            textRect.sizeDelta = new Vector2(
                BattleHudMaxTextWidth, BattleHudIconSize);
            textRect.localScale = Vector3.one;

            battleHudRoot.SetActive(false);
        }

        private RawImage CreateBattleHudIcon(RectTransform parent,
            int index)
        {
            var iconObject = new GameObject(
                "Gilomx Roulette Battle HUD Icon " + index,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            iconObject.transform.SetParent(parent, false);
            var icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;
            icon.color = new Color(1f, 1f, 1f, BattleHudAlpha);

            var rect = icon.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(
                index * (BattleHudIconSize + BattleHudIconGap), 0f);
            rect.sizeDelta = new Vector2(BattleHudIconSize, BattleHudIconSize);
            return icon;
        }

        private bool TryFindBattleHudNativeLayers(out Transform screenCanvas,
            out Transform pauseBackground, out Text textTemplate)
        {
            screenCanvas = null;
            pauseBackground = null;
            textTemplate = null;

            var glyphs = Resources.FindObjectsOfTypeAll<CupheadGlyph>();
            for (var i = 0; i < glyphs.Length; i++)
            {
                var glyph = glyphs[i];
                if (glyph == null || !glyph.gameObject.scene.IsValid())
                    continue;

                var glyphTransform = glyph.transform;
                var help = glyphTransform.parent;
                var background = help == null ? null : help.parent;
                var pause = background == null ? null : background.parent;
                if (glyphTransform.name != "Glyph (2)" || help == null ||
                    help.name != "Help (2)" || background == null ||
                    background.name != "Background" || pause == null ||
                    pause.name != "PauseGUI")
                    continue;

                var canvas = pause.GetComponentInParent<Canvas>();
                var actionTransform = FindDirectChild(help, "Text (1)");
                var action = actionTransform == null
                    ? null
                    : actionTransform.GetComponent<Text>();
                if (canvas == null || action == null)
                    continue;

                // Several PauseGUI instances can remain loaded. Preserve the
                // first one as a template fallback, but always prefer the
                // currently visible battle pause menu.
                if (screenCanvas == null)
                {
                    screenCanvas = canvas.transform;
                    pauseBackground = background;
                    textTemplate = action;
                }
                if (!background.gameObject.activeInHierarchy)
                    continue;
                screenCanvas = canvas.transform;
                pauseBackground = background;
                textTemplate = action;
                return true;
            }

            return screenCanvas != null;
        }

        private void UpdateBattleResultHudContents()
        {
            if (battleHudIcons == null || battleHudIcons.Length < 5 ||
                battleHudChallengeText == null)
                return;

            var hudResult = battleHudResultSnapshot ?? result;

            var weapon1 = Mathf.Clamp(hudResult.Weapon1, 0,
                RouletteData.Weapons.Length - 1);
            var weapon2 = Mathf.Clamp(hudResult.Weapon2, 0,
                RouletteData.Weapons.Length - 1);
            var super = Mathf.Clamp(hudResult.Super, 0,
                RouletteData.Supers.Length - 1);
            var charm = Mathf.Clamp(hudResult.Charm, 0,
                RouletteData.Charms.Length - 1);
            var modifier = Mathf.Clamp(hudResult.Modifier, 0,
                RouletteData.Modifiers.Length - 1);

            if (BattleHudUsesPlaneLoadout())
            {
                SetBattleHudVisibleIconCount(2);
                ApplyBattleHudEquipmentIcon(battleHudIcons[0],
                    RouletteData.Charms[charm].NativeSprite,
                    RouletteData.Charms[charm].Image,
                    RouletteData.Charms[charm].Value == Charm.None);
                ApplyBattleHudChallengeIcon(battleHudIcons[1], modifier);
            }
            else
            {
                SetBattleHudVisibleIconCount(5);
                ApplyBattleHudEquipmentIcon(battleHudIcons[0],
                    RouletteData.Weapons[weapon1].NativeSprite,
                    RouletteData.Weapons[weapon1].Image,
                    RouletteData.Weapons[weapon1].Value == Weapon.None);
                ApplyBattleHudEquipmentIcon(battleHudIcons[1],
                    RouletteData.Weapons[weapon2].NativeSprite,
                    RouletteData.Weapons[weapon2].Image,
                    RouletteData.Weapons[weapon2].Value == Weapon.None);
                ApplyBattleHudEquipmentIcon(battleHudIcons[2],
                    RouletteData.Supers[super].NativeSprite,
                    RouletteData.Supers[super].Image,
                    RouletteData.Supers[super].Value == Super.None);
                ApplyBattleHudEquipmentIcon(battleHudIcons[3],
                    RouletteData.Charms[charm].NativeSprite,
                    RouletteData.Charms[charm].Image,
                    RouletteData.Charms[charm].Value == Charm.None);
                ApplyBattleHudChallengeIcon(battleHudIcons[4], modifier);
            }

            battleHudChallengeText.text =
                battleHudChallengeSnapshot == ModifierId.None
                ? ""
                : LocalizedChallengeLabel(battleHudChallengeSnapshot)
                    .ToUpperInvariant();
            UpdateBattleResultHudLayout();
        }

        private void ApplyBattleHudChallengeIcon(RawImage image,
            int modifier)
        {
            if (battleHudChallengeSnapshot == ModifierId.None)
                ApplyWhiteBattleHudEmptyIcon(image);
            else
                ApplyTextureToBattleHudIcon(image, GetTexture(
                    RouletteData.Modifiers[modifier].Image));
        }

        private bool BattleHudUsesPlaneLoadout()
        {
            var hudResult = battleHudResultSnapshot ?? result;
            if (hudResult == null || RouletteData.Bosses.Length == 0)
                return false;
            var boss = Mathf.Clamp(hudResult.Boss, 0,
                RouletteData.Bosses.Length - 1);
            return RouletteData.Bosses[boss].IsPlane;
        }

        private bool BattleHudUsesDicePalaceChain()
        {
            if (!battleHudPresentationActive)
                return false;

            var hudResult = battleHudResultSnapshot ?? result;
            return hudResult != null && hudResult.Boss >= 0 &&
                   hudResult.Boss < RouletteData.Bosses.Length &&
                   RouletteData.Bosses[hudResult.Boss].Level ==
                   Levels.DicePalaceMain;
        }

        private void SetBattleHudVisibleIconCount(int count)
        {
            battleHudVisibleIconCount = Mathf.Clamp(count, 1,
                battleHudIcons.Length);
            for (var i = 0; i < battleHudIcons.Length; i++)
            {
                var icon = battleHudIcons[i];
                if (icon == null)
                    continue;
                icon.gameObject.SetActive(i < battleHudVisibleIconCount);
                icon.rectTransform.anchoredPosition = new Vector2(
                    i * (BattleHudIconSize + BattleHudIconGap), 0f);
            }
        }

        private static float BattleHudIconsWidth(int count)
        {
            count = Mathf.Max(1, count);
            return BattleHudIconSize * count +
                   BattleHudIconGap * (count - 1);
        }

        private void UpdateBattleResultHudLayout()
        {
            if (battleHudRoot == null || battleHudChallengeText == null)
                return;

            var rootRect = (RectTransform)battleHudRoot.transform;
            float multiplayerGapLeft;
            float multiplayerGapRight;
            var useMultiplayerGap = TryGetBattleHudMultiplayerGap(
                rootRect.parent as RectTransform,
                out multiplayerGapLeft,
                out multiplayerGapRight);
            var iconsWidth = BattleHudIconsWidth(battleHudVisibleIconCount);
            var textRect = battleHudChallengeText.rectTransform;
            var hasText = !string.IsNullOrEmpty(battleHudChallengeText.text);
            var textWidth = 0f;
            if (hasText)
            {
                var maxTextWidth = BattleHudMaxTextWidth;
                if (useMultiplayerGap)
                    maxTextWidth = Mathf.Min(maxTextWidth, Mathf.Max(0f,
                        multiplayerGapRight - multiplayerGapLeft -
                        iconsWidth - BattleHudTextGap));
                battleHudChallengeText.fontSize = battleHudTextBaseFontSize;
                textRect.sizeDelta = new Vector2(
                    maxTextWidth, BattleHudIconSize);
                var preferredWidth = battleHudChallengeText.preferredWidth;
                if (maxTextWidth > 0f && preferredWidth > maxTextWidth)
                {
                    var fittedSize = Mathf.FloorToInt(
                        battleHudTextBaseFontSize *
                        maxTextWidth / preferredWidth);
                    battleHudChallengeText.fontSize = Mathf.Max(15,
                        fittedSize);
                    preferredWidth = battleHudChallengeText.preferredWidth;
                }
                textWidth = Mathf.Min(maxTextWidth,
                    Mathf.Ceil(preferredWidth + 2f));
            }

            textRect.anchoredPosition = new Vector2(
                iconsWidth + BattleHudTextGap,
                BattleHudIconSize * 0.5f);
            textRect.sizeDelta = new Vector2(textWidth, BattleHudIconSize);

            var rootWidth = iconsWidth;
            if (hasText)
                rootWidth += BattleHudTextGap + textWidth;
            rootRect.sizeDelta = new Vector2(rootWidth, BattleHudIconSize);
            var bottomMargin = battleHudOnPauseLayer
                ? BattleHudPauseBottomMargin
                : BattleHudBottomMargin;
            if (useMultiplayerGap)
                PlaceBattleHudInMultiplayerGap(rootRect,
                    multiplayerGapLeft, multiplayerGapRight, bottomMargin);
            else
                PlaceBattleHudAtSinglePlayerPosition(rootRect, bottomMargin);
        }

        private static void PlaceBattleHudAtSinglePlayerPosition(
            RectTransform rootRect, float bottomMargin)
        {
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(
                -BattleHudRightMargin, bottomMargin);
        }

        private static void PlaceBattleHudInMultiplayerGap(
            RectTransform rootRect,
            float gapLeft,
            float gapRight,
            float bottomMargin)
        {
            var parentRect = rootRect.parent as RectTransform;
            if (parentRect == null)
            {
                PlaceBattleHudAtSinglePlayerPosition(rootRect, bottomMargin);
                return;
            }

            var gapCenter = (gapLeft + gapRight) * 0.5f;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(
                gapCenter - parentRect.rect.xMin,
                bottomMargin);
        }

        private static bool TryGetBattleHudMultiplayerGap(
            RectTransform targetParent,
            out float gapLeft,
            out float gapRight)
        {
            gapLeft = 0f;
            gapRight = 0f;
            if (targetParent == null || LevelHudCupheadField == null ||
                LevelHudMugmanField == null ||
                LevelHudPlayerHealthField == null ||
                LevelHudPlayerSuperField == null)
                return false;

            LevelHUD nativeHud;
            try
            {
                nativeHud = LevelHUD.Current;
            }
            catch
            {
                return false;
            }
            if (nativeHud == null || nativeHud.Canvas == null)
                return false;

            var playerOneHud =
                LevelHudCupheadField.GetValue(nativeHud) as LevelHUDPlayer;
            var playerTwoHud =
                LevelHudMugmanField.GetValue(nativeHud) as LevelHUDPlayer;
            if (playerOneHud == null || playerTwoHud == null ||
                !playerOneHud.gameObject.activeInHierarchy ||
                !playerTwoHud.gameObject.activeInHierarchy ||
                playerTwoHud.player == null)
                return false;

            var nativeCanvasRect =
                nativeHud.Canvas.transform as RectTransform;
            if (nativeCanvasRect == null)
                return false;

            Bounds playerOneBounds;
            Bounds playerTwoBounds;
            if (!TryGetNativePlayerHudBounds(playerOneHud,
                    nativeCanvasRect, out playerOneBounds) ||
                !TryGetNativePlayerHudBounds(playerTwoHud,
                    nativeCanvasRect, out playerTwoBounds))
                return false;

            var nativeGapLeft = playerOneBounds.max.x +
                                BattleHudMultiplayerSideGap;
            var nativeGapRight = playerTwoBounds.min.x -
                                 BattleHudMultiplayerSideGap;
            if (nativeGapRight <= nativeGapLeft)
                return false;

            if (!TryConvertNativeCanvasXToTarget(
                    nativeCanvasRect, nativeHud.Canvas,
                    nativeGapLeft, playerOneBounds.center.y,
                    targetParent, out gapLeft) ||
                !TryConvertNativeCanvasXToTarget(
                    nativeCanvasRect, nativeHud.Canvas,
                    nativeGapRight, playerTwoBounds.center.y,
                    targetParent, out gapRight))
                return false;

            return gapRight > gapLeft;
        }

        private static bool TryGetNativePlayerHudBounds(
            LevelHUDPlayer playerHud,
            RectTransform relativeTo,
            out Bounds bounds)
        {
            bounds = new Bounds();
            var hasBounds = false;
            var health = LevelHudPlayerHealthField.GetValue(playerHud)
                as Component;
            var super = LevelHudPlayerSuperField.GetValue(playerHud)
                as Component;
            var components = new[] { health, super };
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null ||
                    !component.gameObject.activeInHierarchy)
                    continue;
                var componentBounds =
                    RectTransformUtility.CalculateRelativeRectTransformBounds(
                        relativeTo, component.transform);
                if (!hasBounds)
                {
                    bounds = componentBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(componentBounds.min);
                    bounds.Encapsulate(componentBounds.max);
                }
            }
            return hasBounds;
        }

        private static bool TryConvertNativeCanvasXToTarget(
            RectTransform sourceCanvasRect,
            Canvas sourceCanvas,
            float sourceX,
            float sourceY,
            RectTransform targetRect,
            out float targetX)
        {
            targetX = 0f;
            var worldPoint = sourceCanvasRect.TransformPoint(
                new Vector3(sourceX, sourceY, 0f));
            var sourceCamera = sourceCanvas.renderMode ==
                               RenderMode.ScreenSpaceOverlay
                ? null
                : sourceCanvas.worldCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(
                sourceCamera, worldPoint);
            var targetCanvas = targetRect.GetComponentInParent<Canvas>();
            var targetCamera = targetCanvas == null ||
                               targetCanvas.renderMode ==
                               RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetRect, screenPoint, targetCamera, out localPoint))
                return false;
            targetX = localPoint.x;
            return true;
        }

        private void EnsureBattleHudSaturationMaterial()
        {
            if (battleHudSaturationMaterial != null ||
                battleHudSaturationShader == null)
                return;

            battleHudSaturationMaterial =
                new Material(battleHudSaturationShader);
            battleHudSaturationMaterial.name =
                "Gilomx Roulette Battle HUD Saturation";
            battleHudSaturationMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        private void UpdateBattleResultHudSaturation()
        {
            // Cuphead's original HUD keeps the normal UI material. Do the same
            // unless this independent overlay must reproduce the roulette's
            // black-and-white transition, avoiding an unnecessary custom
            // shared-material path during ordinary combat.
            var useSaturationMaterial = !battleHudOnNativeCanvas &&
                battleHudChallengeSnapshot == ModifierId.BlackAndWhite;
            if (useSaturationMaterial)
            {
                EnsureBattleHudSaturationMaterial();
                if (battleHudSaturationMaterial != null)
                    battleHudSaturationMaterial.SetFloat(
                        "_Saturation",
                        1f - Mathf.Clamp01(blackAndWhiteBlend));
                else
                    useSaturationMaterial = false;
            }

            if (battleHudUsingSaturationMaterial == useSaturationMaterial)
                return;
            battleHudUsingSaturationMaterial = useSaturationMaterial;

            var iconMaterial = useSaturationMaterial
                ? battleHudSaturationMaterial : null;

            if (battleHudIcons != null)
            {
                for (var i = 0; i < battleHudIcons.Length; i++)
                {
                    if (battleHudIcons[i] != null)
                        battleHudIcons[i].material = iconMaterial;
                }
            }
            var textMaterial = useSaturationMaterial
                ? battleHudSaturationMaterial
                : battleHudChallengeBaseMaterial;
            if (battleHudChallengeText != null)
                battleHudChallengeText.material = textMaterial;
        }

        private void UpdateBattleResultHudReveal()
        {
            if (battleHudIcons == null || battleHudChallengeText == null)
                return;

            if (battleHudRevealStartedAt < 0f)
                battleHudRevealStartedAt = Time.realtimeSinceStartup;
            var elapsed = Time.realtimeSinceStartup -
                          battleHudRevealStartedAt;
            var revealedIconCount = 0;

            for (var i = 0; i < battleHudVisibleIconCount; i++)
            {
                var icon = battleHudIcons[i];
                if (icon == null)
                    continue;

                var localElapsed = elapsed - BattleHudInitialRevealDelay -
                                   i * BattleHudRevealStep;
                var visible = localElapsed >= 0f;
                if (visible)
                    revealedIconCount = i + 1;
                var color = icon.color;
                color.a = visible ? BattleHudAlpha : 0f;
                icon.color = color;

                var scale = 1f;
                if (visible && localElapsed < BattleHudPulseDuration)
                {
                    var progress = Mathf.Clamp01(
                        localElapsed / BattleHudPulseDuration);
                    scale += Mathf.Sin(progress * Mathf.PI) * 0.075f;
                }
                icon.rectTransform.localScale = new Vector3(
                    scale, scale, 1f);
            }

            while (battleHudImpactPlayedCount < revealedIconCount)
            {
                PlayOneShot(battleHudImpactClip, BattleHudImpactVolume);
                battleHudImpactPlayedCount++;
            }

            var textDelay = BattleHudInitialRevealDelay +
                            battleHudVisibleIconCount * BattleHudRevealStep +
                            BattleHudPulseDuration * 0.55f;
            var textProgress = Mathf.Clamp01(
                (elapsed - textDelay) / BattleHudTextRevealDuration);
            var smoothProgress = textProgress * textProgress *
                                 (3f - 2f * textProgress);
            var textColor = battleHudChallengeText.color;
            textColor.a = BattleHudAlpha * smoothProgress;
            battleHudChallengeText.color = textColor;

            // Native confirmation labels settle from a slightly larger
            // state. Keep the right edge fixed while this label settles.
            var textRect = battleHudChallengeText.rectTransform;
            textRect.pivot = new Vector2(1f, 0.5f);
            textRect.anchoredPosition = new Vector2(
                BattleHudIconsWidth(battleHudVisibleIconCount) +
                BattleHudTextGap +
                textRect.sizeDelta.x,
                BattleHudIconSize * 0.5f);
            var textScale = Mathf.Lerp(1.12f, 1f, smoothProgress);
            textRect.localScale = new Vector3(
                textScale, textScale, 1f);
        }

        private void ApplyBattleHudEquipmentIcon(RawImage image,
            string firstFrame, string fallbackImage, bool isEmpty)
        {
            if (isEmpty)
            {
                ApplyWhiteBattleHudEmptyIcon(image);
                return;
            }

            ApplyNativeBattleHudIcon(image, firstFrame, fallbackImage);
        }

        private void ApplyWhiteBattleHudEmptyIcon(RawImage image)
        {
            if (battleHudWhiteEmptyTexture == null)
                battleHudWhiteEmptyTexture =
                    CreateWhiteBattleHudEmptyTexture();
            ApplyTextureToBattleHudIcon(
                image, battleHudWhiteEmptyTexture);
        }

        private Texture2D CreateWhiteBattleHudEmptyTexture()
        {
            var sprite = theme.GetSprite("equip_icon_empty_0001");
            if (sprite != null && sprite.texture != null)
            {
                try
                {
                    var nativeWhite =
                        CreateWhiteSilhouetteFromSprite(sprite);
                    if (nativeWhite != null)
                        return nativeWhite;
                }
                catch (System.Exception exception)
                {
                    Logger.LogWarning(
                        "Could not whiten the native empty HUD icon; " +
                        "using the procedural dotted circle. " + exception);
                }
            }

            return CreateProceduralWhiteEmptyTexture();
        }

        private static Texture2D CreateWhiteSilhouetteFromSprite(
            Sprite sprite)
        {
            var source = sprite.texture;
            var sourceRect = sprite.textureRect;
            var width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            var renderTexture = RenderTexture.GetTemporary(
                width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                var scale = new Vector2(
                    sourceRect.width / source.width,
                    sourceRect.height / source.height);
                var offset = new Vector2(
                    sourceRect.x / source.width,
                    sourceRect.y / source.height);
                Graphics.Blit(source, renderTexture, scale, offset);
                RenderTexture.active = renderTexture;

                texture = new Texture2D(
                    width, height, TextureFormat.ARGB32, false);
                texture.name = "Gilomx White Empty Battle HUD";
                texture.ReadPixels(
                    new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);

                var pixels = texture.GetPixels();
                var visiblePixels = 0;
                for (var i = 0; i < pixels.Length; i++)
                {
                    var alpha = pixels[i].a;
                    if (alpha <= 0.01f)
                    {
                        pixels[i] = Color.clear;
                        continue;
                    }

                    visiblePixels++;
                    pixels[i] = new Color(1f, 1f, 1f, alpha);
                }

                // A dotted ring occupies only a small part of its rectangle.
                // Reject a fully opaque atlas read and use the safe fallback
                // instead of ever showing a white square in the HUD.
                if (visiblePixels > pixels.Length * 0.55f)
                {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    return null;
                }

                texture.SetPixels(pixels);
                texture.Apply(false, false);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static Texture2D CreateProceduralWhiteEmptyTexture()
        {
            const int size = 72;
            const float radius = 27.5f;
            const float thickness = 2.25f;
            const float dashCount = 14f;
            const float dashFill = 0.62f;

            var texture = new Texture2D(
                size, size, TextureFormat.ARGB32, false);
            texture.name = "Gilomx White Empty Battle HUD Fallback";
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var lineDistance = Mathf.Abs(distance - radius);
                    var angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f)
                        angle += Mathf.PI * 2f;
                    var dashPhase = Mathf.Repeat(
                        angle / (Mathf.PI * 2f) * dashCount, 1f);
                    var alpha = dashPhase < dashFill
                        ? Mathf.Clamp01(thickness - lineDistance)
                        : 0f;
                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        private void ApplyNativeBattleHudIcon(RawImage image,
            string firstFrame, string fallbackImage)
        {
            var sprite = theme.GetSprite(firstFrame);

            if (sprite == null || sprite.texture == null)
            {
                ApplyTextureToBattleHudIcon(image, GetTexture(fallbackImage));
                return;
            }

            image.texture = sprite.texture;
            var textureRect = sprite.textureRect;
            image.uvRect = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);
            image.enabled = true;
        }

        private static void ApplyTextureToBattleHudIcon(RawImage image,
            Texture2D texture)
        {
            image.texture = texture;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.enabled = texture != null;
        }

        private bool UpdateBattleResultHudLayer()
        {
            if (battleHudRoot == null)
                return false;

            var gameOverLayer = FindActiveGameOverHudLayer();
            if (gameOverLayer != null)
            {
                PlaceBattleHudInsideMenuLayer(gameOverLayer);
                return true;
            }

            LevelPauseGUI activePauseGui;
            if (TryGetActiveLevelPauseMenu(out activePauseGui))
            {
                PlaceBattleHudInsidePauseLayer(activePauseGui.transform);
                return true;
            }

            return PlaceBattleHudOnGameplayLayer();
        }

        private static bool TryGetActiveLevelPauseMenu(
            out LevelPauseGUI activePauseGui)
        {
            activePauseGui = null;
            var pauseGuis =
                Resources.FindObjectsOfTypeAll<LevelPauseGUI>();
            for (var i = 0; i < pauseGuis.Length; i++)
            {
                var pauseGui = pauseGuis[i];
                if (pauseGui == null ||
                    !pauseGui.gameObject.scene.IsValid() ||
                    !pauseGui.gameObject.activeInHierarchy)
                    continue;
                try
                {
                    // Unlike PauseManager.state, this state is not changed by
                    // parry hit-stop. Both Paused (1) and Animating (2) are a
                    // real visible pause transition.
                    if (Convert.ToInt32(pauseGui.state) != 0)
                    {
                        activePauseGui = pauseGui;
                        return true;
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        private void PlaceBattleHudInsideMenuLayer(Transform layer)
        {
            if (battleHudRoot.transform.parent != layer)
                battleHudRoot.transform.SetParent(layer, false);
            battleHudRoot.transform.SetAsFirstSibling();
            battleHudOnNativeCanvas = false;
            battleHudOnPauseLayer = false;
            SetBattleHudRootAlpha(1f);
        }

        private void PlaceBattleHudInsidePauseLayer(Transform pauseLayer)
        {
            if (battleHudRoot.transform.parent != pauseLayer)
                battleHudRoot.transform.SetParent(pauseLayer, false);
            battleHudRoot.transform.SetAsFirstSibling();
            battleHudOnNativeCanvas = false;
            battleHudOnPauseLayer = true;
            SetBattleHudRootAlpha(BattleHudPauseAlphaMultiplier);
        }

        private void SetBattleHudRootAlpha(float alpha)
        {
            if (battleHudRoot == null)
                return;
            var canvasGroup = battleHudRoot.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        private void FadeBattleHudRootAlphaToFull()
        {
            if (battleHudRoot == null)
                return;
            var canvasGroup = battleHudRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                return;

            var alphaPerSecond =
                (1f - BattleHudPauseAlphaMultiplier) /
                Mathf.Max(0.01f, BattleHudResumeAlphaDuration);
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha,
                1f,
                alphaPerSecond * Time.unscaledDeltaTime);
        }

        private bool PlaceBattleHudOnGameplayLayer()
        {
            if (battleHudRoot == null)
                return false;

            if (battleHudHoldOverlayThroughVictory)
                return PlaceBattleHudOnSceneTransitionLayer() ||
                       PlaceBattleHudOnPersistentOverlay();

            if (SceneLoader.CurrentlyLoading &&
                !battleHudFollowNativeVictoryLayer &&
                BattleHudUsesDicePalaceChain())
                return PlaceBattleHudOnSceneTransitionLayer() ||
                       PlaceBattleHudOnPersistentOverlay();

            Canvas nativeCanvas;
            if (!TryGetNativeBattleHudCanvas(out nativeCanvas))
                return false;

            if (battleHudFollowNativeVictoryLayer)
            {
                return battleHudRoot.transform.parent ==
                       nativeCanvas.transform ||
                       TrySwapBattleHudToNativeVictoryLayer(nativeCanvas);
            }

            // Screen Space Overlay is composited after camera postprocessing.
            // Camera-wide challenges use LevelHUD's camera Canvas so the
            // roulette row receives exactly the same final-frame effect as
            // Cuphead's health and super HUD. Other challenges retain the
            // independent overlay that isolates them from the native parry
            // flash.
            if (battleHudChallengeSnapshot == ModifierId.RgbShift ||
                battleHudChallengeSnapshot == ModifierId.UpsideDown)
                return PlaceBattleHudOnNativeGameplayLayer(nativeCanvas);

            // The camera that renders LevelHUD also receives Cuphead's parry
            // flash. Keep the roulette row on its independent overlay Canvas
            // during active play so that flash cannot tint or pulse it. On a
            // final victory it moves back to LevelHUD above, allowing the
            // native knockout transition to remove both HUDs together.
            return PlaceBattleHudOnPersistentOverlay();
        }

        private bool PlaceBattleHudOnNativeGameplayLayer(Canvas nativeCanvas)
        {
            if (battleHudRoot == null || nativeCanvas == null)
                return false;
            if (battleHudRoot.transform.parent != nativeCanvas.transform)
                battleHudRoot.transform.SetParent(nativeCanvas.transform, false);
            battleHudRoot.transform.SetAsLastSibling();
            battleHudOnNativeCanvas = true;
            battleHudOnPauseLayer = false;
            FadeBattleHudRootAlphaToFull();
            return true;
        }

        private bool PlaceBattleHudOnSceneTransitionLayer()
        {
            if (battleHudRoot == null || SceneLoaderCanvasField == null)
                return false;

            Canvas sceneTransitionCanvas;
            try
            {
                var loader = SceneLoader.Exists
                    ? SceneLoader.instance
                    : null;
                sceneTransitionCanvas = loader == null
                    ? null
                    : SceneLoaderCanvasField.GetValue(loader) as Canvas;
            }
            catch
            {
                return false;
            }

            if (sceneTransitionCanvas == null ||
                !sceneTransitionCanvas.enabled ||
                !sceneTransitionCanvas.gameObject.activeInHierarchy)
                return false;

            if (battleHudRoot.transform.parent !=
                sceneTransitionCanvas.transform)
                battleHudRoot.transform.SetParent(
                    sceneTransitionCanvas.transform, false);
            // SceneLoader's fader is a later sibling on this same canvas, so
            // Cuphead's native three-second victory fade covers the roulette
            // row exactly as it covers the game image.
            battleHudRoot.transform.SetAsFirstSibling();
            battleHudOnNativeCanvas = false;
            battleHudOnPauseLayer = false;
            FadeBattleHudRootAlphaToFull();
            return true;
        }

        private bool PlaceBattleHudOnPersistentOverlay()
        {
            if (battleHudCanvas == null)
                return false;
            if (battleHudRoot.transform.parent != battleHudCanvas.transform)
                battleHudRoot.transform.SetParent(
                    battleHudCanvas.transform, false);
            battleHudRoot.transform.SetAsLastSibling();
            battleHudOnNativeCanvas = false;
            battleHudOnPauseLayer = false;
            FadeBattleHudRootAlphaToFull();
            return true;
        }

        private static bool TryGetNativeBattleHudCanvas(
            out Canvas nativeCanvas)
        {
            try
            {
                var nativeHud = LevelHUD.Current;
                nativeCanvas = nativeHud == null ? null : nativeHud.Canvas;
            }
            catch
            {
                nativeCanvas = null;
            }

            return nativeCanvas != null && nativeCanvas.enabled &&
                   nativeCanvas.gameObject.activeInHierarchy;
        }

        private bool TrySwapBattleHudToNativeVictoryLayer(
            Canvas nativeCanvas)
        {
            if (battleHudRoot == null || nativeCanvas == null)
                return false;
            if (battleHudRoot.transform.parent == nativeCanvas.transform)
            {
                battleHudRoot.transform.SetAsLastSibling();
                battleHudOnNativeCanvas = true;
                battleHudHoldOverlayThroughVictory = false;
                battleHudOnPauseLayer = false;
                return true;
            }

            var overlayRoot = battleHudRoot;
            var nativeRoot = Instantiate(overlayRoot);
            nativeRoot.name = "Gilomx Roulette Battle HUD Victory";
            nativeRoot.SetActive(false);
            nativeRoot.transform.SetParent(nativeCanvas.transform, false);
            nativeRoot.transform.SetAsLastSibling();

            var nativeIcons = nativeRoot.GetComponentsInChildren<RawImage>(true);
            var nativeText = nativeRoot.GetComponentInChildren<Text>(true);
            if (nativeIcons == null || nativeIcons.Length < 5 ||
                nativeText == null)
            {
                Destroy(nativeRoot);
                return false;
            }

            overlayRoot.SetActive(false);
            battleHudRoot = nativeRoot;
            battleHudIcons = nativeIcons;
            battleHudChallengeText = nativeText;
            battleHudOnNativeCanvas = true;
            battleHudHoldOverlayThroughVictory = false;
            battleHudOnPauseLayer = false;
            if (battleHudSaturationMaterial != null)
                battleHudSaturationMaterial.SetFloat("_Saturation", 1f);
            nativeRoot.SetActive(true);
            Destroy(overlayRoot);
            return true;
        }

        private static Transform FindActiveGameOverHudLayer()
        {
            var gameOverGuis =
                Resources.FindObjectsOfTypeAll<LevelGameOverGUI>();
            for (var i = 0; i < gameOverGuis.Length; i++)
            {
                var gameOver = gameOverGuis[i];
                if (gameOver == null || !gameOver.gameObject.scene.IsValid() ||
                    !gameOver.gameObject.activeInHierarchy)
                    continue;

                var background = FindDescendantByName(
                    gameOver.transform, "Background");
                return background ?? gameOver.transform;
            }

            return null;
        }

        private static Transform FindDescendantByName(Transform root,
            string name)
        {
            if (root == null)
                return null;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                    return child;
                var nested = FindDescendantByName(child, name);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private void DestroyBattleResultHud()
        {
            if (battleHudRoot != null)
                Destroy(battleHudRoot);
            if (battleHudCanvas != null)
                Destroy(battleHudCanvas);
            if (battleHudSaturationMaterial != null)
                Destroy(battleHudSaturationMaterial);
            if (battleHudWhiteEmptyTexture != null)
                Destroy(battleHudWhiteEmptyTexture);
            battleHudCanvas = null;
            battleHudRoot = null;
            battleHudIcons = null;
            battleHudChallengeText = null;
            battleHudSaturationMaterial = null;
            battleHudWhiteEmptyTexture = null;
            battleHudChallengeBaseMaterial = null;
            battleHudUsingSaturationMaterial = false;
            battleHudVisibleIconCount = 5;
            battleHudWasVisible = false;
            battleHudOnNativeCanvas = false;
            battleHudOnPauseLayer = false;
            battleHudRevealStartedAt = -1f;
            battleHudImpactPlayedCount = 0;
            battleHudPresentationActive = false;
            battleHudFollowNativeVictoryLayer = false;
            battleHudHoldOverlayThroughVictory = false;
            battleHudResultSnapshot = null;
            battleHudChallengeSnapshot = ModifierId.None;
        }
    }
}
