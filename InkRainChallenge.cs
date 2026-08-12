using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private InkRainChallengeRuntime inkRainRuntime;
        private int inkRainLevelInstanceId = -1;
        private bool inkRainUpdateHeartbeatLogged;
        private bool inkRainUpdateErrorLogged;
        private bool inkRainBattleSignaled;
        private bool inkRainBattleEnded;
        private float nextInkRainDiagnosticAt;

        private void SafeUpdateInkRainChallenge()
        {
            if (!inkRainUpdateHeartbeatLogged)
            {
                inkRainUpdateHeartbeatLogged = true;
                Logger.LogInfo("Lluvia de tinta: ciclo de actualizacion activo.");
            }

            try
            {
                UpdateInkRainChallenge();
                inkRainUpdateErrorLogged = false;
            }
            catch (Exception exception)
            {
                if (inkRainUpdateErrorLogged)
                    return;

                inkRainUpdateErrorLogged = true;
                Logger.LogError("Error actualizando lluvia de tinta: " +
                                exception);
            }
        }

        private void LogInkRainDiagnostic(string message)
        {
            if (Time.unscaledTime < nextInkRainDiagnosticAt)
                return;

            nextInkRainDiagnosticAt = Time.unscaledTime + 2f;
            Logger.LogWarning(message);
        }

        private void InstallInkRainChallengePatches()
        {
            var levelInit = AccessTools.Method(
                typeof(PlayerStatsManager), "LevelInit");
            var postfix = AccessTools.Method(
                typeof(Plugin), "InkRainLevelInitPostfix");
            if (levelInit == null || postfix == null)
            {
                Logger.LogWarning(
                    "No se pudo instalar el inicio de Lluvia de tinta.");
                return;
            }

            harmony.Patch(levelInit,
                postfix: new HarmonyMethod(postfix));
        }

        private static void InkRainLevelInitPostfix()
        {
            var plugin = activeInstance;
            if (plugin == null ||
                !ExperimentalFeatures.EnableInkRainChallenge)
                return;

            if (plugin.activeChallenge != ModifierId.InkRain)
                return;

            plugin.inkRainBattleEnded = false;
            plugin.inkRainBattleSignaled = true;
            if (plugin.inkRainRuntime == null)
                plugin.InitializeInkRainChallenge();
            plugin.inkRainRuntime.Configure(
                true, plugin.difficulty, true);
            plugin.Logger.LogInfo(
                "Lluvia de tinta activada por LevelInit.");
        }

        private void InitializeInkRainChallenge()
        {
            inkRainRuntime = gameObject.GetComponent<InkRainChallengeRuntime>();
            if (inkRainRuntime == null)
                inkRainRuntime = gameObject.AddComponent<InkRainChallengeRuntime>();
            inkRainRuntime.SetLogger(Logger);
            inkRainRuntime.SetAssetsDirectory(AssetsDirectory);
            inkRainRuntime.Configure(false, difficulty, false);
        }

        private void UpdateInkRainChallenge()
        {
            if (!ExperimentalFeatures.EnableInkRainChallenge)
            {
                if (inkRainRuntime != null)
                    inkRainRuntime.Configure(false, difficulty, false);
                inkRainLevelInstanceId = -1;
                return;
            }

            var activeFight = false;
            var levelInstanceId = -1;
            if (!inkRainBattleEnded &&
                activeChallenge == ModifierId.InkRain &&
                !SceneLoader.CurrentlyLoading)
            {
                Level level = null;
                try
                {
                    level = Level.Current;
                }
                catch (Exception exception)
                {
                    LogInkRainDiagnostic(
                        "Level.Current no disponible para lluvia de tinta: " +
                        exception.Message);
                }

                if (level == null)
                {
                    try
                    {
                        level = FindObjectOfType<Level>();
                    }
                    catch (Exception exception)
                    {
                        LogInkRainDiagnostic(
                            "No se pudo buscar Level para lluvia de tinta: " +
                            exception.Message);
                    }
                }

                if (level != null)
                    inkRainBattleSignaled = false;

                activeFight = level != null &&
                              level.LevelType == Level.Type.Battle &&
                              ActiveChallengeMatches(level);
                if (activeFight)
                    levelInstanceId = level.GetInstanceID();
            }

            if (!activeFight && !inkRainBattleEnded &&
                inkRainBattleSignaled && !SceneLoader.CurrentlyLoading)
            {
                activeFight = true;
                levelInstanceId = -2;
            }

            if (inkRainRuntime == null)
                InitializeInkRainChallenge();

            var newSession = activeFight &&
                             inkRainLevelInstanceId != levelInstanceId;
            if (newSession)
                Logger.LogInfo(
                    "Lluvia de tinta detectÃƒÆ’Ã‚Â³ una batalla activa.");
            inkRainRuntime.Configure(activeFight, difficulty, newSession);
            inkRainLevelInstanceId = activeFight ? levelInstanceId : -1;
        }

        private void ClearInkRainChallengeSession()
        {
            inkRainBattleEnded = true;
            inkRainBattleSignaled = false;
            inkRainLevelInstanceId = -1;
            if (inkRainRuntime != null)
                inkRainRuntime.Configure(false, difficulty, false);
        }

        private void DisposeInkRainChallenge()
        {
            inkRainLevelInstanceId = -1;
            if (inkRainRuntime == null)
                return;

            inkRainRuntime.Configure(false, difficulty, false);
            Destroy(inkRainRuntime);
            inkRainRuntime = null;
        }
    }

    internal sealed class InkRainChallengeRuntime : MonoBehaviour
    {
        private sealed class InkDrop
        {
            internal Vector2 Position;
            internal Vector2 Velocity;
            internal float Gravity;
            internal float Radius;
            internal float Age;
            internal int FrameOffset;
        }

        private sealed class InkGroundImpact
        {
            internal Vector2 Position;
            internal float StartTime;
            internal Sprite[] Frames;
        }

        private sealed class InkSplat
        {
            internal Vector2 DesignPosition;
            internal float StartTime;
            internal float Duration;
            internal Sprite[] Frames;
        }

        private sealed class InkSplatTemplate
        {
            internal readonly Vector2 Position;
            internal readonly bool Large;

            internal InkSplatTemplate(float x, float y, bool large)
            {
                Position = new Vector2(x, y);
                Large = large;
            }
        }

        private const float FirstDropDelay = 1.25f;
        private const float InkStep = 0.4f;
        private const float MaximumInk = 1f;
        private const float InkRiseDuration = 0.4f;
        private const float InkFadeDuration = 5f;
        private const float MaximumDropLifetime = 7f;
        private const float DropFrameRate = 24f;
        private const float GroundImpactFrameRate = 24f;
        private const float SplatFrameRate = 12f;
        private const float SplatDelayStep = 0.025f;
        private const float SplatVisualScaleX = 0.65f;
        private const float SplatVisualScaleY = 0.115f;

        // Exact layouts from the three SplatGroup children of
        // Pirate_Ink_Overlay in the original pirate level.
        private static readonly InkSplatTemplate[][] SplatGroups =
        {
            new[]
            {
                S(-129f, 34f), S(136f, 135f), S(287f, -19f),
                L(462f, 248f), L(-240f, -149f), L(-89f, 239f),
                L(-390f, 103f), L(55f, -10f), S(114f, -194f),
                S(515f, 14f), S(199f, 280f), S(-407f, 269f),
                S(-481f, -175f), S(369f, -212f)
            },
            new[]
            {
                S(163f, -161f), S(311f, 141f), L(-343f, -210f),
                L(-84f, 69f), L(465f, 229f), S(512f, -293f),
                S(423f, -70f), S(-352f, 224f), L(106f, 206f),
                L(193f, -10f), L(-476f, 20f), S(-84f, -141f),
                S(-6f, -310f)
            },
            new[]
            {
                S(449f, 257f), L(-524f, 158f), L(508f, 43f),
                L(68f, 46f), S(233f, -211f), L(-351f, 297f),
                S(460f, -174f), S(527f, -298f), L(-372f, -209f),
                L(-229f, 140f), L(235f, 237f), L(-157f, -175f),
                S(276f, 16f), S(8f, 224f), S(49f, -116f),
                S(-445f, -66f)
            }
        };

        private static InkSplatTemplate L(float x, float y)
        {
            return new InkSplatTemplate(x, y, true);
        }

        private static InkSplatTemplate S(float x, float y)
        {
            return new InkSplatTemplate(x, y, false);
        }

        private readonly List<InkDrop> drops = new List<InkDrop>();
        private readonly List<InkGroundImpact> groundImpacts =
            new List<InkGroundImpact>();
        private readonly List<InkSplat> splats = new List<InkSplat>();
        private readonly List<Sprite[]> groundImpactAnimations =
            new List<Sprite[]>();
        private readonly List<Sprite[]> inkScreenAnimations =
            new List<Sprite[]>();
        private AbstractPlayerController[] players =
            new AbstractPlayerController[0];

        private bool challengeActive;
        private bool inkAssetsAttempted;
        private float nextInkAssetRetryAt;
        private Level.Mode difficulty = Level.Mode.Normal;
        private float nextSpawnAt;
        private float nextPlayerScanAt;
        private float inkAlpha;
        private float targetInkAlpha;
        private float holdRemaining;
        private Camera gameplayCamera;
        private string assetsDirectory;
        private readonly List<Texture2D> ownedInkTextures =
            new List<Texture2D>();
        private Sprite[] inkDropFrames;
        private Sprite inkScreenOverlay;
        private ManualLogSource log;
        private bool loggedFirstDrop;

        internal void SetLogger(ManualLogSource value)
        {
            log = value;
        }

        internal void SetAssetsDirectory(string value)
        {
            assetsDirectory = value;
        }

        internal void Configure(bool active, Level.Mode mode, bool newSession)
        {
            difficulty = mode;
            if (newSession)
            {
                ResetState();
                challengeActive = true;
                nextSpawnAt = Time.time + FirstDropDelay;
                EnsureInkAssets();
                return;
            }

            if (challengeActive == active)
                return;

            challengeActive = active;
            if (challengeActive)
            {
                nextSpawnAt = Time.time + FirstDropDelay;
                EnsureInkAssets();
            }
            else
            {
                ResetState();
            }
        }

        private void Update()
        {
            if (!challengeActive)
                return;

            if (SceneLoader.CurrentlyLoading)
            {
                ResetState();
                return;
            }

            if (!EnsureInkAssets())
                return;

            var delta = Time.deltaTime;
            if (delta <= 0f)
                return;

            gameplayCamera = FindGameplayCamera();
            if (gameplayCamera == null)
                return;

            UpdateInk(delta);
            UpdatePlayers();
            UpdateDrops(delta);

            if (Time.time >= nextSpawnAt &&
                drops.Count < MaximumVisibleDrops())
            {
                SpawnWave();
                nextSpawnAt = Time.time + NextSpawnDelay();
            }
        }

        private void UpdateInk(float delta)
        {
            if (holdRemaining > 0f)
                holdRemaining = Mathf.Max(0f, holdRemaining - delta);
            else
                targetInkAlpha = Mathf.MoveTowards(
                    targetInkAlpha, 0f, delta / InkFadeDuration);

            var riseSpeed = MaximumInk / InkRiseDuration;
            var fadeSpeed = MaximumInk / InkFadeDuration;
            inkAlpha = Mathf.MoveTowards(
                inkAlpha,
                targetInkAlpha,
                (targetInkAlpha > inkAlpha ? riseSpeed : fadeSpeed) * delta);

            if (inkAlpha <= 0.001f && targetInkAlpha <= 0.001f)
                splats.Clear();

            for (var i = splats.Count - 1; i >= 0; i--)
            {
                var splat = splats[i];
                if (Time.time >= splat.StartTime + splat.Duration)
                    splats.RemoveAt(i);
            }
        }

        private void UpdatePlayers()
        {
            if (Time.time < nextPlayerScanAt)
                return;

            nextPlayerScanAt = Time.time + 0.15f;
            try
            {
                players = FindObjectsOfType<AbstractPlayerController>();
            }
            catch
            {
                players = new AbstractPlayerController[0];
            }
        }

        private void UpdateDrops(float delta)
        {
            for (var i = groundImpacts.Count - 1; i >= 0; i--)
            {
                var impact = groundImpacts[i];
                var duration = impact.Frames == null
                    ? 0f
                    : impact.Frames.Length / GroundImpactFrameRate;
                if (Time.time >= impact.StartTime + duration)
                    groundImpacts.RemoveAt(i);
            }

            for (var i = drops.Count - 1; i >= 0; i--)
            {
                var drop = drops[i];
                drop.Age += delta;
                var previousPosition = drop.Position;
                drop.Position += drop.Velocity * delta;
                drop.Velocity.y -= drop.Gravity * delta;

                if (TouchesPlayer(drop))
                {
                    RegisterInkHit(drop.Position);
                    drops.RemoveAt(i);
                    continue;
                }

                Vector2 groundImpact;
                if (TryFindGroundImpact(previousPosition, drop.Position,
                        out groundImpact))
                {
                    SpawnGroundImpact(groundImpact);
                    drops.RemoveAt(i);
                    continue;
                }

                var viewport = gameplayCamera.WorldToViewportPoint(
                    new Vector3(drop.Position.x, drop.Position.y, 0f));
                if (drop.Age >= MaximumDropLifetime ||
                    viewport.y < -0.15f ||
                    viewport.x < -0.2f || viewport.x > 1.2f)
                    drops.RemoveAt(i);
            }
        }

        private bool TouchesPlayer(InkDrop drop)
        {
            var cameraHeight = Mathf.Max(1f,
                gameplayCamera.orthographicSize * 2f);
            var playerRadius = cameraHeight * 0.038f;
            var hitDistance = drop.Radius + playerRadius;
            var hitDistanceSquared = hitDistance * hitDistance;

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.isActiveAndEnabled ||
                    !player.gameObject.activeInHierarchy)
                    continue;

                var offset = (Vector2)player.transform.position -
                             drop.Position;
                if (offset.sqrMagnitude <= hitDistanceSquared)
                    return true;
            }
            return false;
        }

        private static bool TryFindGroundImpact(
            Vector2 from, Vector2 to, out Vector2 point)
        {
            point = to;
            var hits = Physics2D.LinecastAll(from, to);
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || !collider.enabled ||
                    collider.isTrigger || collider.name != "Level_Ground")
                    continue;

                point = hits[i].point;
                return true;
            }
            return false;
        }

        private void SpawnGroundImpact(Vector2 position)
        {
            if (groundImpactAnimations.Count == 0)
                return;

            var frames = groundImpactAnimations[UnityEngine.Random.Range(
                0, groundImpactAnimations.Count)];
            if (frames == null || frames.Length == 0)
                return;

            groundImpacts.Add(new InkGroundImpact
            {
                Position = position,
                StartTime = Time.time,
                Frames = frames
            });
        }

        private void RegisterInkHit(Vector2 worldPosition)
        {
            targetInkAlpha = Mathf.Min(
                MaximumInk, Mathf.Max(targetInkAlpha, inkAlpha) + InkStep);
            holdRemaining = InkHoldDurationForDifficulty();

            SpawnNativeSplatGroup();

            try
            {
                AudioManager.Play("level_pirate_squid_blackout_screen");
            }
            catch
            {
            }

            try
            {
                CupheadLevelCamera.Current.Shake(4f, 0.3f, false);
            }
            catch
            {
            }
        }

        private void SpawnNativeSplatGroup()
        {
            if (inkScreenAnimations.Count < 5)
                return;

            splats.Clear();
            var group = SplatGroups[UnityEngine.Random.Range(
                0, SplatGroups.Length)];
            for (var i = 0; i < group.Length; i++)
            {
                var template = group[i];
                var animationIndex = template.Large
                    ? UnityEngine.Random.Range(0, 3)
                    : UnityEngine.Random.Range(3, 5);
                var frames = inkScreenAnimations[animationIndex];
                if (frames == null || frames.Length == 0)
                    continue;

                splats.Add(new InkSplat
                {
                    DesignPosition = template.Position,
                    StartTime = Time.time +
                                UnityEngine.Random.Range(0, 10) *
                                SplatDelayStep,
                    Duration = NativeSplatDuration(animationIndex),
                    Frames = frames
                });
            }
        }

        private static float NativeSplatDuration(int animationIndex)
        {
            switch (animationIndex)
            {
                case 0: return 2f / 3f;
                case 1: return 5f / 6f;
                case 2: return 2f / 3f;
                case 3: return 3f / 8f;
                default: return 5f / 12f;
            }
        }

        private float InkHoldDurationForDifficulty()
        {
            if (difficulty == Level.Mode.Easy)
                return 2.8f;
            if (difficulty == Level.Mode.Hard)
                return 4f;
            return 3.3f;
        }

        private void SpawnWave()
        {
            var capacity = MaximumVisibleDrops() - drops.Count;
            if (capacity <= 0)
                return;

            var count = 1;
            if (difficulty == Level.Mode.Normal &&
                UnityEngine.Random.value < 0.18f)
                count = 2;
            else if (difficulty == Level.Mode.Hard &&
                     UnityEngine.Random.value < 0.36f)
                count = 2;

            count = Mathf.Min(count, capacity);
            for (var i = 0; i < count; i++)
                SpawnDrop(i, count);
        }

        private void SpawnDrop(int waveIndex, int waveCount)
        {
            // Native-style arc: enter from the upper-right, drift left and
            // accelerate downward instead of falling in a vertical line.
            var x = UnityEngine.Random.Range(0.22f, 1.08f);
            if (waveCount > 1)
            {
                var sectionWidth = 0.84f / waveCount;
                x = 0.20f + sectionWidth *
                    (waveIndex + UnityEngine.Random.Range(0.2f, 0.8f));
            }

            var cameraDepth = Mathf.Abs(
                gameplayCamera.transform.position.z);
            var start = gameplayCamera.ViewportToWorldPoint(
                new Vector3(x, 1.12f, cameraDepth));
            var cameraHeight = Mathf.Max(1f,
                gameplayCamera.orthographicSize * 2f);
            var horizontalSpeed =
                UnityEngine.Random.Range(-0.20f, -0.14f) * cameraHeight;
            var fallSpeed = UnityEngine.Random.Range(0.15f, 0.22f) *
                            cameraHeight;
            var gravity = UnityEngine.Random.Range(0.15f, 0.21f) *
                          cameraHeight;

            drops.Add(new InkDrop
            {
                Position = new Vector2(start.x, start.y),
                Velocity = new Vector2(horizontalSpeed, -fallSpeed),
                Gravity = gravity,
                Radius = cameraHeight * UnityEngine.Random.Range(
                    0.016f, 0.021f),
                FrameOffset = UnityEngine.Random.Range(
                    0, inkDropFrames.Length)
            });
            if (!loggedFirstDrop && log != null)
            {
                loggedFirstDrop = true;
                log.LogInfo(
                    "Primera gota de tinta creada y en pantalla.");
            }
        }

        private int MaximumVisibleDrops()
        {
            if (difficulty == Level.Mode.Easy)
                return 2;
            if (difficulty == Level.Mode.Hard)
                return 4;
            return 3;
        }

        private float NextSpawnDelay()
        {
            if (difficulty == Level.Mode.Easy)
                return UnityEngine.Random.Range(1.35f, 1.65f);
            if (difficulty == Level.Mode.Hard)
                return UnityEngine.Random.Range(0.65f, 0.95f);
            return UnityEngine.Random.Range(0.95f, 1.25f);
        }

        private Camera FindGameplayCamera()
        {
            if (gameplayCamera != null && gameplayCamera.enabled &&
                gameplayCamera.gameObject.activeInHierarchy)
                return gameplayCamera;

            var main = Camera.main;
            if (main != null && main.enabled)
                return main;

            var cameras = FindObjectsOfType<Camera>();
            for (var i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled &&
                    cameras[i].orthographic)
                    return cameras[i];
            }
            return cameras.Length > 0 ? cameras[0] : null;
        }

        private bool EnsureInkAssets()
        {
            if (inkDropFrames != null && inkDropFrames.Length > 0)
                return true;
            if (inkAssetsAttempted &&
                Time.realtimeSinceStartup < nextInkAssetRetryAt)
                return false;

            inkAssetsAttempted = true;
            nextInkAssetRetryAt = Time.realtimeSinceStartup + 1.5f;
            try
            {
                var inkRoot = Path.Combine(assetsDirectory, "inkrain");
                var projectileDirectory = Path.Combine(
                    inkRoot, "projectiles");
                var screenDirectory = Path.Combine(inkRoot, "screen");
                inkDropFrames = LoadSpriteSequence(
                    projectileDirectory,
                    "pirate_squid_inkblob_*.png");

                groundImpactAnimations.Clear();
                var impactDirectory = Path.Combine(inkRoot, "impacts");
                var impactGroups = new[] { "a", "b", "c", "d" };
                for (var i = 0; i < impactGroups.Length; i++)
                {
                    var frames = LoadSpriteSequence(
                        impactDirectory,
                        "pirate_squid_ink_death_" + impactGroups[i] +
                        "_*.png");
                    if (frames.Length > 0)
                        groundImpactAnimations.Add(frames);
                }

                var overlayFrames = LoadSpriteSequence(
                    screenDirectory,
                    "pirate_squid_ink_screen_0001.png");
                inkScreenOverlay = overlayFrames.Length > 0
                    ? overlayFrames[0]
                    : null;

                inkScreenAnimations.Clear();
                var groups = new[] { "a", "b", "c", "d", "e" };
                for (var i = 0; i < groups.Length; i++)
                {
                    var frames = LoadSpriteSequence(
                        screenDirectory,
                        "pirate_squid_ink_screen_" + groups[i] + "_*.png");
                    if (frames.Length > 0)
                        inkScreenAnimations.Add(frames);
                }

                if (inkDropFrames.Length == 0)
                {
                    if (log != null)
                        log.LogWarning(
                            "No se encontraron los PNG originales de tinta en " +
                            projectileDirectory);
                    inkDropFrames = null;
                    return false;
                }

                if (log != null)
                    log.LogInfo(
                        "Lluvia de tinta lista desde PNG con " +
                        inkDropFrames.Length +
                        " frames originales, " +
                        groundImpactAnimations.Count +
                        " impactos de suelo y " +
                        inkScreenAnimations.Count +
                        " grupos de manchas.");
                return true;
            }
            catch (Exception ex)
            {
                if (log != null)
                    log.LogWarning(
                        "Error cargando PNG originales de tinta: " +
                        ex.GetType().Name + ": " + ex.Message);
                inkDropFrames = null;
                return false;
            }
        }

        private Sprite[] LoadSpriteSequence(
            string directory, string searchPattern)
        {
            if (string.IsNullOrEmpty(directory) ||
                !Directory.Exists(directory))
                return new Sprite[0];

            var files = Directory.GetFiles(directory, searchPattern);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var frames = new List<Sprite>();
            for (var i = 0; i < files.Length; i++)
            {
                var bytes = File.ReadAllBytes(files[i]);
                var texture = new Texture2D(
                    2, 2, TextureFormat.ARGB32, false);
                texture.name = Path.GetFileNameWithoutExtension(files[i]);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                if (!texture.LoadImage(bytes))
                {
                    Destroy(texture);
                    continue;
                }

                ownedInkTextures.Add(texture);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = texture.name;
                frames.Add(sprite);
            }
            return frames.ToArray();
        }

        private static int CompareSpriteNames(Sprite left, Sprite right)
        {
            return string.CompareOrdinal(
                left != null ? left.name : string.Empty,
                right != null ? right.name : string.Empty);
        }

        private void OnGUI()
        {
            if (SceneLoader.CurrentlyLoading)
                return;

            if (!challengeActive || gameplayCamera == null ||
                inkDropFrames == null || inkDropFrames.Length == 0 ||
                Event.current.type != EventType.Repaint)
                return;

            var previousDepth = GUI.depth;
            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            GUI.depth = 40;
            GUI.matrix = Matrix4x4.identity;

            for (var i = 0; i < drops.Count; i++)
            {
                var drop = drops[i];
                var center = gameplayCamera.WorldToScreenPoint(
                    new Vector3(drop.Position.x, drop.Position.y, 0f));
                if (center.z < 0f)
                    continue;

                var edge = gameplayCamera.WorldToScreenPoint(
                    new Vector3(
                        drop.Position.x + drop.Radius,
                        drop.Position.y, 0f));
                var radiusPixels = Mathf.Max(
                    7f, Mathf.Abs(edge.x - center.x));
                var frameIndex = (Mathf.FloorToInt(
                    drop.Age * DropFrameRate) + drop.FrameOffset) %
                    inkDropFrames.Length;
                var sprite = inkDropFrames[frameIndex];
                var width = radiusPixels * 2f;
                var height = width / SpriteAspect(sprite);
                var rect = new Rect(
                    center.x - width * 0.5f,
                    Screen.height - center.y - height * 0.5f,
                    width,
                    height);
                DrawSprite(rect, sprite, Color.white);
            }

            for (var i = 0; i < groundImpacts.Count; i++)
            {
                var impact = groundImpacts[i];
                if (impact.Frames == null || impact.Frames.Length == 0)
                    continue;

                var elapsed = Time.time - impact.StartTime;
                var frameIndex = Mathf.FloorToInt(
                    elapsed * GroundImpactFrameRate);
                if (frameIndex < 0 || frameIndex >= impact.Frames.Length)
                    continue;

                var sprite = impact.Frames[frameIndex];
                var center = gameplayCamera.WorldToScreenPoint(
                    new Vector3(impact.Position.x, impact.Position.y, 0f));
                if (center.z < 0f)
                    continue;

                var scale = Screen.height / 720f;
                var width = sprite.rect.width * scale;
                var height = sprite.rect.height * scale;
                DrawSprite(
                    new Rect(
                        center.x - width * 0.5f,
                        Screen.height - center.y - height,
                        width, height),
                    sprite,
                    Color.white);
            }

            if (inkAlpha > 0.001f)
            {
                var scale = Mathf.Min(
                    Screen.width / 1280f, Screen.height / 720f);
                for (var i = 0; i < splats.Count; i++)
                {
                    var splat = splats[i];
                    if (splat.Frames == null || splat.Frames.Length == 0)
                        continue;

                    var elapsed = Time.time - splat.StartTime;
                    if (elapsed < 0f || elapsed >= splat.Duration)
                        continue;

                    var frameIndex = Mathf.Min(
                        splat.Frames.Length - 1,
                        Mathf.FloorToInt(elapsed * SplatFrameRate));
                    var sprite = splat.Frames[frameIndex];
                    var width = sprite.rect.width * scale * SplatVisualScaleX;
                    var height = sprite.rect.height * scale * SplatVisualScaleY;
                    var x = Screen.width * 0.5f +
                            splat.DesignPosition.x * scale -
                            width * 0.5f;
                    var y = Screen.height * 0.5f -
                            splat.DesignPosition.y * scale -
                            height * 0.5f;
                    DrawSprite(
                        new Rect(x, y, width, height),
                        sprite,
                        Color.white);
                }

                // The native full-screen ink veil sits in front of the
                // individual splats. Drawing it last keeps their translucent
                // fringe from glowing over an already-darkened scene.
                if (inkScreenOverlay != null)
                {
                    DrawSprite(
                        new Rect(0f, 0f, Screen.width, Screen.height),
                        inkScreenOverlay,
                        new Color(1f, 1f, 1f,
                            Mathf.Clamp01(inkAlpha)));
                }
            }



            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private static float SpriteAspect(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height <= 0f)
                return 1f;
            return sprite.rect.width / sprite.rect.height;
        }

        private static void DrawSprite(
            Rect destination, Sprite sprite, Color color)
        {
            if (sprite == null || sprite.texture == null)
                return;

            var texture = sprite.texture;
            var textureRect = sprite.textureRect;
            var uv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
        }

        private void ResetState()
        {
            drops.Clear();
            groundImpacts.Clear();
            splats.Clear();
            players = new AbstractPlayerController[0];
            gameplayCamera = null;
            inkAlpha = 0f;
            targetInkAlpha = 0f;
            holdRemaining = 0f;
            nextPlayerScanAt = 0f;
        }

        private void OnDestroy()
        {
            ResetState();
            inkDropFrames = null;
            inkScreenOverlay = null;
            groundImpactAnimations.Clear();
            inkScreenAnimations.Clear();
            for (var i = 0; i < ownedInkTextures.Count; i++)
            {
                if (ownedInkTextures[i] != null)
                    Destroy(ownedInkTextures[i]);
            }
            ownedInkTextures.Clear();
        }
    }
}
