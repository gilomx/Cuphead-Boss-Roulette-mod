using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private InkRainChallengeRuntime inkRainRuntime;
        private int inkRainLevelInstanceId = -1;
        private bool inkRainLevelInitSessionConfigured;
        private bool inkRainSquidIntroStartedThisSession;
        private bool inkRainUpdateHeartbeatLogged;
        private bool inkRainUpdateErrorLogged;
        private bool inkRainBattleSignaled;
        private bool inkRainBattleEnded;
        private bool inkRainHasConfiguredLevel;
        private Levels inkRainConfiguredLevel;
        private bool inkRainDicePalaceIntroShown;
        private float nextInkRainDiagnosticAt;

        private void SafeUpdateInkRainChallenge()
        {
            if (!ExperimentalFeatures.EnableInkRainChallenge)
                return;

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
            if (!ExperimentalFeatures.EnableInkRainChallenge)
                return;

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

            var transitionComplete = AccessTools.Method(
                typeof(Level), "_OnTransitionInComplete");
            var transitionPostfix = AccessTools.Method(
                typeof(Plugin),
                "InkRainTransitionInCompletePostfix");
            if (transitionComplete != null && transitionPostfix != null)
                harmony.Patch(transitionComplete,
                    postfix: new HarmonyMethod(transitionPostfix));
            else
                Logger.LogWarning(
                    "No se pudo iniciar la animacion del pulpo de tinta.");

            var announcerBegin = AccessTools.Method(
                typeof(Level), "PlayAnnouncerBegin");
            var announcerBeginPostfix = AccessTools.Method(
                typeof(Plugin), "InkRainAnnouncerBeginPostfix");
            if (announcerBegin != null && announcerBeginPostfix != null)
                harmony.Patch(announcerBegin,
                    postfix: new HarmonyMethod(announcerBeginPostfix));
            else
                Logger.LogWarning(
                    "No se pudo sincronizar la gracia de tinta con Wallop.");

            var levelStarted = AccessTools.Method(
                typeof(Level), "_OnLevelStart");
            var levelStartedPostfix = AccessTools.Method(
                typeof(Plugin), "InkRainLevelStartedPostfix");
            if (levelStarted != null && levelStartedPostfix != null)
                harmony.Patch(levelStarted,
                    postfix: new HarmonyMethod(levelStartedPostfix));
            else
                Logger.LogWarning(
                    "No se pudo instalar el respaldo de gracia de tinta.");
        }

        private static void InkRainTransitionInCompletePostfix(
            Level __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                !ExperimentalFeatures.EnableInkRainChallenge ||
                plugin.activeChallenge != ModifierId.InkRain ||
                !plugin.ActiveChallengeMatches(__instance))
                return;

            if (plugin.inkRainRuntime == null)
                plugin.InitializeInkRainChallenge();
            plugin.BeginInkRainSquidIntroOnce();
        }

        private void BeginInkRainSquidIntroOnce()
        {
            if (inkRainRuntime == null ||
                inkRainSquidIntroStartedThisSession)
                return;

            if (inkRainRuntime.BeginSquidIntro())
                inkRainSquidIntroStartedThisSession = true;
        }

        private static void InkRainAnnouncerBeginPostfix(Level __instance)
        {
            var plugin = activeInstance;
            if (!InkRainPatchMatches(plugin, __instance))
                return;

            if (plugin.inkRainRuntime == null)
                plugin.InitializeInkRainChallenge();
            plugin.inkRainRuntime.BeginInkEffectGracePeriod(false);
        }

        private static void InkRainLevelStartedPostfix(Level __instance)
        {
            var plugin = activeInstance;
            if (!InkRainPatchMatches(plugin, __instance))
                return;

            if (plugin.inkRainRuntime == null)
                plugin.InitializeInkRainChallenge();
            plugin.inkRainRuntime.BeginInkEffectGracePeriod(true);
        }

        private static bool InkRainPatchMatches(
            Plugin plugin, Level level)
        {
            return plugin != null && level != null &&
                   ExperimentalFeatures.EnableInkRainChallenge &&
                   plugin.activeChallenge == ModifierId.InkRain &&
                   plugin.ActiveChallengeMatches(level);
        }

        private static void InkRainLevelInitPostfix()
        {
            var plugin = activeInstance;
            if (plugin == null ||
                !ExperimentalFeatures.EnableInkRainChallenge ||
                plugin.activeChallenge != ModifierId.InkRain)
                return;

            plugin.inkRainBattleEnded = false;
            plugin.inkRainBattleSignaled = true;
            var levelInstanceId = -1;
            var currentLevel = default(Levels);
            var hasCurrentLevel = false;
            try
            {
                var level = Level.Current;
                if (level != null)
                {
                    levelInstanceId = level.GetInstanceID();
                    currentLevel = level.CurrentLevel;
                    hasCurrentLevel = true;
                }
            }
            catch
            {
                levelInstanceId = -1;
            }

            var newAttempt = !plugin.inkRainLevelInitSessionConfigured ||
                (hasCurrentLevel &&
                 (!plugin.inkRainHasConfiguredLevel ||
                  plugin.inkRainConfiguredLevel != currentLevel));
            plugin.inkRainLevelInstanceId = levelInstanceId;
            if (!newAttempt)
                return;

            plugin.inkRainLevelInitSessionConfigured = true;
            plugin.inkRainHasConfiguredLevel = hasCurrentLevel;
            plugin.inkRainConfiguredLevel = currentLevel;
            var dicePalaceChain = plugin.IsActiveDicePalaceChallenge() &&
                                  hasCurrentLevel &&
                                  IsDicePalaceLevel(currentLevel);
            var showSquidIntro = !dicePalaceChain ||
                                 !plugin.inkRainDicePalaceIntroShown;
            if (dicePalaceChain && showSquidIntro)
                plugin.inkRainDicePalaceIntroShown = true;
            plugin.inkRainSquidIntroStartedThisSession = !showSquidIntro;

            if (plugin.inkRainRuntime == null)
                plugin.InitializeInkRainChallenge();
            var pirateChallenge =
                hasCurrentLevel && currentLevel == Levels.Pirate;
            if (!pirateChallenge &&
                plugin.activeChallengeBoss >= 0 &&
                plugin.activeChallengeBoss < RouletteData.Bosses.Length)
            {
                pirateChallenge =
                    RouletteData.Bosses[plugin.activeChallengeBoss].Level ==
                    Levels.Pirate;
            }
            plugin.inkRainRuntime.StartAttempt(
                plugin.difficulty, showSquidIntro, pirateChallenge);
            if (showSquidIntro)
                plugin.BeginInkRainSquidIntroOnce();
            plugin.Logger.LogInfo(showSquidIntro
                ? "Lluvia de tinta: intento iniciado con pulpo."
                : "Lluvia de tinta: subnivel de Rey Dado iniciado sin pulpo.");
        }
        private void InitializeInkRainChallenge()
        {
            if (!ExperimentalFeatures.EnableInkRainChallenge)
                return;

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
                inkRainLevelInitSessionConfigured = false;
                inkRainSquidIntroStartedThisSession = false;
                return;
            }

            // A final K.O. clears activeChallenge immediately, but the native
            // battle transition still needs the current rain and darkness.
            // Keep them only while that transition is loading; once the next
            // scene is revealed, remove the compositor outside the player's
            // view. Dice Palace minion K.O.s keep activeChallenge and do not
            // enter this branch.
            if (activeChallenge != ModifierId.InkRain &&
                inkRainRuntime != null)
            {
                if (inkRainRuntime.ShouldEndKnockoutHold())
                    inkRainRuntime.EndImmediately();
                else if (!inkRainRuntime.IsHoldingThroughKnockout)
                    inkRainRuntime.EndImmediately();
                return;
            }
            // LevelInit already configured the new Ink Rain session. Preserve
            // it while Cuphead reveals the newly loaded scene so the squid can
            // begin beneath that fade instead of being reset every frame.
            if (SceneLoader.CurrentlyLoading &&
                inkRainBattleSignaled &&
                activeChallenge == ModifierId.InkRain &&
                inkRainRuntime != null)
                return;

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

            // A Dice Palace minion has ended, but the roulette session has
            // not. Let its two-second fade finish without disabling or
            // restarting the runtime before the next internal LevelInit.
            if (inkRainBattleEnded && IsActiveDicePalaceChallenge())
                return;

            var newSession = activeFight &&
                             !inkRainLevelInitSessionConfigured;
            if (newSession)
            {
                inkRainLevelInitSessionConfigured = true;
                inkRainSquidIntroStartedThisSession = false;
            }
            if (newSession)
                Logger.LogInfo(
                    "Lluvia de tinta detectÃƒÆ’Ã‚Â³ una batalla activa.");
            inkRainRuntime.Configure(activeFight, difficulty, newSession);
            inkRainLevelInstanceId = activeFight ? levelInstanceId : -1;
        }

        private void BeginInkRainDicePalaceSublevelWinFade()
        {
            if (activeChallenge != ModifierId.InkRain ||
                !IsActiveDicePalaceChallenge())
                return;

            if (inkRainRuntime != null)
                inkRainRuntime.BeginKnockoutHold();
            Logger.LogInfo(
                "Lluvia de tinta: K.O. interno de Rey Dado; " +
                "el efecto continua hasta la transición.");
        }
        private void ClearInkRainChallengeSession()
        {
            inkRainBattleEnded = true;
            inkRainBattleSignaled = false;
            inkRainLevelInstanceId = -1;
            inkRainLevelInitSessionConfigured = false;
            inkRainSquidIntroStartedThisSession = false;
            inkRainHasConfiguredLevel = false;
            inkRainConfiguredLevel = default(Levels);
            inkRainDicePalaceIntroShown = false;
            if (inkRainRuntime != null)
                inkRainRuntime.EndImmediately();
        }

        private void ResetInkRainChallengeForRetry()
        {
            if (activeChallenge != ModifierId.InkRain)
                return;

            inkRainBattleEnded = false;
            inkRainBattleSignaled = false;
            inkRainLevelInstanceId = -1;
            inkRainLevelInitSessionConfigured = false;
            inkRainSquidIntroStartedThisSession = false;
            // Retry/Restart starts a fresh attempt of the current Dice Palace
            // sublevel, so replay the squid. Normal transitions between
            // minions never call this reset and still suppress repeat intros.
            inkRainDicePalaceIntroShown = false;
            if (inkRainRuntime != null)
                inkRainRuntime.Configure(false, difficulty, false);
            Logger.LogInfo(
                "Lluvia de tinta preparada para un nuevo intento.");
        }

        private void DisposeInkRainChallenge()
        {
            inkRainLevelInstanceId = -1;
            inkRainLevelInitSessionConfigured = false;
            inkRainSquidIntroStartedThisSession = false;
            inkRainHasConfiguredLevel = false;
            inkRainConfiguredLevel = default(Levels);
            inkRainDicePalaceIntroShown = false;
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
            internal bool RemoveBelowWorldEdge;
            internal float WorldExitY;
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
            internal Vector2[] PivotPixels;
            internal bool MirrorX;
            internal GameObject Actor;
            internal SpriteRenderer Renderer;
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
        private const float InkFadeDuration = 3f;
        private const float MaximumDropLifetime = 7f;
        private const float DropFrameRate = 24f;
        private const float GroundImpactFrameRate = 24f;
        private const float GroundImpactVisualScale = 0.6f;
        private const float SplatFrameRate = 12f;
        private const float SplatDelayStep = 0.025f;
        private const float SquidFrameRate = 24f;
        private const float SquidVisualScale = 1.10f;
        private const float SquidAnchorViewportY = -0.04f;
        private const float SquidInkOriginX = 46f;
        private const float SquidInkOriginY = 368f;
        // The complete native sequence runs alongside Cuphead's untouched
        // Ready/Wallop timing. Never compress it to fit the one-second
        // pre-announcer window: every drawing stays at its original 24 fps.
        private const float SquidEntranceDuration = 18f / SquidFrameRate;
        private const float SquidAttackOpenDuration = 3f / SquidFrameRate;
        private const float SquidAttackLoopDuration = 22f / SquidFrameRate;
        private const float SquidExitDuration = 29f / SquidFrameRate;
        private const float SquidIntroStartDelay = 1f;
        // The native enter clip invokes OnEnterAnimationComplete on frame 17,
        // two frames before its visual transition into the attack clip. That
        // callback starts the attack loop and creates the first blob at once.
        private const float SquidAttackEventTime = 16f / SquidFrameRate;
        private const float SquidEasyBlobDelay = 0.21f;
        private const float SquidNormalHardBlobDelay = 0.12f;
        private const float SquidNativeBobDistance = 20f;
        private const int SquidRainMaximumVisibleDrops = 20;
        private const float InkEffectGraceAfterAnnouncer = 1f;

        // anim_level_pirate_squid_attack_loop does more than swap the 16
        // drawings: its streamed clip animates the child named InkOrigin
        // (path CRC 2960652783 == CRC32("InkOrigin")). Each Vector4 is the
        // native cubic polynomial a*t^3 + b*t^2 + c*t + d for one 1/24 s
        // segment. Keeping the compressed curve here makes every new blob
        // follow the bottle/nozzle exactly as it does in PirateLevelSquid.
        private static readonly Vector4[] SquidInkOriginCurveX =
        {
            new Vector4(0f, 0f, -576f, -263f),
            new Vector4(0f, 0f, -192f, -287f),
            new Vector4(-0.00659179781f, 0.000274658232f,
                -72.0000076f, -295f),
            new Vector4(0f, 0f, 311.999969f, -298f),
            new Vector4(0f, 0f, 600.000122f, -285f),
            new Vector4(0f, 0f, 1103.99988f, -260f),
            new Vector4(0f, 0f, 1080.00024f, -214f),
            new Vector4(0f, 0f, 1199.99939f, -169f),
            new Vector4(0f, 0f, 480.000122f, -119f),
            new Vector4(0f, 0f, 360.000092f, -99f),
            new Vector4(0f, 0f, 239.999893f, -84f),
            new Vector4(0f, 0f, 48.0000114f, -74f),
            new Vector4(0f, 0f, -935.999573f, -72f),
            new Vector4(0f, 0f, -936.000916f, -111f),
            new Vector4(0f, 0f, -1367.99939f, -150f),
            new Vector4(0f, 0f, 0f, -207f)
        };

        private static readonly Vector4[] SquidInkOriginCurveY =
        {
            new Vector4(0f, 0f, -456f, 417f),
            new Vector4(0f, 0f, -600f, 398f),
            new Vector4(-0.0263671912f, 0.00109863293f,
                -312.000031f, 373f),
            new Vector4(0f, 0f, -167.999985f, 360f),
            new Vector4(0f, 0f, 1176.00024f, 353f),
            new Vector4(0.0527343564f, -0.00219726516f,
                743.999939f, 402f),
            new Vector4(0f, 0f, 288.000061f, 433f),
            new Vector4(0f, 0f, -527.999756f, 445f),
            new Vector4(0f, 0f, -120.000031f, 423f),
            new Vector4(0f, 0f, -600.000122f, 418f),
            new Vector4(0f, 0f, -479.999786f, 393f),
            new Vector4(0f, 0f, -240.000061f, 373f),
            new Vector4(0f, 0f, 959.999573f, 363f),
            new Vector4(0f, 0f, 456.000427f, 403f),
            new Vector4(-0.0263671502f, 0.00109863176f,
                359.999817f, 422f),
            new Vector4(0f, 0f, 0f, 437f)
        };

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
        private readonly List<Vector2[]> inkScreenPivotPixels =
            new List<Vector2[]>();
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
        private Sprite[] squidEntranceFrames;
        private Sprite[] squidAttackFrames;
        private Sprite[] squidAttackLoopFrames;
        private Sprite[] squidLeaveFrames;
        private Sprite[] squidExitFrames;
        private ManualLogSource log;
        private bool loggedFirstDrop;
        private bool loggedFirstGroundImpact;
        private float nextGroundProbeLogAt;
        private InkRainPreFilmRenderer preFilmInkRenderer;
        private GameObject squidActor;
        private SpriteRenderer squidActorRenderer;
        private float nextPreFilmRendererRetryAt;
        private bool loggedPreFilmRendererFailure;
        private bool squidIntroPending;
        private bool squidIntroActive;
        private bool squidAttackAudioActive;
        private bool squidEnterSoundPlayed;
        private bool squidExitSoundPlayed;
        private bool squidAttackPopSoundPlayed;
        private float squidIntroStartedAt;
        private float inkEffectsEnabledAt = float.PositiveInfinity;
        private bool gameplayPaused;
        private float gameplayPauseStartedAt;
        private bool holdThroughKnockout;
        private bool knockoutLoadingSeen;
        private bool visualEnding;
        private bool deactivateAfterVisualEnding;
        private float visualEndingFadeSpeed;
        private bool useNativePirateInkOverlay;
        private PirateLevelSquid nativePirateSquid;
        private SpriteRenderer nativePirateInkOverlayRenderer;

        internal void SetLogger(ManualLogSource value)
        {
            log = value;
        }

        internal void SetAssetsDirectory(string value)
        {
            assetsDirectory = value;
        }

        internal void StartAttempt(
            Level.Mode mode, bool showSquidIntro,
            bool nativePirateInkOverlay)
        {
            difficulty = mode;
            ResetState();
            challengeActive = true;
            useNativePirateInkOverlay = nativePirateInkOverlay;
            RefreshNativePirateReferences();
            squidIntroPending = showSquidIntro;
            nextSpawnAt = showSquidIntro
                ? float.PositiveInfinity
                : Time.time + FirstDropDelay;
            EnsureInkAssets();
        }
        internal void Configure(bool active, Level.Mode mode, bool newSession)
        {
            difficulty = mode;
            if (newSession)
            {
                ResetState();
                challengeActive = true;
                squidIntroPending = true;
                nextSpawnAt = float.PositiveInfinity;
                EnsureInkAssets();
                return;
            }

            if (challengeActive == active)
                return;

            // Victory cleanup asks the runtime to deactivate while its ink
            // is still fading. Keep only that compositor alive until clear.
            if (!active && (holdThroughKnockout ||
                            (visualEnding && deactivateAfterVisualEnding)))
                return;

            challengeActive = active;
            if (challengeActive)
            {
                squidIntroPending = true;
                nextSpawnAt = float.PositiveInfinity;
                EnsureInkAssets();
            }
            else
            {
                ResetState();
            }
        }

        internal bool IsHoldingThroughKnockout
        {
            get { return holdThroughKnockout; }
        }

        internal bool ShouldEndKnockoutHold()
        {
            if (!holdThroughKnockout || !knockoutLoadingSeen)
                return false;

            if (!SceneLoader.CurrentlyLoading)
                return true;

            try
            {
                var level = Level.Current;
                if (level == null || level.LevelType != Level.Type.Battle)
                    return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        internal void BeginKnockoutHold()
        {
            if (!challengeActive)
                return;

            holdThroughKnockout = true;
            knockoutLoadingSeen = false;
            nextSpawnAt = float.PositiveInfinity;
            StopSquidAttackAudio();
        }

        internal void EndImmediately()
        {
            ResetState();
        }
        internal void BeginDefeatFade()
        {
            // Keep the current hold and native five-second fade untouched.
            BeginVisualEnding(0f, false);
        }

        private void BeginVisualEnding(float fadeDuration, bool fadeNow)
        {
            if (!challengeActive)
                return;

            visualEnding = true;
            deactivateAfterVisualEnding = fadeNow;
            visualEndingFadeSpeed = fadeNow && fadeDuration > 0f
                ? Mathf.Max(0.0001f, inkAlpha / fadeDuration)
                : 0f;
            nextSpawnAt = float.PositiveInfinity;
            drops.Clear();
            groundImpacts.Clear();
            players = new AbstractPlayerController[0];
            StopSquidAttackAudio();
            ReleaseSquidActor();
            squidIntroPending = false;
            squidIntroActive = false;
            if (fadeNow)
            {
                holdRemaining = 0f;
                targetInkAlpha = 0f;
            }
        }
        private void Update()
        {
            if (!challengeActive)
                return;

            if (ShouldEndKnockoutHold())
            {
                EndImmediately();
                return;
            }

            if (CupheadTime.GlobalSpeed <= 0f)
            {
                if (!gameplayPaused)
                {
                    gameplayPaused = true;
                    gameplayPauseStartedAt = Time.time;
                }
                return;
            }

            if (gameplayPaused)
            {
                ShiftTimersAfterPause(Mathf.Max(
                    0f, Time.time - gameplayPauseStartedAt));
                gameplayPaused = false;
                gameplayPauseStartedAt = 0f;
            }

            if (!EnsureInkAssets())
                return;

            var delta = Time.deltaTime;
            if (delta <= 0f)
                return;

            gameplayCamera = FindGameplayCamera();
            if (gameplayCamera == null)
                return;

            if (SceneLoader.CurrentlyLoading)
            {
                if (holdThroughKnockout)
                {
                    knockoutLoadingSeen = true;
                    UpdateDrops(delta);
                    UpdatePreFilmInkRenderer();
                    return;
                }

                // During the initial scene reveal, update only the visual
                // squid sequence and its harmless intro drops. Player ink and
                // regular rain remain disabled until gameplay resumes.
                UpdateDrops(delta);
                UpdateSquidActor();
                UpdateSquidIntro();
                UpdateRainSpawning();
                UpdatePreFilmInkRenderer();
                return;
            }

            UpdateInk(delta);
            UpdatePlayers();
            UpdateDrops(delta);
            UpdateSquidActor();
            UpdateSquidIntro();
            UpdateRainSpawning();

            UpdatePreFilmInkRenderer();
            if (visualEnding && deactivateAfterVisualEnding &&
                inkAlpha <= 0.001f && targetInkAlpha <= 0.001f)
                ResetState();
        }

        internal bool BeginSquidIntro()
        {
            if (!challengeActive || !squidIntroPending)
                return false;

            if (!EnsureInkAssets() || squidEntranceFrames == null ||
                squidEntranceFrames.Length == 0 ||
                squidAttackFrames == null || squidAttackFrames.Length == 0 ||
                squidAttackLoopFrames == null ||
                squidAttackLoopFrames.Length == 0 ||
                squidExitFrames == null || squidExitFrames.Length == 0)
            {
                squidIntroPending = false;
                nextSpawnAt = Time.time + FirstDropDelay;
                if (log != null)
                    log.LogWarning(
                        "No se encontro la animacion del pulpo; la lluvia " +
                        "comenzara sin introduccion.");
                return false;
            }

            squidIntroPending = false;
            squidIntroActive = true;
            squidEnterSoundPlayed = false;
            squidExitSoundPlayed = false;
            squidAttackPopSoundPlayed = false;
            squidIntroStartedAt =
                Time.time + SquidIntroStartDelay;
            nextSpawnAt =
                squidIntroStartedAt + SquidAttackEventTime;
            if (log != null)
                log.LogInfo(
                    "Introduccion del pulpo iniciada antes de Ready/Wallop.");
            return true;
        }

        internal void BeginInkEffectGracePeriod(bool fallback)
        {
            if (!float.IsPositiveInfinity(inkEffectsEnabledAt))
                return;

            inkEffectsEnabledAt =
                Time.time + InkEffectGraceAfterAnnouncer;
            if (log != null)
            {
                log.LogInfo(fallback
                    ? "Gracia de tinta iniciada desde el comienzo real " +
                      "del combate (escena sin anuncio Wallop)."
                    : "Las bolitas podran entintar un segundo despues " +
                      "del anuncio Wallop.");
            }
        }

        private void UpdateSquidIntro()
        {
            if (!squidIntroActive)
                return;

            var elapsed = Time.time - squidIntroStartedAt;
            if (elapsed < 0f)
                return;

            if (!squidEnterSoundPlayed)
            {
                squidEnterSoundPlayed = true;
                try
                {
                    AudioManager.Play("level_pirate_squid_enter");
                }
                catch
                {
                }
            }
            var attackVisualStartsAt = SquidEntranceDuration;
            var exitStartsAt = attackVisualStartsAt +
                               SquidAttackOpenDuration +
                               SquidAttackLoopDuration;
            var endsAt = exitStartsAt + SquidExitDuration;

            if (elapsed < exitStartsAt)
            {
                if (elapsed >= SquidAttackEventTime)
                    StartSquidAttackAudio();

                if (elapsed >= attackVisualStartsAt &&
                    !squidAttackPopSoundPlayed)
                {
                    squidAttackPopSoundPlayed = true;
                    try
                    {
                        AudioManager.Play(
                            "level_pirate_squid_attack_pop");
                    }
                    catch
                    {
                    }
                }
                return;
            }

            if (elapsed >= exitStartsAt)
            {
                StopSquidAttackAudio();
                if (!squidExitSoundPlayed)
                {
                    squidExitSoundPlayed = true;
                    try
                    {
                        AudioManager.Play("level_pirate_squid_exit");
                    }
                    catch
                    {
                    }
                }
            }

            if (elapsed < endsAt)
                return;

            squidIntroActive = false;
        }

        private void UpdateRainSpawning()
        {
            if (visualEnding)
                return;

            var squidSpawning = SquidCanEmitRain() && squidActor != null &&
                                squidActorRenderer != null &&
                                squidActorRenderer.enabled;
            var maximumDrops = squidSpawning
                ? SquidRainMaximumVisibleDrops
                : MaximumVisibleDrops();
            if (Time.time < nextSpawnAt || drops.Count >= maximumDrops)
                return;

            if (squidSpawning)
            {
                while (Time.time >= nextSpawnAt &&
                       drops.Count < SquidRainMaximumVisibleDrops &&
                       SquidCanEmitRain())
                {
                    SpawnSquidIntroDrop();
                    nextSpawnAt += NativeSquidBlobDelay();
                }
                return;
            }

            SpawnWave();
            nextSpawnAt = Time.time + NextSpawnDelay();
        }

        private bool SquidCanEmitRain()
        {
            if (!squidIntroActive)
                return false;

            var fullDuration = SquidEntranceDuration +
                               SquidAttackOpenDuration +
                               SquidAttackLoopDuration;
            var elapsed = Time.time - squidIntroStartedAt;
            return elapsed >= SquidAttackEventTime &&
                   elapsed < fullDuration;
        }

        private float NativeSquidBlobDelay()
        {
            return difficulty == Level.Mode.Easy
                ? SquidEasyBlobDelay
                : SquidNormalHardBlobDelay;
        }

        private void StartSquidAttackAudio()
        {
            if (squidAttackAudioActive)
                return;

            squidAttackAudioActive = true;
            try
            {
                AudioManager.PlayLoop("level_pirate_squid_attack_loop");
            }
            catch
            {
            }
        }

        private void StopSquidAttackAudio()
        {
            if (!squidAttackAudioActive)
                return;

            squidAttackAudioActive = false;
            try
            {
                AudioManager.Stop("level_pirate_squid_attack_loop");
            }
            catch
            {
            }
        }

        private void UpdatePreFilmInkRenderer()
        {
            if (preFilmInkRenderer != null &&
                !preFilmInkRenderer.Matches(gameplayCamera))
            {
                preFilmInkRenderer.Dispose();
                preFilmInkRenderer = null;
            }

            if (preFilmInkRenderer == null &&
                Time.unscaledTime >= nextPreFilmRendererRetryAt)
            {
                string error;
                if (!InkRainPreFilmRenderer.TryCreate(
                        gameplayCamera, out preFilmInkRenderer, out error))
                {
                    nextPreFilmRendererRetryAt =
                        Time.unscaledTime + 2f;
                    if (!loggedPreFilmRendererFailure && log != null)
                    {
                        loggedPreFilmRendererFailure = true;
                        log.LogWarning(
                            "Lluvia de tinta usara el render alterno sin " +
                            "grano: " + error);
                    }
                    return;
                }

                loggedPreFilmRendererFailure = false;
                if (log != null)
                    log.LogInfo(
                        "Lluvia de tinta integrada antes de los efectos " +
                        "de pelicula de Cuphead.");
            }

            if (preFilmInkRenderer == null)
                return;

            preFilmInkRenderer.BeginFrame();
            var nativePirateTint = NativePirateDropTint();
            for (var i = 0; i < drops.Count; i++)
            {
                var drop = drops[i];
                var center = gameplayCamera.WorldToScreenPoint(
                    new Vector3(drop.Position.x, drop.Position.y, 0f));
                if (center.z < 0f)
                    continue;

                var edge = gameplayCamera.WorldToScreenPoint(
                    new Vector3(drop.Position.x + drop.Radius,
                        drop.Position.y, 0f));
                var projectedRadius = new Vector2(
                    edge.x - center.x, edge.y - center.y).magnitude;
                var radiusPixels = Mathf.Max(7f, projectedRadius);
                var frameIndex = (Mathf.FloorToInt(
                    drop.Age * DropFrameRate) + drop.FrameOffset) %
                    inkDropFrames.Length;
                var sprite = inkDropFrames[frameIndex];
                var width = radiusPixels * 2f;
                var height = width / SpriteAspect(sprite);
                preFilmInkRenderer.DrawSprite(
                    new Rect(
                        center.x - width * 0.5f,
                        Screen.height - center.y - height * 0.5f,
                        width, height),
                    sprite, nativePirateTint);
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
                var width = sprite.rect.width * scale *
                            GroundImpactVisualScale;
                var height = sprite.rect.height * scale *
                             GroundImpactVisualScale;
                preFilmInkRenderer.DrawSprite(
                    new Rect(
                        center.x - width * 0.5f,
                        Screen.height - center.y - height,
                        width, height),
                    sprite, nativePirateTint);
            }

            if (inkAlpha > 0.001f)
            {
                if (inkScreenOverlay != null)
                {
                    preFilmInkRenderer.DrawSprite(
                        new Rect(0f, 0f, Screen.width, Screen.height),
                        inkScreenOverlay,
                        new Color(1f, 1f, 1f,
                            Mathf.Clamp01(inkAlpha)));
                }
            }
            preFilmInkRenderer.EndFrame();
        }

        private void UpdateInk(float delta)
        {
            if (holdRemaining > 0f)
                holdRemaining = Mathf.Max(0f, holdRemaining - delta);
            else
                targetInkAlpha = Mathf.MoveTowards(
                    targetInkAlpha, 0f, delta / InkFadeDuration);

            var riseSpeed = MaximumInk / InkRiseDuration;
            var fadeSpeed = visualEndingFadeSpeed > 0f
                ? visualEndingFadeSpeed
                : MaximumInk / InkFadeDuration;
            inkAlpha = Mathf.MoveTowards(
                inkAlpha,
                targetInkAlpha,
                (targetInkAlpha > inkAlpha ? riseSpeed : fadeSpeed) * delta);

            if (inkAlpha <= 0.001f && targetInkAlpha <= 0.001f)
                ReleaseSplatActors();
            else
                UpdateSplatActors();
        }

        private void UpdateSplatActors()
        {
            if (gameplayCamera == null)
                return;

            var cameraDepth = Mathf.Abs(gameplayCamera.transform.position.z);
            var screenCenter = gameplayCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.5f, cameraDepth));
            var cameraRight = gameplayCamera.transform.right;
            var cameraUp = gameplayCamera.transform.up;

            for (var i = splats.Count - 1; i >= 0; i--)
            {
                var splat = splats[i];
                var elapsed = Time.time - splat.StartTime;
                if (elapsed >= splat.Duration)
                {
                    ReleaseSplatActor(splat);
                    splats.RemoveAt(i);
                    continue;
                }

                if (elapsed < 0f || splat.Renderer == null ||
                    splat.Frames == null || splat.Frames.Length == 0 ||
                    splat.PivotPixels == null ||
                    splat.PivotPixels.Length != splat.Frames.Length)
                {
                    if (splat.Renderer != null)
                        splat.Renderer.enabled = false;
                    continue;
                }

                var frameIndex = Mathf.Min(splat.Frames.Length - 1,
                    Mathf.FloorToInt(elapsed * SplatFrameRate));
                var sprite = splat.Frames[frameIndex];
                var pivot = splat.PivotPixels[frameIndex];
                var mirrorSign = splat.MirrorX ? -1f : 1f;
                var localPivotOffset = new Vector2(
                    (pivot.x - sprite.rect.width * 0.5f) * mirrorSign,
                    pivot.y - sprite.rect.height * 0.5f);
                var targetPivot = screenCenter +
                    cameraRight * splat.DesignPosition.x +
                    cameraUp * splat.DesignPosition.y;

                splat.Renderer.sprite = sprite;
                splat.Renderer.enabled = true;
                splat.Actor.transform.position = targetPivot -
                    cameraRight * localPivotOffset.x -
                    cameraUp * localPivotOffset.y;
                splat.Actor.transform.rotation =
                    gameplayCamera.transform.rotation;
                splat.Actor.transform.localScale =
                    new Vector3(mirrorSign, 1f, 1f);
            }
        }

        private static void ReleaseSplatActor(InkSplat splat)
        {
            if (splat != null && splat.Actor != null)
                Destroy(splat.Actor);
            if (splat != null)
            {
                splat.Actor = null;
                splat.Renderer = null;
            }
        }

        private void ReleaseSplatActors()
        {
            for (var i = 0; i < splats.Count; i++)
                ReleaseSplatActor(splats[i]);
            splats.Clear();
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

                if (!squidIntroActive &&
                    Time.time >= inkEffectsEnabledAt &&
                    TouchesPlayer(drop))
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

                if (drop.Age >= MaximumDropLifetime ||
                    (drop.RemoveBelowWorldEdge &&
                     drop.Position.y < drop.WorldExitY))
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

        private bool TryFindGroundImpact(
            Vector2 from, Vector2 to, out Vector2 point)
        {
            point = to;
            var hits = Physics2D.LinecastAll(from, to);
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || !collider.enabled ||
                    collider.name != "Level_Ground")
                    continue;

                point = hits[i].point;
                if (!loggedFirstGroundImpact && log != null)
                {
                    loggedFirstGroundImpact = true;
                    log.LogInfo(
                        "Lluvia de tinta: suelo Level_Ground detectado en " +
                        FormatColliderDiagnostic(collider, hits[i].point) +
                        ".");
                }
                return true;
            }

            LogGroundProbe(from, to, hits);
            return false;
        }

        private void LogGroundProbe(
            Vector2 from, Vector2 to, RaycastHit2D[] hits)
        {
            if (log == null || hits == null || hits.Length == 0 ||
                Time.unscaledTime < nextGroundProbeLogAt)
                return;

            nextGroundProbeLogAt = Time.unscaledTime + 2f;
            var message = "Lluvia de tinta: linecast sin Level_Ground de " +
                          from + " a " + to + "; impactos=" + hits.Length;
            var count = Mathf.Min(hits.Length, 8);
            for (var i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                    continue;

                message += " | " +
                           FormatColliderDiagnostic(collider, hits[i].point);
            }
            log.LogInfo(message + ".");
        }

        private static string FormatColliderDiagnostic(
            Collider2D collider, Vector2 point)
        {
            var layer = collider.gameObject.layer;
            var layerName = LayerMask.LayerToName(layer);
            var tag = "<sin tag>";
            try
            {
                tag = collider.tag;
            }
            catch
            {
            }

            return "ruta=" + GetTransformPath(collider.transform) +
                   ", tipo=" + collider.GetType().Name +
                   ", layer=" + layer + "(" + layerName + ")" +
                   ", tag=" + tag +
                   ", trigger=" + collider.isTrigger +
                   ", punto=" + point;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<sin transform>";

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
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

        private void RefreshNativePirateReferences()
        {
            if (!useNativePirateInkOverlay)
                return;

            if (nativePirateSquid == null)
            {
                var squids = Resources.FindObjectsOfTypeAll<PirateLevelSquid>();
                for (var i = 0; i < squids.Length; i++)
                {
                    var squid = squids[i];
                    if (squid != null && squid.gameObject.scene.IsValid())
                    {
                        nativePirateSquid = squid;
                        break;
                    }
                }
            }

            if (nativePirateInkOverlayRenderer == null)
            {
                var overlay = PirateLevelSquidInkOverlay.Current;
                if (overlay != null)
                    nativePirateInkOverlayRenderer =
                        overlay.GetComponent<SpriteRenderer>();
            }

        }

        private Color NativePirateDropTint()
        {
            if (!useNativePirateInkOverlay)
                return Color.white;

            RefreshNativePirateReferences();
            if (nativePirateInkOverlayRenderer == null ||
                !nativePirateInkOverlayRenderer.enabled)
                return Color.white;

            var visibleLight = 1f - Mathf.Clamp01(
                nativePirateInkOverlayRenderer.color.a);
            return new Color(visibleLight, visibleLight, visibleLight, 1f);
        }
        private void RegisterInkHit(Vector2 worldPosition)
        {
            if (useNativePirateInkOverlay)
            {
                try
                {
                    var nativeOverlay = PirateLevelSquidInkOverlay.Current;
                    if (nativeOverlay != null)
                    {
                        nativeOverlay.Hit();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    if (log != null)
                        log.LogWarning(
                            "No se pudo usar el overlay nativo de tinta: " +
                            exception.Message);
                }
            }

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
            if (inkScreenAnimations.Count < 5 ||
                inkScreenPivotPixels.Count < 5)
                return;

            ReleaseSplatActors();
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

                var actor = new GameObject(template.Large
                    ? "Pirate_Ink_Large"
                    : "Pirate_Ink_Small");
                actor.hideFlags = HideFlags.HideAndDontSave;
                var renderer = actor.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = "Effects";
                renderer.sortingOrder = 0;
                renderer.color = Color.white;

                var splat = new InkSplat
                {
                    DesignPosition = template.Position,
                    StartTime = Time.time +
                                UnityEngine.Random.Range(0, 10) *
                                SplatDelayStep,
                    Duration = NativeSplatDuration(animationIndex),
                    Frames = frames,
                    PivotPixels = inkScreenPivotPixels[animationIndex],
                    MirrorX = UnityEngine.Random.value >= 0.5f,
                    Actor = actor,
                    Renderer = renderer
                };
                renderer.enabled = false;
                splats.Add(splat);
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
                return 2f;
            if (difficulty == Level.Mode.Hard)
                return 2.5f;
            return 2.2f;
        }

        private void SpawnWave()
        {
            var capacity = MaximumVisibleDrops() - drops.Count;
            if (capacity <= 0)
                return;

            var count = 1;
            var waveRoll = UnityEngine.Random.value;
            if (difficulty == Level.Mode.Easy && waveRoll < 0.45f)
            {
                count = 2;
            }
            else if (difficulty == Level.Mode.Normal)
            {
                if (waveRoll < 0.15f)
                    count = 3;
                else if (waveRoll < 0.50f)
                    count = 2;
            }
            else if (difficulty == Level.Mode.Hard)
            {
                if (waveRoll < 0.10f)
                    count = 4;
                else if (waveRoll < 0.25f)
                    count = 3;
                else if (waveRoll < 0.60f)
                    count = 2;
            }

            count = Mathf.Min(count, capacity);
            for (var i = 0; i < count; i++)
                SpawnDrop(i, count);
        }

        private void SpawnSquidIntroDrop()
        {
            if (drops.Count >= SquidRainMaximumVisibleDrops ||
                squidActor == null || squidActorRenderer == null ||
                !squidActorRenderer.enabled)
                return;

            var sourceOrigin = CurrentSquidInkOrigin();
            var localOrigin = new Vector3(
                sourceOrigin.x / 100f,
                sourceOrigin.y / 100f,
                0f);
            var origin = squidActor.transform.TransformPoint(localOrigin);
            var cameraHeight = Mathf.Max(
                1f, gameplayCamera.orthographicSize * 2f);

            float minHorizontal;
            float maxHorizontal;
            float minVertical;
            float maxVertical;
            float gravity;
            if (difficulty == Level.Mode.Easy)
            {
                minHorizontal = -330f / 720f;
                maxHorizontal = 330f / 720f;
                minVertical = 550f / 720f;
                maxVertical = 700f / 720f;
                gravity = 900f / 720f;
            }
            else if (difficulty == Level.Mode.Hard)
            {
                minHorizontal = -260f / 720f;
                maxHorizontal = 330f / 720f;
                minVertical = 550f / 720f;
                maxVertical = 850f / 720f;
                gravity = 1000f / 720f;
            }
            else
            {
                minHorizontal = -300f / 720f;
                maxHorizontal = 300f / 720f;
                minVertical = 500f / 720f;
                maxVertical = 800f / 720f;
                gravity = 1000f / 720f;
            }

            drops.Add(new InkDrop
            {
                Position = new Vector2(origin.x, origin.y),
                Velocity = new Vector2(
                    UnityEngine.Random.Range(
                        minHorizontal, maxHorizontal) * cameraHeight,
                    UnityEngine.Random.Range(
                        minVertical, maxVertical) * cameraHeight),
                Gravity = gravity * cameraHeight,
                Radius = cameraHeight * UnityEngine.Random.Range(
                    0.016f, 0.021f),
                FrameOffset = 0
            });

            if (!loggedFirstDrop && log != null)
            {
                loggedFirstDrop = true;
                log.LogInfo(
                    "Primera gota lanzada desde InkOrigin del pulpo.");
            }
        }

        private Vector2 CurrentSquidInkOrigin()
        {
            var fixedOrigin = new Vector2(
                SquidInkOriginX, SquidInkOriginY);
            if (!squidIntroActive)
                return fixedOrigin;

            var elapsed = Time.time - squidIntroStartedAt;
            var loopStartsAt = SquidEntranceDuration +
                               SquidAttackOpenDuration;
            if (elapsed < loopStartsAt)
                return fixedOrigin;

            var loopDuration = SquidInkOriginCurveX.Length /
                               SquidFrameRate;
            var loopTime = Mathf.Repeat(
                elapsed - loopStartsAt, loopDuration);
            var segment = Mathf.Min(
                SquidInkOriginCurveX.Length - 1,
                Mathf.FloorToInt(loopTime * SquidFrameRate));
            var segmentTime = loopTime - segment / SquidFrameRate;
            return new Vector2(
                EvaluateStreamedCurve(
                    SquidInkOriginCurveX[segment], segmentTime),
                EvaluateStreamedCurve(
                    SquidInkOriginCurveY[segment], segmentTime));
        }

        private static float EvaluateStreamedCurve(
            Vector4 coefficients, float time)
        {
            return ((coefficients.x * time + coefficients.y) * time +
                    coefficients.z) * time + coefficients.w;
        }

        private void SpawnDrop(int waveIndex, int waveCount)
        {
            // Native-style arc: enter from the upper-right, drift left and
            // accelerate downward instead of falling in a vertical line.
            // Extend the spawn strip equally past both visible sides. The old
            // 5%-115% range was biased toward one side; that bias rotated with
            // the Dogfight camera and became especially visible at 90/270°.
            var x = UnityEngine.Random.Range(-0.05f, 1.05f);
            if (waveCount > 1)
            {
                var sectionWidth = 1.10f / waveCount;
                x = -0.05f + sectionWidth *
                    (waveIndex + UnityEngine.Random.Range(0f, 1f));
            }

            var cameraDepth = Mathf.Abs(
                gameplayCamera.transform.position.z);
            var verticalDelay = 0f;
            if (difficulty == Level.Mode.Hard && waveCount > 1)
            {
                verticalDelay = UnityEngine.Random.Range(0.20f, 0.40f);
            }
            else if (difficulty == Level.Mode.Normal)
            {
                verticalDelay = UnityEngine.Random.Range(0.20f, 0.40f);
            }
            else if (waveCount > 1)
            {
                verticalDelay = UnityEngine.Random.Range(0.20f, 0.40f);
            }
            var bottomLeft = gameplayCamera.ViewportToWorldPoint(
                new Vector3(0f, 0f, cameraDepth));
            var bottomRight = gameplayCamera.ViewportToWorldPoint(
                new Vector3(1f, 0f, cameraDepth));
            var topLeft = gameplayCamera.ViewportToWorldPoint(
                new Vector3(0f, 1f, cameraDepth));
            var topRight = gameplayCamera.ViewportToWorldPoint(
                new Vector3(1f, 1f, cameraDepth));
            var minWorldX = Mathf.Min(
                bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            var maxWorldX = Mathf.Max(
                bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            var minWorldY = Mathf.Min(
                bottomLeft.y, bottomRight.y, topLeft.y, topRight.y);
            var maxWorldY = Mathf.Max(
                bottomLeft.y, bottomRight.y, topLeft.y, topRight.y);
            var visibleWorldWidth = Mathf.Max(1f, maxWorldX - minWorldX);
            var visibleWorldHeight = Mathf.Max(1f, maxWorldY - minWorldY);
            var start = new Vector3(
                Mathf.Lerp(minWorldX, maxWorldX, x),
                maxWorldY + visibleWorldHeight * (0.12f + verticalDelay),
                0f);
            var cameraHeight = Mathf.Max(1f,
                gameplayCamera.orthographicSize * 2f);
            var horizontalSpeed =
                UnityEngine.Random.Range(-0.20f, -0.14f) * cameraHeight;
            var fallSpeed = UnityEngine.Random.Range(0.15f, 0.22f) *
                            cameraHeight;
            var gravity = UnityEngine.Random.Range(0.22f, 0.32f) *
                          cameraHeight;
            var worldExitY = minWorldY - visibleWorldHeight * 0.20f;

            drops.Add(new InkDrop
            {
                Position = new Vector2(start.x, start.y),
                Velocity = new Vector2(horizontalSpeed, -fallSpeed),
                Gravity = gravity,
                RemoveBelowWorldEdge = true,
                WorldExitY = worldExitY,
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
                return 8;
            if (difficulty == Level.Mode.Hard)
                return 30;
            return 20;
        }

        private void ShiftTimersAfterPause(float pausedDuration)
        {
            if (pausedDuration <= 0f)
                return;

            ShiftFiniteTimer(ref nextSpawnAt, pausedDuration);
            ShiftFiniteTimer(ref inkEffectsEnabledAt, pausedDuration);
            ShiftFiniteTimer(ref nextPlayerScanAt, pausedDuration);
            if (squidIntroStartedAt > 0f)
                squidIntroStartedAt += pausedDuration;

            for (var i = 0; i < groundImpacts.Count; i++)
                groundImpacts[i].StartTime += pausedDuration;
            for (var i = 0; i < splats.Count; i++)
                splats[i].StartTime += pausedDuration;
        }

        private static void ShiftFiniteTimer(
            ref float timer, float amount)
        {
            if (!float.IsInfinity(timer) && !float.IsNaN(timer) &&
                timer > 0f)
                timer += amount;
        }

        private float NextSpawnDelay()
        {
            if (difficulty == Level.Mode.Easy)
                return UnityEngine.Random.Range(0.80f, 1.00f);
            if (difficulty == Level.Mode.Hard)
                return UnityEngine.Random.Range(0.25f, 0.60f);
            return UnityEngine.Random.Range(0.40f, 0.65f);
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
                var squidDirectory = Path.Combine(inkRoot, "squid");
                inkDropFrames = LoadSpriteSequence(
                    projectileDirectory,
                    "pirate_squid_inkblob_*.png");
                squidEntranceFrames = LoadSpriteSequence(
                    squidDirectory,
                    "pirate_squid_entrance_*.png",
                    new Vector2(0.5f, 0f));
                var allSquidAttackFrames = LoadSpriteSequence(
                    squidDirectory,
                    "pirate_squid_????.png",
                    new Vector2(0.5f, 0f));
                squidLeaveFrames = LoadSpriteSequence(
                    squidDirectory,
                    "pirate_squid_leave_*.png",
                    new Vector2(0.5f, 0f));
                squidAttackFrames = SpriteRange(
                    allSquidAttackFrames, 0, 3);
                squidAttackLoopFrames = SpriteRange(
                    allSquidAttackFrames, 3, 16);
                squidExitFrames = JoinSpriteRanges(
                    SpriteRange(allSquidAttackFrames, 3, 7),
                    squidLeaveFrames);

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
                inkScreenPivotPixels.Clear();
                var nativeScreenDirectory = Path.Combine(
                    inkRoot, "screen-native");
                LoadNativeSplatSequences(nativeScreenDirectory);

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
                        " grupos de manchas y pulpo " +
                        (squidEntranceFrames == null ? 0 :
                            squidEntranceFrames.Length) + "/" +
                        (squidAttackFrames == null ? 0 :
                            squidAttackFrames.Length) + "/" +
                        (squidAttackLoopFrames == null ? 0 :
                            squidAttackLoopFrames.Length) + "/" +
                        (squidExitFrames == null ? 0 :
                            squidExitFrames.Length) + ".");
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

        private void LoadNativeSplatSequences(string directory)
        {
            var pivotFile = Path.Combine(directory, "pivots.tsv");
            if (!Directory.Exists(directory) || !File.Exists(pivotFile))
                return;

            var pivots = new Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(pivotFile);
            for (var i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split('\t');
                float pivotX;
                float pivotY;
                if (parts.Length != 3 ||
                    !float.TryParse(parts[1], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out pivotX) ||
                    !float.TryParse(parts[2], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out pivotY))
                    continue;
                pivots[parts[0]] = new Vector2(pivotX, pivotY);
            }

            var groups = new[] { "a", "b", "c", "d", "e" };
            for (var groupIndex = 0; groupIndex < groups.Length;
                 groupIndex++)
            {
                var files = Directory.GetFiles(directory,
                    "pirate_squid_ink_screen_" + groups[groupIndex] +
                    "_*.png");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                var frames = new List<Sprite>();
                var framePivots = new List<Vector2>();
                for (var frameIndex = 0; frameIndex < files.Length;
                     frameIndex++)
                {
                    var fileName = Path.GetFileName(files[frameIndex]);
                    Vector2 normalizedPivot;
                    if (!pivots.TryGetValue(fileName, out normalizedPivot))
                        continue;

                    var bytes = File.ReadAllBytes(files[frameIndex]);
                    var texture = new Texture2D(
                        2, 2, TextureFormat.ARGB32, false);
                    texture.name = Path.GetFileNameWithoutExtension(fileName);
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    if (!texture.LoadImage(bytes))
                    {
                        Destroy(texture);
                        continue;
                    }

                    ownedInkTextures.Add(texture);
                    var sprite = Sprite.Create(texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 1f);
                    sprite.name = texture.name;
                    frames.Add(sprite);
                    framePivots.Add(new Vector2(
                        normalizedPivot.x * texture.width,
                        normalizedPivot.y * texture.height));
                }

                if (frames.Count > 0)
                {
                    inkScreenAnimations.Add(frames.ToArray());
                    inkScreenPivotPixels.Add(framePivots.ToArray());
                }
            }
        }
        private Sprite[] LoadSpriteSequence(
            string directory, string searchPattern)
        {
            return LoadSpriteSequence(
                directory, searchPattern, new Vector2(0.5f, 0.5f));
        }

        private Sprite[] LoadSpriteSequence(
            string directory, string searchPattern, Vector2 pivot)
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
                    pivot,
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

        private static Sprite[] SpriteRange(
            Sprite[] source, int start, int count)
        {
            if (source == null || start < 0 || count <= 0 ||
                start >= source.Length)
                return new Sprite[0];

            count = Mathf.Min(count, source.Length - start);
            var result = new Sprite[count];
            Array.Copy(source, start, result, 0, count);
            return result;
        }

        private static Sprite[] JoinSpriteRanges(
            Sprite[] first, Sprite[] second)
        {
            var firstLength = first == null ? 0 : first.Length;
            var secondLength = second == null ? 0 : second.Length;
            var result = new Sprite[firstLength + secondLength];
            if (firstLength > 0)
                Array.Copy(first, 0, result, 0, firstLength);
            if (secondLength > 0)
                Array.Copy(second, 0, result, firstLength, secondLength);
            return result;
        }

        private void OnGUI()
        {
            if (SceneLoader.CurrentlyLoading && !holdThroughKnockout)
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

            if (preFilmInkRenderer == null)
            {
                DrawDropsWithGui();
                DrawGroundImpactsWithGui();
            }

            // Fallback only: native hit splats are real SpriteRenderers. Keep
            // only the full-screen veil here if composition is unavailable.
            if (preFilmInkRenderer == null && inkAlpha > 0.001f &&
                inkScreenOverlay != null)
            {
                DrawSprite(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    inkScreenOverlay,
                    new Color(1f, 1f, 1f, Mathf.Clamp01(inkAlpha)));
            }

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private Sprite CurrentSquidIntroSprite()
        {
            if (!squidIntroActive)
                return null;

            var elapsed = Time.time - squidIntroStartedAt;
            if (elapsed < 0f)
                return null;
            if (elapsed < SquidEntranceDuration)
            {
                var index = Mathf.Min(
                    squidEntranceFrames.Length - 1,
                    Mathf.FloorToInt(elapsed * SquidFrameRate));
                return squidEntranceFrames[index];
            }

            elapsed -= SquidEntranceDuration;
            if (elapsed < SquidAttackOpenDuration)
            {
                var index = Mathf.Min(
                    squidAttackFrames.Length - 1,
                    Mathf.FloorToInt(elapsed * SquidFrameRate));
                return squidAttackFrames[index];
            }

            elapsed -= SquidAttackOpenDuration;
            if (elapsed < SquidAttackLoopDuration)
            {
                var index = Mathf.FloorToInt(elapsed * SquidFrameRate) %
                            squidAttackLoopFrames.Length;
                return squidAttackLoopFrames[index];
            }

            elapsed -= SquidAttackLoopDuration;
            if (elapsed < SquidExitDuration)
            {
                var index = Mathf.Min(
                    squidExitFrames.Length - 1,
                    Mathf.FloorToInt(elapsed * SquidFrameRate));
                return squidExitFrames[index];
            }
            return null;
        }

        private static SpriteRenderer FindNativePirateDockBackRenderer()
        {
            var renderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.gameObject.scene.IsValid())
                    continue;

                if (renderer.sprite.name == "pirateDockA" ||
                    GetTransformPath(renderer.transform) == "Level/Dock/Back")
                    return renderer;
            }
            return null;
        }
        private void UpdateSquidActor()
        {
            var sprite = CurrentSquidIntroSprite();
            if (!squidIntroActive || gameplayCamera == null || sprite == null)
            {
                if (squidActorRenderer != null)
                    squidActorRenderer.enabled = false;
                return;
            }

            if (squidActor == null || squidActorRenderer == null)
            {
                ReleaseSquidActor();
                squidActor = new GameObject(
                    "Gilomx native squid introduction actor");
                squidActor.hideFlags = HideFlags.HideAndDontSave;
                squidActorRenderer =
                    squidActor.AddComponent<SpriteRenderer>();
                var sortingLayers = SortingLayer.layers;
                if (sortingLayers != null && sortingLayers.Length > 0)
                {
                    var highestLayer = sortingLayers[0];
                    for (var i = 1; i < sortingLayers.Length; i++)
                    {
                        if (sortingLayers[i].value > highestLayer.value)
                            highestLayer = sortingLayers[i];
                    }
                    squidActorRenderer.sortingLayerID = highestLayer.id;
                }
                squidActorRenderer.sortingOrder = short.MaxValue;
                squidActorRenderer.color = Color.white;
            }

            if (squidActor.transform.parent != gameplayCamera.transform)
                squidActor.transform.SetParent(
                    gameplayCamera.transform, false);

            var depth = Mathf.Max(
                1f, gameplayCamera.nearClipPlane + 0.5f);
            RefreshNativePirateReferences();
            var anchoredToNativePirate = useNativePirateInkOverlay &&
                                         nativePirateSquid != null;
            var anchor = anchoredToNativePirate
                ? nativePirateSquid.transform.position
                : gameplayCamera.ViewportToWorldPoint(
                    new Vector3(0.5f, SquidAnchorViewportY, depth));
            var viewportBottom = gameplayCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 0f, depth));
            var viewportTop = gameplayCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 1f, depth));
            var worldUnitsPerPixel = Vector3.Distance(
                viewportBottom, viewportTop) /
                Mathf.Max(1f, gameplayCamera.pixelHeight);
            var spriteScale = Screen.height / 720f * SquidVisualScale *
                              worldUnitsPerPixel * sprite.pixelsPerUnit;
            var elapsed = Mathf.Max(
                0f, Time.time - squidIntroStartedAt);
            var bobPhase = Mathf.PingPong(elapsed, 1f);
            var easedBob = 0.5f -
                           Mathf.Cos(bobPhase * Mathf.PI) * 0.5f;

            if (useNativePirateInkOverlay)
            {
                // The native prefab uses a 620x620 sprite at PPU 1 and scale 1.
                // Our exported PNG uses PPU 100, so scale 100 reproduces the
                // same 620 world-unit bounds without changing other levels.
                spriteScale = sprite.pixelsPerUnit;
                anchor.x = -73f;
                anchor.y = -220f - SquidNativeBobDistance * easedBob;
                anchor.z = 0f;

                var dockBackRenderer = FindNativePirateDockBackRenderer();
                squidActorRenderer.sortingLayerID = dockBackRenderer != null
                    ? dockBackRenderer.sortingLayerID
                    : SortingLayer.NameToID("Background");
                squidActorRenderer.sortingOrder = -10;
            }
            else if (!anchoredToNativePirate)
            {
                var bobPixels = SquidNativeBobDistance * easedBob;
                anchor -= gameplayCamera.transform.up *
                          (bobPixels * Screen.height / 720f *
                           SquidVisualScale * worldUnitsPerPixel);
            }

            squidActorRenderer.sprite = sprite;
            squidActorRenderer.enabled = true;
            squidActor.transform.localPosition =
                gameplayCamera.transform.InverseTransformPoint(anchor);
            squidActor.transform.localRotation = Quaternion.identity;
            squidActor.transform.localScale =
                new Vector3(spriteScale, spriteScale, 1f);
        }

        private void ReleaseSquidActor()
        {
            if (squidActor != null)
                Destroy(squidActor);
            squidActor = null;
            squidActorRenderer = null;
        }

        private void DrawDropsWithGui()
        {
            for (var i = 0; i < drops.Count; i++)
            {
                var drop = drops[i];
                var center = gameplayCamera.WorldToScreenPoint(
                    new Vector3(drop.Position.x, drop.Position.y, 0f));
                if (center.z < 0f)
                    continue;

                var edge = gameplayCamera.WorldToScreenPoint(
                    new Vector3(drop.Position.x + drop.Radius,
                        drop.Position.y, 0f));
                var projectedRadius = new Vector2(
                    edge.x - center.x, edge.y - center.y).magnitude;
                var radiusPixels = Mathf.Max(7f, projectedRadius);
                var frameIndex = (Mathf.FloorToInt(
                    drop.Age * DropFrameRate) + drop.FrameOffset) %
                    inkDropFrames.Length;
                var sprite = inkDropFrames[frameIndex];
                var width = radiusPixels * 2f;
                var height = width / SpriteAspect(sprite);
                DrawSprite(
                    new Rect(
                        center.x - width * 0.5f,
                        Screen.height - center.y - height * 0.5f,
                        width, height),
                    sprite, Color.white);
            }
        }

        private void DrawGroundImpactsWithGui()
        {
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
                var width = sprite.rect.width * scale *
                            GroundImpactVisualScale;
                var height = sprite.rect.height * scale *
                             GroundImpactVisualScale;
                DrawSprite(
                    new Rect(
                        center.x - width * 0.5f,
                        Screen.height - center.y - height,
                        width, height),
                    sprite, Color.white);
            }
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
            StopSquidAttackAudio();
            if (preFilmInkRenderer != null)
            {
                preFilmInkRenderer.Dispose();
                preFilmInkRenderer = null;
            }
            ReleaseSquidActor();
            drops.Clear();
            groundImpacts.Clear();
            ReleaseSplatActors();
            players = new AbstractPlayerController[0];
            gameplayCamera = null;
            inkAlpha = 0f;
            targetInkAlpha = 0f;
            holdRemaining = 0f;
            nextPlayerScanAt = 0f;
            loggedFirstGroundImpact = false;
            nextGroundProbeLogAt = 0f;
            nextPreFilmRendererRetryAt = 0f;
            loggedPreFilmRendererFailure = false;
            squidIntroPending = false;
            squidIntroActive = false;
            squidEnterSoundPlayed = false;
            squidExitSoundPlayed = false;
            squidAttackPopSoundPlayed = false;
            squidIntroStartedAt = 0f;
            inkEffectsEnabledAt = float.PositiveInfinity;
            gameplayPaused = false;
            gameplayPauseStartedAt = 0f;
            holdThroughKnockout = false;
            knockoutLoadingSeen = false;
            visualEnding = false;
            deactivateAfterVisualEnding = false;
            visualEndingFadeSpeed = 0f;
            useNativePirateInkOverlay = false;
            nativePirateSquid = null;
            nativePirateInkOverlayRenderer = null;
        }

        private void OnDestroy()
        {
            ResetState();
            inkDropFrames = null;
            inkScreenOverlay = null;
            squidEntranceFrames = null;
            squidAttackFrames = null;
            squidAttackLoopFrames = null;
            squidLeaveFrames = null;
            squidExitFrames = null;
            groundImpactAnimations.Clear();
            inkScreenAnimations.Clear();
            inkScreenPivotPixels.Clear();
            for (var i = 0; i < ownedInkTextures.Count; i++)
            {
                if (ownedInkTextures[i] != null)
                    Destroy(ownedInkTextures[i]);
            }
            ownedInkTextures.Clear();
        }
    }

    internal sealed class InkRainPreFilmRenderer : IDisposable
    {
        private readonly Camera camera;
        private readonly CommandBuffer commandBuffer;
        private readonly Material material;
        private readonly MaterialPropertyBlock properties;
        private readonly Mesh quad;
        private bool disposed;

        private InkRainPreFilmRenderer(
            Camera targetCamera, Shader transparentShader)
        {
            camera = targetCamera;
            material = new Material(transparentShader);
            material.name = "Gilomx ink before film grain";
            material.hideFlags = HideFlags.HideAndDontSave;
            properties = new MaterialPropertyBlock();
            quad = CreateQuad();

            commandBuffer = new CommandBuffer();
            commandBuffer.name = "Gilomx ink before Cuphead film effects";
            camera.AddCommandBuffer(
                CameraEvent.BeforeImageEffects, commandBuffer);
        }

        internal static bool TryCreate(
            Camera camera, out InkRainPreFilmRenderer renderer,
            out string error)
        {
            renderer = null;
            error = null;
            if (camera == null)
            {
                error = "la camara de combate no esta disponible.";
                return false;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null || !shader.isSupported)
            {
                error = "Cuphead no expuso un shader transparente compatible.";
                return false;
            }

            try
            {
                renderer = new InkRainPreFilmRenderer(camera, shader);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                renderer = null;
                return false;
            }
        }

        internal bool Matches(Camera targetCamera)
        {
            return !disposed && camera != null && camera == targetCamera;
        }

        internal void ClearFrame()
        {
            if (!disposed)
                commandBuffer.Clear();
        }

        internal void BeginFrame()
        {
            if (disposed)
                return;

            commandBuffer.Clear();
            commandBuffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget);
            commandBuffer.SetViewProjectionMatrices(
                Matrix4x4.identity, Matrix4x4.identity);
        }

        internal void DrawSprite(Rect screenRect, Sprite sprite, Color color)
        {
            if (disposed || sprite == null || sprite.texture == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;

            var centerX = (screenRect.x + screenRect.width * 0.5f) /
                          Screen.width * 2f - 1f;
            var centerY = 1f -
                          (screenRect.y + screenRect.height * 0.5f) /
                          Screen.height * 2f;
            var scaleX = screenRect.width / Screen.width;
            var scaleY = screenRect.height / Screen.height;
            var matrix = Matrix4x4.TRS(
                new Vector3(centerX, centerY, 0f),
                Quaternion.identity,
                new Vector3(scaleX, scaleY, 1f));

            properties.Clear();
            properties.SetTexture("_MainTex", sprite.texture);
            properties.SetColor("_Color", color);
            commandBuffer.DrawMesh(
                quad, matrix, material, 0, -1, properties);
        }

        internal void DrawComposite(RenderTexture composite)
        {
            if (disposed || composite == null || !composite.IsCreated())
                return;

            commandBuffer.Blit(
                composite,
                BuiltinRenderTextureType.CameraTarget,
                material, 0);
            commandBuffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget);
            commandBuffer.SetViewProjectionMatrices(
                Matrix4x4.identity, Matrix4x4.identity);
        }

        internal void EndFrame()
        {
            // Kept as a named boundary so rebuilding the command buffer remains
            // explicit if the renderer later needs temporary render targets.
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh();
            mesh.name = "Gilomx full-screen ink quad";
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(-1f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            if (camera != null && commandBuffer != null)
                camera.RemoveCommandBuffer(
                    CameraEvent.BeforeImageEffects, commandBuffer);
            if (commandBuffer != null)
                commandBuffer.Release();
            if (material != null)
                UnityEngine.Object.Destroy(material);
            if (quad != null)
                UnityEngine.Object.Destroy(quad);
        }
    }
}
