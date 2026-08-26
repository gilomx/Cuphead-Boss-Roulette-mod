using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Loads bundled TikTok gift art once on Unity's main thread. Stream
    /// interactions use the installed catalog image instead of downloading a
    /// remote URL while gameplay is running.
    /// </summary>
    internal static class CreatorToolsGiftImageCache
    {
        private const float GiftImageHeight = 28f;

        private static readonly Dictionary<string, CachedGiftImage> Images =
            new Dictionary<string, CachedGiftImage>(
                StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> FailedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite TryGet(
            string imagePath,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(imagePath))
                return null;

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(imagePath);
            }
            catch (Exception exception)
            {
                error = "The gift image path was invalid: " +
                    exception.Message;
                return null;
            }

            CachedGiftImage cached;
            if (Images.TryGetValue(normalizedPath, out cached))
                return cached.Sprite;
            if (FailedPaths.Contains(normalizedPath) ||
                !File.Exists(normalizedPath))
            {
                if (!FailedPaths.Contains(normalizedPath))
                {
                    FailedPaths.Add(normalizedPath);
                    error = "The gift image file was not found: " +
                        normalizedPath;
                }
                return null;
            }

            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                texture = new Texture2D(
                    2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(File.ReadAllBytes(normalizedPath)))
                    // InvalidDataException is unavailable in Cuphead's
                    // legacy Mono runtime even though the net35 reference
                    // assemblies allow it at compile time. Keep this type in
                    // mscorlib so merely entering this method cannot trigger
                    // a TypeLoadException before the image is decoded.
                    throw new InvalidOperationException(
                        "Unity could not decode the gift image.");
                texture.name = "CreatorTools_GiftTexture_" +
                    Path.GetFileNameWithoutExtension(normalizedPath);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;

                var pixelsPerUnit = Mathf.Max(
                    1f, texture.height / GiftImageHeight);
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                sprite.name = "CreatorTools_GiftSprite_" +
                    Path.GetFileNameWithoutExtension(normalizedPath);
                Images.Add(normalizedPath,
                    new CachedGiftImage(texture, sprite));
                return sprite;
            }
            catch (Exception exception)
            {
                FailedPaths.Add(normalizedPath);
                error = "Could not load the gift image '" +
                    normalizedPath + "': " + exception.Message;
                if (sprite != null)
                    UnityEngine.Object.Destroy(sprite);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
                return null;
            }
        }

        internal static void Clear()
        {
            foreach (var pair in Images)
            {
                if (pair.Value.Sprite != null)
                    UnityEngine.Object.Destroy(pair.Value.Sprite);
                if (pair.Value.Texture != null)
                    UnityEngine.Object.Destroy(pair.Value.Texture);
            }
            Images.Clear();
            FailedPaths.Clear();
        }

        private sealed class CachedGiftImage
        {
            internal readonly Texture2D Texture;
            internal readonly Sprite Sprite;

            internal CachedGiftImage(Texture2D texture, Sprite sprite)
            {
                Texture = texture;
                Sprite = sprite;
            }
        }
    }
}
