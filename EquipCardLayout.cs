using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private GUIStyle equipScriptStyle;
        private GUIStyle equipHeaderStyle;
        private GUIStyle equipBossStyle;
        private GUIStyle equipFightStyle;
        private GUIStyle equipSlotStyle;
        private GUIStyle equipFooterStyle;
        private Font equipStylesFont;

        private void DrawRoulette()
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f * Mathf.Clamp01(cardVisibility));
            GUI.DrawTexture(new Rect(0f, 0f, DesignWidth, DesignHeight), Texture2D.whiteTexture);
            GUI.color = previousColor;

            EnsureEquipCardStyles();

            var baseMatrix = GUI.matrix;
            var eased = EaseOutBack(cardVisibility);
            var center = new Vector3(DesignWidth * 0.5f, DesignHeight * 0.5f, 0f);
            var offset = new Vector3((1f - cardVisibility) * 470f, (1f - cardVisibility) * 34f, 0f);
            var roll = (1f - cardVisibility) * 7.5f;
            var size = Mathf.Max(0.82f, 0.88f + 0.12f * eased);
            var cardMotion =
                Matrix4x4.TRS(center + offset, Quaternion.Euler(0f, 0f, roll), new Vector3(size, size, 1f)) *
                Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
            GUI.matrix = baseMatrix * cardMotion;

            const float cardWidth = 570f;
            const float cardHeight = 704f;
            var card = new Rect((DesignWidth - cardWidth) * 0.5f, 8f, cardWidth, cardHeight);
            theme.DrawPaper(card);

            GUI.BeginGroup(card);
            DrawEquipCardInterior(new Rect(17f, 17f, cardWidth - 34f, cardHeight - 34f));
            GUI.EndGroup();
            GUI.matrix = baseMatrix;
        }

        private static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value) - 1f;
            const float overshoot = 1.70158f;
            return 1f + (overshoot + 1f) * value * value * value + overshoot * value * value;
        }

        private void DrawEquipCardInterior(Rect interior)
        {
            GUI.color = new Color(0.57f, 0.64f, 0.39f);
            GUI.DrawTexture(interior, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameTheme.DrawBorder(interior, new Color(0.11f, 0.22f, 0.19f), 4f);
            GameTheme.DrawBorder(new Rect(interior.x + 9f, interior.y + 9f, interior.width - 18f, interior.height - 18f),
                new Color(0.25f, 0.31f, 0.20f), 2f);

            GUI.Label(new Rect(42f, 29f, 215f, 55f), "Ruleta", equipScriptStyle);
            GUI.Label(new Rect(220f, 38f, 222f, 39f), "BOSS CARD", equipHeaderStyle);
            GUI.Label(new Rect(468f, 24f, 50f, 65f), "1P", equipHeaderStyle);
            DrawInkLine(new Rect(35f, 91f, 500f, 3f));

            var bossIndex = DisplayIndex(0, result.Boss, RouletteData.Bosses.Length, 0);
            var boss = RouletteData.Bosses[bossIndex];
            var bossPortrait = PulseRect(new Rect(183f, 102f, 204f, 204f), 0);
            DrawTexture(bossPortrait, boss.Image);

            GUI.Label(new Rect(45f, 307f, 480f, 41f), boss.Character.ToUpperInvariant(), equipBossStyle);
            GUI.Label(new Rect(45f, 344f, 480f, 25f), boss.Fight.ToUpperInvariant(), equipFightStyle);

            var weapon1 = DisplayIndex(1, result.Weapon1, RouletteData.Weapons.Length, 0);
            var weapon2 = DisplayIndex(2, result.Weapon2, RouletteData.Weapons.Length, RouletteData.Weapons.Length / 2);
            var super = DisplayIndex(3, result.Super, RouletteData.Supers.Length, RouletteData.Supers.Length / 3);
            var charm = DisplayIndex(4, result.Charm, RouletteData.Charms.Length, RouletteData.Charms.Length / 4);

            DrawEquipSlot(73f, 381f, "TIRO A", RouletteData.Weapons[weapon1].Image,
                RouletteData.Weapons[weapon1].NativeSprite, 1);
            DrawEquipSlot(190f, 381f, "TIRO B", RouletteData.Weapons[weapon2].Image,
                RouletteData.Weapons[weapon2].NativeSprite, 2);
            DrawEquipSlot(307f, 381f, "SÚPER", RouletteData.Supers[super].Image,
                RouletteData.Supers[super].NativeSprite, 3);
            DrawEquipSlot(424f, 381f, "AMULETO", RouletteData.Charms[charm].Image,
                RouletteData.Charms[charm].NativeSprite, 4);

            DrawEquipCardChallenge(bossIndex);
            DrawEquipCardSettings();
            DrawEquipCardSpinBand();

            GUI.Label(new Rect(38f, 665f, 494f, 18f),
                "FLECHAS MOVER   ·   ENTER CONFIRMAR   ·   ESC VOLVER", equipFooterStyle);
        }

        private void DrawEquipSlot(float centerX, float y, string label, string fallbackImage,
            string nativeSprite, int field)
        {
            var rect = PulseRect(new Rect(centerX - 45f, y, 90f, 90f), field);
            if (!theme.DrawSprite(nativeSprite, rect, Color.white))
                DrawTexture(rect, fallbackImage);
            GUI.Label(new Rect(centerX - 55f, y + 89f, 110f, 25f), label, equipSlotStyle);
        }

        private void DrawEquipCardChallenge(int bossIndex)
        {
            if (uglyMode)
            {
                var rollingModifier = CurrentRollingModifier(bossIndex);
                var modifier = DisplayIndex(5, result.Modifier, RouletteData.Modifiers.Length, rollingModifier - ticker);
                GUI.Label(new Rect(46f, 500f, 328f, 34f), "OJALÁ TE SALGA ALGO FEO.", subtitleStyle);
                var modifierRect = PulseRect(new Rect(397f, 482f, 72f, 72f), 5);
                DrawTexture(modifierRect, RouletteData.Modifiers[modifier].Image);
            }
            else
            {
                GUI.Label(new Rect(70f, 500f, 430f, 34f), "¡QUE LA SUERTE ELIJA POR TI!", subtitleStyle);
            }
        }

        private void DrawEquipCardSettings()
        {
            const float y = 568f;
            DrawSettingButton(0, new Rect(45f, y, 102f, 29f),
                difficulty == Level.Mode.Easy ? "✓ SIMPLE" : "SIMPLE",
                difficulty == Level.Mode.Easy, delegate { difficulty = Level.Mode.Easy; });
            DrawSettingButton(1, new Rect(153f, y, 102f, 29f),
                difficulty == Level.Mode.Normal ? "✓ NORMAL" : "NORMAL",
                difficulty == Level.Mode.Normal, delegate { difficulty = Level.Mode.Normal; });
            DrawSettingButton(2, new Rect(261f, y, 102f, 29f),
                difficulty == Level.Mode.Hard ? "✓ EXPERTO" : "EXPERTO",
                difficulty == Level.Mode.Hard, delegate { difficulty = Level.Mode.Hard; });
            DrawSettingButton(3, new Rect(369f, y, 156f, 29f),
                uglyMode ? "✓ MODO FEO" : "MODO FEO",
                uglyMode, delegate { uglyMode = !uglyMode; });
        }

        private void DrawSettingButton(int index, Rect rect, string label, bool active, System.Action action)
        {
            if (GUI.Button(rect, label, active ? buttonActiveStyle : buttonStyle))
                action();
            if (navigationIndex == index)
                DrawNavigationCursor(new Rect(rect.x - 32f, rect.y - 8f, 42f, 42f));
        }

        private void DrawEquipCardSpinBand()
        {
            var band = new Rect(35f, 610f, 500f, 47f);
            GUI.color = new Color(0.94f, 0.90f, 0.78f);
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameTheme.DrawBorder(band, new Color(0.23f, 0.31f, 0.28f), 3f);

            var label = running ? "GIRANDO..." :
                pendingLoad ? "PREPARANDO COMBATE..." :
                status.IndexOf("PARTIDA", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? "SELECCIONA UNA PARTIDA"
                    : "¡GIRAR!";
            GUI.enabled = !running && !pendingLoad;
            if (GUI.Button(new Rect(39f, 614f, 492f, 39f), label, equipBossStyle))
                StartRoulette();
            GUI.enabled = true;

            if (navigationIndex == 4 && !running && !pendingLoad)
                DrawNavigationCursor(new Rect(3f, 607f, 51f, 51f));
        }

        private void DrawNavigationCursor(Rect rect)
        {
            var frame = 1 + ((int)(Time.realtimeSinceStartup * 12f) % 5);
            theme.DrawSprite("hand_cursor_boil_000" + frame, rect, Color.white);
        }

        private void DrawInkLine(Rect rect)
        {
            GUI.color = new Color(0.12f, 0.24f, 0.21f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void EnsureEquipCardStyles()
        {
            if (equipScriptStyle != null && equipStylesFont == theme.TitleFont)
                return;

            equipStylesFont = theme.TitleFont;
            equipScriptStyle = NewStyle(theme.AccentFont, 40, TextAnchor.MiddleLeft,
                new Color(0.91f, 0.88f, 0.72f), FontStyle.Italic);
            equipHeaderStyle = NewStyle(theme.BodyFont, 25, TextAnchor.MiddleCenter,
                new Color(0.20f, 0.25f, 0.16f), FontStyle.Normal);
            equipBossStyle = NewStyle(theme.TitleFont, 27, TextAnchor.MiddleCenter, Ink, FontStyle.Normal);
            equipFightStyle = NewStyle(theme.BodyFont, 14, TextAnchor.MiddleCenter, Ink, FontStyle.Normal);
            equipSlotStyle = NewStyle(theme.BodyFont, 15, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            equipFooterStyle = NewStyle(theme.BodyFont, 11, TextAnchor.MiddleCenter,
                new Color(0.18f, 0.22f, 0.16f), FontStyle.Normal);
        }
    }
}
