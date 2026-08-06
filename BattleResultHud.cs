using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const float BattleHudAlpha = 0.70f;
        private const float BattleHudIconSize = 48f;
        private const float BattleHudIconGap = -2f;
        private const float BattleHudRightMargin = 26f;
        private const float BattleHudBottomMargin = 15f;
        private const float BattleHudTextGap = 10f;
        private const float BattleHudMaxTextWidth = 420f;
        private const float BattleHudInitialRevealDelay = 1.1f;
        private const float BattleHudRevealStep = 0.28f;
        private const float BattleHudPulseDuration = 0.38f;
        private const float BattleHudTextRevealDuration = 0.28f;
        private const float BattleHudImpactVolume = 0.85f;

        private GameObject battleHudCanvas;
        private GameObject battleHudRoot;
        private RawImage[] battleHudIcons;
        private Text battleHudChallengeText;
        private Material battleHudSaturationMaterial;
        private int battleHudTextBaseFontSize;
        private int battleHudVisibleIconCount = 5;
        private float battleHudRevealStartedAt = -1f;
        private bool battleHudWasVisible;
        private bool battleHudOnNativeCanvas;
        private bool battleHudPresentationActive;
        private bool battleHudFollowNativeVictoryLayer;
        private RouletteResult battleHudResultSnapshot;
        private string battleHudChallengeSnapshot = "";
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
            if (SceneLoader.CurrentlyLoading &&
                (!battleHudFollowNativeVictoryLayer || battleHudRoot == null))
                return false;

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
            battleHudPresentationActive = true;
            battleHudFollowNativeVictoryLayer = false;
            battleHudChallengeSnapshot = activeChallenge ?? "";
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

        private void KeepBattleResultHudThroughVictory()
        {
            if (!battleHudPresentationActive)
                return;

            battleHudFollowNativeVictoryLayer = true;
            Canvas nativeCanvas;
            if (TryGetNativeBattleHudCanvas(out nativeCanvas))
                TrySwapBattleHudToNativeVictoryLayer(nativeCanvas);
        }

        private void EndBattleResultHudSession()
        {
            battleHudPresentationActive = false;
            battleHudFollowNativeVictoryLayer = false;
            battleHudResultSnapshot = null;
            battleHudChallengeSnapshot = "";
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
                typeof(RectTransform));
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
            battleHudChallengeText.raycastTarget = false;
            battleHudChallengeText.alignment = TextAnchor.MiddleLeft;
            battleHudChallengeText.resizeTextForBestFit = false;
            battleHudChallengeText.horizontalOverflow =
                HorizontalWrapMode.Overflow;
            battleHudChallengeText.verticalOverflow = VerticalWrapMode.Overflow;
            var textColor = battleHudChallengeText.color;
            textColor.a = BattleHudAlpha;
            battleHudChallengeText.color = textColor;
            battleHudChallengeText.material = battleHudSaturationMaterial;
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
            icon.material = battleHudSaturationMaterial;

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

                screenCanvas = canvas.transform;
                pauseBackground = background;
                textTemplate = action;
                return true;
            }

            return false;
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
                ApplyNativeBattleHudIcon(battleHudIcons[0],
                    RouletteData.Charms[charm].NativeSprite,
                    RouletteData.Charms[charm].Image);
                ApplyBattleHudChallengeIcon(battleHudIcons[1], modifier);
            }
            else
            {
                SetBattleHudVisibleIconCount(5);
                ApplyNativeBattleHudIcon(battleHudIcons[0],
                    RouletteData.Weapons[weapon1].NativeSprite,
                    RouletteData.Weapons[weapon1].Image);
                ApplyNativeBattleHudIcon(battleHudIcons[1],
                    RouletteData.Weapons[weapon2].NativeSprite,
                    RouletteData.Weapons[weapon2].Image);
                ApplyNativeBattleHudIcon(battleHudIcons[2],
                    RouletteData.Supers[super].NativeSprite,
                    RouletteData.Supers[super].Image);
                ApplyNativeBattleHudIcon(battleHudIcons[3],
                    RouletteData.Charms[charm].NativeSprite,
                    RouletteData.Charms[charm].Image);
                ApplyBattleHudChallengeIcon(battleHudIcons[4], modifier);
            }

            battleHudChallengeText.text =
                string.IsNullOrEmpty(battleHudChallengeSnapshot)
                ? ""
                : "RETO: " + battleHudChallengeSnapshot.ToUpperInvariant();
            UpdateBattleResultHudLayout();
        }

        private void ApplyBattleHudChallengeIcon(RawImage image,
            int modifier)
        {
            if (string.IsNullOrEmpty(battleHudChallengeSnapshot))
                ApplyNativeBattleHudIcon(image,
                    "equip_icon_empty_0001", "weapons/vacio.png");
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

            var textRect = battleHudChallengeText.rectTransform;
            var hasText = !string.IsNullOrEmpty(battleHudChallengeText.text);
            var textWidth = 0f;
            if (hasText)
            {
                battleHudChallengeText.fontSize = battleHudTextBaseFontSize;
                textRect.sizeDelta = new Vector2(
                    BattleHudMaxTextWidth, BattleHudIconSize);
                var preferredWidth = battleHudChallengeText.preferredWidth;
                if (preferredWidth > BattleHudMaxTextWidth)
                {
                    var fittedSize = Mathf.FloorToInt(
                        battleHudTextBaseFontSize *
                        BattleHudMaxTextWidth / preferredWidth);
                    battleHudChallengeText.fontSize = Mathf.Max(15,
                        fittedSize);
                    preferredWidth = battleHudChallengeText.preferredWidth;
                }
                textWidth = Mathf.Min(BattleHudMaxTextWidth,
                    Mathf.Ceil(preferredWidth + 2f));
            }

            textRect.anchoredPosition = new Vector2(
                BattleHudIconsWidth(battleHudVisibleIconCount) + BattleHudTextGap,
                BattleHudIconSize * 0.5f);
            textRect.sizeDelta = new Vector2(textWidth, BattleHudIconSize);

            var rootWidth = BattleHudIconsWidth(battleHudVisibleIconCount);
            if (hasText)
                rootWidth += BattleHudTextGap + textWidth;
            ((RectTransform)battleHudRoot.transform).sizeDelta =
                new Vector2(rootWidth, BattleHudIconSize);
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
            EnsureBattleHudSaturationMaterial();
            if (battleHudSaturationMaterial == null)
                return;

            // The LevelHUD camera already receives the scene transition.
            var saturation = battleHudOnNativeCanvas
                ? 1f : string.Equals(battleHudChallengeSnapshot,
                BlackAndWhiteChallenge, StringComparison.OrdinalIgnoreCase)
                ? 1f - Mathf.Clamp01(blackAndWhiteBlend)
                : 1f;
            battleHudSaturationMaterial.SetFloat("_Saturation", saturation);

            if (battleHudIcons != null)
            {
                for (var i = 0; i < battleHudIcons.Length; i++)
                {
                    if (battleHudIcons[i] != null &&
                        battleHudIcons[i].material !=
                        battleHudSaturationMaterial)
                        battleHudIcons[i].material =
                            battleHudSaturationMaterial;
                }
            }
            if (battleHudChallengeText != null &&
                battleHudChallengeText.material !=
                battleHudSaturationMaterial)
                battleHudChallengeText.material =
                    battleHudSaturationMaterial;
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

            try
            {
                if (Convert.ToInt32(PauseManager.state) != 0)
                {
                    Transform screenCanvas;
                    Transform pauseBackground;
                    Text textTemplate;
                    TryFindBattleHudNativeLayers(out screenCanvas,
                        out pauseBackground, out textTemplate);
                    if (pauseBackground != null)
                    {
                        PlaceBattleHudInsideMenuLayer(pauseBackground);
                        return true;
                    }

                    return false;
                }
            }
            catch
            {
            }

            return PlaceBattleHudOnGameplayLayer();
        }

        private void PlaceBattleHudInsideMenuLayer(Transform layer)
        {
            // King Dice is a chain of scene-local fights. Keeping its active
            // HUD under the persistent overlay prevents a surviving pause,
            // defeat, or transition layer from making it react to parry
            // flashes after the next internal scene loads. The final victory
            // explicitly enables the native layer before reaching this path.
            if (BattleHudUsesDicePalaceChain() &&
                !battleHudFollowNativeVictoryLayer &&
                battleHudCanvas != null)
            {
                if (battleHudRoot.transform.parent !=
                    battleHudCanvas.transform)
                    battleHudRoot.transform.SetParent(
                        battleHudCanvas.transform, false);
                battleHudRoot.transform.SetAsLastSibling();
                battleHudOnNativeCanvas = false;
                return;
            }

            if (battleHudRoot.transform.parent != layer)
                battleHudRoot.transform.SetParent(layer, false);
            battleHudRoot.transform.SetAsFirstSibling();
            battleHudOnNativeCanvas = false;
        }

        private bool PlaceBattleHudOnGameplayLayer()
        {
            if (battleHudRoot == null)
                return false;

            Canvas nativeCanvas;
            if (!TryGetNativeBattleHudCanvas(out nativeCanvas))
                return false;

            if (battleHudFollowNativeVictoryLayer)
            {
                return battleHudRoot.transform.parent ==
                       nativeCanvas.transform ||
                       TrySwapBattleHudToNativeVictoryLayer(nativeCanvas);
            }

            // The camera that renders LevelHUD also receives Cuphead's parry
            // flash. Keep the roulette row on its independent overlay Canvas
            // during active play so that flash cannot tint or pulse it. On a
            // final victory it moves back to LevelHUD above, allowing the
            // native knockout transition to remove both HUDs together.
            if (battleHudCanvas == null)
                return false;
            if (battleHudRoot.transform.parent != battleHudCanvas.transform)
                battleHudRoot.transform.SetParent(
                    battleHudCanvas.transform, false);
            battleHudRoot.transform.SetAsLastSibling();
            battleHudOnNativeCanvas = false;
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
            battleHudCanvas = null;
            battleHudRoot = null;
            battleHudIcons = null;
            battleHudChallengeText = null;
            battleHudSaturationMaterial = null;
            battleHudVisibleIconCount = 5;
            battleHudWasVisible = false;
            battleHudOnNativeCanvas = false;
            battleHudRevealStartedAt = -1f;
            battleHudImpactPlayedCount = 0;
            battleHudPresentationActive = false;
            battleHudFollowNativeVictoryLayer = false;
            battleHudResultSnapshot = null;
            battleHudChallengeSnapshot = "";
        }
    }
}
