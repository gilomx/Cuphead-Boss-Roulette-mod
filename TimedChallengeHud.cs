using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private RectTransform timedChallengeRoot;
        private RectTransform timedWarningRoot;
        private RectTransform timedActiveRoot;
        private CanvasGroup timedWarningGroup;
        private CanvasGroup timedActiveGroup;
        private Text timedWarningTitle;
        private Text timedWarningNumber;
        private Text timedActiveTitle;
        private Text timedActiveDonor;
        private float timedHudRetryAt;
        private readonly CreatorToolsTimedChallengeHudState timedHudState = new CreatorToolsTimedChallengeHudState();
        private const float TimedHudTopMargin = 35f;

        private bool PrepareTimedChallengeHud()
        {
            if (timedChallengeRoot != null) return true;
            if (Time.realtimeSinceStartup < timedHudRetryAt) return false;
            timedHudRetryAt = Time.realtimeSinceStartup + 0.5f;
            if (!PrepareBattleResultHud() || battleHudChallengeText == null) return false;
            // Share the working roulette canvas, scale and typography. This
            // root is independent of whether the roulette row is visible.
            timedChallengeRoot = (RectTransform)new GameObject("Gilomx Timed Challenge HUD",
                typeof(RectTransform)).transform;
            PlaceTimedChallengeRoot(battleHudCanvas.transform, false);
            var template = battleHudChallengeText;
            var size = battleHudTextBaseFontSize;
            timedWarningRoot = CreateTimedHudGroup("Challenge incoming", new Vector2(0.5f, 1f), out timedWarningGroup);
            timedActiveRoot = CreateTimedHudGroup("Active challenge", new Vector2(0.5f, 1f), out timedActiveGroup);
            timedWarningTitle = CreateTimedHudText(template, timedWarningRoot, "Incoming label", size, 0f);
            timedWarningNumber = CreateTimedHudText(template, timedWarningRoot, "Countdown", size * 3, -size * 2.2f);
            timedActiveTitle = CreateTimedHudText(template, timedActiveRoot, "Challenge and remaining time", size, 0f);
            timedActiveDonor = CreateTimedHudText(template, timedActiveRoot, "Sender", size, -size * 1.45f);
            timedChallengeRoot.gameObject.SetActive(false);
            Logger.LogInfo("HUD de reto temporal preparado en la capa de la ruleta.");
            return true;
        }

        private void PlaceTimedChallengeRoot(Transform parent, bool behindMenu)
        {
            if (timedChallengeRoot.parent != parent)
                timedChallengeRoot.SetParent(parent, false);
            if (behindMenu) timedChallengeRoot.SetAsFirstSibling();
            else timedChallengeRoot.SetAsLastSibling();
            timedChallengeRoot.anchorMin = Vector2.zero;
            timedChallengeRoot.anchorMax = Vector2.one;
            timedChallengeRoot.offsetMin = timedChallengeRoot.offsetMax = Vector2.zero;
            timedChallengeRoot.localScale = Vector3.one;
            timedChallengeRoot.localRotation = Quaternion.identity;
            timedChallengeRoot.anchoredPosition3D = Vector3.zero;
            if (timedChallengeRoot.gameObject.layer != parent.gameObject.layer)
                foreach (var child in timedChallengeRoot.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = parent.gameObject.layer;
        }

        private bool UpdateTimedChallengeHudLayer()
        {
            var gameOverLayer = FindActiveGameOverHudLayer();
            if (gameOverLayer != null)
            {
                // Match the roulette HUD: it belongs inside the defeat
                // background rather than covering the native menu's buttons.
                PlaceTimedChallengeRoot(gameOverLayer, true);
                return true;
            }
            if (timedHudState.DefeatSnapshot != null)
            {
                // LevelHUD can disappear before the death menu animates in.
                PlaceTimedChallengeRoot(battleHudCanvas.transform, false);
                return true;
            }
            LevelPauseGUI pause;
            if (TryGetActiveLevelPauseMenu(out pause))
            {
                PlaceTimedChallengeRoot(pause.transform, true);
                return true;
            }
            Canvas nativeCanvas;
            if (!TryGetNativeBattleHudCanvas(out nativeCanvas)) return false;
            var cameraEffect = ShouldShowActiveChallenge() &&
                (activeChallenge == ModifierId.RgbShift || activeChallenge == ModifierId.UpsideDown);
            PlaceTimedChallengeRoot(cameraEffect ? nativeCanvas.transform : battleHudCanvas.transform, false);
            return true;
        }

        private RectTransform CreateTimedHudGroup(string name, Vector2 anchor, out CanvasGroup group)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(timedChallengeRoot, false);
            obj.layer = timedChallengeRoot.gameObject.layer;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 180f);
            group = obj.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return rect;
        }

        private Text CreateTimedHudText(Text template, RectTransform parent, string name, int size, float y)
        {
            // Copy appearance only. Cloning native menu objects also copies
            // localization/animation behaviours and their hidden render state.
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.gameObject.layer = parent.gameObject.layer;
            text.font = template.font;
            text.fontStyle = template.fontStyle;
            text.lineSpacing = template.lineSpacing;
            text.gameObject.SetActive(true);
            text.enabled = true;
            text.fontSize = Mathf.Max(1, size);
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var color = template.color;
            color.a = 1f;
            text.color = color;
            // A current camera challenge can tint the battle HUD material.
            // Start with its native text material, not a temporary effect.
            text.material = battleHudChallengeBaseMaterial;
            foreach (var source in template.GetComponents<Shadow>())
            {
                var shadow = source is Outline ? text.gameObject.AddComponent<Outline>() : text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = source.effectColor;
                shadow.effectDistance = source.effectDistance;
                shadow.useGraphicAlpha = source.useGraphicAlpha;
                shadow.enabled = source.enabled;
            }
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = new Vector3(0f, y, 0f);
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(900f, size * 1.8f);
            rect.localScale = Vector3.one;
            return text;
        }

        private void UpdateTimedChallengeHud()
        {
            if (timedChallengeInteractions == null || timedHudState.WaitingForAttempt ||
                (!timedChallengeInteractions.Busy && timedHudState.DefeatSnapshot == null) || SceneLoader.CurrentlyLoading)
            {
                HideTimedChallengeHud();
                return;
            }
            if (!PrepareTimedChallengeHud()) return;
            if (!UpdateTimedChallengeHudLayer())
            {
                timedChallengeRoot.gameObject.SetActive(false);
                return;
            }
            timedChallengeRoot.gameObject.SetActive(true);
            var frozen = timedHudState.DefeatSnapshot != null;
            var timer = timedHudState.DefeatSnapshot ?? timedChallengeInteractions.Presentation;
            var warning = timer.CountingDown;
            timedWarningRoot.gameObject.SetActive(warning);
            timedActiveRoot.gameObject.SetActive(!warning);
            var pauseAlpha = !frozen && IsCreatorToolsInteractionPaused() ? BattleHudPauseAlphaMultiplier : 1f;
            if (warning)
            {
                timedWarningTitle.text = L(ModText.ChallengeIncoming).ToUpperInvariant();
                timedWarningNumber.text = Mathf.Max(1, Mathf.CeilToInt(timer.CountdownRemaining)).ToString();
                var exiting = !frozen && timer.Phase == TimedChallengePhase.CountdownExit;
                var t = Mathf.Clamp01(timer.PhaseElapsed / (exiting ? CreatorToolsTimedChallenge.ExitSeconds : 0.25f));
                var eased = frozen ? 1f : 1f - Mathf.Pow(1f - t, 3f);
                timedWarningGroup.alpha = (exiting ? 1f - eased : eased) * pauseAlpha;
                timedWarningRoot.anchoredPosition = new Vector2(0f, -TimedHudTopMargin + (exiting ? eased * 14f : 0f));
                timedWarningRoot.localScale = Vector3.one * (exiting ? 1f + eased * 0.12f : 0.90f + eased * 0.10f);
                var beat = Mathf.Clamp01((timer.PhaseElapsed % 1f) / 0.22f);
                timedWarningNumber.rectTransform.localScale = Vector3.one * (exiting || frozen ? 1f : 1f + 0.14f * (1f - beat) * (1f - beat));
                if (timedHudState.TakeCountdownCue(timer, timedChallengeInteractions.GameplayAvailable))
                    PlayNativeMenuSound("menu_equipment_move", selectionClip, 0.45f);
            }
            else
            {
                timedActiveTitle.text = LocalizedChallengeLabel(ModifierId.HalfDamage).ToUpperInvariant() +
                    "  ·  " + Mathf.CeilToInt(timer.Remaining) + " S";
                timedActiveDonor.text = ((frozen ? timedHudState.DefeatDonor : timedChallengeInteractions.Donor) ?? string.Empty).ToUpperInvariant();
                timedActiveDonor.gameObject.SetActive(!string.IsNullOrEmpty(timedActiveDonor.text));
                var exiting = !frozen && timer.Phase == TimedChallengePhase.Exit;
                var t = Mathf.Clamp01(timer.PhaseElapsed / (exiting ? CreatorToolsTimedChallenge.ExitSeconds : BattleHudTextRevealDuration));
                var eased = frozen ? 1f : 1f - Mathf.Pow(1f - t, 3f);
                timedActiveGroup.alpha = BattleHudAlpha * pauseAlpha * (exiting ? 1f - eased : eased);
                timedActiveRoot.anchoredPosition = new Vector2(0f, -TimedHudTopMargin + (exiting ? eased * 10f : (1f - eased) * 10f));
                timedActiveRoot.localScale = Vector3.one * (exiting ? 1f - eased * 0.06f : 1.06f - eased * 0.06f);
            }
        }

        private void HoldTimedChallengeHudAfterDefeat(Level level)
        {
            if (level == null || timedChallengeInteractions == null ||
                level.GetInstanceID() != creatorToolsInteractionLevelInstanceId) return;
            timedHudState.HoldAfterDefeat(timedChallengeInteractions.Presentation, timedChallengeInteractions.Donor);
        }

        private void ResetTimedChallengeHudForAttempt(bool waitingForAttempt)
        {
            timedHudState.Reset(waitingForAttempt);
            if (timedChallengeInteractions != null) timedChallengeInteractions.EndGameplayLevel();
            HideTimedChallengeHud();
        }

        private void HideTimedChallengeHud()
        {
            if (timedChallengeRoot == null) return;
            // Keep our root alive when the native scene/menu unloads.
            if (battleHudCanvas != null && timedChallengeRoot.parent != battleHudCanvas.transform)
                PlaceTimedChallengeRoot(battleHudCanvas.transform, false);
            timedChallengeRoot.gameObject.SetActive(false);
        }

        private void DestroyTimedChallengeHud()
        {
            if (timedChallengeRoot != null) Destroy(timedChallengeRoot.gameObject);
            timedChallengeRoot = null;
            timedHudRetryAt = 0f;
        }
    }
}
