using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const int EquipActionUnequip = 15;
        private const float ManualChallengeFramesPerSecond = 12.5f;
        private const string ManualCupheadFrontAsset =
            "equip-card/353_ch_equip_front_no_text.png";
        private const string ManualMugmanFrontAsset =
            "equip-card/251_mm_equip_front_no_text.png";
        private const string ManualChallengeGridAsset =
            "equip-card/968_generic_equip_back_9_icons.png";

        private static readonly int[] ManualChallengeRowStarts = { 0, 5, 9 };
        private static readonly int[] ManualChallengeRowLengths = { 5, 4, 3 };
        private static readonly Vector2 ManualChallengeTextBlockOffset =
            new Vector2(0f, -28f);
        private static readonly Vector2 ManualChallengeDescriptionOffset =
            new Vector2(0f, -8f);
        private static readonly Vector2[] ManualChallengeIconPositions =
        {
            new Vector2(-202f, 114f),
            new Vector2(-101f, 114f),
            new Vector2(-3f, 114f),
            new Vector2(98f, 114f),
            new Vector2(196f, 114f),
            new Vector2(-151f, 21f),
            new Vector2(-53f, 21f),
            new Vector2(47f, 20f),
            new Vector2(147f, 20f),
            new Vector2(-103f, -68f),
            new Vector2(-2f, -68f),
            new Vector2(96f, -68f)
        };

        private static readonly ModifierId[] ManualChallengeDisplayOrder =
        {
            ModifierId.BlackAndWhite,
            ModifierId.RgbShift,
            ModifierId.InkRain,
            ModifierId.UpsideDown,
            ModifierId.HpOne,
            ModifierId.StiffMode,
            ModifierId.HalfDamage,
            ModifierId.NoDash,
            ModifierId.NoEx,
            ModifierId.MiniPlaneOnly,
            ModifierId.NoBombs,
            ModifierId.NoPeashooter
        };

        private static readonly FieldInfo MapEquipCardFrontField =
            AccessTools.Field(typeof(MapEquipUICard), "front");
        private static readonly FieldInfo MapEquipCardBackSelectField =
            AccessTools.Field(typeof(MapEquipUICard), "backSelect");
        private static readonly FieldInfo MapEquipCardPlayerInputField =
            AccessTools.Field(typeof(MapEquipUICard), "playerInput");
        private static readonly MethodInfo MapEquipCardRotateToBackSelectMethod =
            AccessTools.Method(typeof(MapEquipUICard), "RotateToBackSelect");
        private static readonly MethodInfo MapEquipCardRotateToFrontMethod =
            AccessTools.Method(typeof(MapEquipUICard), "RotateToFront");

        private static readonly FieldInfo BackSelectHeaderTextField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "headerText");
        private static readonly FieldInfo BackSelectTitleTextField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "titleText");
        private static readonly FieldInfo BackSelectExTextField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "exText");
        private static readonly FieldInfo BackSelectDescriptionTextField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect),
                "descriptionText");
        private static readonly FieldInfo BackSelectCursorField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "cursor");
        private static readonly FieldInfo BackSelectSelectionCursorField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect),
                "selectionCursor");
        private static readonly FieldInfo BackSelectIconsBackField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "iconsBack");
        private static readonly FieldInfo BackSelectSuperIconsBackField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect),
                "superIconsBack");
        private static readonly FieldInfo BackSelectDlcIconsBackField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "DLCIconsBack");
        private static readonly FieldInfo BackSelectNormalIconsField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "normalIcons");
        private static readonly FieldInfo BackSelectSuperIconsField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "superIcons");
        private static readonly FieldInfo BackSelectDlcIconsField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "DLCIcons");
        private static readonly FieldInfo BackSelectSelectedIconsField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect),
                "selectedIcons");
        private static readonly FieldInfo BackSelectIndexField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "index");
        private static readonly FieldInfo BackSelectLastIndexField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "lastIndex");
        private static readonly FieldInfo BackSelectSlotField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "slot");
        private static readonly FieldInfo BackSelectLastSlotField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect), "lastSlot");
        private static readonly FieldInfo BackSelectNoneUnlockedField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect),
                "noneUnlocked");
        private static readonly FieldInfo BackSelectItemSelectedField =
            AccessTools.Field(typeof(MapEquipUICardBackSelect),
                "itemSelected");

        private static readonly FieldInfo MapCardIconImageField =
            AccessTools.Field(typeof(AbstractMapCardIcon), "iconImage");
        private static readonly FieldInfo MapCardIconFramesField =
            AccessTools.Field(typeof(AbstractMapCardIcon), "icons");
        private static readonly FieldInfo MapCardIconNormalFramesField =
            AccessTools.Field(typeof(AbstractMapCardIcon), "normalIcons");
        private static readonly FieldInfo MapCardIconGreyFramesField =
            AccessTools.Field(typeof(AbstractMapCardIcon), "greyIcons");
        private static readonly FieldInfo BackSelectIconIndexField =
            AccessTools.Field(typeof(MapEquipUICardBackSelectIcon),
                "<Index>k__BackingField");

        private sealed class ManualChallengeCardState
        {
            internal MapEquipUICard Card;
            internal MapEquipUICardFront Front;
            internal MapEquipUICardBackSelect Back;
            internal bool SelectorActive;
            internal int Index;
            internal MapEquipUICardBackSelectIcon[] OriginalDlcIcons;
            internal Vector2[] OriginalDlcIconPositions;
            internal Vector2[] OriginalDlcImagePositions;
            internal Vector2[] OriginalDlcImageSizes;
            internal Vector3[] OriginalDlcImageScales;
            internal bool[] OriginalDlcImagePreserveAspect;
            internal Color[] OriginalDlcImageColors;
            internal MapEquipUICardBackSelectIcon[] ChallengeIcons;
            internal GameObject[] ClonedIconObjects;
            internal Image ChallengeGridImage;
            internal Sprite OriginalChallengeGridSprite;
            internal bool OriginalChallengeGridEnabled;
            internal bool ChallengeGridCaptured;
            internal GameObject HeaderFallback;
            internal bool HeaderImageWasEnabled;
            internal bool HeaderImageStateCaptured;
            internal bool DescriptionStyleCaptured;
            internal bool DescriptionAutoSizing;
            internal bool DescriptionWordWrapping;
            internal bool DescriptionRichText;
            internal float DescriptionFontSizeMin;
            internal float DescriptionFontSizeMax;
            internal int LastAnimationTick = -1;
            internal bool SelectorIconsNormalized;
            internal bool TitleStyleCaptured;
            internal bool TitleResizeTextForBestFit;
            internal int TitleResizeTextMinSize;
            internal int TitleResizeTextMaxSize;
            internal Vector2 TitleAnchoredPosition;
            internal bool TitlePositionCaptured;
            internal bool ExStyleCaptured;
            internal bool ExResizeTextForBestFit;
            internal int ExResizeTextMinSize;
            internal int ExResizeTextMaxSize;
            internal Vector2 ExAnchoredPosition;
            internal bool ExPositionCaptured;
            internal Vector2 DescriptionAnchoredPosition;
            internal bool DescriptionPositionCaptured;
        }

        private readonly Dictionary<int, ManualChallengeCardState>
            manualChallengeCardStates =
                new Dictionary<int, ManualChallengeCardState>();
        private readonly Dictionary<ModifierId, Sprite[]>
            manualChallengeSprites =
                new Dictionary<ModifierId, Sprite[]>();
        private readonly List<Sprite> ownedManualChallengeSprites =
            new List<Sprite>();
        private Sprite manualCupheadFrontSprite;
        private Sprite manualMugmanFrontSprite;
        private Sprite manualChallengeGridSprite;

        private ConfigEntry<ModifierId>[] equippedChallengeBySave;
        private bool activeChallengeFromManualEquipment;
        private bool activeChallengeTargetAssigned;
        private Levels activeChallengeTargetLevel;
        private bool activeChallengePlaneControls;

        private void InitializeManualChallengeEquipment()
        {
            equippedChallengeBySave = new ConfigEntry<ModifierId>[3];
            for (var i = 0; i < equippedChallengeBySave.Length; i++)
            {
                equippedChallengeBySave[i] = Config.Bind(
                    "Juego",
                    "RetoEquipadoPartida" + (i + 1),
                    ModifierId.None,
                    "Reto equipado fuera de la ruleta para la partida " +
                    (i + 1) + ". Se aplica al iniciar un combate compatible.");
            }
        }

        private void InstallManualChallengeEquipmentPatches()
        {
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICardFront), "Init"),
                null,
                "ManualChallengeFrontInitPostfix",
                "initialization of the native challenge slot");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICardFront), "Refresh"),
                null,
                "ManualChallengeFrontRefreshPostfix",
                "refresh of the native challenge slot");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICardFront),
                    "ChangeSelection"),
                null,
                "ManualChallengeFrontSelectionPostfix",
                "title of the native challenge slot");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(LocalizationHelper),
                    "ApplyTranslation",
                    new[] { typeof(TranslationElement) }),
                null,
                "ManualChallengeListLabelTranslationPostfix",
                "localized label of the native challenge slot");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICard),
                    "HandleInputFront"),
                "ManualChallengeFrontInputPrefix",
                null,
                "input for the native challenge slot");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICard),
                    "HandleInputBackSelect"),
                "ManualChallengeBackInputPrefix",
                null,
                "input for the native challenge selector");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICard), "OnDestroy"),
                "ManualChallengeCardDestroyPrefix",
                null,
                "cleanup of the native challenge selector");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICard), "ResetToFront"),
                null,
                "ManualChallengeResetToFrontPostfix",
                "reset of the challenge selector during player changes");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(MapEquipUICardBackSelect),
                    "set_selection_cursor"),
                "ManualChallengeSelectionCursorCoroutinePrefix",
                null,
                "delayed cursor ownership for the challenge selector");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(PlayerData), "ClearSlot"),
                null,
                "ClearManualChallengeForDeletedSavePostfix",
                "cleanup of challenges for deleted saves");
            PatchManualChallengeMethod(
                AccessTools.Method(typeof(Level), "Awake"),
                "ActivateEquippedChallengeForLevelPrefix",
                null,
                "activation of equipped challenges in normal battles");
        }

        private void PatchManualChallengeMethod(MethodInfo original,
            string prefixName, string postfixName, string label)
        {
            var prefix = string.IsNullOrEmpty(prefixName)
                ? null
                : AccessTools.Method(typeof(Plugin), prefixName);
            var postfix = string.IsNullOrEmpty(postfixName)
                ? null
                : AccessTools.Method(typeof(Plugin), postfixName);
            if (original == null ||
                (!string.IsNullOrEmpty(prefixName) && prefix == null) ||
                (!string.IsNullOrEmpty(postfixName) && postfix == null))
            {
                Logger.LogWarning("Could not install " + label + ".");
                return;
            }

            harmony.Patch(original,
                prefix == null ? null : new HarmonyMethod(prefix),
                postfix == null ? null : new HarmonyMethod(postfix));
        }

        private static void ManualChallengeFrontInitPostfix(
            MapEquipUICardFront __instance)
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.RefreshManualChallengeFront(__instance);
        }

        private static void ManualChallengeFrontRefreshPostfix(
            MapEquipUICardFront __instance)
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.RefreshManualChallengeFront(__instance);
        }

        private static void ManualChallengeFrontSelectionPostfix(
            MapEquipUICardFront __instance)
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.RefreshManualChallengeFront(__instance);
        }

        private static void ManualChallengeListLabelTranslationPostfix(
            LocalizationHelper __instance)
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.RefreshManualChallengeLocalizedElement(__instance);
        }

        private static bool ManualChallengeFrontInputPrefix(
            MapEquipUICard __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                MapEquipCardFrontField == null ||
                MapEquipCardPlayerInputField == null)
                return true;

            var front = MapEquipCardFrontField.GetValue(__instance) as
                MapEquipUICardFront;
            if (front == null || !front.checkListSelected)
                return true;

            var input = MapEquipCardPlayerInputField.GetValue(__instance) as
                Player;
            if (input == null)
                return true;

            if (input.GetButtonDown((int)CupheadButton.Accept))
            {
                plugin.OpenManualChallengeSelector(__instance, front);
                return false;
            }

            if (input.GetButtonDown(EquipActionUnequip))
            {
                AudioManager.Play("menu_equipment_equip");
                plugin.SetEquippedManualChallenge(ModifierId.None);
                front.Refresh();
                front.ChangeSelection(0);
                return false;
            }

            return true;
        }

        private static bool ManualChallengeBackInputPrefix(
            MapEquipUICard __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null)
                return true;

            ManualChallengeCardState state;
            if (!plugin.manualChallengeCardStates.TryGetValue(
                    __instance.GetInstanceID(), out state) ||
                state == null || !state.SelectorActive)
                return true;

            return plugin.HandleManualChallengeBackInput(state);
        }

        private static void ManualChallengeCardDestroyPrefix(
            MapEquipUICard __instance)
        {
            var plugin = activeInstance;
            if (plugin != null && __instance != null)
                plugin.RemoveManualChallengeCardState(__instance);
        }

        private static void ManualChallengeResetToFrontPostfix(
            MapEquipUICard __instance)
        {
            var plugin = activeInstance;
            if (plugin != null && __instance != null)
                plugin.DeactivateManualChallengeSelector(__instance);
        }

        private static bool ManualChallengeSelectionCursorCoroutinePrefix(
            MapEquipUICardBackSelect __instance, ref IEnumerator __result)
        {
            var plugin = activeInstance;
            if (plugin == null ||
                !plugin.IsManualChallengeSelectorActive(__instance))
                return true;

            // Setup(CHARM) can start a delayed native coroutine which later
            // restores the charm checkmark. Suppress only that coroutine while
            // this back card belongs to the challenge selector.
            __result = LockManualChallengeSelectorInput(__instance);
            return false;
        }

        private static IEnumerator LockManualChallengeSelectorInput(
            MapEquipUICardBackSelect back)
        {
            if (back == null)
                yield break;

            back.lockInput = true;
            try
            {
                yield return new WaitForSeconds(0.2f);
            }
            finally
            {
                if (back != null)
                    back.lockInput = false;
            }
        }

        private static void ClearManualChallengeForDeletedSavePostfix(
            int __0)
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.ClearEquippedManualChallengeForSaveSlot(__0);
        }

        private static void ActivateEquippedChallengeForLevelPrefix(
            Level __instance)
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.TryActivateEquippedChallengeForLevel(__instance);
        }

        private void RefreshManualChallengeFront(MapEquipUICardFront front)
        {
            if (front == null || modLocalization == null)
                return;

            var challenge = GetEquippedManualChallenge();
            SetManualChallengeIcon(front.checklist, challenge);
            RefreshManualChallengeSlotLabels(front);
            if (front.checkListSelected && front.title != null)
            {
                var name = challenge == ModifierId.None
                    ? modLocalization.Text(ModText.EquipEmpty)
                    : modLocalization.ModifierName(challenge);
                front.title.text = name.ToUpperInvariant();
            }
        }

        private void RefreshManualChallengeSlotLabels(
            MapEquipUICardFront front)
        {
            if (front == null)
                return;
            var helpers = front.GetComponentsInChildren<
                LocalizationHelper>(true);
            for (var i = 0; i < helpers.Length; i++)
                RefreshManualChallengeLocalizedElement(helpers[i]);
        }

        private void RefreshManualChallengeLocalizedElement(
            LocalizationHelper helper)
        {
            if (helper == null || modLocalization == null)
                return;
            var front = helper.GetComponentInParent<MapEquipUICardFront>();
            if (front == null)
                return;

            ApplyManualChallengeFrontBackground(helper);
            if (helper.textComponent == null)
                return;

            ModText label;
            var isChallenge = front.checklist != null &&
                helper.transform.IsChildOf(front.checklist.transform);
            if (isChallenge)
                label = ModText.SlotChallenge;
            else if (Localization.language != Localization.Languages.English)
                return;
            else if (front.weaponA != null &&
                     helper.transform.IsChildOf(front.weaponA.transform))
                label = ModText.SlotWeaponA;
            else if (front.weaponB != null &&
                     helper.transform.IsChildOf(front.weaponB.transform))
                label = ModText.SlotWeaponB;
            else if (front.super != null &&
                     helper.transform.IsChildOf(front.super.transform))
                label = ModText.SlotSuper;
            else if (front.item != null &&
                     helper.transform.IsChildOf(front.item.transform))
                label = ModText.SlotCharm;
            else
                return;

            helper.textComponent.text = EquipCardLabel(label);
            helper.textComponent.enabled = true;
        }

        private string EquipCardLabel(ModText label)
        {
            if (Localization.language == Localization.Languages.English)
            {
                switch (label)
                {
                    case ModText.SlotWeaponA:
                        return "Shot-A";
                    case ModText.SlotWeaponB:
                        return "Shot-B";
                    case ModText.SlotSuper:
                        return "Super";
                    case ModText.SlotCharm:
                        return "Charm";
                    case ModText.SlotChallenge:
                        return "Challenge";
                }
            }

            var text = modLocalization.Text(label);
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            text = text.ToLowerInvariant();
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private void ApplyManualChallengeFrontBackground(
            LocalizationHelper helper)
        {
            if (helper == null || helper.imageComponent == null)
                return;

            var objectName = helper.gameObject.name ?? string.Empty;
            Sprite replacement = null;
            if (objectName.StartsWith("Back_Cuphead",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (manualCupheadFrontSprite == null)
                    manualCupheadFrontSprite = LoadManualEquipCardSprite(
                        ManualCupheadFrontAsset,
                        "Gilomx Cuphead Challenge Equip Front");
                replacement = manualCupheadFrontSprite;
            }
            else if (objectName.StartsWith("Back_Mugman",
                         StringComparison.OrdinalIgnoreCase))
            {
                if (manualMugmanFrontSprite == null)
                    manualMugmanFrontSprite = LoadManualEquipCardSprite(
                        ManualMugmanFrontAsset,
                        "Gilomx Mugman Challenge Equip Front");
                replacement = manualMugmanFrontSprite;
            }

            if (replacement == null)
                return;
            helper.imageComponent.sprite = replacement;
        }

        private Sprite LoadManualEquipCardSprite(string relativePath,
            string spriteName)
        {
            var texture = GetTexture(relativePath);
            if (texture == null)
                return null;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = spriteName;
            ownedManualChallengeSprites.Add(sprite);
            return sprite;
        }

        private void RefreshAllManualChallengeFronts()
        {
            var fronts = Resources.FindObjectsOfTypeAll<MapEquipUICardFront>();
            for (var i = 0; i < fronts.Length; i++)
            {
                var front = fronts[i];
                if (front != null && front.gameObject.scene.IsValid())
                    RefreshManualChallengeFront(front);
            }
        }

        private void RefreshManualChallengeLocalization()
        {
            RefreshAllManualChallengeFronts();
            foreach (var pair in manualChallengeCardStates)
            {
                var state = pair.Value;
                if (state == null || !state.SelectorActive)
                    continue;
                UpdateManualChallengeSelectorText(state);
                ApplyManualChallengeHeader(state);
            }
        }

        private void OpenManualChallengeSelector(MapEquipUICard card,
            MapEquipUICardFront front)
        {
            if (card == null || front == null ||
                MapEquipCardBackSelectField == null ||
                MapEquipCardRotateToBackSelectMethod == null)
                return;

            var back = MapEquipCardBackSelectField.GetValue(card) as
                MapEquipUICardBackSelect;
            if (back == null)
                return;

            var state = GetOrCreateManualChallengeCardState(card, front, back);
            state.SelectorActive = true;

            // Let Cuphead create the real back-select card first. We then
            // populate that card with challenge art and keep its native flip,
            // cursor, typography and sounds.
            MapEquipCardRotateToBackSelectMethod.Invoke(card,
                new object[] { MapEquipUICard.Slot.CHARM });
            SetupManualChallengeSelector(state, true);
        }

        private ManualChallengeCardState GetOrCreateManualChallengeCardState(
            MapEquipUICard card, MapEquipUICardFront front,
            MapEquipUICardBackSelect back)
        {
            var key = card.GetInstanceID();
            ManualChallengeCardState state;
            if (!manualChallengeCardStates.TryGetValue(key, out state) ||
                state == null)
            {
                state = new ManualChallengeCardState();
                manualChallengeCardStates[key] = state;
            }
            state.Card = card;
            state.Front = front;
            state.Back = back;
            return state;
        }

        private void SetupManualChallengeSelector(
            ManualChallengeCardState state, bool focusEquippedChallenge)
        {
            if (state == null || state.Back == null)
                return;

            if (!EnsureManualChallengeGrid(state))
            {
                Logger.LogWarning(
                    "The native equipment card could not create 12 challenge slots.");
                state.SelectorActive = false;
                if (state.Card != null &&
                    MapEquipCardRotateToFrontMethod != null)
                    MapEquipCardRotateToFrontMethod.Invoke(
                        state.Card, new object[0]);
                return;
            }

            var challengeIcons = state.ChallengeIcons;
            if (challengeIcons == null || challengeIcons.Length == 0)
            {
                Logger.LogWarning(
                    "The native equipment card has no reusable challenge slots.");
                state.SelectorActive = false;
                if (state.Card != null &&
                    MapEquipCardRotateToFrontMethod != null)
                    MapEquipCardRotateToFrontMethod.Invoke(
                        state.Card, new object[0]);
                return;
            }

            if (focusEquippedChallenge)
            {
                var equippedIndex = IndexOfManualChallenge(
                    GetEquippedManualChallenge());
                if (equippedIndex < 0)
                    equippedIndex = 0;
                state.Index = equippedIndex;
            }

            if (focusEquippedChallenge)
                Logger.LogInfo("Native challenge selector: " +
                               ManualChallengeDisplayOrder.Length +
                               " challenges on one 5-4-3 grid.");
            var itemCount = ManualChallengeDisplayOrder.Length;
            state.Index = Mathf.Clamp(state.Index, 0,
                Math.Max(0, itemCount - 1));

            SetBackSelectGroupActive(
                GetBackSelectIcons(BackSelectSuperIconsField, state.Back),
                false);
            var normalIcons = GetBackSelectIcons(
                BackSelectNormalIconsField, state.Back);
            var dlcIcons = GetBackSelectIcons(
                BackSelectDlcIconsField, state.Back);
            SetBackSelectGroupActive(normalIcons, false);
            SetBackSelectGroupActive(dlcIcons, true);
            SetBackSelectGroupActive(state.ChallengeIcons, true);
            SetImageEnabled(BackSelectIconsBackField, state.Back,
                false);
            SetImageEnabled(BackSelectSuperIconsBackField, state.Back, false);
            SetImageEnabled(BackSelectDlcIconsBackField, state.Back,
                true);

            // Keep the native back-card bookkeeping coherent with the grid we
            // own. This also makes any native animation callback observe the
            // challenge grid rather than the DLC charm grid.
            SetBackSelectNativeState(state, challengeIcons);
            ApplyManualChallengeDescriptionLayout(state);

            for (var i = 0; i < challengeIcons.Length; i++)
            {
                var icon = challengeIcons[i];
                if (icon == null)
                    continue;
                var visibleNow = i < itemCount;
                icon.gameObject.SetActive(visibleNow);
                if (!visibleNow)
                    continue;
                // Setup(CHARM) starts Cuphead's native 0.07-second icon
                // coroutine. The challenge selector owns its frames, so stop
                // that second writer before starting our 12.5 FPS clock.
                icon.StopAllCoroutines();
            }
            state.LastAnimationTick = -1;
            state.SelectorIconsNormalized = false;
            UpdateManualChallengeVisibleFrames(state);

            ApplyManualChallengeHeader(state);
            UpdateManualChallengeSelectorCursor(state, false);
            UpdateManualChallengeSelectionCursor(state);
            UpdateManualChallengeSelectorText(state);
        }

        private bool EnsureManualChallengeGrid(
            ManualChallengeCardState state)
        {
            if (state == null || state.Back == null)
                return false;
            if (state.ChallengeIcons != null &&
                state.ChallengeIcons.Length ==
                    ManualChallengeDisplayOrder.Length)
                return true;

            var nativeIcons = GetBackSelectIcons(
                BackSelectDlcIconsField, state.Back);
            if (nativeIcons == null || nativeIcons.Length < 9)
                return false;

            var gridImage = BackSelectDlcIconsBackField == null
                ? null
                : BackSelectDlcIconsBackField.GetValue(state.Back) as Image;
            if (gridImage == null)
                return false;
            if (manualChallengeGridSprite == null)
                manualChallengeGridSprite = LoadManualEquipCardSprite(
                    ManualChallengeGridAsset,
                    "Gilomx 12 Challenge Equip Grid");
            if (manualChallengeGridSprite == null)
                return false;

            var challengeIcons = new MapEquipUICardBackSelectIcon[
                ManualChallengeDisplayOrder.Length];
            for (var i = 0; i < 9; i++)
                challengeIcons[i] = nativeIcons[i];

            var clones = new GameObject[3];
            var originalPositions = new Vector2[9];
            var originalImagePositions = new Vector2[9];
            var originalImageSizes = new Vector2[9];
            var originalImageScales = new Vector3[9];
            var originalImagePreserveAspect = new bool[9];
            var originalImageColors = new Color[9];
            for (var i = 0; i < originalPositions.Length; i++)
            {
                var rect = nativeIcons[i] == null
                    ? null
                    : nativeIcons[i].transform as RectTransform;
                if (rect == null)
                    return false;
                originalPositions[i] = rect.anchoredPosition;

                var image = GetManualChallengeIconImage(nativeIcons[i]);
                var imageRect = image == null ? null : image.rectTransform;
                if (imageRect == null)
                    return false;
                originalImagePositions[i] = imageRect.anchoredPosition;
                originalImageSizes[i] = imageRect.sizeDelta;
                originalImageScales[i] = imageRect.localScale;
                originalImagePreserveAspect[i] = image.preserveAspect;
                originalImageColors[i] = image.color;
            }

            for (var i = 0; i < clones.Length; i++)
            {
                var source = nativeIcons[5 + i];
                var clone = CloneManualChallengeIcon(
                    source, ManualChallengeIconPositions[9 + i],
                    9 + i);
                if (clone == null)
                {
                    for (var j = 0; j < i; j++)
                    {
                        if (clones[j] != null)
                            Destroy(clones[j]);
                    }
                    return false;
                }
                clones[i] = clone.gameObject;
                challengeIcons[9 + i] = clone;
            }

            for (var i = 0; i < originalPositions.Length; i++)
            {
                var rect = nativeIcons[i].transform as RectTransform;
                rect.anchoredPosition = ManualChallengeIconPositions[i];
            }

            for (var i = 0; i < challengeIcons.Length; i++)
            {
                if (challengeIcons[i] != null &&
                    BackSelectIconIndexField != null)
                    BackSelectIconIndexField.SetValue(challengeIcons[i], i);
            }

            state.OriginalDlcIcons = nativeIcons;
            state.OriginalDlcIconPositions = originalPositions;
            state.OriginalDlcImagePositions = originalImagePositions;
            state.OriginalDlcImageSizes = originalImageSizes;
            state.OriginalDlcImageScales = originalImageScales;
            state.OriginalDlcImagePreserveAspect =
                originalImagePreserveAspect;
            state.OriginalDlcImageColors = originalImageColors;
            state.ChallengeIcons = challengeIcons;
            state.ClonedIconObjects = clones;

            state.ChallengeGridImage = gridImage;
            state.OriginalChallengeGridSprite = gridImage.sprite;
            state.OriginalChallengeGridEnabled = gridImage.enabled;
            state.ChallengeGridCaptured = true;
            gridImage.sprite = manualChallengeGridSprite;
            gridImage.enabled = true;
            return true;
        }

        private MapEquipUICardBackSelectIcon CloneManualChallengeIcon(
            MapEquipUICardBackSelectIcon source, Vector2 anchoredPosition,
            int index)
        {
            if (source == null)
                return null;
            var sourceRect = source.transform as RectTransform;
            if (sourceRect == null)
                return null;

            var cloneObject = Instantiate(source.gameObject) as GameObject;
            if (cloneObject == null)
                return null;
            cloneObject.name = "Gilomx Challenge Icon " + (index + 1);
            cloneObject.SetActive(false);
            var cloneRect = cloneObject.transform as RectTransform;
            cloneRect.SetParent(sourceRect.parent, false);
            cloneRect.anchorMin = sourceRect.anchorMin;
            cloneRect.anchorMax = sourceRect.anchorMax;
            cloneRect.pivot = sourceRect.pivot;
            cloneRect.sizeDelta = sourceRect.sizeDelta;
            cloneRect.anchoredPosition = anchoredPosition;
            cloneRect.localScale = sourceRect.localScale;
            cloneRect.localRotation = sourceRect.localRotation;
            cloneRect.SetSiblingIndex(sourceRect.parent.childCount - 1);

            var clone = cloneObject.GetComponent<
                MapEquipUICardBackSelectIcon>();
            if (clone == null)
            {
                Destroy(cloneObject);
                return null;
            }
            if (BackSelectIconIndexField != null)
                BackSelectIconIndexField.SetValue(clone, index);
            return clone;
        }

        private void RestoreManualChallengeGrid(
            ManualChallengeCardState state)
        {
            if (state == null)
                return;

            if (state.Back != null && state.OriginalDlcIcons != null &&
                BackSelectSelectedIconsField != null)
                BackSelectSelectedIconsField.SetValue(
                    state.Back, state.OriginalDlcIcons);

            if (state.OriginalDlcIcons != null &&
                state.OriginalDlcIconPositions != null)
            {
                var count = Math.Min(state.OriginalDlcIcons.Length,
                    state.OriginalDlcIconPositions.Length);
                for (var i = 0; i < count; i++)
                {
                    var icon = state.OriginalDlcIcons[i];
                    var rect = icon == null
                        ? null
                        : icon.transform as RectTransform;
                    if (rect != null)
                        rect.anchoredPosition =
                            state.OriginalDlcIconPositions[i];

                    var image = GetManualChallengeIconImage(icon);
                    var imageRect = image == null
                        ? null
                        : image.rectTransform;
                    if (imageRect == null)
                        continue;
                    if (state.OriginalDlcImagePositions != null &&
                        i < state.OriginalDlcImagePositions.Length)
                        imageRect.anchoredPosition =
                            state.OriginalDlcImagePositions[i];
                    if (state.OriginalDlcImageSizes != null &&
                        i < state.OriginalDlcImageSizes.Length)
                        imageRect.sizeDelta =
                            state.OriginalDlcImageSizes[i];
                    if (state.OriginalDlcImageScales != null &&
                        i < state.OriginalDlcImageScales.Length)
                        imageRect.localScale =
                            state.OriginalDlcImageScales[i];
                    if (state.OriginalDlcImagePreserveAspect != null &&
                        i < state.OriginalDlcImagePreserveAspect.Length)
                        image.preserveAspect =
                            state.OriginalDlcImagePreserveAspect[i];
                    if (state.OriginalDlcImageColors != null &&
                        i < state.OriginalDlcImageColors.Length)
                        image.color = state.OriginalDlcImageColors[i];
                }
            }

            if (state.ClonedIconObjects != null)
            {
                for (var i = 0; i < state.ClonedIconObjects.Length; i++)
                {
                    var clone = state.ClonedIconObjects[i];
                    if (clone == null)
                        continue;
                    clone.SetActive(false);
                    Destroy(clone);
                }
            }

            if (state.ChallengeGridCaptured &&
                state.ChallengeGridImage != null)
            {
                state.ChallengeGridImage.sprite =
                    state.OriginalChallengeGridSprite;
                state.ChallengeGridImage.enabled =
                    state.OriginalChallengeGridEnabled;
            }

            state.OriginalDlcIcons = null;
            state.OriginalDlcIconPositions = null;
            state.OriginalDlcImagePositions = null;
            state.OriginalDlcImageSizes = null;
            state.OriginalDlcImageScales = null;
            state.OriginalDlcImagePreserveAspect = null;
            state.OriginalDlcImageColors = null;
            state.ChallengeIcons = null;
            state.ClonedIconObjects = null;
            state.ChallengeGridImage = null;
            state.OriginalChallengeGridSprite = null;
            state.ChallengeGridCaptured = false;
            state.SelectorIconsNormalized = false;
        }

        private void SetBackSelectNativeState(ManualChallengeCardState state,
            MapEquipUICardBackSelectIcon[] icons)
        {
            if (state == null || state.Back == null || icons == null)
                return;

            if (BackSelectSelectedIconsField != null)
                BackSelectSelectedIconsField.SetValue(state.Back, icons);
            if (BackSelectIndexField != null)
                BackSelectIndexField.SetValue(state.Back, state.Index);
            if (BackSelectLastIndexField != null)
                BackSelectLastIndexField.SetValue(state.Back, state.Index);
            if (BackSelectSlotField != null)
                BackSelectSlotField.SetValue(
                    state.Back, MapEquipUICard.Slot.CHARM);
            if (BackSelectLastSlotField != null)
                BackSelectLastSlotField.SetValue(
                    state.Back, MapEquipUICard.Slot.CHARM);
            if (BackSelectNoneUnlockedField != null)
                BackSelectNoneUnlockedField.SetValue(state.Back, false);
            if (BackSelectItemSelectedField != null)
                BackSelectItemSelectedField.SetValue(state.Back,
                    GetEquippedManualChallenge() != ModifierId.None);
        }

        private bool HandleManualChallengeBackInput(
            ManualChallengeCardState state)
        {
            if (state == null)
                return false;
            if (state.Card == null || state.Back == null ||
                MapEquipCardPlayerInputField == null)
            {
                CloseManualChallengeSelector(state);
                return false;
            }

            var input = MapEquipCardPlayerInputField.GetValue(state.Card) as
                Player;
            if (input == null)
            {
                CloseManualChallengeSelector(state);
                return false;
            }

            if (input.GetButtonDown((int)CupheadButton.Cancel))
            {
                CloseManualChallengeSelector(state);
                return false;
            }

            if (state.Back.lockInput)
                return false;

            if (input.GetButtonDown(EquipActionUnequip))
            {
                AudioManager.Play("menu_equipment_equip");
                SetEquippedManualChallenge(ModifierId.None);
                SetupManualChallengeSelector(state, false);
                return false;
            }

            if (input.GetButtonDown((int)CupheadButton.MenuUp))
            {
                MoveManualChallengeSelection(state, -1, 0);
                return false;
            }
            if (input.GetButtonDown((int)CupheadButton.MenuDown))
            {
                MoveManualChallengeSelection(state, 1, 0);
                return false;
            }
            if (input.GetButtonDown((int)CupheadButton.MenuRight))
            {
                MoveManualChallengeSelection(state, 0, 1);
                return false;
            }
            if (input.GetButtonDown((int)CupheadButton.MenuLeft))
            {
                MoveManualChallengeSelection(state, 0, -1);
                return false;
            }

            if (input.GetButtonDown((int)CupheadButton.Accept))
            {
                AudioManager.Play("menu_equipment_equip");
                var alreadyEquipped = CurrentManualChallenge(state) ==
                                      GetEquippedManualChallenge();
                SetEquippedManualChallenge(CurrentManualChallenge(state));
                PlayManualChallengeSelectionAnimation(
                    state, alreadyEquipped);
                return false;
            }

            // This Harmony prefix completely owns input while the challenge
            // page is active, so native charm selection cannot run behind it.
            return false;
        }

        private void MoveManualChallengeSelection(
            ManualChallengeCardState state, int rowDirection,
            int columnDirection)
        {
            var icons = state == null ? null : state.ChallengeIcons;
            if (icons == null || state.Index < 0 ||
                state.Index >= icons.Length || icons[state.Index] == null)
                return;

            var row = ManualChallengeRowForIndex(state.Index);
            if (row < 0)
                return;
            var candidate = state.Index;
            if (columnDirection != 0)
            {
                var start = ManualChallengeRowStarts[row];
                var length = ManualChallengeRowLengths[row];
                var column = state.Index - start;
                candidate = start + Wrap(column + columnDirection, length);
            }
            else if (rowDirection != 0)
            {
                var targetRow = row + rowDirection;
                if (targetRow < 0 ||
                    targetRow >= ManualChallengeRowStarts.Length)
                    return;
                candidate = ClosestManualChallengeInRow(
                    icons, state.Index, targetRow);
            }

            if (candidate < 0 || candidate >= icons.Length ||
                icons[candidate] == null || candidate == state.Index)
                return;

            state.Index = candidate;
            UpdateManualChallengeSelectorCursor(state, true);
            UpdateManualChallengeSelectorText(state);
        }

        private static int ManualChallengeRowForIndex(int index)
        {
            for (var row = 0; row < ManualChallengeRowStarts.Length; row++)
            {
                var start = ManualChallengeRowStarts[row];
                if (index >= start &&
                    index < start + ManualChallengeRowLengths[row])
                    return row;
            }
            return -1;
        }

        private static int ClosestManualChallengeInRow(
            MapEquipUICardBackSelectIcon[] icons, int sourceIndex,
            int targetRow)
        {
            if (icons == null || sourceIndex < 0 ||
                sourceIndex >= icons.Length || icons[sourceIndex] == null)
                return -1;
            var sourceRect = icons[sourceIndex].transform as RectTransform;
            var sourceX = sourceRect == null
                ? icons[sourceIndex].transform.localPosition.x
                : sourceRect.anchoredPosition.x;
            var start = ManualChallengeRowStarts[targetRow];
            var length = ManualChallengeRowLengths[targetRow];
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < length; i++)
            {
                var index = start + i;
                if (index < 0 || index >= icons.Length ||
                    icons[index] == null)
                    continue;
                var rect = icons[index].transform as RectTransform;
                var x = rect == null
                    ? icons[index].transform.localPosition.x
                    : rect.anchoredPosition.x;
                var distance = Mathf.Abs(x - sourceX);
                if (distance >= bestDistance)
                    continue;
                best = index;
                bestDistance = distance;
            }
            return best;
        }

        private void FocusManualChallenge(ManualChallengeCardState state,
            ModifierId challenge)
        {
            var icons = state == null ? null : state.ChallengeIcons;
            if (icons == null || icons.Length == 0)
                return;
            var globalIndex = IndexOfManualChallenge(challenge);
            if (globalIndex < 0)
                globalIndex = 0;
            state.Index = globalIndex;
        }

        private void CloseManualChallengeSelector(
            ManualChallengeCardState state)
        {
            if (state == null)
                return;

            state.SelectorActive = false;
            RestoreManualChallengeHeader(state);
            RestoreManualChallengeGrid(state);
            try
            {
                if (state.Back != null)
                    state.Back.Setup(MapEquipUICard.Slot.CHARM);
                if (state.Front != null)
                    state.Front.ChangeSelection(0);
                if (state.Card != null &&
                    MapEquipCardRotateToFrontMethod != null)
                    MapEquipCardRotateToFrontMethod.Invoke(
                        state.Card, new object[0]);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not close the native challenge selector: " +
                    exception.Message);
            }
        }

        private void RemoveManualChallengeCardState(MapEquipUICard card)
        {
            ManualChallengeCardState state;
            if (card == null || !manualChallengeCardStates.TryGetValue(
                    card.GetInstanceID(), out state))
                return;
            RestoreManualChallengeHeader(state);
            RestoreManualChallengeGrid(state);
            manualChallengeCardStates.Remove(card.GetInstanceID());
        }

        private void DeactivateManualChallengeSelector(MapEquipUICard card)
        {
            ManualChallengeCardState state;
            if (card == null || !manualChallengeCardStates.TryGetValue(
                    card.GetInstanceID(), out state) || state == null)
                return;
            state.SelectorActive = false;
            RestoreManualChallengeHeader(state);
            RestoreManualChallengeGrid(state);
        }

        private bool IsManualChallengeSelectorActive(
            MapEquipUICardBackSelect back)
        {
            if (back == null)
                return false;
            foreach (var pair in manualChallengeCardStates)
            {
                var state = pair.Value;
                if (state != null && state.SelectorActive &&
                    state.Back == back)
                    return true;
            }
            return false;
        }

        private void UpdateManualChallengeSelectors()
        {
            if (manualChallengeCardStates.Count == 0)
                return;

            var stale = new List<int>();
            foreach (var pair in manualChallengeCardStates)
            {
                var state = pair.Value;
                if (state == null || state.Card == null)
                {
                    stale.Add(pair.Key);
                    continue;
                }
                if (!state.SelectorActive || state.Back == null)
                    continue;

                UpdateManualChallengeVisibleFrames(state);
                UpdateManualChallengeSelectorText(state);
                ApplyManualChallengeHeader(state);
            }
            for (var i = 0; i < stale.Count; i++)
                manualChallengeCardStates.Remove(stale[i]);
        }

        private void UpdateManualChallengeVisibleFrames(
            ManualChallengeCardState state)
        {
            var icons = state == null ? null : state.ChallengeIcons;
            if (icons == null || icons.Length == 0)
                return;
            var animationTick = (int)(Time.realtimeSinceStartup *
                ManualChallengeFramesPerSecond);
            if (state.LastAnimationTick == animationTick)
                return;
            state.LastAnimationTick = animationTick;
            var itemCount = Math.Min(icons.Length,
                ManualChallengeDisplayOrder.Length);
            for (var i = 0; i < itemCount; i++)
            {
                SetManualChallengeIcon(icons[i],
                    ManualChallengeAt(i));
                if (!state.SelectorIconsNormalized)
                    NormalizeManualChallengeSelectorIcon(icons[i]);
            }
            state.SelectorIconsNormalized = true;
        }

        private static Image GetManualChallengeIconImage(
            AbstractMapCardIcon icon)
        {
            return icon == null || MapCardIconImageField == null
                ? null
                : MapCardIconImageField.GetValue(icon) as Image;
        }

        private static void NormalizeManualChallengeSelectorIcon(
            AbstractMapCardIcon icon)
        {
            var image = GetManualChallengeIconImage(icon);
            if (image == null)
                return;
            var rect = image.rectTransform;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            // All challenge frames are authored on an 80x80 canvas. Assign
            // sizeDelta directly so Unity does not collapse stretched anchors
            // as Image.SetNativeSize() would.
            rect.sizeDelta = new Vector2(80f, 80f);
        }

        private void UpdateManualChallengeSelectorCursor(
            ManualChallengeCardState state, bool playSound)
        {
            var icons = state == null ? null : state.ChallengeIcons;
            var cursor = BackSelectCursorField == null
                ? null
                : BackSelectCursorField.GetValue(state.Back) as
                    MapEquipUICursor;
            if (icons == null || cursor == null || state.Index < 0 ||
                state.Index >= icons.Length || icons[state.Index] == null)
                return;

            if (playSound)
                AudioManager.Play("menu_equipment_move");
            cursor.SetPosition(icons[state.Index].transform.position);
        }

        private void PlayManualChallengeSelectionAnimation(
            ManualChallengeCardState state, bool alreadyEquipped)
        {
            if (state == null || state.Back == null)
                return;

            var cursor = BackSelectCursorField == null
                ? null
                : BackSelectCursorField.GetValue(state.Back) as
                    MapEquipUICursor;
            if (cursor != null)
                cursor.SelectIcon(alreadyEquipped);
        }

        private void UpdateManualChallengeSelectionCursor(
            ManualChallengeCardState state)
        {
            var selectionCursor = BackSelectSelectionCursorField == null
                ? null
                : BackSelectSelectionCursorField.GetValue(state.Back) as
                    MapEquipUICardBackSelectSelectionCursor;
            var icons = state == null ? null : state.ChallengeIcons;
            if (selectionCursor == null || icons == null || icons.Length == 0)
                return;

            var equippedIndex = IndexOfManualChallenge(
                GetEquippedManualChallenge());
            var equippedSlot = equippedIndex;
            if (equippedSlot < 0 ||
                equippedSlot >= icons.Length || icons[equippedSlot] == null)
            {
                selectionCursor.Hide();
                return;
            }

            selectionCursor.selectedIndex = equippedSlot;
            selectionCursor.SetPosition(
                icons[equippedSlot].transform.position);
            selectionCursor.Show();
        }

        private void UpdateManualChallengeSelectorText(
            ManualChallengeCardState state)
        {
            if (state == null || state.Back == null || modLocalization == null)
                return;

            ApplyManualChallengeDescriptionLayout(state);

            var challenge = CurrentManualChallenge(state);
            var title = BackSelectTitleTextField == null
                ? null
                : BackSelectTitleTextField.GetValue(state.Back) as Text;
            var ex = BackSelectExTextField == null
                ? null
                : BackSelectExTextField.GetValue(state.Back) as Text;
            var description = BackSelectDescriptionTextField == null
                ? null
                : BackSelectDescriptionTextField.GetValue(state.Back) as
                    TMP_Text;

            if (title != null)
                title.text =
                    modLocalization.ModifierName(challenge).ToUpperInvariant();
            if (ex != null)
                ex.text = string.Empty;
            if (description != null)
                description.text =
                    modLocalization.ModifierDescriptionRichText(challenge);
        }

        private void ApplyManualChallengeDescriptionLayout(
            ManualChallengeCardState state)
        {
            var title = state == null || state.Back == null ||
                        BackSelectTitleTextField == null
                ? null
                : BackSelectTitleTextField.GetValue(state.Back) as Text;
            if (title != null)
            {
                if (!state.TitleStyleCaptured)
                {
                    state.TitleResizeTextForBestFit =
                        title.resizeTextForBestFit;
                    state.TitleResizeTextMinSize = title.resizeTextMinSize;
                    state.TitleResizeTextMaxSize = title.resizeTextMaxSize;
                    state.TitleStyleCaptured = true;
                }
                if (!state.TitlePositionCaptured)
                {
                    state.TitleAnchoredPosition =
                        title.rectTransform.anchoredPosition;
                    state.TitlePositionCaptured = true;
                }
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 12;
                title.resizeTextMaxSize = Math.Max(12, title.fontSize);
                title.rectTransform.anchoredPosition =
                    state.TitleAnchoredPosition +
                    ManualChallengeTextBlockOffset;
            }

            var ex = state == null || state.Back == null ||
                     BackSelectExTextField == null
                ? null
                : BackSelectExTextField.GetValue(state.Back) as Text;
            if (ex != null)
            {
                if (!state.ExStyleCaptured)
                {
                    state.ExResizeTextForBestFit = ex.resizeTextForBestFit;
                    state.ExResizeTextMinSize = ex.resizeTextMinSize;
                    state.ExResizeTextMaxSize = ex.resizeTextMaxSize;
                    state.ExStyleCaptured = true;
                }
                if (!state.ExPositionCaptured)
                {
                    state.ExAnchoredPosition =
                        ex.rectTransform.anchoredPosition;
                    state.ExPositionCaptured = true;
                }
                ex.resizeTextForBestFit = true;
                ex.resizeTextMinSize = 11;
                ex.resizeTextMaxSize = Math.Max(11, ex.fontSize);
                ex.rectTransform.anchoredPosition =
                    state.ExAnchoredPosition +
                    ManualChallengeTextBlockOffset;
            }

            var description = state == null || state.Back == null ||
                              BackSelectDescriptionTextField == null
                ? null
                : BackSelectDescriptionTextField.GetValue(state.Back) as
                    TMP_Text;
            if (description == null)
                return;

            if (!state.DescriptionStyleCaptured)
            {
                state.DescriptionAutoSizing = description.enableAutoSizing;
                state.DescriptionWordWrapping =
                    description.enableWordWrapping;
                state.DescriptionRichText = description.richText;
                state.DescriptionFontSizeMin = description.fontSizeMin;
                state.DescriptionFontSizeMax = description.fontSizeMax;
                state.DescriptionStyleCaptured = true;
            }
            if (!state.DescriptionPositionCaptured)
            {
                state.DescriptionAnchoredPosition =
                    description.rectTransform.anchoredPosition;
                state.DescriptionPositionCaptured = true;
            }

            description.enableAutoSizing = true;
            description.enableWordWrapping = true;
            description.richText = true;
            description.fontSizeMin = 10f;
            description.fontSizeMax = Math.Min(20f,
                Math.Max(14f, state.DescriptionFontSizeMax));
            description.rectTransform.anchoredPosition =
                state.DescriptionAnchoredPosition +
                ManualChallengeDescriptionOffset;
        }

        private void RestoreManualChallengeDescriptionLayout(
            ManualChallengeCardState state)
        {
            if (state == null)
                return;
            var title = state.Back == null ||
                        BackSelectTitleTextField == null
                ? null
                : BackSelectTitleTextField.GetValue(state.Back) as Text;
            if (title != null && state.TitleStyleCaptured)
            {
                title.resizeTextForBestFit =
                    state.TitleResizeTextForBestFit;
                title.resizeTextMinSize = state.TitleResizeTextMinSize;
                title.resizeTextMaxSize = state.TitleResizeTextMaxSize;
            }
            if (title != null && state.TitlePositionCaptured)
                title.rectTransform.anchoredPosition =
                    state.TitleAnchoredPosition;
            state.TitleStyleCaptured = false;
            state.TitlePositionCaptured = false;

            var ex = state.Back == null || BackSelectExTextField == null
                ? null
                : BackSelectExTextField.GetValue(state.Back) as Text;
            if (ex != null && state.ExStyleCaptured)
            {
                ex.resizeTextForBestFit = state.ExResizeTextForBestFit;
                ex.resizeTextMinSize = state.ExResizeTextMinSize;
                ex.resizeTextMaxSize = state.ExResizeTextMaxSize;
            }
            if (ex != null && state.ExPositionCaptured)
                ex.rectTransform.anchoredPosition =
                    state.ExAnchoredPosition;
            state.ExStyleCaptured = false;
            state.ExPositionCaptured = false;

            if (!state.DescriptionStyleCaptured)
                return;
            var description = state.Back == null ||
                              BackSelectDescriptionTextField == null
                ? null
                : BackSelectDescriptionTextField.GetValue(state.Back) as
                    TMP_Text;
            if (description != null)
            {
                description.enableAutoSizing =
                    state.DescriptionAutoSizing;
                description.enableWordWrapping =
                    state.DescriptionWordWrapping;
                description.richText = state.DescriptionRichText;
                description.fontSizeMin = state.DescriptionFontSizeMin;
                description.fontSizeMax = state.DescriptionFontSizeMax;
            }
            if (description != null && state.DescriptionPositionCaptured)
                description.rectTransform.anchoredPosition =
                    state.DescriptionAnchoredPosition;
            state.DescriptionStyleCaptured = false;
            state.DescriptionPositionCaptured = false;
        }

        private void ApplyManualChallengeHeader(
            ManualChallengeCardState state)
        {
            if (state == null || state.Back == null || modLocalization == null)
                return;

            var header = BackSelectHeaderTextField == null
                ? null
                : BackSelectHeaderTextField.GetValue(state.Back) as
                    LocalizationHelper;
            if (header == null)
                return;

            var text = EquipCardLabel(ModText.SlotChallenge);

            if (header.imageComponent != null)
            {
                if (!state.HeaderImageStateCaptured)
                {
                    state.HeaderImageWasEnabled =
                        header.imageComponent.enabled;
                    state.HeaderImageStateCaptured = true;
                }
                header.imageComponent.enabled = false;
            }

            if (header.textComponent != null)
            {
                header.textComponent.text = text;
                return;
            }
            if (header.textMeshProComponent != null)
            {
                header.textMeshProComponent.text = text;
                return;
            }

            var fallback = EnsureManualChallengeHeaderFallback(state, header);
            if (fallback != null)
                fallback.text = text;
        }

        private Text EnsureManualChallengeHeaderFallback(
            ManualChallengeCardState state, LocalizationHelper header)
        {
            if (state.HeaderFallback != null)
                return state.HeaderFallback.GetComponent<Text>();

            var sourceRect = header.imageComponent == null
                ? header.transform as RectTransform
                : header.imageComponent.rectTransform;
            if (sourceRect == null)
                return null;

            state.HeaderFallback = new GameObject(
                "Gilomx Challenge Header",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = state.HeaderFallback.GetComponent<RectTransform>();
            rect.SetParent(sourceRect.parent, false);
            rect.SetSiblingIndex(sourceRect.GetSiblingIndex() + 1);
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.anchoredPosition = sourceRect.anchoredPosition;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.pivot = sourceRect.pivot;
            rect.localScale = sourceRect.localScale;
            rect.localRotation = sourceRect.localRotation;

            var title = BackSelectTitleTextField == null
                ? null
                : BackSelectTitleTextField.GetValue(state.Back) as Text;
            var result = state.HeaderFallback.GetComponent<Text>();
            result.alignment = TextAnchor.MiddleCenter;
            result.font = title == null ? theme.TitleFont : title.font;
            result.fontSize = title == null
                ? 24
                : Math.Max(20, title.fontSize);
            result.fontStyle = FontStyle.Normal;
            result.color = title == null ? Color.black : title.color;
            result.raycastTarget = false;
            return result;
        }

        private void RestoreManualChallengeHeader(
            ManualChallengeCardState state)
        {
            if (state == null)
                return;
            RestoreManualChallengeDescriptionLayout(state);
            if (state.HeaderFallback != null)
            {
                Destroy(state.HeaderFallback);
                state.HeaderFallback = null;
            }
            if (!state.HeaderImageStateCaptured || state.Back == null ||
                BackSelectHeaderTextField == null)
                return;
            var header = BackSelectHeaderTextField.GetValue(state.Back) as
                LocalizationHelper;
            if (header != null && header.imageComponent != null)
                header.imageComponent.enabled =
                    state.HeaderImageWasEnabled;
            state.HeaderImageStateCaptured = false;
        }

        private void SetManualChallengeIcon(AbstractMapCardIcon icon,
            ModifierId challenge)
        {
            if (icon == null)
                return;
            var frames = GetManualChallengeSprites(challenge);
            if (frames == null || frames.Length == 0)
                return;

            if (MapCardIconFramesField != null)
                MapCardIconFramesField.SetValue(icon, frames);
            if (MapCardIconNormalFramesField != null)
                MapCardIconNormalFramesField.SetValue(icon, frames);
            if (MapCardIconGreyFramesField != null)
                MapCardIconGreyFramesField.SetValue(icon, frames);
            var image = MapCardIconImageField == null
                ? null
                : MapCardIconImageField.GetValue(icon) as Image;
            if (image == null)
                return;

            var frame = frames.Length == 1
                ? 0
                : (int)(Time.realtimeSinceStartup *
                    ManualChallengeFramesPerSecond) % frames.Length;
            image.sprite = frames[frame];
            image.color = Color.white;
            image.preserveAspect = true;
        }

        private Sprite[] GetManualChallengeSprites(ModifierId challenge)
        {
            Sprite[] frames;
            if (manualChallengeSprites.TryGetValue(challenge, out frames))
                return frames;

            if (challenge == ModifierId.None)
            {
                var empty = theme == null
                    ? null
                    : theme.GetSprite("equip_icon_empty_0001");
                frames = empty == null ? new Sprite[0] : new[] { empty };
                if (frames.Length > 0)
                    manualChallengeSprites[challenge] = frames;
                return frames;
            }

            var modifier = FindModifierEntry(challenge);
            if (modifier == null)
            {
                frames = new Sprite[0];
                manualChallengeSprites[challenge] = frames;
                return frames;
            }

            var sprites = new List<Sprite>();
            for (var i = 1; i <= modifier.FrameCount; i++)
            {
                var path = ModifierFramePath(modifier.Image, i);
                var texture = GetTexture(path);
                if (texture == null)
                    continue;
                var sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "Gilomx Challenge " + challenge + " " + i;
                sprites.Add(sprite);
                ownedManualChallengeSprites.Add(sprite);
            }
            frames = sprites.ToArray();
            manualChallengeSprites[challenge] = frames;
            return frames;
        }

        private static string ModifierFramePath(string firstFrame, int frame)
        {
            if (string.IsNullOrEmpty(firstFrame) || frame <= 1)
                return firstFrame;
            var marker = firstFrame.LastIndexOf(
                "_01.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return firstFrame;
            return firstFrame.Substring(0, marker) + "_" +
                   frame.ToString("00") +
                   firstFrame.Substring(marker + 3);
        }

        private void DisposeManualChallengeEquipment()
        {
            foreach (var pair in manualChallengeCardStates)
            {
                RestoreManualChallengeHeader(pair.Value);
                RestoreManualChallengeGrid(pair.Value);
            }
            manualChallengeCardStates.Clear();
            manualChallengeSprites.Clear();
            for (var i = 0; i < ownedManualChallengeSprites.Count; i++)
            {
                if (ownedManualChallengeSprites[i] != null)
                    Destroy(ownedManualChallengeSprites[i]);
            }
            ownedManualChallengeSprites.Clear();
            manualCupheadFrontSprite = null;
            manualMugmanFrontSprite = null;
            manualChallengeGridSprite = null;
        }

        private ModifierId GetEquippedManualChallenge()
        {
            if (equippedChallengeBySave == null)
                return ModifierId.None;
            var slot = CurrentManualChallengeSaveSlot();
            if (slot < 0 || slot >= equippedChallengeBySave.Length ||
                equippedChallengeBySave[slot] == null)
                return ModifierId.None;

            var challenge = equippedChallengeBySave[slot].Value;
            if (challenge == ModifierId.NoMiniPlane)
                challenge = ModifierId.NoDash;
            return FindModifierEntry(challenge) == null ||
                   !ExperimentalFeatures.IsChallengeEnabled(challenge)
                ? ModifierId.None
                : challenge;
        }

        private void SetEquippedManualChallenge(ModifierId challenge)
        {
            if (challenge == ModifierId.NoMiniPlane)
                challenge = ModifierId.NoDash;
            if (FindModifierEntry(challenge) == null ||
                !ExperimentalFeatures.IsChallengeEnabled(challenge))
                challenge = ModifierId.None;
            var slot = CurrentManualChallengeSaveSlot();
            if (slot < 0 || equippedChallengeBySave == null ||
                slot >= equippedChallengeBySave.Length ||
                equippedChallengeBySave[slot] == null)
                return;

            equippedChallengeBySave[slot].Value = challenge;
            Config.Save();
            RefreshAllManualChallengeFronts();
            RefreshAllManualChallengeSelectorCursors();
            Logger.LogInfo("Equipped manual challenge for save " +
                           (slot + 1) + ": " + challenge + ".");
        }

        private void RefreshAllManualChallengeSelectorCursors()
        {
            foreach (var pair in manualChallengeCardStates)
            {
                var state = pair.Value;
                if (state == null || !state.SelectorActive ||
                    state.Back == null)
                    continue;
                UpdateManualChallengeSelectionCursor(state);
                UpdateManualChallengeSelectorCursor(state, false);
            }
        }

        private void ClearEquippedManualChallengeForSaveSlot(int slot)
        {
            if (slot < 0 || equippedChallengeBySave == null ||
                slot >= equippedChallengeBySave.Length ||
                equippedChallengeBySave[slot] == null)
                return;

            equippedChallengeBySave[slot].Value = ModifierId.None;
            Config.Save();
            RefreshAllManualChallengeFronts();
            RefreshAllManualChallengeSelectorCursors();
            Logger.LogInfo("Cleared manual challenge for deleted save " +
                           (slot + 1) + ".");
        }

        private static int CurrentManualChallengeSaveSlot()
        {
            try
            {
                if (!PlayerData.Initialized || PlayerData.Data == null)
                    return -1;
                var slot = PlayerData.CurrentSaveFileIndex;
                return slot >= 0 && slot < 3 ? slot : -1;
            }
            catch
            {
                return -1;
            }
        }

        private void TryActivateEquippedChallengeForLevel(Level level)
        {
            if (level == null)
                return;

            // A roulette result owns its own temporary challenge and always
            // takes precedence without changing the manually equipped one.
            // Its "RETO" option can also be disabled, in which case this
            // battle intentionally runs with no challenge at all.
            if (loanedLoadoutsActive)
                return;

            if (activeChallenge != ModifierId.None &&
                !activeChallengeFromManualEquipment)
                return;

            Levels targetLevel;
            bool planeControls;
            if (!TryResolveManualChallengeBattle(
                    level, out targetLevel, out planeControls))
            {
                if (activeChallengeFromManualEquipment)
                    ClearActiveChallenge();
                return;
            }

            var challenge = GetEquippedManualChallenge();
            var modifier = FindModifierEntry(challenge);
            if (challenge == ModifierId.None || modifier == null)
            {
                if (activeChallengeFromManualEquipment)
                    ClearActiveChallenge();
                return;
            }

            if (!ManualChallengeMatchesControls(
                    challenge, modifier.Kind, planeControls))
            {
                if (activeChallengeFromManualEquipment)
                    ClearActiveChallenge();
                Logger.LogInfo("Equipped challenge " + challenge +
                               " remains selected but is not compatible with " +
                               level.CurrentLevel + ".");
                return;
            }

            SetActiveChallengeSession(challenge, targetLevel,
                planeControls, true, -1);
            Logger.LogInfo("Activated equipped challenge " + challenge +
                           " for " + level.CurrentLevel + ".");
        }

        private static bool TryResolveManualChallengeBattle(Level level,
            out Levels targetLevel, out bool planeControls)
        {
            targetLevel = default(Levels);
            planeControls = false;
            try
            {
                if (level.LevelType != Level.Type.Battle)
                    return false;
                targetLevel = level.CurrentLevel;
            }
            catch
            {
                return false;
            }

            if (IsDicePalaceLevel(targetLevel))
            {
                targetLevel = Levels.DicePalaceMain;
                planeControls = false;
                return true;
            }

            for (var i = 0; i < RouletteData.Bosses.Length; i++)
            {
                if (RouletteData.Bosses[i].Level != targetLevel)
                    continue;
                planeControls = RouletteData.Bosses[i].IsPlane;
                return true;
            }

            // Keep future or auxiliary shmup battles functional even before
            // they are promoted into the roulette's boss catalog.
            var name = targetLevel.ToString();
            planeControls = name.StartsWith(
                "Flying", StringComparison.Ordinal) ||
                name == "Robot";
            return true;
        }

        private static bool ManualChallengeMatchesControls(
            ModifierId challenge, ModifierKind kind, bool planeControls)
        {
            // The manual Equip Card intentionally reuses No Dash and Stiff
            // Mode on airplane battles. Keep that broader behavior local to
            // manual equipment so the F6 roulette preserves its original
            // ground/plane challenge catalog and odds.
            if (planeControls &&
                (challenge == ModifierId.NoDash ||
                 challenge == ModifierId.StiffMode))
                return true;
            return kind == ModifierKind.Both ||
                   (planeControls && kind == ModifierKind.Plane) ||
                   (!planeControls && kind == ModifierKind.Ground);
        }

        private ModifierEntry FindModifierEntry(ModifierId challenge)
        {
            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                if (RouletteData.Modifiers[i].Id == challenge)
                    return RouletteData.Modifiers[i];
            }
            return null;
        }

        private static int IndexOfManualChallenge(ModifierId challenge)
        {
            for (var i = 0; i < ManualChallengeDisplayOrder.Length; i++)
            {
                if (ManualChallengeDisplayOrder[i] == challenge)
                    return i;
            }
            return -1;
        }

        private static ModifierId ManualChallengeAt(int index)
        {
            return index >= 0 && index < ManualChallengeDisplayOrder.Length
                ? ManualChallengeDisplayOrder[index]
                : ModifierId.None;
        }

        private ModifierId CurrentManualChallenge(
            ManualChallengeCardState state)
        {
            return state == null
                ? ModifierId.None
                : ManualChallengeAt(state.Index);
        }

        private static MapEquipUICardBackSelectIcon[] GetBackSelectIcons(
            FieldInfo field, MapEquipUICardBackSelect back)
        {
            return field == null || back == null
                ? null
                : field.GetValue(back) as MapEquipUICardBackSelectIcon[];
        }

        private static void SetBackSelectGroupActive(
            MapEquipUICardBackSelectIcon[] icons, bool activeNow)
        {
            if (icons == null)
                return;
            for (var i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                    icons[i].gameObject.SetActive(activeNow);
            }
        }

        private static void SetImageEnabled(FieldInfo field,
            MapEquipUICardBackSelect back, bool enabledNow)
        {
            var image = field == null || back == null
                ? null
                : field.GetValue(back) as Image;
            if (image != null)
                image.enabled = enabledNow;
        }
    }
}
