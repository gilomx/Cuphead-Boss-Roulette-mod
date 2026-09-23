using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed partial class InkRainChallengeRuntime
    {
        private IncrementalAssetPreparation inkAssetPreparation;
        private bool inkAssetsReady;
        private readonly List<Sprite> ownedInkSprites = new List<Sprite>();
        private Stopwatch inkAssetPreparationClock;
        private IncrementalAssetPreparation squidAssetPreparation;
        private bool squidAssetsReady;
        private readonly List<Sprite> ownedSquidSprites = new List<Sprite>();
        private readonly List<Texture2D> ownedSquidTextures = new List<Texture2D>();
        private Stopwatch squidAssetPreparationClock;

        internal bool AssetsSettled
        {
            get { return inkAssetPreparation != null && inkAssetPreparation.Settled; }
        }

        internal bool SquidAssetsSettled
        {
            get { return squidAssetPreparation != null && squidAssetPreparation.Settled; }
        }

        // Read-only even on a cold process: eligibility, intro and gameplay
        // Update never perform disk reads, PNG decoding or texture creation.
        private bool EnsureInkAssets() { return inkAssetsReady; }

        internal bool NeedsAssetPreparation(bool includeSquid)
        {
            return !AssetsSettled || (includeSquid && inkAssetsReady && !SquidAssetsSettled);
        }

        internal void PrepareAssetsDuringLoading(bool includeSquid = false)
        {
            if (!SceneLoader.CurrentlyLoading) return;
            if (AssetsSettled)
            {
                if (includeSquid) PrepareSquidAssetsDuringLoading();
                return;
            }
            if (inkAssetPreparation == null)
            {
                inkAssetPreparationClock = Stopwatch.StartNew();
                inkAssetPreparation = new IncrementalAssetPreparation(PrepareInkAssetSteps().GetEnumerator(),
                    delegate(Exception exception)
                    {
                        ReleaseInkAssets();
                        if (log != null) log.LogWarning("Ink asset preparation failed under loading fade: " + exception.Message);
                    });
            }
            inkAssetPreparation.Advance(true);
            if (inkAssetPreparation.Settled && !inkAssetPreparation.Failed && log != null)
                log.LogInfo("Ink assets prepared under loading fade: " + inkAssetPreparation.CompletedSteps +
                    " images, " + inkAssetPreparationClock.ElapsedMilliseconds + " ms total, longest image " +
                    inkAssetPreparation.MaximumStepMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
        }

        private IEnumerable<Action> PrepareInkAssetSteps()
        {
            var root = Path.Combine(assetsDirectory, "inkrain");
            var center = new Vector2(0.5f, 0.5f);
            foreach (var step in SpriteSequenceSteps(Path.Combine(root, "projectiles"), "pirate_squid_inkblob_*.png", center,
                delegate(Sprite[] frames) { inkDropFrames = frames; })) yield return step;

            foreach (var group in new[] { "a", "b", "c", "d" })
                foreach (var step in SpriteSequenceSteps(Path.Combine(root, "impacts"), "pirate_squid_ink_death_" + group + "_*.png", center,
                    delegate(Sprite[] frames) { if (frames.Length > 0) groundImpactAnimations.Add(frames); })) yield return step;
            foreach (var step in SpriteSequenceSteps(Path.Combine(root, "screen"), "pirate_squid_ink_screen_0001.png", center,
                delegate(Sprite[] frames) { inkScreenOverlay = frames.Length > 0 ? frames[0] : null; })) yield return step;
            foreach (var step in NativeSplatSteps(Path.Combine(root, "screen-native"))) yield return step;
            if (inkDropFrames == null || inkDropFrames.Length == 0)
                throw new InvalidOperationException("No ink projectile images were found.");
            inkAssetsReady = true;
        }

        // Only the equipped/roulette challenge requests these frames. Their
        // ownership and failures are independent of the shared rain catalog.
        internal void PrepareSquidAssetsDuringLoading()
        {
            if (!SceneLoader.CurrentlyLoading || !inkAssetsReady || SquidAssetsSettled) return;
            if (squidAssetPreparation == null)
            {
                squidAssetPreparationClock = Stopwatch.StartNew();
                squidAssetPreparation = new IncrementalAssetPreparation(PrepareSquidAssetSteps().GetEnumerator(),
                    delegate(Exception exception)
                    {
                        ReleaseSquidAssets();
                        if (log != null) log.LogWarning("Squid intro preparation failed under loading fade: " + exception.Message);
                    });
            }
            squidAssetPreparation.Advance(true);
            if (squidAssetPreparation.Settled && !squidAssetPreparation.Failed && log != null)
                log.LogInfo("Squid intro prepared under loading fade: " + squidAssetPreparation.CompletedSteps +
                    " images, " + squidAssetPreparationClock.ElapsedMilliseconds + " ms total.");
        }

        private IEnumerable<Action> PrepareSquidAssetSteps()
        {
            var squid = Path.Combine(Path.Combine(assetsDirectory, "inkrain"), "squid");
            var bottom = new Vector2(0.5f, 0f);
            foreach (var step in SpriteSequenceSteps(squid, "pirate_squid_entrance_*.png", bottom,
                delegate(Sprite[] frames) { squidEntranceFrames = frames; }, true)) yield return step;
            Sprite[] allAttack = null;
            foreach (var step in SpriteSequenceSteps(squid, "pirate_squid_????.png", bottom,
                delegate(Sprite[] frames) { allAttack = frames; }, true)) yield return step;
            foreach (var step in SpriteSequenceSteps(squid, "pirate_squid_leave_*.png", bottom,
                delegate(Sprite[] frames) { squidLeaveFrames = frames; }, true)) yield return step;
            squidAttackFrames = SpriteRange(allAttack, 0, 3);
            squidAttackLoopFrames = SpriteRange(allAttack, 3, 16);
            squidExitFrames = JoinSpriteRanges(SpriteRange(allAttack, 3, 7), squidLeaveFrames);
            if (squidEntranceFrames.Length == 0 || squidAttackFrames.Length == 0 ||
                squidAttackLoopFrames.Length == 0 || squidLeaveFrames.Length == 0)
                throw new InvalidOperationException("The squid intro images are incomplete.");
            squidAssetsReady = true;
        }

        private IEnumerable<Action> SpriteSequenceSteps(string directory, string pattern, Vector2 pivot, Action<Sprite[]> completed,
            bool forSquid = false)
        {
            var frames = new List<Sprite>();
            var files = Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : new string[0];
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var imagePath = file;
                yield return delegate { frames.Add(LoadInkSprite(imagePath, pivot, 100f, forSquid)); };
            }
            completed(frames.ToArray());
        }

        private IEnumerable<Action> NativeSplatSteps(string directory)
        {
            var pivotFile = Path.Combine(directory, "pivots.tsv");
            if (!Directory.Exists(directory) || !File.Exists(pivotFile)) yield break;
            var pivots = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(pivotFile))
            {
                var parts = line.Split('\t');
                float x, y;
                if (parts.Length == 3 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                    pivots[parts[0]] = new Vector2(x, y);
            }
            foreach (var group in new[] { "a", "b", "c", "d", "e" })
            {
                var files = Directory.GetFiles(directory, "pirate_squid_ink_screen_" + group + "_*.png");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                var frames = new List<Sprite>();
                var positions = new List<Vector2>();
                foreach (var file in files)
                {
                    Vector2 position;
                    if (!pivots.TryGetValue(Path.GetFileName(file), out position)) continue;
                    var imagePath = file;
                    var normalizedPivot = position;
                    yield return delegate
                    {
                        var sprite = LoadInkSprite(imagePath, new Vector2(0.5f, 0.5f), 1f);
                        frames.Add(sprite);
                        positions.Add(new Vector2(normalizedPivot.x * sprite.rect.width, normalizedPivot.y * sprite.rect.height));
                    };
                }
                if (frames.Count > 0)
                {
                    inkScreenAnimations.Add(frames.ToArray());
                    inkScreenPivotPixels.Add(positions.ToArray());
                }
            }
        }

        private Sprite LoadInkSprite(string path, Vector2 pivot, float pixelsPerUnit, bool forSquid = false)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            (forSquid ? ownedSquidTextures : ownedInkTextures).Add(texture);
            texture.name = Path.GetFileNameWithoutExtension(path);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // Cuphead's Mono omits InvalidDataException. Referencing it even
            // on an unused error branch prevents this method/iterator from
            // being JIT-compiled, so use the same core exception as gift images.
            if (!texture.LoadImage(bytes)) throw new InvalidOperationException("Could not decode " + Path.GetFileName(path));
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, pixelsPerUnit);
            (forSquid ? ownedSquidSprites : ownedInkSprites).Add(sprite);
            sprite.name = texture.name;
            return sprite;
        }

        private void ReleaseInkAssets()
        {
            inkAssetsReady = false;
            inkDropFrames = null;
            inkScreenOverlay = null;
            groundImpactAnimations.Clear();
            inkScreenAnimations.Clear();
            inkScreenPivotPixels.Clear();
            foreach (var sprite in ownedInkSprites) if (sprite != null) Destroy(sprite);
            ownedInkSprites.Clear();
            foreach (var texture in ownedInkTextures) if (texture != null) Destroy(texture);
            ownedInkTextures.Clear();
        }

        private void ReleaseSquidAssets()
        {
            squidAssetsReady = false;
            squidEntranceFrames = squidAttackFrames = squidAttackLoopFrames = squidLeaveFrames = squidExitFrames = null;
            foreach (var sprite in ownedSquidSprites) if (sprite != null) Destroy(sprite);
            ownedSquidSprites.Clear();
            foreach (var texture in ownedSquidTextures) if (texture != null) Destroy(texture);
            ownedSquidTextures.Clear();
        }

        private void DisposeInkAssetPreparation()
        {
            if (inkAssetPreparation != null) inkAssetPreparation.Dispose();
            if (squidAssetPreparation != null) squidAssetPreparation.Dispose();
            ReleaseInkAssets();
            ReleaseSquidAssets();
        }
    }
}
