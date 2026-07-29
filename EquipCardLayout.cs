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
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, DesignWidth, DesignHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            EnsureEquipCardStyles();

            const float cardWidth = 570f;
            const float cardHeight = 660f;
            var card = new Rect((DesignWidth - cardWidth) * 0.5f, 24f, cardWidth, cardHeight);
            theme.DrawPaper(card);

            GUI.BeginGroup(card);
            DrawEquipCardInterior(new Rect(17f, 17f, cardWidth - 34f, cardHeight - 34f));
            GUI.EndGroup();

            if (secretVisible)
                DrawEquipCardSecretPanel(card);
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
            var bossPortrait = PulseRect(new Rect(183f, 104f, 204f, 204f), 0);

            GUI.color = new Color(0.45f, 0.45f, 0.35f, 0.45f);
            GUI.DrawTexture(bossPortrait, GetEquipCircleTexture("portrait"));
            GUI.color = Color.white;
            DrawTexture(new Rect(bossPortrait.x + 7f, bossPortrait.y + 7f,
                bossPortrait.width - 14f, bossPortrait.height - 14f), boss.Image);

            GUI.Label(new Rect(45f, 309f, 480f, 41f), boss.Character.ToUpperInvariant(), equipBossStyle);
            GUI.Label(new Rect(45f, 346f, 480f, 25f), boss.Fight.ToUpperInvariant(), equipFightStyle);

            var weapon1 = DisplayIndex(1, result.Weapon1, RouletteData.Weapons.Length, 0);
            var weapon2 = DisplayIndex(2, result.Weapon2, RouletteData.Weapons.Length, RouletteData.Weapons.Length / 2);
            var super = DisplayIndex(3, result.Super, RouletteData.Supers.Length, RouletteData.Supers.Length / 3);
            var charm = DisplayIndex(4, result.Charm, RouletteData.Charms.Length, RouletteData.Charms.Length / 4);

            DrawEquipSlot(73f, 384f, "TIRO A", RouletteData.Weapons[weapon1].Image,
                RouletteData.Weapons[weapon1].NativeSprite, 1, new Color(0.39f, 0.65f, 0.66f));
            DrawEquipSlot(190f, 384f, "TIRO B", RouletteData.Weapons[weapon2].Image,
                RouletteData.Weapons[weapon2].NativeSprite, 2, new Color(0.29f, 0.67f, 0.55f));
            DrawEquipSlot(307f, 384f, "SÚPER", RouletteData.Supers[super].Image,
                RouletteData.Supers[super].NativeSprite, 3, new Color(0.49f, 0.41f, 0.54f));
            DrawEquipSlot(424f, 384f, "AMULETO", RouletteData.Charms[charm].Image,
                RouletteData.Charms[charm].NativeSprite, 4, new Color(0.77f, 0.23f, 0.45f));

            DrawEquipCardChallenge(bossIndex);
            DrawEquipCardSettings();
            DrawEquipCardSpinBand();

            GUI.Label(new Rect(38f, 609f, 494f, 18f),
                "F6 CERRAR   ·   F7 GIRAR   ·   CTRL+I SELECCIÓN FORZADA", equipFooterStyle);
        }

        private void DrawEquipSlot(float centerX, float y, string label, string fallbackImage,
            string nativeSprite, int field, Color fill)
        {
            var rect = PulseRect(new Rect(centerX - 42f, y, 84f, 84f), field);
            GUI.color = fill;
            GUI.DrawTexture(rect, GetEquipCircleTexture("slot"));
            GUI.color = Color.white;

            var iconRect = new Rect(rect.x + 13f, rect.y + 13f, rect.width - 26f, rect.height - 26f);
            if (!theme.DrawSprite(nativeSprite, iconRect, Color.white))
                DrawTexture(iconRect, fallbackImage);

            GUI.Label(new Rect(centerX - 55f, y + 86f, 110f, 25f), label, equipSlotStyle);
        }

        private void DrawEquipCardChallenge(int bossIndex)
        {
            if (uglyMode)
            {
                var rollingModifier = CurrentRollingModifier(bossIndex);
                var modifier = DisplayIndex(5, result.Modifier, RouletteData.Modifiers.Length, rollingModifier - ticker);
                GUI.Label(new Rect(58f, 493f, 305f, 30f), "OJALÁ TE SALGA ALGO FEO.", subtitleStyle);
                var modifierRect = PulseRect(new Rect(385f, 480f, 62f, 62f), 5);
                GUI.color = new Color(0.86f, 0.49f, 0.17f);
                GUI.DrawTexture(modifierRect, GetEquipCircleTexture("modifier"));
                GUI.color = Color.white;
                DrawTexture(new Rect(modifierRect.x + 7f, modifierRect.y + 7f,
                    modifierRect.width - 14f, modifierRect.height - 14f),
                    RouletteData.Modifiers[modifier].Image);
            }
            else
            {
                GUI.Label(new Rect(70f, 491f, 430f, 32f), "¡QUE LA SUERTE ELIJA POR TI!", subtitleStyle);
            }
        }

        private void DrawEquipCardSettings()
        {
            var y = 539f;
            if (GUI.Button(new Rect(45f, y, 102f, 27f), difficulty == Level.Mode.Easy ? "✓ SIMPLE" : "SIMPLE",
                difficulty == Level.Mode.Easy ? buttonActiveStyle : buttonStyle))
                difficulty = Level.Mode.Easy;
            if (GUI.Button(new Rect(153f, y, 102f, 27f), difficulty == Level.Mode.Normal ? "✓ NORMAL" : "NORMAL",
                difficulty == Level.Mode.Normal ? buttonActiveStyle : buttonStyle))
                difficulty = Level.Mode.Normal;
            if (GUI.Button(new Rect(261f, y, 102f, 27f), difficulty == Level.Mode.Hard ? "✓ EXPERTO" : "EXPERTO",
                difficulty == Level.Mode.Hard ? buttonActiveStyle : buttonStyle))
                difficulty = Level.Mode.Hard;
            if (GUI.Button(new Rect(369f, y, 156f, 27f), uglyMode ? "✓ MODO FEO" : "MODO FEO",
                uglyMode ? buttonActiveStyle : buttonStyle))
                uglyMode = !uglyMode;
        }

        private void DrawEquipCardSpinBand()
        {
            var band = new Rect(35f, 573f, 500f, 43f);
            GUI.color = new Color(0.94f, 0.90f, 0.78f);
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameTheme.DrawBorder(band, new Color(0.23f, 0.31f, 0.28f), 3f);

            var label = running ? "GIRANDO..." :
                pendingLoad ? "PREPARANDO COMBATE..." :
                status.IndexOf("PARTIDA", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? "SELECCIONA UNA PARTIDA"
                    : "¡GIRAR!  ·  F7";
            GUI.enabled = !running && !pendingLoad;
            if (GUI.Button(new Rect(39f, 577f, 492f, 35f), label, equipBossStyle))
                StartRoulette();
            GUI.enabled = true;

            var buttonRect = new Rect(39f, 577f, 492f, 35f);
            if (buttonRect.Contains(Event.current.mousePosition))
                theme.DrawSprite("hand_cursor_boil_0001", new Rect(3f, 567f, 53f, 53f), Color.white);
        }

        private void DrawEquipCardSecretPanel(Rect card)
        {
            var panel = new Rect(card.xMax + 20f, card.y + 120f, 284f, 274f);
            if (panel.xMax > DesignWidth - 12f)
                panel.x = card.x - panel.width - 20f;
            theme.DrawPaper(panel);
            GUI.BeginGroup(panel);
            GUI.Label(new Rect(18f, 15f, 248f, 35f), "MENÚ SECRETO", equipHeaderStyle);
            if (GUI.Button(new Rect(24f, 58f, 236f, 34f),
                forceSelection ? "✓ RESULTADO FIJO" : "RESULTADO FIJO",
                forceSelection ? buttonActiveStyle : buttonStyle))
                forceSelection = !forceSelection;
            if (GUI.Button(new Rect(22f, 112f, 45f, 38f), "‹", buttonStyle))
                forced.Boss = Wrap(forced.Boss - 1, RouletteData.Bosses.Length);
            GUI.Label(new Rect(71f, 106f, 142f, 52f),
                RouletteData.Bosses[forced.Boss].Character.ToUpperInvariant(), equipSlotStyle);
            if (GUI.Button(new Rect(217f, 112f, 45f, 38f), "›", buttonStyle))
                forced.Boss = Wrap(forced.Boss + 1, RouletteData.Bosses.Length);
            if (GUI.Button(new Rect(34f, 174f, 216f, 36f), "COPIAR EQUIPO VISIBLE", buttonStyle))
            {
                forced.Weapon1 = result.Weapon1;
                forced.Weapon2 = result.Weapon2;
                forced.Super = result.Super;
                forced.Charm = result.Charm;
                forced.Modifier = result.Modifier;
            }
            GUI.Label(new Rect(24f, 220f, 236f, 37f), "CTRL+I PARA CERRAR", equipFooterStyle);
            GUI.EndGroup();
        }

        private Texture2D GetEquipCircleTexture(string key)
        {
            var cacheKey = "__equip_circle_" + key;
            Texture2D texture;
            if (textures.TryGetValue(cacheKey, out texture))
                return texture;

            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                Color color;
                if (distance > 63f)
                    color = Color.clear;
                else if (distance > 58f)
                    color = new Color(0.16f, 0.17f, 0.14f, 1f);
                else if (distance > 53f)
                    color = new Color(0.76f, 0.68f, 0.50f, 1f);
                else
                    color = Color.white;
                texture.SetPixel(x, y, color);
            }
            texture.Apply(false, true);
            texture.filterMode = FilterMode.Bilinear;
            texture.name = cacheKey;
            textures[cacheKey] = texture;
            return texture;
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
