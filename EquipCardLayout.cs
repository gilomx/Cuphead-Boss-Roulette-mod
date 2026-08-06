using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private GUIStyle equipBossStyle;
        private GUIStyle equipFightStyle;
        private GUIStyle equipBossShadowStyle;
        private GUIStyle equipSlotStyle;
        private GUIStyle checklistLabelStyle;
        private GUIStyle checklistValueStyle;
        private GUIStyle checklistSpinStyle;
        private Font equipStylesFont;
        // AJUSTE VISUAL DE LOS CIRCULOS (X/Y).
        // X mueve horizontalmente: menor = izquierda, mayor = derecha.
        // Y mueve verticalmente: menor = arriba, mayor = abajo.
        private static readonly Vector2 ShotACenter = new Vector2(98.4f, 399f);
        private static readonly Vector2 ShotBCenter = new Vector2(199.1f, 399f);
        private static readonly Vector2 SuperCenter = new Vector2(298.9f, 399f);
        private static readonly Vector2 CharmCenter = new Vector2(397.7f, 399f);
        private static readonly Vector2 ChallengeCenter = new Vector2(497.1f, 399f);
        private const float EquipIconSize = 80f;
        private const float EquipLabelGap = 4f;
        private const float EquipIconFramesPerSecond = 12.5f;
        private static readonly Regex TransparentFightMarkup = new Regex(
            "<color=#00000000>.*?</color>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex FightMarkup = new Regex(
            "<[^>]+>", RegexOptions.Singleline);

        private void DrawRoulette()
        {

            EnsureEquipCardStyles();

            var baseMatrix = GUI.matrix;
            var center = new Vector3(DesignWidth * 0.5f, DesignHeight * 0.5f, 0f);
            var rawOffsetY = (1f - cardVisibility) * 760f;
            var screenScale = Mathf.Min(
                Screen.width / DesignWidth, Screen.height / DesignHeight);
            var offsetY = screenScale > 0f
                ? Mathf.Round(rawOffsetY * screenScale) / screenScale
                : rawOffsetY;
            var motion =
                Matrix4x4.TRS(center + new Vector3(0f, offsetY, 0f),
                    Quaternion.Euler(0f, 0f, cardRoll), Vector3.one) *
                Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
            GUI.matrix = baseMatrix * motion;

            const float cardWidth = 595f;
            const float cardHeight = 668f;
            var card = new Rect((DesignWidth - cardWidth) * 0.5f, 26f, cardWidth, cardHeight);


            var background = GetTexture("card/roulette-card.png");
            if (background != null)
                GUI.DrawTexture(card, background, ScaleMode.StretchToFill, true);
            else
                theme.DrawPaper(card);

            GUI.BeginGroup(card);
            DrawEquipCardContents();
            GUI.EndGroup();
            GUI.matrix = baseMatrix;
        }

        private void DrawEquipCardContents()
        {
            var bossIndex = DisplayPoolIndex(
                0, result.Boss, availableBossIndices, 0);
            var boss = RouletteData.Bosses[bossIndex];
            var bossPortrait = PulseRect(new Rect(208.5f, 116f, 178f, 178f), 0);
            DrawTexture(bossPortrait, boss.Image);

            var bossTitle = LocalizedBossName(boss).ToUpperInvariant();
            GUI.Label(new Rect(55.5f, 277f, 487f, 39f), bossTitle, equipBossShadowStyle);
            GUI.Label(new Rect(54f, 275f, 487f, 39f), bossTitle, equipBossStyle);
            var fightTitle = LocalizedFightName(boss);
            if (!string.IsNullOrEmpty(fightTitle))
                GUI.Label(new Rect(54f, 309f, 487f, 24f),
                    fightTitle.ToUpperInvariant(), equipFightStyle);

            var weapon1 = DisplayPoolIndex(
                1, result.Weapon1, availableWeaponIndices, 0);
            var weapon2 = DisplayPoolIndex(
                2, result.Weapon2, availableWeaponIndices,
                availableWeaponIndices.Count / 2);
            var super = DisplayPoolIndex(
                3, result.Super, availableSuperIndices,
                availableSuperIndices.Count / 3);
            var charm = DisplayPoolIndex(
                4, result.Charm, availableCharmIndices,
                availableCharmIndices.Count / 4);

            DrawEquipSlot(ShotACenter, "TIRO A", RouletteData.Weapons[weapon1].Image,
                RouletteData.Weapons[weapon1].NativeSprite, 1);
            DrawEquipSlot(ShotBCenter, "TIRO B", RouletteData.Weapons[weapon2].Image,
                RouletteData.Weapons[weapon2].NativeSprite, 2);
            DrawEquipSlot(SuperCenter, "SÚPER", RouletteData.Supers[super].Image,
                RouletteData.Supers[super].NativeSprite, 3);
            DrawEquipSlot(CharmCenter, "AMULETO", RouletteData.Charms[charm].Image,
                RouletteData.Charms[charm].NativeSprite, 4);
            DrawModifierSlot(bossIndex);

            DrawChecklistSettings();
            DrawSpinBand();
        }

        private void DrawEquipSlot(Vector2 center, string label, string fallbackImage,
            string nativeSprite, int field)
        {
            var halfSize = EquipIconSize * 0.5f;
            var rect = PulseRect(new Rect(center.x - halfSize, center.y - halfSize,
                EquipIconSize, EquipIconSize), field);
            var animatedName = AnimatedSpriteName(nativeSprite, 3, EquipIconFramesPerSecond);
            if (!theme.DrawSprite(animatedName, rect, Color.white) &&
                !theme.DrawSprite(nativeSprite, rect, Color.white))
                DrawTexture(rect, fallbackImage);

            if (running && revealed <= field)
            {
                var sheen = AnimatedSpriteName("equip_icon_sheen_0001", 5, 12f);
                theme.DrawSprite(sheen, rect, new Color(1f, 1f, 1f, 0.28f));
            }
            GUI.Label(new Rect(center.x - 49f, center.y + halfSize + EquipLabelGap,
                98f, 23f), label, equipSlotStyle);
        }

        private void DrawModifierSlot(int bossIndex)
        {
            var halfSize = EquipIconSize * 0.5f;
            var rect = PulseRect(new Rect(ChallengeCenter.x - halfSize,
                ChallengeCenter.y - halfSize, EquipIconSize, EquipIconSize), 5);
            if (uglyMode)
            {
                var rollingModifier = CurrentRollingModifier(bossIndex);
                var modifier = DisplayIndex(5, result.Modifier, RouletteData.Modifiers.Length,
                    rollingModifier - ticker);
                DrawTexture(rect, AnimatedTexturePath(
                    RouletteData.Modifiers[modifier].Image, 3, EquipIconFramesPerSecond));
            }
            else
            {
                var empty = AnimatedSpriteName("equip_icon_empty_0001", 3, EquipIconFramesPerSecond);
                if (!theme.DrawSprite(empty, rect, Color.white))
                    DrawTexture(rect, "weapons/vacio.png");
            }

            if (running && revealed <= 5)
            {
                var sheen = AnimatedSpriteName("equip_icon_sheen_0001", 5, 12f);
                theme.DrawSprite(sheen, rect, new Color(1f, 1f, 1f, 0.28f));
            }
            GUI.Label(new Rect(ChallengeCenter.x - 49f,
                ChallengeCenter.y + halfSize + EquipLabelGap, 98f, 23f), "RETO",
                equipSlotStyle);
        }

        private void DrawChecklistSettings()
        {
            DrawChecklistRow(0, 468f, "DIFICULTAD", DifficultyLabel());
            DrawChecklistRow(1, 498f, "RETO", uglyMode ? "ACTIVADO" : "DESACTIVADO");
            DrawChecklistRow(2, 528f, "CARGA AUTOMÁTICA", autoLoad.Value ? "ACTIVADA" : "DESACTIVADA");
        }

        private void DrawChecklistRow(int index, float y, string label, string value)
        {
            if (navigationIndex == index && !running && !pendingLoad)
            {
                GUI.color = new Color(0.24f, 0.31f, 0.20f, 0.18f);
                GUI.DrawTexture(new Rect(61f, y, 472f, 27f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                DrawNavigationCursor(new Rect(29f, y - 7f, 42f, 42f));
            }

            GUI.Label(new Rect(72f, y, 250f, 27f), label, checklistLabelStyle);
            GUI.Label(new Rect(316f, y, 207f, 27f),
                index == 0 ? "‹  " + value + "  ›" : value, checklistValueStyle);
            DrawInkLine(new Rect(72f, y + 27f, 451f, 1f));
        }

        private void DrawSpinBand()
        {
            var band = new Rect(52f, 584f, 491f, 52f);
            if (navigationIndex == 3 && !running && !pendingLoad)
                DrawNavigationCursor(new Rect(202f, 590f, 42f, 42f));

            var label = running ? "GIRANDO..." :
                pendingLoad ? "PREPARANDO COMBATE..." :
                resultReady ? "¡JUGAR!" :
                status.IndexOf("PARTIDA", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "SELECCIONA UNA PARTIDA"
                    : "¡GIRAR!";
            GUI.Label(band, label, checklistSpinStyle);
        }

        private string DifficultyLabel()
        {
            if (difficulty == Level.Mode.Easy)
                return "SIMPLE";
            if (difficulty == Level.Mode.Hard)
                return "EXPERTO";
            return "NORMAL";
        }

        private string LocalizedBossName(BossEntry boss)
        {
            try
            {
                var element = Localization.Find(boss.Level.ToString());
                if (element != null)
                {
                    var translated = element.translation.text;
                    if (!string.IsNullOrEmpty(translated))
                        return translated.Replace("\\N", " ").Replace("\\n", " ");
                }
            }
            catch
            {
            }
            return boss.Character;
        }

        private string LocalizedFightName(BossEntry boss)
        {
            var useSpanishSpainFallback = false;
            try
            {
                var language = Localization.language;
                if (language != Localization.Languages.SpanishSpain &&
                    language != Localization.Languages.SpanishAmerica)
                    return string.Empty;
                useSpanishSpainFallback =
                    language == Localization.Languages.SpanishSpain;

                // This is the same key used by Cuphead's native difficulty card.
                var element = Localization.Find(boss.Level + "Selection");
                if (element != null)
                {
                    var translated = PlainFightTitle(element.translation.SanitizedText());
                    if (!string.IsNullOrEmpty(translated))
                        return translated;
                }
            }
            catch
            {
                // Missing localization should leave the subtitle empty instead
                // of displaying a title from a different language.
            }
            return useSpanishSpainFallback ? boss.Fight : string.Empty;
        }

        private static string PlainFightTitle(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = TransparentFightMarkup.Replace(value, "");
            value = FightMarkup.Replace(value, "");
            var lines = value.Replace("\\N", "\n").Replace("\\n", "\n")
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
                lines[i] = lines[i].Trim(' ', '\t', ';', '"');
            return string.Join(" ", lines).Trim();
        }

        private static string AnimatedSpriteName(string firstFrame, int frameCount, float framesPerSecond)
        {
            if (string.IsNullOrEmpty(firstFrame) || frameCount < 2)
                return firstFrame;
            var marker = firstFrame.LastIndexOf("_0001", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return firstFrame;
            var frame = 1 + ((int)(Time.realtimeSinceStartup * framesPerSecond) % frameCount);
            return firstFrame.Substring(0, marker) + "_000" + frame;
        }

        private static string AnimatedTexturePath(string firstFrame,
            int frameCount, float framesPerSecond)
        {
            if (string.IsNullOrEmpty(firstFrame) || frameCount < 2)
                return firstFrame;

            var marker = firstFrame.LastIndexOf(
                "_01.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return firstFrame;

            var frame = 1 + ((int)(Time.realtimeSinceStartup * framesPerSecond) % frameCount);
            return firstFrame.Substring(0, marker) + "_" + frame.ToString("00") +
                   firstFrame.Substring(marker + 3);
        }

        private void DrawNavigationCursor(Rect rect)
        {
            var frame = 1 + ((int)(Time.realtimeSinceStartup * 12f) % 5);
            theme.DrawSprite("hand_cursor_boil_000" + frame, rect, Color.white);
        }

        private void DrawInkLine(Rect rect)
        {
            GUI.color = new Color(0.12f, 0.24f, 0.21f, 0.65f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void EnsureEquipCardStyles()
        {
            if (equipBossStyle != null && equipStylesFont == theme.TitleFont)
                return;

            equipStylesFont = theme.TitleFont;
            var cardText = new Color(0.96f, 0.92f, 0.76f);
            var secondaryText = new Color(0.91f, 0.86f, 0.69f);
            var nativeInk = new Color(0.24f, 0.14f, 0.20f);
            equipBossStyle = NewStyle(theme.TitleFont, 27, TextAnchor.MiddleCenter, cardText, FontStyle.Normal);
            equipBossShadowStyle = NewStyle(theme.TitleFont, 27, TextAnchor.MiddleCenter,
                new Color(0.12f, 0.07f, 0.10f, 0.58f), FontStyle.Normal);
            equipFightStyle = NewStyle(theme.BodyFont, 14, TextAnchor.MiddleCenter, secondaryText, FontStyle.Normal);
            equipSlotStyle = NewStyle(theme.BodyFont, 14, TextAnchor.MiddleCenter, nativeInk, FontStyle.Bold);
            checklistLabelStyle = NewStyle(theme.BodyFont, 17, TextAnchor.MiddleLeft, cardText, FontStyle.Normal);
            checklistValueStyle = NewStyle(theme.TitleFont, 17, TextAnchor.MiddleRight, cardText, FontStyle.Normal);
            checklistSpinStyle = NewStyle(theme.TitleFont, 27, TextAnchor.MiddleCenter, nativeInk, FontStyle.Normal);
        }
    }
}
