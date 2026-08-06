using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class GameTheme
    {
        private readonly Dictionary<string, Sprite> sprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private float lastScanAt = -10f;
        internal Font TitleFont { get; private set; }
        internal Font BodyFont { get; private set; }
        internal Font AccentFont { get; private set; }
        internal Texture2D PaperTexture { get; private set; }
        internal Texture2D ShadowTexture { get; private set; }
        internal bool FontsReady { get { return TitleFont != null && BodyFont != null; } }

        internal GameTheme()
        {
            PaperTexture = CreatePaperTexture();
            ShadowTexture = CreateSolidTexture(new Color32(21, 18, 15, 190));
            Refresh();
        }

        internal void Refresh()
        {
            if (Time.realtimeSinceStartup - lastScanAt < 2f)
                return;

            lastScanAt = Time.realtimeSinceStartup;
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            TitleFont = FindFont(fonts, "CupheadVogue-ExtraBold-merged", "CupheadVogue-ExtraBold", "CupheadVogue-Bold-merged");
            BodyFont = FindFont(fonts, "CupheadMemphis-Medium-merged", "CupheadMemphis-Medium", "CupheadVogue-Bold-merged");
            AccentFont = FindFont(fonts, "CupheadHenriette-Localized", "CupheadHenriette-A-merged", "CupheadVogue-Bold-merged");

            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite != null && !string.IsNullOrEmpty(sprite.name) && !sprites.ContainsKey(sprite.name))
                    sprites.Add(sprite.name, sprite);
            }
        }

        internal Sprite GetSprite(string name)
        {
            Refresh();
            Sprite sprite;
            if (string.IsNullOrEmpty(name) || !sprites.TryGetValue(name, out sprite) ||
                sprite == null)
                return null;
            return sprite;
        }

        internal bool DrawSprite(string name, Rect rect, Color color)
        {
            var sprite = GetSprite(name);
            if (sprite == null || sprite.texture == null)
                return false;

            var textureRect = sprite.textureRect;
            var texture = sprite.texture;
            var uv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previous;
            return true;
        }

        internal void DrawPaper(Rect rect)
        {
            GUI.DrawTexture(new Rect(rect.x + 9f, rect.y + 11f, rect.width, rect.height), ShadowTexture);
            GUI.DrawTexture(rect, PaperTexture);
            DrawBorder(rect, new Color(0.08f, 0.07f, 0.06f), 5f);
            DrawBorder(new Rect(rect.x + 9f, rect.y + 9f, rect.width - 18f, rect.height - 18f),
                new Color(0.24f, 0.19f, 0.14f, 0.75f), 2f);
        }

        internal static void DrawBorder(Rect rect, Color color, float thickness)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        internal void Dispose()
        {
            if (PaperTexture != null)
                UnityEngine.Object.Destroy(PaperTexture);
            if (ShadowTexture != null)
                UnityEngine.Object.Destroy(ShadowTexture);
        }

        private static Font FindFont(IEnumerable<Font> fonts, params string[] names)
        {
            foreach (var name in names)
            {
                var font = fonts.FirstOrDefault(item =>
                    item != null && string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase));
                if (font != null)
                    return font;
            }
            return fonts.FirstOrDefault(item => item != null);
        }

        private static Texture2D CreatePaperTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var pixels = new Color32[size * size];
            var random = new System.Random(1930);
            for (var i = 0; i < pixels.Length; i++)
            {
                var noise = random.Next(-8, 9);
                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(238 + noise, 0, 255),
                    (byte)Mathf.Clamp(222 + noise, 0, 255),
                    (byte)Mathf.Clamp(181 + noise, 0, 255),
                    255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.name = "GilomxRoulettePaper";
            return texture;
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }
    }
}
