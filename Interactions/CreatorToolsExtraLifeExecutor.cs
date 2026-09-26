using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsExtraLifeExecutor :
        ICreatorToolsInteractionExecutor,
        ICreatorToolsPhasePersistentInteractionExecutor
    {
        private const string FallbackHeartRelativePath =
            "creator-tools/interactions/extra-life-heart.png";

        private readonly string assetsDirectory;
        private readonly Func<bool> canPreloadNativeAssets;
        private readonly Func<bool> canSpawn;
        private readonly Func<bool> hpOneActive;
        private readonly Func<Shader> suspendedShader;
        private readonly Func<bool> playArrivalSound;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly CreatorToolsExtraLifeInventory inventory =
            new CreatorToolsExtraLifeInventory();
        private CreatorToolsExtraLifeHeart heart;

        private GameObject heartPrefab;
        private Texture2D fallbackHeartTexture;
        private Sprite fallbackHeartSprite;
        private bool fallbackHeartLoadFailed;
        private bool fallbackHeartUseLogged;
        private Vector3 heartSpawnOffset;
        private bool hasHeartSpawnOffset;
        private int attemptSerial;
        private bool hpOneWasActive;
        private bool defeatedDuringHpOne;
        private bool disposed;

        internal CreatorToolsExtraLifeExecutor(
            string assetsDirectory,
            Func<bool> canPreloadNativeAssets,
            Func<bool> canSpawn,
            Func<bool> hpOneActive,
            Func<Shader> suspendedShader,
            Func<bool> playArrivalSound,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.assetsDirectory = assetsDirectory ?? string.Empty;
            this.canPreloadNativeAssets = canPreloadNativeAssets;
            this.canSpawn = canSpawn;
            this.hpOneActive = hpOneActive;
            this.suspendedShader = suspendedShader;
            this.playArrivalSound = playArrivalSound;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        public bool NativeAssetsSettled
        {
            get { return true; }
        }

        public bool Supports(string item)
        {
            return string.Equals(
                item, CreatorToolsHelp.ExtraLife,
                StringComparison.Ordinal);
        }

        public bool IsAvailable(string item)
        {
            return Supports(item) && !disposed && Evaluate(canSpawn) &&
                PlayerOne() != null;
        }

        public void Update()
        {
            if (disposed)
                return;

            // Aircraft levels do not load Ms. Chalice's terrestrial shield
            // prefab. Capture its native heart while the shared interaction
            // catalog is already visiting a ground scene behind the loading
            // fade, so a session that starts in an aircraft gets the exact
            // same animated heart as a session that visited ground first.
            if (heartPrefab == null && Evaluate(canPreloadNativeAssets))
                ResolveHeartPrefab();

            var player = PlayerOne();
            var currentAttempt = CurrentAttemptSerial();
            if (currentAttempt != attemptSerial)
            {
                attemptSerial = currentAttempt;
                defeatedDuringHpOne = false;
                DestroyHearts();
            }

            var hpOne = Evaluate(hpOneActive);
            if (hpOne && player != null &&
                (player.IsDead || player.stats == null ||
                 player.stats.Health <= 0))
            {
                defeatedDuringHpOne = true;
                DestroyHearts();
            }

            if (hpOne && !hpOneWasActive)
                SetHeartSuspended(true);
            else if (!hpOne && hpOneWasActive && !defeatedDuringHpOne)
                SetHeartSuspended(false);
            hpOneWasActive = hpOne;

            if (player == null || player.IsDead ||
                !player.gameObject.activeInHierarchy ||
                defeatedDuringHpOne || !Evaluate(canSpawn))
                return;

            EnsureHeartView(player, hpOne);
        }

        public bool TrySpawn(
            string item,
            string donor,
            string giftImagePath,
            out ICreatorToolsInteractionHandle handle,
            out string feedbackCode,
            out string error)
        {
            handle = null;
            feedbackCode = "requires_gameplay_level";
            error = string.Empty;
            if (!Supports(item))
            {
                feedbackCode = "unknown_item";
                return false;
            }
            if (!IsAvailable(item))
                return false;

            var player = PlayerOne();
            var credit = inventory.Add(donor, giftImagePath);
            if (player != null && !player.IsDead &&
                !defeatedDuringHpOne && heart == null)
            {
                CreatorToolsExtraLifeHeart createdHeart;
                if (!TryCreateHeart(
                        inventory[0], player,
                        out createdHeart, out error))
                {
                    feedbackCode = "native_assets_loading";
                    inventory.Remove(credit.Id);
                    return false;
                }
                heart = createdHeart;
                heart.SetSuspended(
                    Evaluate(hpOneActive), ResolveSuspendedShader());
                PlayArrival(player);
            }

            handle = CompletedHandle.Instance;
            feedbackCode = "spawned";
            return true;
        }

        internal bool TryProtect(
            PlayerStatsManager stats, DamageDealer.DamageInfo damage)
        {
            if (disposed || stats == null || damage == null ||
                damage.damage <= 0f || stats.SuperInvincible ||
                stats.State == PlayerStatsManager.PlayerState.Super ||
                stats.ChaliceShieldOn || Evaluate(hpOneActive) ||
                defeatedDuringHpOne || !IsPlayerOne(stats))
                return false;
            if (heart == null || inventory.Count == 0 ||
                heart.CreditId != inventory[0].Id)
                return false;

            CreatorToolsExtraLifeInventory.Credit credit;
            if (!inventory.TryConsume(false, out credit))
                return false;

            if (heart.CreditId == credit.Id)
                heart.Pop();

            // PlayerDamageReceiver performs the native post-hit cleanup. The
            // temporary flag makes PlayerStatsManager follow exactly its
            // Chalice shield branch without tying our inventory to that one
            // native boolean.
            stats.SetChaliceShield(true);
            if (stats.ChaliceShieldOn)
                return true;

            inventory.RestoreFront(credit);
            return false;
        }

        public void EndGameplayLevel()
        {
            DestroyHearts();
            hpOneWasActive = false;
            defeatedDuringHpOne = false;
            attemptSerial = 0;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            DestroyHearts();
            inventory.Clear();
            heartPrefab = null;
            if (fallbackHeartSprite != null)
                UnityEngine.Object.Destroy(fallbackHeartSprite);
            if (fallbackHeartTexture != null)
                UnityEngine.Object.Destroy(fallbackHeartTexture);
            fallbackHeartSprite = null;
            fallbackHeartTexture = null;
        }

        private void EnsureHeartView(
            AbstractPlayerController player, bool suspended)
        {
            // Only the oldest redeemed life is materialized. Later credits
            // remain FIFO-reserved until the visible heart finishes popping.
            if (heart != null || inventory.Count == 0)
                return;

            CreatorToolsExtraLifeHeart createdHeart;
            string ignored;
            if (!TryCreateHeart(
                    inventory[0], player, out createdHeart, out ignored))
                return;
            heart = createdHeart;
            heart.SetSuspended(suspended, ResolveSuspendedShader());
            PlayArrival(player);
        }

        private bool TryCreateHeart(
            CreatorToolsExtraLifeInventory.Credit credit,
            AbstractPlayerController player,
            out CreatorToolsExtraLifeHeart view,
            out string error)
        {
            view = null;
            error = string.Empty;
            ResolveHeartPrefab();

            GameObject root = null;
            try
            {
                root = heartPrefab != null
                    ? UnityEngine.Object.Instantiate(heartPrefab)
                    : CreateFallbackHeart(player, out error);
                if (root == null)
                    return false;
                root.name = "CreatorTools_ExtraLifeHeart_" + credit.Id;
                root.transform.position = ResolveHeartSpawnPosition(player);
                root.SetActive(true);
                view = root.AddComponent<CreatorToolsExtraLifeHeart>();
                view.Initialize(
                    credit.Id,
                    credit.Donor,
                    credit.GiftImagePath,
                    player);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                view = null;
                return false;
            }
        }

        private GameObject CreateFallbackHeart(
            AbstractPlayerController player,
            out string error)
        {
            error = string.Empty;
            if (!ResolveFallbackHeartSprite(out error))
                return null;

            var root = new GameObject(
                "CreatorTools_ExtraLifeHeart_Fallback");
            root.layer = player == null
                ? 0
                : player.gameObject.layer;
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = fallbackHeartSprite;
            var playerRenderer = FindPlayerRenderer(player);
            if (playerRenderer != null)
            {
                renderer.sortingLayerID = playerRenderer.sortingLayerID;
                renderer.sortingOrder = playerRenderer.sortingOrder + 1;
            }
            if (!fallbackHeartUseLogged && logInfo != null)
            {
                fallbackHeartUseLogged = true;
                logInfo(
                    "La vida extra usa su respaldo persistente en un nivel " +
                    "sin el prefab terrestre de Caliz.");
            }
            return root;
        }

        private bool ResolveFallbackHeartSprite(out string error)
        {
            error = string.Empty;
            if (fallbackHeartSprite != null)
                return true;
            if (fallbackHeartLoadFailed)
            {
                error = "The bundled extra-life heart image is unavailable.";
                return false;
            }

            try
            {
                var path = Path.Combine(
                    assetsDirectory,
                    FallbackHeartRelativePath.Replace(
                        '/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        "The bundled extra-life heart image was not found.",
                        path);
                var texture = new Texture2D(
                    2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(texture);
                    throw new InvalidOperationException(
                        "Unity could not decode the extra-life heart image.");
                }
                texture.name = "CreatorTools_ExtraLifeHeart_Texture";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    1f);
                sprite.name = "CreatorTools_ExtraLifeHeart_Sprite";
                fallbackHeartTexture = texture;
                fallbackHeartSprite = sprite;
                return true;
            }
            catch (Exception exception)
            {
                fallbackHeartLoadFailed = true;
                error = exception.ToString();
                return false;
            }
        }

        private static SpriteRenderer FindPlayerRenderer(
            AbstractPlayerController player)
        {
            if (player == null)
                return null;
            var ground = player as LevelPlayerController;
            if (ground != null && ground.animationController != null)
                return ground.animationController.GetSpriteRenderer();
            var plane = player as PlanePlayerController;
            if (plane != null && plane.animationController != null)
                return plane.animationController.GetSpriteRenderer();
            return player.GetComponentInChildren<SpriteRenderer>();
        }

        private bool ResolveHeartPrefab()
        {
            if (heartPrefab != null)
                return true;

            var players = UnityEngine.Object
                .FindObjectsOfType<LevelPlayerController>();
            for (var i = 0; i < players.Length && heartPrefab == null; i++)
                TryCaptureHeartPrefab(players[i] == null
                    ? null
                    : players[i].weaponManager);

            if (heartPrefab != null)
                return true;
            var shieldPrefabs = Resources
                .FindObjectsOfTypeAll<PlayerSuperChaliceShield>();
            for (var i = 0;
                 i < shieldPrefabs.Length && heartPrefab == null;
                 i++)
                TryCaptureHeartPrefab(shieldPrefabs[i]);
            return heartPrefab != null;
        }

        private void TryCaptureHeartPrefab(LevelPlayerWeaponManager manager)
        {
            if (manager == null || heartPrefab != null)
                return;
            try
            {
                var prefabs = Traverse.Create(manager)
                    .Field("superPrefabs").GetValue();
                var shield = Traverse.Create(prefabs)
                    .Field("chaliceShield")
                    .GetValue<PlayerSuperChaliceShield>();
                TryCaptureHeartPrefab(shield);
            }
            catch
            {
            }
        }

        private void TryCaptureHeartPrefab(PlayerSuperChaliceShield shield)
        {
            if (shield == null || heartPrefab != null)
                return;
            try
            {
                var native = Traverse.Create(shield);
                heartPrefab = native.Field("shieldHeartPrefab")
                    .GetValue<GameObject>();
                var spawn = native.Field("shieldHeartSpawnPos")
                    .GetValue<Transform>();
                if (heartPrefab != null && spawn != null)
                {
                    heartSpawnOffset = shield.transform
                        .InverseTransformPoint(spawn.position);
                    hasHeartSpawnOffset = true;
                }
                if (heartPrefab != null && logInfo != null)
                    logInfo(
                        "Corazon nativo de Vida extra guardado para " +
                        "niveles terrestres y de avion.");
            }
            catch
            {
                heartPrefab = null;
                hasHeartSpawnOffset = false;
            }
        }

        private Vector3 ResolveHeartSpawnPosition(
            AbstractPlayerController player)
        {
            if (player == null)
                return Vector3.zero;
            // shieldHeartSpawnPos belongs to Ms. Chalice's terrestrial Super
            // animation. Applying that transform to Cuphead, Mugman or an
            // aircraft can place the heart above the viewport. Those players
            // begin at their own visible position and the native orbit takes
            // over immediately.
            if (!hasHeartSpawnOffset || !IsChalice(player))
                return player.transform.position;
            // The native Super II creates the floating heart from this keyed
            // point before its own follow routine carries it into orbit.
            return player.transform.TransformPoint(heartSpawnOffset);
        }

        private static bool IsChalice(AbstractPlayerController player)
        {
            try
            {
                return player != null && player.stats != null &&
                    Traverse.Create(player.stats)
                        .Field("isChalice").GetValue<bool>();
            }
            catch
            {
                return false;
            }
        }

        private void SetHeartSuspended(bool value)
        {
            var shader = ResolveSuspendedShader();
            if (heart != null)
                heart.SetSuspended(value, shader);
        }

        private Shader ResolveSuspendedShader()
        {
            try { return suspendedShader == null ? null : suspendedShader(); }
            catch { return null; }
        }

        private void DestroyHearts()
        {
            if (heart != null)
                UnityEngine.Object.Destroy(heart.gameObject);
            heart = null;
        }

        private void PlayArrival(AbstractPlayerController player)
        {
            var played = false;
            try
            {
                played = playArrivalSound != null && playArrivalSound();
            }
            catch (Exception exception)
            {
                if (logWarning != null)
                    logWarning("Could not play the packaged extra-life " +
                        "arrival: " + exception.Message);
            }
            if (!played)
            {
                try
                {
                    AudioManager.Play("player_super_chalice_shield");
                }
                catch (Exception exception)
                {
                    if (logWarning != null)
                        logWarning("Could not play the extra-life arrival: " +
                            exception.Message);
                }
            }

            try
            {
                var effect = player.gameObject
                    .GetComponent<CreatorToolsExtraLifeArrivalEffect>();
                if (effect != null)
                    UnityEngine.Object.Destroy(effect);
                effect = player.gameObject
                    .AddComponent<CreatorToolsExtraLifeArrivalEffect>();
                effect.Initialize(player);
            }
            catch (Exception exception)
            {
                if (logWarning != null)
                    logWarning("Could not animate the extra-life arrival: " +
                        exception.Message);
            }
        }

        private static AbstractPlayerController PlayerOne()
        {
            try
            {
                return PlayerManager.GetPlayer(PlayerId.PlayerOne);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPlayerOne(PlayerStatsManager stats)
        {
            try
            {
                return stats.basePlayer != null &&
                    stats.basePlayer.id == PlayerId.PlayerOne;
            }
            catch
            {
                return false;
            }
        }

        private static int CurrentAttemptSerial()
        {
            try
            {
                var level = Level.Current;
                return level == null ? 0 : level.GetInstanceID();
            }
            catch
            {
                return 0;
            }
        }

        private static bool Evaluate(Func<bool> predicate)
        {
            if (predicate == null)
                return false;
            try { return predicate(); }
            catch { return false; }
        }

        private sealed class CompletedHandle :
            ICreatorToolsInteractionHandle
        {
            internal static readonly CompletedHandle Instance =
                new CompletedHandle();

            public bool IsComplete
            {
                get { return true; }
            }

            public void Dispose()
            {
            }
        }
    }
}
