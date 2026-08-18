using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private readonly List<GameObject>
            creatorToolsNativeLocalizedRowObjects =
                new List<GameObject>();
        private readonly List<Text>
            creatorToolsNativeLocalizedRowTexts =
                new List<Text>();
        private Color creatorToolsNativeLocalizedRowColor = Color.white;
        private float creatorToolsNativeLocalizedRowCenterX;
        private float creatorToolsNativeLocalizedRowWorldWidth;
        private int creatorToolsNativeLocalizedRowBaseFontSize;

        private void PrepareCreatorToolsNativeLocalizedRows()
        {
            DestroyCreatorToolsNativeLocalizedRows();
            if (creatorToolsMenuPage !=
                    CreatorToolsMenuPage.RouletteOverlay ||
                creatorToolsNativeMenuRows.Count <
                    CreatorToolsMenuItemCount)
                return;

            var bottom = creatorToolsNativeMenuRows[
                CreatorToolsMenuItemCount - 1];
            if (bottom == null || bottom.ValueText == null)
                return;

            var appearance = creatorToolsNativeMenuRows[0].LabelText;
            if (appearance == null)
                appearance = bottom.ValueText;
            creatorToolsNativeLocalizedRowColor = appearance.color;
            creatorToolsNativeLocalizedRowBaseFontSize =
                appearance.fontSize;
            MeasureCreatorToolsNativeLocalizedRowArea(
                bottom.ValueText.rectTransform);

            for (var i = 0;
                 i < CreatorToolsMenuItemCount - 2; i++)
            {
                var row = creatorToolsNativeMenuRows[i];
                if (row == null || row.ValueText == null)
                    continue;

                var displayObject = new GameObject(
                    "Gilomx Stream Overlay Localized Row " + i,
                    typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Text));
                displayObject.transform.SetParent(
                    bottom.ValueText.transform.parent, false);
                displayObject.transform.SetAsLastSibling();
                var displayText = displayObject.GetComponent<Text>();
                CopyCreatorToolsNativeRowAppearance(
                    appearance, displayText);
                CopyCreatorToolsNativeRowRect(
                    bottom.ValueText.rectTransform,
                    displayText.rectTransform);
                displayObject.SetActive(true);
                creatorToolsNativeLocalizedRowObjects.Add(displayObject);
                creatorToolsNativeLocalizedRowTexts.Add(displayText);
            }

            MaintainCreatorToolsNativeLocalizedRows();
        }

        private static void CopyCreatorToolsNativeRowAppearance(
            Text source, Text target)
        {
            target.font = source.font;
            target.material = source.material;
            target.fontStyle = source.fontStyle;
            target.fontSize = source.fontSize;
            target.lineSpacing = source.lineSpacing;
            target.supportRichText = true;
            target.alignment = TextAnchor.MiddleCenter;
            target.alignByGeometry = source.alignByGeometry;
            target.resizeTextForBestFit = false;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Overflow;
            target.raycastTarget = false;
            target.color = source.color;
        }

        private static void CopyCreatorToolsNativeRowRect(
            RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private void MaintainCreatorToolsNativeLocalizedRows()
        {
            if (!creatorToolsMenuOpen ||
                creatorToolsMenuPage !=
                    CreatorToolsMenuPage.RouletteOverlay ||
                creatorToolsNativeLocalizedRowTexts.Count == 0)
                return;

            var selection = OptionsVerticalSelectionField == null ||
                            creatorToolsNativeOptions == null
                ? -1
                : (int)OptionsVerticalSelectionField.GetValue(
                    creatorToolsNativeOptions);
            var rowCount = Mathf.Min(
                creatorToolsNativeLocalizedRowTexts.Count,
                CreatorToolsMenuItemCount - 2);
            for (var i = 0; i < rowCount; i++)
            {
                var row = creatorToolsNativeMenuRows[i];
                var display = creatorToolsNativeLocalizedRowTexts[i];
                if (row == null || row.ValueText == null ||
                    display == null)
                    continue;

                if (row.LabelText != null)
                    row.LabelText.text = string.Empty;
                row.ValueText.text = string.Empty;
                display.color = creatorToolsNativeLocalizedRowColor;
                display.fontSize =
                    creatorToolsNativeLocalizedRowBaseFontSize;
                display.text = CreatorToolsNativeLocalizedRowText(
                    i, selection == i);
                FitCreatorToolsNativeLocalizedRow(display, row);
                SpaceCreatorToolsNativeLocalizedRow(
                    display.rectTransform, i, rowCount);
                display.gameObject.SetActive(true);
            }
        }


        private void SpaceCreatorToolsNativeLocalizedRow(
            RectTransform display, int index, int rowCount)
        {
            if (display == null || rowCount < 2 ||
                creatorToolsNativeMenuRows.Count <
                    CreatorToolsMenuItemCount)
                return;

            var firstY = CreatorToolsNativeRowCenterY(
                creatorToolsNativeMenuRows[0]);
            var lastY = CreatorToolsNativeRowCenterY(
                creatorToolsNativeMenuRows[rowCount - 1]);
            var bottom = creatorToolsNativeMenuRows[
                CreatorToolsMenuItemCount - 1];
            if (bottom == null || bottom.ValueText == null)
                return;

            var step = Mathf.Abs(firstY - lastY) /
                       (rowCount - 1);
            if (step < 0.001f)
                return;
            var bottomY = bottom.ValueText.rectTransform.position.y;
            var direction = Mathf.Sign(firstY - bottomY);
            if (Mathf.Abs(direction) < 0.001f)
                direction = 1f;
            var lastTargetY = bottomY + direction * step * 2f;
            var position = display.position;
            position.y = Mathf.Lerp(
                firstY, lastTargetY, index / (rowCount - 1f));
            display.position = position;
        }

        private static float CreatorToolsNativeRowCenterY(
            CreatorToolsNativeButtonSnapshot row)
        {
            if (row == null || row.ValueText == null)
                return 0f;
            var bottom = float.MaxValue;
            var top = float.MinValue;
            IncludeCreatorToolsNativeVerticalRect(
                row.LabelText == null
                    ? null
                    : row.LabelText.rectTransform,
                ref bottom, ref top);
            IncludeCreatorToolsNativeVerticalRect(
                row.ValueText.rectTransform, ref bottom, ref top);
            return bottom != float.MaxValue && top != float.MinValue
                ? (bottom + top) * 0.5f
                : row.ValueText.rectTransform.position.y;
        }

        private float CreatorToolsNativeLocalizedRowPositionY(
            int index, float fallback)
        {
            return index >= 0 &&
                   index < creatorToolsNativeLocalizedRowTexts.Count &&
                   creatorToolsNativeLocalizedRowTexts[index] != null
                ? creatorToolsNativeLocalizedRowTexts[index]
                    .rectTransform.position.y
                : fallback;
        }

        private string CreatorToolsNativeLocalizedRowText(
            int index, bool selected)
        {
            var label = CreatorToolsMenuLabel(index);
            var value = CreatorToolsMenuValue(index);
            if (string.IsNullOrEmpty(label))
                return value;
            if (string.IsNullOrEmpty(value))
                return label;
            if (!selected)
                return label + ": " + value;
            return label + ": <color=#" +
                   CreatorToolsNativeColorHex(
                       creatorToolsNativeSelectedColor) + ">" +
                   value + "</color>";
        }

        private static string CreatorToolsNativeColorHex(Color color)
        {
            var value = (Color32)color;
            return value.r.ToString("X2") +
                   value.g.ToString("X2") +
                   value.b.ToString("X2") +
                   value.a.ToString("X2");
        }

        private void FitCreatorToolsNativeLocalizedRow(
            Text displayText, CreatorToolsNativeButtonSnapshot row)
        {
            if (creatorToolsNativeLocalizedRowWorldWidth <= 0f)
                return;

            var display = displayText.rectTransform;
            var worldScale = Mathf.Abs(display.lossyScale.x);
            if (worldScale < 0.001f)
                worldScale = 1f;
            display.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                creatorToolsNativeLocalizedRowWorldWidth / worldScale);

            var bottom = float.MaxValue;
            var top = float.MinValue;
            IncludeCreatorToolsNativeVerticalRect(
                row.LabelText == null
                    ? null
                    : row.LabelText.rectTransform,
                ref bottom, ref top);
            IncludeCreatorToolsNativeVerticalRect(
                row.ValueText.rectTransform, ref bottom, ref top);
            var worldScaleY = Mathf.Abs(display.lossyScale.y);
            if (worldScaleY < 0.001f)
                worldScaleY = 1f;
            if (bottom != float.MaxValue && top != float.MinValue &&
                top > bottom)
                display.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    (top - bottom) * 1.15f / worldScaleY);

            var position = display.position;
            position.x = creatorToolsNativeLocalizedRowCenterX;
            position.y = bottom != float.MaxValue &&
                         top != float.MinValue
                ? (bottom + top) * 0.5f
                : row.ValueText.rectTransform.position.y;
            display.position = position;
            CenterCreatorToolsNativeLocalizedRowOptically(
                displayText);
        }

        private void CenterCreatorToolsNativeLocalizedRowOptically(
            Text displayText)
        {
            if (displayText == null ||
                string.IsNullOrEmpty(displayText.text))
                return;

            var display = displayText.rectTransform;
            var settings = displayText.GetGenerationSettings(
                display.rect.size);
            if (!displayText.cachedTextGenerator.Populate(
                    displayText.text, settings))
                return;

            var vertices = displayText.cachedTextGenerator.verts;
            var count = Mathf.Max(0, vertices.Count - 4);
            var left = float.MaxValue;
            var right = float.MinValue;
            for (var i = 0; i + 3 < count; i += 4)
            {
                var glyphLeft = float.MaxValue;
                var glyphRight = float.MinValue;
                var glyphBottom = float.MaxValue;
                var glyphTop = float.MinValue;
                for (var vertex = 0; vertex < 4; vertex++)
                {
                    var point = vertices[i + vertex].position;
                    glyphLeft = Mathf.Min(glyphLeft, point.x);
                    glyphRight = Mathf.Max(glyphRight, point.x);
                    glyphBottom = Mathf.Min(glyphBottom, point.y);
                    glyphTop = Mathf.Max(glyphTop, point.y);
                }

                // Spaces and control characters have no visible quad.
                if (glyphRight - glyphLeft < 0.01f ||
                    glyphTop - glyphBottom < 0.01f)
                    continue;
                left = Mathf.Min(left, glyphLeft);
                right = Mathf.Max(right, glyphRight);
            }

            if (left == float.MaxValue || right == float.MinValue)
                return;
            var scaleFactor = Mathf.Abs(settings.scaleFactor);
            if (scaleFactor < 0.001f)
                scaleFactor = 1f;
            var visibleCenter = (left + right) * 0.5f / scaleFactor;
            var localOffset = display.rect.center.x - visibleCenter;
            var position = display.position;
            position.x = creatorToolsNativeLocalizedRowCenterX +
                         localOffset * Mathf.Abs(display.lossyScale.x);
            display.position = position;
        }

        private static void IncludeCreatorToolsNativeVerticalRect(
            RectTransform rect, ref float bottom, ref float top)
        {
            if (rect == null)
                return;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (var i = 0; i < corners.Length; i++)
            {
                bottom = Mathf.Min(bottom, corners[i].y);
                top = Mathf.Max(top, corners[i].y);
            }
        }

        private void MeasureCreatorToolsNativeLocalizedRowArea(
            RectTransform bottom)
        {
            var centerSum = 0f;
            var centerCount = 0;
            for (var i = 0;
                 i < CreatorToolsMenuItemCount - 2; i++)
            {
                var row = creatorToolsNativeMenuRows[i];
                if (row == null || row.LabelText == null ||
                    row.ValueText == null)
                    continue;
                centerSum += (
                    CreatorToolsNativeTextAnchorX(row.LabelText) +
                    CreatorToolsNativeTextAnchorX(row.ValueText)) * 0.5f;
                centerCount++;
            }

            if (centerCount > 0)
                creatorToolsNativeLocalizedRowCenterX =
                    centerSum / centerCount;
            else
            {
                var bottomLeft = float.MaxValue;
                var bottomRight = float.MinValue;
                IncludeCreatorToolsNativeRect(
                    bottom, ref bottomLeft, ref bottomRight);
                creatorToolsNativeLocalizedRowCenterX =
                    (bottomLeft + bottomRight) * 0.5f;
            }

            ApplyCreatorToolsCanvasCenter(bottom);
            var widest = 0f;
            for (var i = 0;
                 i < CreatorToolsMenuItemCount - 2; i++)
            {
                var row = creatorToolsNativeMenuRows[i];
                if (row == null || row.ValueText == null)
                    continue;
                var left = float.MaxValue;
                var right = float.MinValue;
                IncludeCreatorToolsNativeRect(
                    row.LabelText == null
                        ? null
                        : row.LabelText.rectTransform,
                    ref left, ref right);
                IncludeCreatorToolsNativeRect(
                    row.ValueText.rectTransform, ref left, ref right);
                if (left != float.MaxValue && right != float.MinValue)
                    widest = Mathf.Max(widest, right - left);
            }
            creatorToolsNativeLocalizedRowWorldWidth = widest * 0.90f;
        }

        private void ApplyCreatorToolsCanvasCenter(
            RectTransform bottom)
        {
            var canvas = bottom == null
                ? null
                : bottom.GetComponentInParent<Canvas>();
            var reference = canvas == null
                ? null
                : canvas.rootCanvas.transform as RectTransform;
            if (reference == null ||
                Mathf.Abs(reference.rect.width) < 0.001f)
                return;

            var localPoint = reference.InverseTransformPoint(
                bottom.position);
            localPoint.x = reference.rect.center.x;
            creatorToolsNativeLocalizedRowCenterX =
                reference.TransformPoint(localPoint).x;
        }

        private static void IncludeCreatorToolsNativeRect(
            RectTransform rect, ref float left, ref float right)
        {
            if (rect == null)
                return;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (var i = 0; i < corners.Length; i++)
            {
                left = Mathf.Min(left, corners[i].x);
                right = Mathf.Max(right, corners[i].x);
            }
        }
        private static float CreatorToolsNativeTextAnchorX(Text text)
        {
            var left = float.MaxValue;
            var right = float.MinValue;
            IncludeCreatorToolsNativeRect(
                text.rectTransform, ref left, ref right);
            switch (text.alignment)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                    return left;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    return right;
                default:
                    return (left + right) * 0.5f;
            }
        }



        private void DestroyCreatorToolsNativeLocalizedRows()
        {
            for (var i = 0;
                 i < creatorToolsNativeLocalizedRowObjects.Count; i++)
            {
                if (creatorToolsNativeLocalizedRowObjects[i] != null)
                    DestroyImmediate(
                        creatorToolsNativeLocalizedRowObjects[i]);
            }
            creatorToolsNativeLocalizedRowObjects.Clear();
            creatorToolsNativeLocalizedRowTexts.Clear();
            creatorToolsNativeLocalizedRowCenterX = 0f;
            creatorToolsNativeLocalizedRowWorldWidth = 0f;
            creatorToolsNativeLocalizedRowBaseFontSize = 0;
        }
    }
}
