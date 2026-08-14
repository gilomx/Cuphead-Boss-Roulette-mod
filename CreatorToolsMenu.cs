using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private const int CreatorToolsPauseMenuIndex = 4;
        private const int CreatorToolsMenuItemCount = 7;
        private const string CreatorToolsPauseRowName =
            "Gilomx Las Pichi Ruleta Pause Row";

        private static readonly FieldInfo LevelPauseMenuItemsField =
            AccessTools.Field(typeof(LevelPauseGUI), "menuItems");
        private static readonly FieldInfo LevelPauseSelectionField =
            AccessTools.Field(typeof(LevelPauseGUI), "_selection");
        private static readonly MethodInfo LevelPauseUpdateSelectionMethod =
            AccessTools.Method(typeof(LevelPauseGUI), "UpdateSelection");
        private static readonly FieldInfo LevelPauseOptionsField =
            AccessTools.Field(typeof(LevelPauseGUI), "options");
        private static readonly MethodInfo LevelPauseOpenOptionsMethod =
            AccessTools.Method(typeof(LevelPauseGUI), "Options");
        private static readonly FieldInfo OptionsVisualObjectField =
            AccessTools.Field(typeof(OptionsGUI), "visualObject");
        private static readonly FieldInfo OptionsVisualButtonsField =
            AccessTools.Field(typeof(OptionsGUI), "visualObjectButtons");
        private static readonly FieldInfo OptionsCurrentItemsField =
            AccessTools.Field(typeof(OptionsGUI), "currentItems");
        private static readonly FieldInfo OptionsVerticalSelectionField =
            AccessTools.Field(typeof(OptionsGUI), "_verticalSelection");
        private static readonly MethodInfo OptionsToVisualMethod =
            AccessTools.Method(typeof(OptionsGUI), "ToVisual");
        private static readonly MethodInfo OptionsToPauseMenuMethod =
            AccessTools.Method(typeof(OptionsGUI), "ToPauseMenu");
        private static readonly MethodInfo OptionsCenterVisualMethod =
            AccessTools.Method(typeof(OptionsGUI), "CenterVisual");
        private static readonly MethodInfo OptionsUpdateVerticalMethod =
            AccessTools.Method(typeof(OptionsGUI),
                "UpdateVerticalSelection");
        private static readonly MethodInfo OptionsMenuSelectSoundMethod =
            AccessTools.Method(typeof(OptionsGUI), "MenuSelectSound");
        private static readonly Type OptionsButtonType =
            typeof(OptionsGUI).GetNestedType("Button",
                BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo OptionsButtonTextField =
            AccessTools.Field(OptionsButtonType, "text");
        private static readonly FieldInfo OptionsButtonLocalizationField =
            AccessTools.Field(OptionsButtonType, "localizationHelper");
        private static readonly FieldInfo OptionsButtonValuesField =
            AccessTools.Field(OptionsButtonType, "options");
        private static readonly FieldInfo OptionsButtonSelectionField =
            AccessTools.Field(OptionsButtonType, "selection");
        private static readonly FieldInfo OptionsButtonWrapField =
            AccessTools.Field(OptionsButtonType, "wrap");
        private static readonly MethodInfo OptionsButtonUpdateMethod =
            AccessTools.Method(OptionsButtonType, "updateSelection");
        private static readonly MethodInfo OptionsButtonIncrementMethod =
            AccessTools.Method(OptionsButtonType, "incrementSelection");
        private static readonly FieldInfo LocalizationTextField =
            AccessTools.Field(typeof(LocalizationHelper), "textComponent");

        private bool creatorToolsMenuOpen;
        private MapPauseUI creatorToolsPauseOwner;
        private OptionsGUI creatorToolsNativeOptions;
        private readonly List<CreatorToolsNativeButtonSnapshot>
            creatorToolsNativeButtonSnapshots =
                new List<CreatorToolsNativeButtonSnapshot>();
        private readonly List<CreatorToolsNativeButtonSnapshot>
            creatorToolsNativeMenuRows =
                new List<CreatorToolsNativeButtonSnapshot>();
        private object[] creatorToolsNativeOriginalItems;
        private Text creatorToolsNativeTitle;
        private string creatorToolsNativeTitleText;
        private LocalizationHelper creatorToolsNativeTitleLocalization;
        private bool creatorToolsNativeTitleLocalizationEnabled;
        private bool creatorToolsNativeConfigured;
        private int creatorToolsMenuSelection;
        private string creatorToolsMenuNotice;
        private float creatorToolsMenuNoticeUntil;

        private sealed class CreatorToolsPauseSelectState
        {
            internal Text[] ExtendedItems;
            internal int Selection;
        }

        private sealed class CreatorToolsNativeButtonSnapshot
        {
            internal object Button;
            internal Text ValueText;
            internal string Value;
            internal bool ValueActive;
            internal LocalizationHelper LocalizationHelper;
            internal bool LocalizationEnabled;
            internal Text LabelText;
            internal string Label;
            internal bool LabelActive;
            internal LocalizationHelper LabelLocalizationHelper;
            internal bool LabelLocalizationEnabled;
            internal string[] Options;
            internal int Selection;
            internal bool Wrap;
            internal GameObject Row;
            internal bool RowActive;
        }

        private void InstallCreatorToolsMenuPatches()
        {
            var initBasic = AccessTools.Method(typeof(LevelPauseGUI), "Init",
                new[] { typeof(bool), typeof(OptionsGUI),
                    typeof(AchievementsGUI) });
            var initTower = AccessTools.Method(typeof(LevelPauseGUI), "Init",
                new[] { typeof(bool), typeof(OptionsGUI),
                    typeof(AchievementsGUI),
                    typeof(RestartTowerConfirmGUI) });
            var initPostfix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseInitPostfix");
            var update = AccessTools.Method(typeof(LevelPauseGUI), "Update");
            var updatePrefix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseUpdatePrefix");
            var onPause = AccessTools.Method(
                typeof(LevelPauseGUI), "OnPause");
            var onPausePrefix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseOnPausePrefix");
            var onPausePostfix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseOnPausePostfix");
            var select = AccessTools.Method(typeof(LevelPauseGUI), "Select");
            var selectPrefix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseSelectPrefix");
            var selectPostfix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseSelectPostfix");
            var destroy = AccessTools.Method(
                typeof(LevelPauseGUI), "OnDestroy");
            var destroyPostfix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseDestroyPostfix");
            var languageChanged = AccessTools.Method(
                typeof(LevelPauseGUI), "onLanguageChangedEventHandler");
            var languagePrefix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseLanguagePrefix");
            var languagePostfix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsPauseLanguagePostfix");
            var showOptions = AccessTools.Method(
                typeof(OptionsGUI), "ShowMainOptionMenu");
            var showOptionsPostfix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsOptionsShowPostfix");
            var toMainOptions = AccessTools.Method(
                typeof(OptionsGUI), "ToMainOptions");
            var toMainOptionsPrefix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsOptionsToMainPrefix");
            var visualHorizontal = AccessTools.Method(
                typeof(OptionsGUI), "VisualHorizontalSelect");
            var visualHorizontalPrefix = AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsOptionsHorizontalPrefix");
            var visualSelect = AccessTools.Method(
                typeof(OptionsGUI), "VisualSelect");
            var visualSelectPrefix = AccessTools.Method(
                typeof(Plugin), "CreatorToolsOptionsSelectPrefix");

            if (initPostfix != null &&
                (initBasic != null || initTower != null))
            {
                if (initBasic != null)
                    harmony.Patch(initBasic,
                        postfix: new HarmonyMethod(initPostfix));
                if (initTower != null)
                    harmony.Patch(initTower,
                        postfix: new HarmonyMethod(initPostfix));
            }
            else
                Logger.LogWarning(
                    "Could not install the Creator Tools pause row.");

            if (update != null && updatePrefix != null)
                harmony.Patch(update,
                    prefix: new HarmonyMethod(updatePrefix));
            if (onPause != null && onPausePrefix != null &&
                onPausePostfix != null)
                harmony.Patch(onPause,
                    prefix: new HarmonyMethod(onPausePrefix),
                    postfix: new HarmonyMethod(onPausePostfix));
            if (select != null && selectPrefix != null &&
                selectPostfix != null)
                harmony.Patch(select,
                    prefix: new HarmonyMethod(selectPrefix),
                    postfix: new HarmonyMethod(selectPostfix));
            if (destroy != null && destroyPostfix != null)
                harmony.Patch(destroy,
                    postfix: new HarmonyMethod(destroyPostfix));
            if (languageChanged != null && languagePrefix != null &&
                languagePostfix != null)
                harmony.Patch(languageChanged,
                    prefix: new HarmonyMethod(languagePrefix),
                    postfix: new HarmonyMethod(languagePostfix));
            if (showOptions != null && showOptionsPostfix != null)
                harmony.Patch(showOptions,
                    postfix: new HarmonyMethod(showOptionsPostfix));
            if (toMainOptions != null && toMainOptionsPrefix != null)
                harmony.Patch(toMainOptions,
                    prefix: new HarmonyMethod(toMainOptionsPrefix));
            if (visualHorizontal != null &&
                visualHorizontalPrefix != null)
                harmony.Patch(visualHorizontal,
                    prefix: new HarmonyMethod(visualHorizontalPrefix));
            if (visualSelect != null && visualSelectPrefix != null)
                harmony.Patch(visualSelect,
                    prefix: new HarmonyMethod(visualSelectPrefix));
        }

        private static void CreatorToolsPauseInitPostfix(
            LevelPauseGUI __instance)
        {
            var plugin = activeInstance;
            var mapPause = __instance as MapPauseUI;
            if (plugin == null || mapPause == null)
                return;
            plugin.InstallCreatorToolsPauseRow(mapPause);
        }

        private void InstallCreatorToolsPauseRow(MapPauseUI pause)
        {
            if (LevelPauseMenuItemsField == null)
                return;
            var items = LevelPauseMenuItemsField.GetValue(pause) as Text[];
            if (items == null || items.Length <= 3)
                return;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null &&
                    items[i].gameObject.name == CreatorToolsPauseRowName)
                    return;
            }

            var options = items[3];
            if (options == null)
                return;

            var rowObject = Instantiate(options.gameObject);
            rowObject.name = CreatorToolsPauseRowName;
            var rowParent = options.transform.parent;
            rowObject.transform.SetParent(rowParent, false);
            rowObject.transform.SetSiblingIndex(
                options.transform.GetSiblingIndex() + 1);
            rowObject.SetActive(true);
            var helper = rowObject.GetComponent<LocalizationHelper>();
            if (helper != null)
                DestroyImmediate(helper);
            var row = rowObject.GetComponent<Text>();
            if (row == null)
            {
                Destroy(rowObject);
                return;
            }
            row.text = "LA PICHI RULETA";

            var nativeLayout = rowParent == null
                ? null
                : rowParent.GetComponent<LayoutGroup>();
            if (nativeLayout == null)
            {
                var step = CreatorToolsPauseRowStep(items, options);
                var optionsPosition = options.rectTransform.anchoredPosition;
                row.rectTransform.anchoredPosition =
                    optionsPosition + new Vector2(0f, step);

                for (var i = 4; i < items.Length; i++)
                {
                    if (items[i] == null)
                        continue;
                    items[i].rectTransform.anchoredPosition +=
                        new Vector2(0f, step);
                }
            }

            var extended = new Text[items.Length + 1];
            Array.Copy(items, 0, extended, 0,
                CreatorToolsPauseMenuIndex);
            extended[CreatorToolsPauseMenuIndex] = row;
            Array.Copy(items, CreatorToolsPauseMenuIndex, extended,
                CreatorToolsPauseMenuIndex + 1,
                items.Length - CreatorToolsPauseMenuIndex);
            LevelPauseMenuItemsField.SetValue(pause, extended);
            if (nativeLayout != null && rowParent is RectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    (RectTransform)rowParent);
            RefreshCreatorToolsPauseSelection(pause);
            Logger.LogInfo(
                "Creator Tools added LA PICHI RULETA to the map pause menu.");
        }

        private static float CreatorToolsPauseRowStep(
            Text[] items, Text options)
        {
            var optionsY = options.rectTransform.anchoredPosition.y;
            for (var i = 4; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;
                var delta = items[i].rectTransform.anchoredPosition.y -
                            optionsY;
                if (Mathf.Abs(delta) >= 8f && Mathf.Abs(delta) <= 100f)
                    return delta;
            }
            if (items.Length > 2 && items[2] != null)
            {
                var delta = optionsY -
                            items[2].rectTransform.anchoredPosition.y;
                if (Mathf.Abs(delta) >= 8f && Mathf.Abs(delta) <= 100f)
                    return delta;
            }
            return -40f;
        }

        private static bool CreatorToolsPauseUpdatePrefix(
            LevelPauseGUI __instance)
        {
            var plugin = activeInstance;
            var mapPause = __instance as MapPauseUI;
            if (plugin != null && mapPause != null)
                EnsureCreatorToolsPauseRowLabel(mapPause);
            return true;
        }

        private static void CreatorToolsPauseOnPausePrefix(
            LevelPauseGUI __instance, out Text[] __state)
        {
            __state = null;
            var mapPause = __instance as MapPauseUI;
            if (mapPause == null || !HasCreatorToolsPauseRow(mapPause))
                return;

            // LevelPauseGUI.OnPause() hard-codes menuItems[4] as the native
            // Player 2 leave row. Let Cuphead see its untouched eight-item
            // array while it applies Multiplayer visibility, otherwise the
            // original row is shifted to index 5 and remains visible.
            __state = LevelPauseMenuItemsField.GetValue(mapPause) as Text[];
            LevelPauseMenuItemsField.SetValue(
                mapPause, RemoveCreatorToolsPauseRow(__state));
        }

        private static void CreatorToolsPauseOnPausePostfix(
            LevelPauseGUI __instance, Text[] __state)
        {
            var mapPause = __instance as MapPauseUI;
            if (mapPause == null || __state == null)
                return;
            LevelPauseMenuItemsField.SetValue(mapPause, __state);
            EnsureCreatorToolsPauseRowLabel(mapPause);
            RefreshCreatorToolsPauseSelection(mapPause);
        }

        private static void CreatorToolsOptionsShowPostfix(
            OptionsGUI __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || !plugin.creatorToolsMenuOpen ||
                plugin.creatorToolsNativeOptions != __instance)
                return;
            plugin.ConfigureCreatorToolsNativeOptions();
        }

        private static bool CreatorToolsOptionsToMainPrefix(
            OptionsGUI __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || !plugin.creatorToolsMenuOpen ||
                plugin.creatorToolsNativeOptions != __instance)
                return true;
            plugin.CloseCreatorToolsMenu(true);
            return false;
        }

        private static bool CreatorToolsOptionsHorizontalPrefix(
            OptionsGUI __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || !plugin.creatorToolsMenuOpen ||
                plugin.creatorToolsNativeOptions != __instance)
                return true;
            plugin.ApplyCreatorToolsNativeSelection();
            return false;
        }

        private static bool CreatorToolsOptionsSelectPrefix(
            OptionsGUI __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || !plugin.creatorToolsMenuOpen ||
                plugin.creatorToolsNativeOptions != __instance)
                return true;
            plugin.ActivateCreatorToolsNativeSelection();
            return false;
        }

        private static bool CreatorToolsPauseSelectPrefix(
            LevelPauseGUI __instance,
            out CreatorToolsPauseSelectState __state)
        {
            __state = null;
            var plugin = activeInstance;
            var mapPause = __instance as MapPauseUI;
            if (plugin == null || mapPause == null ||
                !HasCreatorToolsPauseRow(mapPause))
                return true;

            var selected = GetCreatorToolsPauseSelection(mapPause);
            if (selected == CreatorToolsPauseMenuIndex)
            {
                plugin.OpenCreatorToolsMenu(mapPause);
                return false;
            }
            var extended = LevelPauseMenuItemsField.GetValue(
                mapPause) as Text[];
            __state = new CreatorToolsPauseSelectState
            {
                ExtendedItems = extended,
                Selection = selected
            };
            LevelPauseMenuItemsField.SetValue(mapPause,
                RemoveCreatorToolsPauseRow(extended));
            SetCreatorToolsPauseSelection(mapPause,
                selected > CreatorToolsPauseMenuIndex
                    ? selected - 1
                    : selected,
                false);
            return true;
        }

        private static void CreatorToolsPauseSelectPostfix(
            LevelPauseGUI __instance,
            CreatorToolsPauseSelectState __state)
        {
            if (__state == null || __state.ExtendedItems == null)
                return;
            var mapPause = __instance as MapPauseUI;
            if (mapPause == null)
                return;
            LevelPauseMenuItemsField.SetValue(
                mapPause, __state.ExtendedItems);
            SetCreatorToolsPauseSelection(
                mapPause, __state.Selection, true);
        }

        private static void CreatorToolsPauseLanguagePrefix(
            LevelPauseGUI __instance, out Text[] __state)
        {
            __state = null;
            var mapPause = __instance as MapPauseUI;
            if (mapPause == null || !HasCreatorToolsPauseRow(mapPause))
                return;
            __state = LevelPauseMenuItemsField.GetValue(mapPause) as Text[];
            LevelPauseMenuItemsField.SetValue(
                mapPause, RemoveCreatorToolsPauseRow(__state));
        }

        private static void CreatorToolsPauseLanguagePostfix(
            LevelPauseGUI __instance, Text[] __state)
        {
            var mapPause = __instance as MapPauseUI;
            if (mapPause == null || __state == null)
                return;
            LevelPauseMenuItemsField.SetValue(mapPause, __state);
            EnsureCreatorToolsPauseRowLabel(mapPause);
            RefreshCreatorToolsPauseSelection(mapPause);
        }

        private static void CreatorToolsPauseDestroyPostfix(
            LevelPauseGUI __instance)
        {
            var plugin = activeInstance;
            if (plugin != null &&
                plugin.creatorToolsPauseOwner == __instance)
                plugin.CloseCreatorToolsMenu(false);
        }

        private static bool HasCreatorToolsPauseRow(MapPauseUI pause)
        {
            if (LevelPauseMenuItemsField == null || pause == null)
                return false;
            var items = LevelPauseMenuItemsField.GetValue(pause) as Text[];
            return items != null &&
                   items.Length > CreatorToolsPauseMenuIndex &&
                   items[CreatorToolsPauseMenuIndex] != null &&
                   items[CreatorToolsPauseMenuIndex].gameObject.name ==
                   CreatorToolsPauseRowName;
        }

        private static Text[] RemoveCreatorToolsPauseRow(Text[] extended)
        {
            if (extended == null ||
                extended.Length <= CreatorToolsPauseMenuIndex)
                return extended;
            var compact = new Text[extended.Length - 1];
            Array.Copy(extended, 0, compact, 0,
                CreatorToolsPauseMenuIndex);
            Array.Copy(extended, CreatorToolsPauseMenuIndex + 1,
                compact, CreatorToolsPauseMenuIndex,
                extended.Length - CreatorToolsPauseMenuIndex - 1);
            return compact;
        }

        private static void EnsureCreatorToolsPauseRowLabel(
            MapPauseUI pause)
        {
            if (!HasCreatorToolsPauseRow(pause))
                return;
            var items = LevelPauseMenuItemsField.GetValue(pause) as Text[];
            var row = items[CreatorToolsPauseMenuIndex];
            row.text = "LA PICHI RULETA";
            if (!row.gameObject.activeSelf)
                row.gameObject.SetActive(true);
        }

        private void OpenCreatorToolsMenu(MapPauseUI pause)
        {
            if (pause == null || creatorToolsMenuOpen)
                return;
            if (visible)
                SetVisible(false);

            var options = LevelPauseOptionsField == null
                ? null
                : LevelPauseOptionsField.GetValue(pause) as OptionsGUI;
            if (options == null || LevelPauseOpenOptionsMethod == null)
            {
                Logger.LogWarning(
                    "Creator Tools could not open Cuphead's native options screen.");
                return;
            }

            creatorToolsPauseOwner = pause;
            creatorToolsNativeOptions = options;
            creatorToolsMenuOpen = true;
            creatorToolsNativeConfigured = false;
            creatorToolsMenuNotice = null;
            try
            {
                AudioManager.Play("level_menu_select");
                LevelPauseOpenOptionsMethod.Invoke(pause, null);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    "Creator Tools could not enter the native options screen: " +
                    ex.Message);
                creatorToolsMenuOpen = false;
                creatorToolsPauseOwner = null;
                creatorToolsNativeOptions = null;
            }
        }

        private void CloseCreatorToolsMenu(bool returnToPauseList)
        {
            var pause = creatorToolsPauseOwner;
            var options = creatorToolsNativeOptions;
            if (creatorToolsPreviewSetting != null &&
                creatorToolsPreviewSetting.Value)
                SetCreatorToolsPreview(false);
            RestoreCreatorToolsNativeOptions();

            creatorToolsMenuOpen = false;
            creatorToolsPauseOwner = null;
            creatorToolsNativeOptions = null;
            creatorToolsMenuNotice = null;

            if (returnToPauseList && options != null &&
                OptionsToPauseMenuMethod != null)
            {
                try
                {
                    OptionsToPauseMenuMethod.Invoke(options, null);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(
                        "Creator Tools could not close the native options screen: " +
                        ex.Message);
                }
            }

            if (pause != null && HasCreatorToolsPauseRow(pause))
                EnsureCreatorToolsPauseRowLabel(pause);
        }

        private void ConfigureCreatorToolsNativeOptions()
        {
            if (creatorToolsNativeConfigured ||
                creatorToolsNativeOptions == null)
                return;

            try
            {
                if (OptionsToVisualMethod == null ||
                    OptionsVisualObjectField == null ||
                    OptionsVisualButtonsField == null ||
                    OptionsCurrentItemsField == null)
                    throw new InvalidOperationException(
                        "The native Visual menu is unavailable.");

                var options = creatorToolsNativeOptions;
                OptionsToVisualMethod.Invoke(options, null);
                var visualObject = OptionsVisualObjectField.GetValue(options)
                    as GameObject;
                var buttons = OptionsVisualButtonsField.GetValue(options)
                    as Array;
                var currentItems = OptionsCurrentItemsField.GetValue(options)
                    as IList;
                if (visualObject == null || buttons == null ||
                    buttons.Length < CreatorToolsMenuItemCount ||
                    currentItems == null)
                    throw new InvalidOperationException(
                        "Cuphead's Visual menu does not expose enough rows.");

                creatorToolsNativeOriginalItems =
                    new object[currentItems.Count];
                currentItems.CopyTo(creatorToolsNativeOriginalItems, 0);

                var valueTexts = new HashSet<Text>();
                for (var i = 0; i < buttons.Length; i++)
                {
                    var value = OptionsButtonTextField.GetValue(
                        buttons.GetValue(i)) as Text;
                    if (value != null)
                        valueTexts.Add(value);
                }

                var usedLabels = new HashSet<Text>();
                for (var i = 0; i < buttons.Length; i++)
                {
                    var button = buttons.GetValue(i);
                    var value = OptionsButtonTextField.GetValue(button)
                        as Text;
                    if (value == null)
                        continue;
                    var label = FindCreatorToolsNativeLabel(
                        visualObject, value, valueTexts, usedLabels);
                    if (label != null)
                        usedLabels.Add(label);
                    var localization =
                        OptionsButtonLocalizationField.GetValue(button)
                            as LocalizationHelper;
                    var labelLocalization = label == null
                        ? null
                        : label.GetComponent<LocalizationHelper>();
                    var row = FindCreatorToolsNativeRow(
                        visualObject, value, label);
                    creatorToolsNativeButtonSnapshots.Add(
                        new CreatorToolsNativeButtonSnapshot
                        {
                            Button = button,
                            ValueText = value,
                            Value = value.text,
                            ValueActive = value.gameObject.activeSelf,
                            LocalizationHelper = localization,
                            LocalizationEnabled = localization != null &&
                                localization.enabled,
                            LabelText = label,
                            Label = label == null ? null : label.text,
                            LabelActive = label != null &&
                                label.gameObject.activeSelf,
                            LabelLocalizationHelper = labelLocalization,
                            LabelLocalizationEnabled =
                                labelLocalization != null &&
                                labelLocalization.enabled,
                            Options = (string[])
                                OptionsButtonValuesField.GetValue(button),
                            Selection = (int)
                                OptionsButtonSelectionField.GetValue(button),
                            Wrap = (bool)
                                OptionsButtonWrapField.GetValue(button),
                            Row = row,
                            RowActive = row != null && row.activeSelf
                        });
                }

                for (var i = 0;
                     i < creatorToolsNativeButtonSnapshots.Count; i++)
                {
                    var row = creatorToolsNativeButtonSnapshots[i].Row;
                    if (row == null)
                        continue;
                    var shared = false;
                    for (var j = 0;
                         j < creatorToolsNativeButtonSnapshots.Count; j++)
                    {
                        if (i != j &&
                            creatorToolsNativeButtonSnapshots[j].Row == row)
                        {
                            shared = true;
                            break;
                        }
                    }
                    if (shared)
                        creatorToolsNativeButtonSnapshots[i].Row = null;
                }

                creatorToolsNativeTitle = FindCreatorToolsNativeTitle(
                    visualObject, valueTexts, usedLabels);
                if (creatorToolsNativeTitle != null)
                {
                    creatorToolsNativeTitleText =
                        creatorToolsNativeTitle.text;
                    creatorToolsNativeTitleLocalization =
                        creatorToolsNativeTitle.GetComponent<
                            LocalizationHelper>();
                    creatorToolsNativeTitleLocalizationEnabled =
                        creatorToolsNativeTitleLocalization != null &&
                        creatorToolsNativeTitleLocalization.enabled;
                    if (creatorToolsNativeTitleLocalization != null)
                        creatorToolsNativeTitleLocalization.enabled = false;
                    creatorToolsNativeTitle.text = "LA PICHI RULETA";
                }

                currentItems.Clear();
                for (var i = 0;
                     i < creatorToolsNativeButtonSnapshots.Count; i++)
                    SetCreatorToolsNativeRowActive(
                        creatorToolsNativeButtonSnapshots[i], false);

                for (var i = 0; i < CreatorToolsMenuItemCount; i++)
                {
                    // Keep the six setting rows in the normal Visual slots,
                    // but reuse Cuphead's dedicated bottom BACK action for
                    // URL copy. This leaves the clipboard action centered and
                    // prevents a long label/value pair from overflowing.
                    var sourceIndex = i == CreatorToolsMenuItemCount - 1
                        ? creatorToolsNativeButtonSnapshots.Count - 1
                        : i;
                    var snapshot = creatorToolsNativeButtonSnapshots[
                        sourceIndex];
                    creatorToolsNativeMenuRows.Add(snapshot);
                    SetCreatorToolsNativeRowActive(snapshot, true);

                    if (snapshot.LocalizationHelper != null)
                        snapshot.LocalizationHelper.enabled = false;
                    OptionsButtonLocalizationField.SetValue(
                        snapshot.Button, null);
                    if (snapshot.LabelLocalizationHelper != null)
                        snapshot.LabelLocalizationHelper.enabled = false;
                    if (snapshot.LabelText != null &&
                        i < CreatorToolsMenuItemCount - 1)
                        snapshot.LabelText.text =
                            CreatorToolsMenuLabel(i);

                    var values = CreatorToolsNativeValues(i);
                    OptionsButtonValuesField.SetValue(
                        snapshot.Button, values);
                    OptionsButtonWrapField.SetValue(
                        snapshot.Button, true);
                    var selection = CreatorToolsNativeSelection(i);
                    if (values.Length > 0)
                        OptionsButtonUpdateMethod.Invoke(
                            snapshot.Button,
                            new object[] { selection });
                    else
                    {
                        OptionsButtonSelectionField.SetValue(
                            snapshot.Button, 0);
                        snapshot.ValueText.text = CreatorToolsSpanish
                            ? "COPIAR URL"
                            : "COPY URL";
                    }
                    currentItems.Add(snapshot.Button);
                }

                if (OptionsVerticalSelectionField != null)
                    OptionsVerticalSelectionField.SetValue(options, 0);
                if (OptionsUpdateVerticalMethod != null)
                    OptionsUpdateVerticalMethod.Invoke(options, null);
                creatorToolsNativeConfigured = true;
                Logger.LogInfo(
                    "Creator Tools is using Cuphead's native Visual menu (" +
                    creatorToolsNativeButtonSnapshots.Count +
                    " rows detected, " + usedLabels.Count +
                    " labels matched)." );
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    "Creator Tools could not configure the native Visual menu: " +
                    ex.Message);
                CloseCreatorToolsMenu(true);
            }
        }

        private void RestoreCreatorToolsNativeOptions()
        {
            var options = creatorToolsNativeOptions;
            for (var i = 0;
                 i < creatorToolsNativeButtonSnapshots.Count; i++)
            {
                var snapshot = creatorToolsNativeButtonSnapshots[i];
                try
                {
                    OptionsButtonLocalizationField.SetValue(
                        snapshot.Button, snapshot.LocalizationHelper);
                    OptionsButtonValuesField.SetValue(
                        snapshot.Button, snapshot.Options);
                    OptionsButtonWrapField.SetValue(
                        snapshot.Button, snapshot.Wrap);
                    if (snapshot.Options != null &&
                        snapshot.Options.Length > snapshot.Selection &&
                        snapshot.Selection >= 0)
                        OptionsButtonUpdateMethod.Invoke(
                            snapshot.Button,
                            new object[] { snapshot.Selection });
                    else
                    {
                        OptionsButtonSelectionField.SetValue(
                            snapshot.Button, snapshot.Selection);
                        if (snapshot.ValueText != null)
                            snapshot.ValueText.text = snapshot.Value;
                    }
                    if (snapshot.ValueText != null)
                    {
                        snapshot.ValueText.text = snapshot.Value;
                        snapshot.ValueText.gameObject.SetActive(
                            snapshot.ValueActive);
                    }
                    if (snapshot.LocalizationHelper != null)
                        snapshot.LocalizationHelper.enabled =
                            snapshot.LocalizationEnabled;
                    if (snapshot.LabelText != null)
                    {
                        snapshot.LabelText.text = snapshot.Label;
                        snapshot.LabelText.gameObject.SetActive(
                            snapshot.LabelActive);
                    }
                    if (snapshot.LabelLocalizationHelper != null)
                        snapshot.LabelLocalizationHelper.enabled =
                            snapshot.LabelLocalizationEnabled;
                    if (snapshot.Row != null)
                        snapshot.Row.SetActive(snapshot.RowActive);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(
                        "Creator Tools could not restore one native Visual row: " +
                        ex.Message);
                }
            }

            if (options != null && OptionsCurrentItemsField != null &&
                creatorToolsNativeOriginalItems != null)
            {
                var currentItems = OptionsCurrentItemsField.GetValue(options)
                    as IList;
                if (currentItems != null)
                {
                    currentItems.Clear();
                    for (var i = 0;
                         i < creatorToolsNativeOriginalItems.Length; i++)
                        currentItems.Add(
                            creatorToolsNativeOriginalItems[i]);
                }
            }

            if (creatorToolsNativeTitle != null)
            {
                creatorToolsNativeTitle.text =
                    creatorToolsNativeTitleText;
                if (creatorToolsNativeTitleLocalization != null)
                    creatorToolsNativeTitleLocalization.enabled =
                        creatorToolsNativeTitleLocalizationEnabled;
            }

            creatorToolsNativeButtonSnapshots.Clear();
            creatorToolsNativeMenuRows.Clear();
            creatorToolsNativeOriginalItems = null;
            creatorToolsNativeTitle = null;
            creatorToolsNativeTitleText = null;
            creatorToolsNativeTitleLocalization = null;
            creatorToolsNativeConfigured = false;
        }

        private void ApplyCreatorToolsNativeSelection()
        {
            if (!creatorToolsNativeConfigured ||
                creatorToolsNativeOptions == null ||
                OptionsVerticalSelectionField == null)
                return;
            var index = (int)OptionsVerticalSelectionField.GetValue(
                creatorToolsNativeOptions);
            if (index < 0 || index >= CreatorToolsMenuItemCount ||
                index >= creatorToolsNativeMenuRows.Count)
                return;
            var snapshot = creatorToolsNativeMenuRows[index];
            var selection = (int)OptionsButtonSelectionField.GetValue(
                snapshot.Button);

            switch (index)
            {
                case 0:
                    SetCreatorToolsEnabled(selection == 1);
                    SetCreatorToolsNativeButtonSelection(
                        snapshot,
                        creatorToolsEnabledSetting.Value ? 1 : 0);
                    break;
                case 1:
                    SetCreatorToolsPreview(selection == 1);
                    SetCreatorToolsNativeButtonSelection(
                        snapshot,
                        creatorToolsPreviewSetting.Value ? 1 : 0);
                    if (creatorToolsNativeMenuRows.Count > 0)
                        SetCreatorToolsNativeButtonSelection(
                            creatorToolsNativeMenuRows[0],
                            creatorToolsEnabledSetting.Value ? 1 : 0);
                    break;
                case 2:
                    creatorToolsScaleSetting.Value =
                        1f + selection * 0.5f;
                    creatorToolsLabelKey = null;
                    break;
                case 3:
                    creatorToolsOrderSetting.Value =
                        (CreatorToolsOrder)selection;
                    break;
                case 4:
                    creatorToolsAlignmentSetting.Value =
                        (CreatorToolsAlignment)selection;
                    break;
                case 5:
                    creatorToolsOpacitySetting.Value =
                        25 + selection * 5;
                    break;
            }

            if (index != 1)
                PublishCreatorToolsState(true);
            if (OptionsMenuSelectSoundMethod != null)
                OptionsMenuSelectSoundMethod.Invoke(
                    creatorToolsNativeOptions, null);
        }

        private void ActivateCreatorToolsNativeSelection()
        {
            if (!creatorToolsNativeConfigured ||
                creatorToolsNativeOptions == null ||
                OptionsVerticalSelectionField == null)
                return;
            var index = (int)OptionsVerticalSelectionField.GetValue(
                creatorToolsNativeOptions);
            if (index < 0 || index >= CreatorToolsMenuItemCount ||
                index >= creatorToolsNativeMenuRows.Count)
                return;
            var snapshot = creatorToolsNativeMenuRows[index];
            if (index == CreatorToolsMenuItemCount - 1)
            {
                CopyCreatorToolsUrl();
                snapshot.ValueText.text = CreatorToolsSpanish
                    ? "COPIADA"
                    : "COPIED";
                if (OptionsMenuSelectSoundMethod != null)
                    OptionsMenuSelectSoundMethod.Invoke(
                        creatorToolsNativeOptions, null);
                return;
            }

            OptionsButtonIncrementMethod.Invoke(snapshot.Button, null);
            ApplyCreatorToolsNativeSelection();
        }

        private static void SetCreatorToolsNativeButtonSelection(
            CreatorToolsNativeButtonSnapshot snapshot, int selection)
        {
            if (snapshot == null || snapshot.Button == null)
                return;
            var values = OptionsButtonValuesField.GetValue(snapshot.Button)
                as string[];
            if (values == null || values.Length == 0)
                return;
            selection = Mathf.Clamp(selection, 0, values.Length - 1);
            OptionsButtonUpdateMethod.Invoke(
                snapshot.Button, new object[] { selection });
        }

        private string[] CreatorToolsNativeValues(int index)
        {
            switch (index)
            {
                case 0:
                case 1:
                    return CreatorToolsSpanish
                        ? new[] { "DESACTIVADO", "ACTIVADO" }
                        : new[] { "DISABLED", "ENABLED" };
                case 2:
                    return new[] { "1X", "1.5X", "2X" };
                case 3:
                    return CreatorToolsSpanish
                        ? new[] { "ICONOS ARRIBA", "TEXTO ARRIBA" }
                        : new[] { "ICONS ABOVE", "TEXT ABOVE" };
                case 4:
                    return CreatorToolsSpanish
                        ? new[] { "IZQUIERDA", "CENTRO", "DERECHA" }
                        : new[] { "LEFT", "CENTER", "RIGHT" };
                case 5:
                    var opacityValues = new string[16];
                    for (var i = 0; i < opacityValues.Length; i++)
                        opacityValues[i] = (25 + i * 5) + "%";
                    return opacityValues;
                default:
                    return new string[0];
            }
        }

        private int CreatorToolsNativeSelection(int index)
        {
            switch (index)
            {
                case 0:
                    return creatorToolsEnabledSetting.Value ? 1 : 0;
                case 1:
                    return creatorToolsPreviewSetting.Value ? 1 : 0;
                case 2:
                    return Mathf.Clamp(Mathf.RoundToInt(
                        (creatorToolsScaleSetting.Value - 1f) / 0.5f),
                        0, 2);
                case 3:
                    return (int)creatorToolsOrderSetting.Value;
                case 4:
                    return (int)creatorToolsAlignmentSetting.Value;
                case 5:
                    return Mathf.Clamp(
                        (creatorToolsOpacitySetting.Value - 25) / 5,
                        0, 15);
                default:
                    return 0;
            }
        }

        private static void SetCreatorToolsNativeRowActive(
            CreatorToolsNativeButtonSnapshot snapshot, bool active)
        {
            if (snapshot.Row != null)
                snapshot.Row.SetActive(active);
            else
            {
                if (snapshot.ValueText != null)
                    snapshot.ValueText.gameObject.SetActive(active);
                if (snapshot.LabelText != null)
                    snapshot.LabelText.gameObject.SetActive(active);
            }
        }

        private static Text FindCreatorToolsNativeLabel(
            GameObject visualObject, Text value,
            HashSet<Text> values, HashSet<Text> used)
        {
            if (visualObject == null || value == null)
                return null;
            var parent = value.transform.parent;
            while (parent != null &&
                   parent.gameObject != visualObject)
            {
                var localTexts = parent.GetComponentsInChildren<Text>(true);
                var local = BestCreatorToolsNativeLabel(
                    value, localTexts, values, used);
                if (local != null)
                    return local;
                parent = parent.parent;
            }
            return BestCreatorToolsNativeLabel(
                value,
                visualObject.GetComponentsInChildren<Text>(true),
                values, used);
        }

        private static Text BestCreatorToolsNativeLabel(
            Text value, Text[] candidates, HashSet<Text> values,
            HashSet<Text> used)
        {
            Text best = null;
            var bestScore = float.MaxValue;
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate == value ||
                    values.Contains(candidate) || used.Contains(candidate))
                    continue;
                var deltaY = Mathf.Abs(
                    candidate.rectTransform.position.y -
                    value.rectTransform.position.y);
                var deltaX = Mathf.Abs(
                    candidate.rectTransform.position.x -
                    value.rectTransform.position.x);
                var score = deltaY * 10000f + deltaX;
                if (score >= bestScore)
                    continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        private static GameObject FindCreatorToolsNativeRow(
            GameObject visualObject, Text value, Text label)
        {
            if (visualObject == null || value == null || label == null)
                return null;
            var ancestors = new HashSet<Transform>();
            var current = value.transform;
            while (current != null && current.gameObject != visualObject)
            {
                ancestors.Add(current);
                current = current.parent;
            }
            current = label.transform;
            while (current != null && current.gameObject != visualObject)
            {
                if (ancestors.Contains(current))
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        private static Text FindCreatorToolsNativeTitle(
            GameObject visualObject, HashSet<Text> values,
            HashSet<Text> labels)
        {
            if (visualObject == null)
                return null;
            Text best = null;
            var bestSize = int.MinValue;
            var allTexts = visualObject.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < allTexts.Length; i++)
            {
                var candidate = allTexts[i];
                if (candidate == null || values.Contains(candidate) ||
                    labels.Contains(candidate))
                    continue;
                if (candidate.fontSize <= bestSize)
                    continue;
                best = candidate;
                bestSize = candidate.fontSize;
            }
            return best;
        }

        private void UpdateCreatorToolsMenuInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) ||
                IsControllerMenuButtonDown(CupheadButton.Cancel))
            {
                PlayNativeMenuSound(
                    "menu_carddown", closeClip, 0.65f);
                CloseCreatorToolsMenu(true);
                return;
            }

            var move = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                IsControllerMenuButtonDown(CupheadButton.MenuUp))
                move = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow) ||
                     IsControllerMenuButtonDown(CupheadButton.MenuDown))
                move = 1;
            if (move != 0)
            {
                creatorToolsMenuSelection = Wrap(
                    creatorToolsMenuSelection + move,
                    CreatorToolsMenuItemCount);
                PlayNativeMenuSound(
                    "menu_equipment_move", selectionClip, 0.45f);
                return;
            }

            var direction = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                IsControllerMenuButtonDown(CupheadButton.MenuLeft))
                direction = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) ||
                     IsControllerMenuButtonDown(CupheadButton.MenuRight))
                direction = 1;
            if (direction != 0 && creatorToolsMenuSelection != 6)
            {
                ChangeCreatorToolsMenuSetting(
                    creatorToolsMenuSelection, direction);
                PlayNativeMenuSound(
                    "menu_equipment_move", selectionClip, 0.45f);
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Return) &&
                !Input.GetKeyDown(KeyCode.KeypadEnter) &&
                !IsControllerMenuButtonDown(CupheadButton.Accept))
                return;
            if (creatorToolsMenuSelection == 6)
                CopyCreatorToolsUrl();
            else
                ChangeCreatorToolsMenuSetting(
                    creatorToolsMenuSelection, 1);
            PlayNativeMenuSound(
                "menu_equipment_select", selectionClip, 0.65f);
        }

        private void ChangeCreatorToolsMenuSetting(
            int setting, int direction)
        {
            switch (setting)
            {
                case 0:
                    SetCreatorToolsEnabled(
                        !creatorToolsEnabledSetting.Value);
                    break;
                case 1:
                    SetCreatorToolsPreview(
                        !creatorToolsPreviewSetting.Value);
                    break;
                case 2:
                    var scaleIndex = Mathf.Clamp(Mathf.RoundToInt(
                        (creatorToolsScaleSetting.Value - 1f) / 0.5f),
                        0, 2);
                    scaleIndex = Wrap(scaleIndex + direction, 3);
                    creatorToolsScaleSetting.Value =
                        1f + scaleIndex * 0.5f;
                    creatorToolsLabelKey = null;
                    break;
                case 3:
                    creatorToolsOrderSetting.Value =
                        creatorToolsOrderSetting.Value ==
                        CreatorToolsOrder.IconsAbove
                            ? CreatorToolsOrder.TextAbove
                            : CreatorToolsOrder.IconsAbove;
                    break;
                case 4:
                    var alignment = (int)
                        creatorToolsAlignmentSetting.Value;
                    creatorToolsAlignmentSetting.Value =
                        (CreatorToolsAlignment)Wrap(
                            alignment + direction, 3);
                    break;
                case 5:
                    var opacityIndex =
                        (creatorToolsOpacitySetting.Value - 25) / 5;
                    opacityIndex = Wrap(
                        opacityIndex + direction, 16);
                    creatorToolsOpacitySetting.Value =
                        25 + opacityIndex * 5;
                    break;
            }
            PublishCreatorToolsState(true);
        }

        private void CopyCreatorToolsUrl()
        {
            GUIUtility.systemCopyBuffer = CreatorToolsUrl;
            creatorToolsMenuNotice = CreatorToolsSpanish
                ? "URL COPIADA"
                : "URL COPIED";
            creatorToolsMenuNoticeUntil =
                Time.realtimeSinceStartup + 2.5f;
        }

        private void DrawCreatorToolsMenu()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.52f);
            GUI.DrawTexture(new Rect(
                0f, 0f, DesignWidth, DesignHeight),
                Texture2D.whiteTexture);
            GUI.color = Color.white;

            var panel = new Rect(310f, 56f, 660f, 608f);
            theme.DrawPaper(panel);
            GUI.BeginGroup(panel);

            GUI.color = Ink;
            GUI.DrawTexture(new Rect(18f, 16f, 624f, 58f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 15f, 612f, 58f),
                "LA PICHI RULETA", titleStyle);

            var leftStyle = new GUIStyle(bodyStyle);
            leftStyle.alignment = TextAnchor.MiddleLeft;
            var rightStyle = new GUIStyle(bodyStyle);
            rightStyle.alignment = TextAnchor.MiddleRight;
            var selectedLeft = new GUIStyle(leftStyle);
            selectedLeft.normal.textColor = Color.white;
            var selectedRight = new GUIStyle(rightStyle);
            selectedRight.normal.textColor = Color.white;

            for (var i = 0; i < CreatorToolsMenuItemCount; i++)
            {
                var row = new Rect(42f, 94f + i * 56f, 576f, 46f);
                var selected = creatorToolsMenuSelection == i;
                GUI.color = selected
                    ? Red
                    : new Color(0.88f, 0.79f, 0.60f, 0.70f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GameTheme.DrawBorder(row, Ink, selected ? 3f : 2f);
                GUI.Label(new Rect(row.x + 15f, row.y,
                    315f, row.height), CreatorToolsMenuLabel(i),
                    selected ? selectedLeft : leftStyle);
                GUI.Label(new Rect(row.x + 330f, row.y,
                    231f, row.height), CreatorToolsMenuValue(i),
                    selected ? selectedRight : rightStyle);

                if (GUI.Button(row, string.Empty, GUIStyle.none))
                {
                    creatorToolsMenuSelection = i;
                    if (i == 6)
                        CopyCreatorToolsUrl();
                    else
                        ChangeCreatorToolsMenuSetting(i, 1);
                    PlayNativeMenuSound(
                        "menu_equipment_select", selectionClip, 0.65f);
                }
            }

            GUI.Label(new Rect(42f, 490f, 576f, 28f),
                CreatorToolsServerStatus(), subtitleStyle);
            if (!string.IsNullOrEmpty(creatorToolsMenuNotice) &&
                Time.realtimeSinceStartup < creatorToolsMenuNoticeUntil)
                GUI.Label(new Rect(42f, 520f, 576f, 25f),
                    creatorToolsMenuNotice, subtitleStyle);
            GUI.Label(new Rect(42f, 556f, 576f, 24f),
                CreatorToolsSpanish
                    ? "ACEPTAR: CAMBIAR  ·  CANCELAR: VOLVER"
                    : "ACCEPT: CHANGE  ·  CANCEL: BACK",
                smallStyle);
            GUI.EndGroup();
        }

        private string CreatorToolsMenuLabel(int index)
        {
            if (!CreatorToolsSpanish)
            {
                switch (index)
                {
                    case 0: return "CREATOR TOOLS";
                    case 1: return "PREVIEW";
                    case 2: return "SIZE";
                    case 3: return "ORDER";
                    case 4: return "ALIGNMENT";
                    case 5: return "OPACITY";
                    default: return "COPY OVERLAY URL";
                }
            }
            switch (index)
            {
                case 0: return "CREATOR TOOLS";
                case 1: return "VISTA PREVIA";
                case 2: return "TAMAÑO";
                case 3: return "ORDEN";
                case 4: return "ALINEACIÓN";
                case 5: return "OPACIDAD";
                default: return "COPIAR URL DEL OVERLAY";
            }
        }

        private string CreatorToolsMenuValue(int index)
        {
            switch (index)
            {
                case 0:
                    return CreatorToolsOnOff(
                        creatorToolsEnabledSetting.Value);
                case 1:
                    return CreatorToolsOnOff(
                        creatorToolsPreviewSetting.Value);
                case 2:
                    return creatorToolsScaleSetting.Value.ToString(
                        "0.0#", System.Globalization.CultureInfo.InvariantCulture) +
                        "X";
                case 3:
                    if (creatorToolsOrderSetting.Value ==
                        CreatorToolsOrder.TextAbove)
                        return CreatorToolsSpanish
                            ? "TEXTO ARRIBA"
                            : "TEXT ABOVE";
                    return CreatorToolsSpanish
                        ? "ICONOS ARRIBA"
                        : "ICONS ABOVE";
                case 4:
                    if (creatorToolsAlignmentSetting.Value ==
                        CreatorToolsAlignment.Left)
                        return CreatorToolsSpanish
                            ? "IZQUIERDA"
                            : "LEFT";
                    if (creatorToolsAlignmentSetting.Value ==
                        CreatorToolsAlignment.Right)
                        return CreatorToolsSpanish
                            ? "DERECHA"
                            : "RIGHT";
                    return CreatorToolsSpanish ? "CENTRO" : "CENTER";
                case 5:
                    return creatorToolsOpacitySetting.Value + "%";
                default:
                    return ">";
            }
        }

        private string CreatorToolsOnOff(bool value)
        {
            if (CreatorToolsSpanish)
                return value ? "ACTIVADO" : "DESACTIVADO";
            return value ? "ENABLED" : "DISABLED";
        }

        private string CreatorToolsServerStatus()
        {
            if (!string.IsNullOrEmpty(creatorToolsServerError))
                return CreatorToolsSpanish
                    ? "ERROR: " + creatorToolsServerError
                    : "ERROR: NO LOCAL PORT AVAILABLE";
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning)
                return CreatorToolsSpanish
                    ? "SERVIDOR DESACTIVADO"
                    : "SERVER DISABLED";
            var clients = creatorToolsServer.ClientCount;
            var status = CreatorToolsUrl + "  ·  " + clients +
                         (clients == 1 ? " CLIENT" : " CLIENTS");
            if (creatorToolsPortChanged)
                status += CreatorToolsSpanish
                    ? "  ·  PUERTO ACTUALIZADO"
                    : "  ·  PORT UPDATED";
            return status;
        }

        private bool CreatorToolsSpanish
        {
            get
            {
                if (modLocalization == null)
                    return true;
                return modLocalization.CurrentLanguage ==
                           Localization.Languages.SpanishAmerica ||
                       modLocalization.CurrentLanguage ==
                           Localization.Languages.SpanishSpain;
            }
        }

        private static int GetCreatorToolsPauseSelection(
            LevelPauseGUI pause)
        {
            if (pause == null || LevelPauseSelectionField == null)
                return 0;
            return (int)LevelPauseSelectionField.GetValue(pause);
        }

        private static void SetCreatorToolsPauseSelection(
            LevelPauseGUI pause, int selection, bool refresh)
        {
            if (pause == null || LevelPauseSelectionField == null)
                return;
            LevelPauseSelectionField.SetValue(pause, selection);
            if (refresh)
                RefreshCreatorToolsPauseSelection(pause);
        }

        private static void RefreshCreatorToolsPauseSelection(
            LevelPauseGUI pause)
        {
            if (pause == null || LevelPauseUpdateSelectionMethod == null)
                return;
            LevelPauseUpdateSelectionMethod.Invoke(pause, null);
        }
    }
}
