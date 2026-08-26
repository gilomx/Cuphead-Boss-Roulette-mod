using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    internal enum CreatorToolsAlignment
    {
        Left,
        Center,
        Right
    }

    internal enum CreatorToolsOrder
    {
        IconsAbove,
        TextAbove
    }

    internal enum CreatorToolsRetryBehavior
    {
        Keep,
        Reappear
    }

    public sealed partial class Plugin
    {
        private const int CreatorToolsDefaultPort = 18081;
        private const float CreatorToolsDevilPhaseTransitionBlockDelay = 6f;
        private const float CreatorToolsSaltbakerPhaseOneBlockDelay = 2.5f;
        private const float CreatorToolsInteractionPhaseTransitionTimeout = 30f;

        private ConfigEntry<bool> creatorToolsEnabledSetting;
        private ConfigEntry<float> creatorToolsScaleSetting;
        private ConfigEntry<CreatorToolsOrder> creatorToolsOrderSetting;
        private ConfigEntry<CreatorToolsAlignment>
            creatorToolsAlignmentSetting;
        private ConfigEntry<int> creatorToolsOpacitySetting;
        private ConfigEntry<bool> creatorToolsPreviewSetting;
        private ConfigEntry<bool> creatorToolsLogoSetting;
        private ConfigEntry<CreatorToolsRetryBehavior>
            creatorToolsRetryBehaviorSetting;
        private ConfigEntry<int>
            creatorToolsInteractionMaximumActiveSetting;

        private CreatorToolsServer creatorToolsServer;
        private CreatorToolsInteractionController creatorToolsInteractions;
        private CreatorToolsDashboardController creatorToolsDashboard;
        private CreatorToolsStreamRulesController creatorToolsStreamRules;
        private bool creatorToolsBattleSessionActive;
        private bool creatorToolsBattleCompleted;
        private bool creatorToolsBattleVisible;
        private int creatorToolsBattleSessionId;
        private int creatorToolsRevealedIcons;
        private bool creatorToolsTextVisible;
        private string creatorToolsLastPublishedState;
        private string creatorToolsLabelKey;
        private int creatorToolsLabelRevision;
        private bool creatorToolsLabelRenderFailureLogged;
        private string creatorToolsServerError;
        private bool creatorToolsInteractionLevelStartObserved;
        private int creatorToolsInteractionLevelInstanceId = -1;
        private float creatorToolsInteractionAllowedAt =
            float.PositiveInfinity;
        private bool creatorToolsInteractionPhaseTransitionBlocked;
        private bool creatorToolsInteractionPhaseTransitionActivated;
        private bool creatorToolsInteractionPhaseTransitionProtectionEnabled =
            true;
        private bool creatorToolsInteractionPhaseTransitionActorsCleared;
        private int creatorToolsInteractionPhaseTransitionLevelInstanceId = -1;
        private float creatorToolsInteractionPhaseTransitionStartedAt =
            float.PositiveInfinity;
        private float creatorToolsInteractionPhaseTransitionBlockDelay;
        private float creatorToolsInteractionPhaseTransitionPlayableElapsed;
        private int creatorToolsInteractionPhaseTransitionLastPlayableFrame =
            -1;

        private void InitializeCreatorTools()
        {
            creatorToolsEnabledSetting = Config.Bind(
                "Creator Tools", "Activado", false,
                "Muestra u oculta el overlay local para OBS.");
            creatorToolsScaleSetting = Config.Bind(
                "Creator Tools", "Tamano", 1f,
                "Escala del overlay: 1, 1.5 o 2.");
            creatorToolsOrderSetting = Config.Bind(
                "Creator Tools", "Orden", CreatorToolsOrder.IconsAbove,
                "Distribucion vertical del overlay.");
            creatorToolsAlignmentSetting = Config.Bind(
                "Creator Tools", "Alineacion",
                CreatorToolsAlignment.Center,
                "Alineacion horizontal del overlay.");
            creatorToolsOpacitySetting = Config.Bind(
                "Creator Tools", "Opacidad", 100,
                "Opacidad del overlay: 25, 50, 75 o 100.");
            creatorToolsPreviewSetting = Config.Bind(
                "Creator Tools", "VistaPrevia", false,
                "Muestra un resultado simulado mientras no hay combate.");
            creatorToolsLogoSetting = Config.Bind(
                "Creator Tools", "MostrarNombre", false,
                "Muestra el logo del mod cuando el HUD no esta activo.");
            creatorToolsRetryBehaviorSetting = Config.Bind(
                "Creator Tools", "AlReintentar",
                CreatorToolsRetryBehavior.Reappear,
                "Al reintentar, mantiene el overlay o repite su animacion.");
            creatorToolsInteractionMaximumActiveSetting = Config.Bind(
                "Creator Tools",
                "InteraccionesMaximasEnPantalla",
                1,
                "Cantidad maxima de interacciones visibles al mismo tiempo.");

            creatorToolsDashboard = new CreatorToolsDashboardController();
            creatorToolsStreamRules = new CreatorToolsStreamRulesController(
                AssetsDirectory,
                Config.ConfigFilePath,
                delegate(string message) { Logger.LogWarning(message); });
            creatorToolsInteractions = new CreatorToolsInteractionController(
                this,
                Config.ConfigFilePath,
                CanPreloadNativeInteractionAssets,
                CanSpawnCreatorToolsInteraction,
                GetCreatorToolsInteractionMaximumActive,
                SetCreatorToolsInteractionMaximumActive,
                GetCreatorToolsInteractionPhaseTransitionProtectionEnabled,
                SetCreatorToolsInteractionPhaseTransitionProtectionEnabled,
                delegate(string message) { Logger.LogInfo(message); },
                delegate(string message) { Logger.LogWarning(message); });
            LevelPauseGUI.OnPauseEvent +=
                OnCreatorToolsInteractionPaused;
            LevelPauseGUI.OnUnpauseEvent +=
                OnCreatorToolsInteractionUnpaused;

            NormalizeCreatorToolsSettings();
            // Preview is a temporary positioning aid, never a persisted
            // overlay state. Recover safely if the game closed while the
            // settings screen was still open.
            if (creatorToolsPreviewSetting.Value)
                creatorToolsPreviewSetting.Value = false;
            StartCreatorToolsServer();
        }

        private bool CanPreloadNativeInteractionAssets()
        {
            if (SceneLoader.CurrentlyLoading)
                return false;

            // The map remains the preferred preload window, but a player may
            // enter a native boss before the serialized cache queue finishes
            // or enable Creator Tools after the fight has already begun.
            // Scoped lifecycle guards make those remaining additive captures
            // safe without requiring a roulette-started session.
            if (CanUseRouletteOnMap())
                return true;
            // Inside gameplay, wait for the same stable, unpaused start gate
            // used by dispatch. This avoids additive scene I/O during the
            // intro, pause, defeat and result transitions.
            return CanSpawnCreatorToolsInteraction();
        }

        private bool CanSpawnCreatorToolsInteraction()
        {
            if (SceneLoader.CurrentlyLoading ||
                creatorToolsInteractionLevelInstanceId < 0 ||
                IsCreatorToolsInteractionPaused() ||
                Mathf.Max(0f, CupheadTime.GlobalSpeed) <= 0f)
                return false;
            Level level;
            if (!TryGetActiveCreatorToolsGameplayLevel(out level))
                return false;
            var now = Time.realtimeSinceStartup;
            if (level.GetInstanceID() !=
                    creatorToolsInteractionLevelInstanceId ||
                now < creatorToolsInteractionAllowedAt)
                return false;

            if (IsCreatorToolsInteractionPhaseTransitionBlocked(level))
                return false;
            return true;
        }

        private bool IsCreatorToolsInteractionPhaseTransitionBlocked(
            Level level)
        {
            if (!creatorToolsInteractionPhaseTransitionBlocked)
                return false;
            if (!creatorToolsInteractionPhaseTransitionProtectionEnabled)
            {
                ResetCreatorToolsInteractionPhaseTransition();
                return false;
            }
            if (level == null ||
                level.GetInstanceID() !=
                    creatorToolsInteractionPhaseTransitionLevelInstanceId)
            {
                ResetCreatorToolsInteractionPhaseTransition();
                return false;
            }

            var elapsed = Mathf.Max(
                0f,
                Time.time -
                    creatorToolsInteractionPhaseTransitionStartedAt);
            if (creatorToolsInteractionPhaseTransitionLastPlayableFrame !=
                Time.frameCount)
            {
                creatorToolsInteractionPhaseTransitionLastPlayableFrame =
                    Time.frameCount;
                creatorToolsInteractionPhaseTransitionPlayableElapsed +=
                    Mathf.Max(0f, Time.unscaledDeltaTime);
            }
            if (!creatorToolsInteractionPhaseTransitionActivated &&
                creatorToolsInteractionPhaseTransitionPlayableElapsed <
                    creatorToolsInteractionPhaseTransitionBlockDelay)
                return false;
            if (!creatorToolsInteractionPhaseTransitionActivated)
            {
                creatorToolsInteractionPhaseTransitionActivated = true;
                if (creatorToolsInteractions != null)
                    creatorToolsInteractions.InvalidateState();
                Logger.LogInfo(
                    "Creator Tools phase-transition dispatch block " +
                    "activated after " +
                    creatorToolsInteractionPhaseTransitionPlayableElapsed
                        .ToString(
                            "0.00", CultureInfo.InvariantCulture) + "s.");
            }

            if (elapsed <=
                CreatorToolsInteractionPhaseTransitionTimeout)
                return true;

            Logger.LogWarning(
                "Creator Tools phase-transition protection timed out; " +
                "interaction dispatch will resume now.");
            ResetCreatorToolsInteractionPhaseTransition();
            return false;
        }

        private bool GetCreatorToolsInteractionPhaseTransitionProtectionEnabled()
        {
            return creatorToolsInteractionPhaseTransitionProtectionEnabled;
        }

        private void SetCreatorToolsInteractionPhaseTransitionProtectionEnabled(
            bool enabled)
        {
            if (creatorToolsInteractionPhaseTransitionProtectionEnabled ==
                enabled)
                return;

            var canceledActiveTransition =
                creatorToolsInteractionPhaseTransitionBlocked && !enabled;
            var elapsed = canceledActiveTransition
                ? Mathf.Max(
                    0f,
                    Time.time -
                        creatorToolsInteractionPhaseTransitionStartedAt)
                : 0f;
            creatorToolsInteractionPhaseTransitionProtectionEnabled = enabled;
            if (!enabled)
                ResetCreatorToolsInteractionPhaseTransition();
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.InvalidateState();

            Logger.LogInfo(
                "Creator Tools phase-transition protection " +
                (enabled ? "enabled." : "disabled for this session.") +
                (canceledActiveTransition
                    ? " Active protection canceled after " +
                        elapsed.ToString(
                            "0.00", CultureInfo.InvariantCulture) + "s."
                    : string.Empty));
        }

        private void BeginCreatorToolsInteractionPhaseTransition(
            Level level,
            string transition,
            string signal,
            float blockDelay)
        {
            if (!creatorToolsInteractionPhaseTransitionProtectionEnabled ||
                level == null ||
                level.GetInstanceID() !=
                    creatorToolsInteractionLevelInstanceId)
                return;
            var instanceId = level.GetInstanceID();
            if (creatorToolsInteractionPhaseTransitionBlocked &&
                creatorToolsInteractionPhaseTransitionLevelInstanceId ==
                    instanceId)
                return;

            creatorToolsInteractionPhaseTransitionBlocked = true;
            creatorToolsInteractionPhaseTransitionActivated = false;
            creatorToolsInteractionPhaseTransitionActorsCleared = false;
            creatorToolsInteractionPhaseTransitionLevelInstanceId = instanceId;
            creatorToolsInteractionPhaseTransitionStartedAt = Time.time;
            creatorToolsInteractionPhaseTransitionBlockDelay =
                Mathf.Max(0f, blockDelay);
            creatorToolsInteractionPhaseTransitionPlayableElapsed = 0f;
            creatorToolsInteractionPhaseTransitionLastPlayableFrame =
                Time.frameCount;
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.InvalidateState();
            Logger.LogInfo(
                "Creator Tools phase-transition protection signaled: " +
                transition + " at " + signal +
                "; dispatch remains active for " +
                creatorToolsInteractionPhaseTransitionBlockDelay.ToString(
                    "0.00", CultureInfo.InvariantCulture) + "s.");
        }

        private void ClearCreatorToolsInteractionPhaseTransitionActors(
            Level level,
            string transition,
            string signal)
        {
            if (!creatorToolsInteractionPhaseTransitionBlocked ||
                creatorToolsInteractionPhaseTransitionActorsCleared ||
                level == null ||
                level.GetInstanceID() !=
                    creatorToolsInteractionPhaseTransitionLevelInstanceId)
                return;

            if (!creatorToolsInteractionPhaseTransitionActivated)
            {
                creatorToolsInteractionPhaseTransitionActivated = true;
                if (creatorToolsInteractions != null)
                    creatorToolsInteractions.InvalidateState();
            }
            creatorToolsInteractionPhaseTransitionActorsCleared = true;
            var elapsed = Mathf.Max(
                0f,
                Time.time -
                    creatorToolsInteractionPhaseTransitionStartedAt);
            var cleared = creatorToolsInteractions == null
                ? 0
                : creatorToolsInteractions.ClearActiveForPhaseTransition();
            Logger.LogInfo(
                "Creator Tools cleared " + cleared +
                " active interaction actor(s) for " + transition +
                " at " + signal + " after " + elapsed.ToString(
                    "0.00", CultureInfo.InvariantCulture) + "s.");
        }

        private void EndCreatorToolsInteractionPhaseTransition(
            Level level,
            string transition,
            string signal)
        {
            if (!creatorToolsInteractionPhaseTransitionBlocked ||
                level == null ||
                level.GetInstanceID() !=
                    creatorToolsInteractionPhaseTransitionLevelInstanceId)
                return;

            var elapsed = Mathf.Max(
                0f,
                Time.time -
                    creatorToolsInteractionPhaseTransitionStartedAt);
            ResetCreatorToolsInteractionPhaseTransition();
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.InvalidateState();
            Logger.LogInfo(
                "Creator Tools phase-transition protection ended: " +
                transition + " at " + signal + " after " +
                elapsed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "s.");
        }

        private void ResetCreatorToolsInteractionPhaseTransition()
        {
            creatorToolsInteractionPhaseTransitionBlocked = false;
            creatorToolsInteractionPhaseTransitionActivated = false;
            creatorToolsInteractionPhaseTransitionActorsCleared = false;
            creatorToolsInteractionPhaseTransitionLevelInstanceId = -1;
            creatorToolsInteractionPhaseTransitionStartedAt =
                float.PositiveInfinity;
            creatorToolsInteractionPhaseTransitionBlockDelay = 0f;
            creatorToolsInteractionPhaseTransitionPlayableElapsed = 0f;
            creatorToolsInteractionPhaseTransitionLastPlayableFrame = -1;
        }

        private static bool TryGetActiveCreatorToolsGameplayLevel(
            out Level level)
        {
            level = null;
            if (SceneLoader.CurrentlyLoading)
                return false;
            try
            {
                level = Level.Current;
                return level != null && !level.Ending &&
                    (level.LevelType == Level.Type.Battle ||
                     level.LevelType == Level.Type.Platforming);
            }
            catch
            {
                level = null;
                return false;
            }
        }

        private void RefreshCreatorToolsInteractionGameplayLevel()
        {
            Level level;
            if (!TryGetActiveCreatorToolsGameplayLevel(out level))
                return;
            RegisterCreatorToolsInteractionGameplayLevel(level, false);
        }

        private void RegisterCreatorToolsInteractionGameplayLevel(
            Level level,
            bool rearmExistingLevel)
        {
            if (level == null)
                return;
            var instanceId = level.GetInstanceID();
            var sameLevel =
                creatorToolsInteractionLevelInstanceId == instanceId;
            if (sameLevel && !rearmExistingLevel)
            {
                if (creatorToolsInteractionLevelStartObserved &&
                    creatorToolsInteractions != null)
                {
                    creatorToolsInteractionLevelStartObserved = false;
                    creatorToolsInteractions.ConfirmGameplayLevelStart();
                }
                return;
            }
            if (sameLevel && rearmExistingLevel &&
                creatorToolsInteractions != null &&
                creatorToolsInteractions.GameplayLevelActive)
            {
                creatorToolsInteractionLevelStartObserved = false;
                creatorToolsInteractions.ConfirmGameplayLevelStart();
                return;
            }

            CreatorToolsInteractionPresentation.ClearLevelEndSnapshots();

            var shouldClearPreviousAttempt =
                (!sameLevel &&
                 creatorToolsInteractionLevelInstanceId >= 0) ||
                (sameLevel && rearmExistingLevel);
            if (shouldClearPreviousAttempt &&
                creatorToolsInteractions != null)
                creatorToolsInteractions.EndGameplayLevel();
            creatorToolsInteractionLevelInstanceId = instanceId;
            creatorToolsInteractionAllowedAt = Time.realtimeSinceStartup;
            ResetCreatorToolsInteractionPhaseTransition();
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.BeginGameplayLevel(
                    rearmExistingLevel ||
                    creatorToolsInteractionLevelStartObserved);
            creatorToolsInteractionLevelStartObserved = false;
            Logger.LogInfo(
                "Creator Tools interactions registered gameplay level " +
                level.CurrentLevel + ".");
        }

        private static bool IsCreatorToolsInteractionPaused()
        {
            try
            {
                LevelPauseGUI pauseGui;
                return TryGetActiveLevelPauseMenu(out pauseGui);
            }
            catch
            {
                return false;
            }
        }

        private void InstallCreatorToolsPatches()
        {
            InstallCreatorToolsMenuPatches();
            NativeZeppelinCache.InstallLifecyclePatches(
                harmony,
                delegate(string message) { Logger.LogWarning(message); });
            CreatorToolsZeppelinProjectilePresentation.InstallPatches(
                harmony,
                delegate(string message) { Logger.LogWarning(message); });
            NativeHomingCarrotCache.InstallLifecyclePatches(
                harmony,
                delegate(string message) { Logger.LogWarning(message); });
            NativeCagneyHomingPlantCache.InstallLifecyclePatches(
                harmony,
                delegate(string message) { Logger.LogWarning(message); });
            NativeFrogsFireflyCache.InstallLifecyclePatches(
                harmony,
                delegate(string message) { Logger.LogWarning(message); });
            InstallCreatorToolsGameplayLoadPatch();
            InstallCreatorToolsPhaseTransitionPatches();

            var levelStarted = HarmonyLib.AccessTools.Method(
                typeof(Level), "_OnLevelStart");
            var levelStartedPostfix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsInteractionLevelStartedPostfix");
            if (levelStarted != null && levelStartedPostfix != null)
                harmony.Patch(
                    levelStarted,
                    postfix: new HarmonyLib.HarmonyMethod(
                        levelStartedPostfix));
            else
                Logger.LogWarning(
                    "Could not install the Creator Tools interaction start guard.");

            var levelEnded = HarmonyLib.AccessTools.Method(
                typeof(Level), "_OnLevelEnd");
            var levelDestroyed = HarmonyLib.AccessTools.Method(
                typeof(Level), "OnDestroy");
            var levelEndedPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsInteractionLevelEndedPrefix");
            var levelDestroyedPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsInteractionLevelDestroyedPrefix");
            if (levelEnded != null && levelEndedPrefix != null)
                harmony.Patch(
                    levelEnded,
                    prefix: new HarmonyLib.HarmonyMethod(
                        levelEndedPrefix));
            else
                Logger.LogWarning(
                    "Could not install the Creator Tools interaction end guard.");

            if (levelDestroyed != null && levelDestroyedPrefix != null)
                harmony.Patch(
                    levelDestroyed,
                    prefix: new HarmonyLib.HarmonyMethod(
                        levelDestroyedPrefix));
            else
                Logger.LogWarning(
                    "Could not install the Creator Tools interaction scene cleanup.");
        }

        private void InstallCreatorToolsGameplayLoadPatch()
        {
            var loadLevel = HarmonyLib.AccessTools.Method(
                typeof(SceneLoader),
                "LoadLevel",
                new[]
                {
                    typeof(Levels),
                    typeof(SceneLoader.Transition),
                    typeof(SceneLoader.Icon),
                    typeof(SceneLoader.Context)
                });
            var loadLevelPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsGameplayLevelLoadPrefix");
            if (loadLevel == null || loadLevelPrefix == null)
            {
                Logger.LogWarning(
                    "Could not install the Creator Tools gameplay-load " +
                    "status hook.");
                return;
            }
            harmony.Patch(
                loadLevel,
                prefix: new HarmonyLib.HarmonyMethod(loadLevelPrefix));
        }

        private static void CreatorToolsGameplayLevelLoadPrefix()
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.BeginCreatorToolsInteractionGameplayLevelLoad(
                    "SceneLoader.LoadLevel");
        }

        private void BeginCreatorToolsInteractionGameplayLevelLoad(
            string source)
        {
            if (creatorToolsInteractions == null ||
                !creatorToolsInteractions.BeginGameplayLevelLoad())
                return;
            creatorToolsInteractionLevelStartObserved = false;
            Logger.LogInfo(
                "Creator Tools interactions are starting a battle from " +
                source + ".");
        }

        private void CancelCreatorToolsInteractionGameplayLevelLoad()
        {
            creatorToolsInteractionLevelStartObserved = false;
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.CancelGameplayLevelLoad();
        }

        private void InstallCreatorToolsPhaseTransitionPatches()
        {
            var devilTransitionStart = HarmonyLib.AccessTools.Method(
                typeof(DevilLevelSittingDevil), "StartTransform");
            var devilTransitionCommit = HarmonyLib.AccessTools.Method(
                typeof(DevilLevel), "ZoomOut");
            var devilInputRestoreIterator = HarmonyLib.AccessTools.Inner(
                typeof(DevilLevel), "<disable_input_cr>c__Iterator3");
            var devilTransitionEnd = devilInputRestoreIterator == null
                ? null
                : HarmonyLib.AccessTools.Method(
                    devilInputRestoreIterator, "MoveNext");
            var saltbakerPhaseOneStart = HarmonyLib.AccessTools.Method(
                typeof(SaltbakerLevelSaltbaker), "phase_one_to_two_cr");
            var saltbakerPhaseOneCommit = HarmonyLib.AccessTools.Method(
                typeof(SaltbakerLevelSaltbaker), "AniEvent_HandsClosed");
            var saltbakerPhaseOneEnd = HarmonyLib.AccessTools.Method(
                typeof(SaltbakerLevelSaltbaker), "AniEvent_RestorePlayers");
            var saltbakerPhaseTwoStart = HarmonyLib.AccessTools.Method(
                typeof(SaltbakerLevelSaltbaker), "OnPhaseThree");
            var saltbakerPhaseTwoEndIterator = HarmonyLib.AccessTools.Inner(
                typeof(SaltbakerLevel),
                "<phase_two_to_three_cr>c__Iterator0");
            var saltbakerPhaseTwoEnd = saltbakerPhaseTwoEndIterator == null
                ? null
                : HarmonyLib.AccessTools.Method(
                    saltbakerPhaseTwoEndIterator, "MoveNext");
            var oldManPhaseThreeStart = HarmonyLib.AccessTools.Method(
                typeof(OldManLevelSockPuppetHandler), "OnPhase3");
            var oldManPhaseThreeEndIterator = HarmonyLib.AccessTools.Inner(
                typeof(OldManLevel), "<phase_3_trans_cr>c__Iterator9");
            var oldManPhaseThreeEnd = oldManPhaseThreeEndIterator == null
                ? null
                : HarmonyLib.AccessTools.Method(
                    oldManPhaseThreeEndIterator, "MoveNext");
            var startPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsDevilTransitionStartPrefix");
            var commitPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsDevilTransitionCommitPrefix");
            var endPostfix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsDevilInputRestorePostfix");
            var saltbakerStartPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsSaltbakerPhaseOneStartPrefix");
            var saltbakerCommitPostfix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsSaltbakerPhaseOneCommitPostfix");
            var saltbakerEndPostfix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsSaltbakerPhaseOneEndPostfix");
            var saltbakerPhaseTwoStartPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsSaltbakerPhaseTwoStartPrefix");
            var saltbakerPhaseTwoEndPostfix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsSaltbakerPhaseTwoEndPostfix");
            var oldManPhaseThreeStartPrefix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsOldManPhaseThreeStartPrefix");
            var oldManPhaseThreeEndPostfix = HarmonyLib.AccessTools.Method(
                typeof(Plugin),
                "CreatorToolsOldManPhaseThreeEndPostfix");

            if (devilTransitionStart == null ||
                devilTransitionCommit == null ||
                devilTransitionEnd == null ||
                startPrefix == null || commitPrefix == null ||
                endPostfix == null)
            {
                Logger.LogWarning(
                    "Could not install the Creator Tools Devil phase " +
                    "transition protection.");
                return;
            }

            harmony.Patch(
                devilTransitionStart,
                prefix: new HarmonyLib.HarmonyMethod(startPrefix));
            harmony.Patch(
                devilTransitionCommit,
                prefix: new HarmonyLib.HarmonyMethod(commitPrefix));
            harmony.Patch(
                devilTransitionEnd,
                postfix: new HarmonyLib.HarmonyMethod(endPostfix));

            if (saltbakerPhaseOneStart == null ||
                saltbakerPhaseOneCommit == null ||
                saltbakerPhaseOneEnd == null ||
                saltbakerStartPrefix == null ||
                saltbakerCommitPostfix == null ||
                saltbakerEndPostfix == null)
            {
                Logger.LogWarning(
                    "Could not install the Creator Tools Saltbaker phase " +
                    "1 to 2 transition protection.");
                return;
            }

            harmony.Patch(
                saltbakerPhaseOneStart,
                prefix: new HarmonyLib.HarmonyMethod(
                    saltbakerStartPrefix));
            harmony.Patch(
                saltbakerPhaseOneCommit,
                postfix: new HarmonyLib.HarmonyMethod(
                    saltbakerCommitPostfix));
            harmony.Patch(
                saltbakerPhaseOneEnd,
                postfix: new HarmonyLib.HarmonyMethod(
                    saltbakerEndPostfix));

            if (saltbakerPhaseTwoStart == null ||
                saltbakerPhaseTwoEnd == null ||
                saltbakerPhaseTwoStartPrefix == null ||
                saltbakerPhaseTwoEndPostfix == null)
            {
                Logger.LogWarning(
                    "Could not install the Creator Tools Saltbaker phase " +
                    "2 to 3 transition protection.");
                return;
            }

            harmony.Patch(
                saltbakerPhaseTwoStart,
                prefix: new HarmonyLib.HarmonyMethod(
                    saltbakerPhaseTwoStartPrefix));
            harmony.Patch(
                saltbakerPhaseTwoEnd,
                postfix: new HarmonyLib.HarmonyMethod(
                    saltbakerPhaseTwoEndPostfix));

            if (oldManPhaseThreeStart == null ||
                oldManPhaseThreeEnd == null ||
                oldManPhaseThreeStartPrefix == null ||
                oldManPhaseThreeEndPostfix == null)
            {
                Logger.LogWarning(
                    "Could not install the Creator Tools Glumstone phase " +
                    "2 to 3 transition protection.");
                return;
            }

            harmony.Patch(
                oldManPhaseThreeStart,
                prefix: new HarmonyLib.HarmonyMethod(
                    oldManPhaseThreeStartPrefix));
            harmony.Patch(
                oldManPhaseThreeEnd,
                postfix: new HarmonyLib.HarmonyMethod(
                    oldManPhaseThreeEndPostfix));
        }

        private static void CreatorToolsDevilTransitionStartPrefix()
        {
            var plugin = activeInstance;
            DevilLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsDevilLevel(out level))
                return;
            plugin.BeginCreatorToolsInteractionPhaseTransition(
                level,
                "Devil phase 1 to 2",
                "StartTransform",
                CreatorToolsDevilPhaseTransitionBlockDelay);
        }

        private static void CreatorToolsDevilTransitionCommitPrefix(
            DevilLevel __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null)
                return;
            plugin.ClearCreatorToolsInteractionPhaseTransitionActors(
                __instance, "Devil phase 1 to 2", "ZoomOut");
        }

        private static void CreatorToolsDevilInputRestorePostfix(
            bool __result)
        {
            if (__result)
                return;
            var plugin = activeInstance;
            DevilLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsDevilLevel(out level))
                return;
            plugin.EndCreatorToolsInteractionPhaseTransition(
                level,
                "Devil phase 1 to 2",
                "disable_input_cr completion");
        }

        private static void CreatorToolsSaltbakerPhaseOneStartPrefix()
        {
            var plugin = activeInstance;
            SaltbakerLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsSaltbakerLevel(out level))
                return;
            plugin.BeginCreatorToolsInteractionPhaseTransition(
                level,
                "Saltbaker phase 1 to 2",
                "phase_one_to_two_cr",
                CreatorToolsSaltbakerPhaseOneBlockDelay);
        }

        private static void CreatorToolsSaltbakerPhaseOneCommitPostfix()
        {
            var plugin = activeInstance;
            SaltbakerLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsSaltbakerLevel(out level))
                return;
            plugin.ClearCreatorToolsInteractionPhaseTransitionActors(
                level,
                "Saltbaker phase 1 to 2",
                "AniEvent_HandsClosed");
        }

        private static void CreatorToolsSaltbakerPhaseOneEndPostfix()
        {
            var plugin = activeInstance;
            SaltbakerLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsSaltbakerLevel(out level))
                return;
            plugin.EndCreatorToolsInteractionPhaseTransition(
                level,
                "Saltbaker phase 1 to 2",
                "AniEvent_RestorePlayers");
        }

        private static void CreatorToolsSaltbakerPhaseTwoStartPrefix()
        {
            var plugin = activeInstance;
            SaltbakerLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsSaltbakerLevel(out level))
                return;
            plugin.BeginCreatorToolsInteractionPhaseTransition(
                level,
                "Saltbaker phase 2 to 3",
                "OnPhaseThree after KillFires",
                0f);
            plugin.ClearCreatorToolsInteractionPhaseTransitionActors(
                level,
                "Saltbaker phase 2 to 3",
                "OnPhaseThree after KillFires");
        }

        private static void CreatorToolsSaltbakerPhaseTwoEndPostfix(
            bool __result)
        {
            if (__result)
                return;
            var plugin = activeInstance;
            SaltbakerLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsSaltbakerLevel(out level))
                return;
            plugin.EndCreatorToolsInteractionPhaseTransition(
                level,
                "Saltbaker phase 2 to 3",
                "phase_two_to_three_cr completion");
        }

        private static void CreatorToolsOldManPhaseThreeStartPrefix()
        {
            var plugin = activeInstance;
            OldManLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsOldManLevel(out level))
                return;
            plugin.BeginCreatorToolsInteractionPhaseTransition(
                level,
                "Glumstone phase 2 to 3",
                "SockPuppetHandler.OnPhase3",
                0f);
            plugin.ClearCreatorToolsInteractionPhaseTransitionActors(
                level,
                "Glumstone phase 2 to 3",
                "SockPuppetHandler.OnPhase3");
        }

        private static void CreatorToolsOldManPhaseThreeEndPostfix(
            bool __result)
        {
            if (__result)
                return;
            var plugin = activeInstance;
            OldManLevel level;
            if (plugin == null ||
                !TryGetCurrentCreatorToolsOldManLevel(out level))
                return;
            plugin.EndCreatorToolsInteractionPhaseTransition(
                level,
                "Glumstone phase 2 to 3",
                "phase_3_trans_cr completion");
        }

        private static bool TryGetCurrentCreatorToolsOldManLevel(
            out OldManLevel level)
        {
            level = null;
            try
            {
                level = Level.Current as OldManLevel;
                return level != null;
            }
            catch
            {
                level = null;
                return false;
            }
        }

        private static bool TryGetCurrentCreatorToolsSaltbakerLevel(
            out SaltbakerLevel level)
        {
            level = null;
            try
            {
                level = Level.Current as SaltbakerLevel;
                return level != null;
            }
            catch
            {
                level = null;
                return false;
            }
        }

        private static bool TryGetCurrentCreatorToolsDevilLevel(
            out DevilLevel level)
        {
            level = null;
            try
            {
                level = Level.Current as DevilLevel;
                return level != null;
            }
            catch
            {
                level = null;
                return false;
            }
        }

        private static void CreatorToolsInteractionLevelStartedPostfix(
            Level __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null)
                return;
            if (__instance.LevelType != Level.Type.Battle &&
                __instance.LevelType != Level.Type.Platforming)
                return;
            plugin.creatorToolsInteractionLevelStartObserved = true;

            Level current;
            try { current = Level.Current; }
            catch { return; }
            if (current == null || current != __instance)
                return;

            plugin.RegisterCreatorToolsInteractionGameplayLevel(
                __instance, true);
        }

        private static void CreatorToolsInteractionLevelEndedPrefix(
            Level __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                plugin.creatorToolsInteractionLevelInstanceId !=
                    __instance.GetInstanceID())
                return;

            plugin.creatorToolsInteractionAllowedAt =
                float.PositiveInfinity;
            plugin.creatorToolsInteractionLevelStartObserved = false;
            plugin.ResetCreatorToolsInteractionPhaseTransition();
            CreatorToolsInteractionPresentation.FreezeActorsForLevelEnd(
                __instance,
                delegate(string message)
                {
                    plugin.Logger.LogWarning(message);
                });
            if (plugin.creatorToolsInteractions != null)
                plugin.creatorToolsInteractions.SuspendGameplayLevel();
        }

        private static void CreatorToolsInteractionLevelDestroyedPrefix(
            Level __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null ||
                plugin.creatorToolsInteractionLevelInstanceId !=
                    __instance.GetInstanceID())
                return;

            plugin.creatorToolsInteractionLevelInstanceId = -1;
            plugin.creatorToolsInteractionAllowedAt =
                float.PositiveInfinity;
            plugin.creatorToolsInteractionLevelStartObserved = false;
            plugin.ResetCreatorToolsInteractionPhaseTransition();
            if (plugin.creatorToolsInteractions != null)
                plugin.creatorToolsInteractions.EndGameplayLevel();
        }

        private void OnCreatorToolsInteractionPaused()
        {
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.InvalidateState();
        }

        private void OnCreatorToolsInteractionUnpaused()
        {
            if (creatorToolsInteractions != null)
                creatorToolsInteractions.InvalidateState();
        }

        private void NormalizeCreatorToolsSettings()
        {
            var scale = creatorToolsScaleSetting.Value;
            creatorToolsScaleSetting.Value = scale < 1.25f
                ? 1f
                : scale < 1.75f ? 1.5f : 2f;
            var opacity = creatorToolsOpacitySetting.Value;
            opacity = Mathf.Clamp(opacity, 25, 100);
            opacity = Mathf.RoundToInt(opacity / 5f) * 5;
            creatorToolsOpacitySetting.Value = opacity;
            SetCreatorToolsInteractionMaximumActive(
                GetCreatorToolsInteractionMaximumActive());
        }

        private int GetCreatorToolsInteractionMaximumActive()
        {
            if (creatorToolsInteractionMaximumActiveSetting == null)
                return 1;
            return Mathf.Clamp(
                creatorToolsInteractionMaximumActiveSetting.Value,
                1,
                CreatorToolsInteractionController.MaximumActiveLimit);
        }

        private void SetCreatorToolsInteractionMaximumActive(int value)
        {
            if (creatorToolsInteractionMaximumActiveSetting == null)
                return;
            var normalized = Mathf.Clamp(
                value,
                1,
                CreatorToolsInteractionController.MaximumActiveLimit);
            if (creatorToolsInteractionMaximumActiveSetting.Value !=
                normalized)
                creatorToolsInteractionMaximumActiveSetting.Value =
                    normalized;
        }

        private bool StartCreatorToolsServer()
        {
            creatorToolsServerError = null;
            if (creatorToolsServer == null)
            {
                creatorToolsServer = new CreatorToolsServer(
                    AssetsDirectory,
                    delegate(string message) { Logger.LogInfo(message); },
                    delegate(string message) { Logger.LogWarning(message); });
            }
            if (creatorToolsServer.IsRunning)
                return true;

            if (!creatorToolsServer.Start(CreatorToolsDefaultPort))
            {
                creatorToolsServerError =
                    "EL PUERTO 18081 ESTÁ OCUPADO";
                Logger.LogWarning(
                    "Creator Tools requires fixed port 18081, but it is " +
                    "already in use. Close the application using the port " +
                    "and try again.");
                return false;
            }

            PublishCreatorToolsState(true);
            PublishCreatorToolsForceConfig(true);
            return true;
        }

        private void StopCreatorToolsServer()
        {
            if (creatorToolsServer != null)
                creatorToolsServer.Stop();
            creatorToolsLastPublishedState = null;
        }

        private void SetCreatorToolsEnabled(bool enabled)
        {
            if (creatorToolsEnabledSetting.Value != enabled)
                creatorToolsEnabledSetting.Value = enabled;
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning)
                StartCreatorToolsServer();
            PublishCreatorToolsState(true);
        }

        private bool SetCreatorToolsPreview(bool enabled)
        {
            // Preview is visual overlay output, so enabling it also enables
            // the overlay. The local server itself remains independent.
            if (enabled)
            {
                if (!creatorToolsEnabledSetting.Value)
                    SetCreatorToolsEnabled(true);
                else if (creatorToolsServer == null ||
                         !creatorToolsServer.IsRunning)
                    StartCreatorToolsServer();
                if (creatorToolsServer == null ||
                    !creatorToolsServer.IsRunning)
                    enabled = false;
            }

            creatorToolsPreviewSetting.Value = enabled;
            PublishCreatorToolsState(true);
            return creatorToolsPreviewSetting.Value;
        }

        private void UpdateCreatorTools()
        {
            if (creatorToolsEnabledSetting == null)
                return;

            if ((creatorToolsServer == null ||
                 !creatorToolsServer.IsRunning) &&
                string.IsNullOrEmpty(creatorToolsServerError))
                StartCreatorToolsServer();

            UpdateCreatorToolsChallengeLabel();
            UpdateCreatorToolsForceConfig();
            if (creatorToolsDashboard != null)
                creatorToolsDashboard.Update(creatorToolsServer);
            if (creatorToolsStreamRules != null)
                creatorToolsStreamRules.Update(creatorToolsServer);
            if (creatorToolsInteractions != null)
            {
                // `_OnLevelStart` can precede a stable `Level.Current` on
                // native entry paths. Polling the authoritative current level
                // makes normal boss entrances as reliable as roulette loads.
                RefreshCreatorToolsInteractionGameplayLevel();
                creatorToolsInteractions.Update(creatorToolsServer);
            }
        }

        private void DisposeCreatorTools()
        {
            LevelPauseGUI.OnPauseEvent -=
                OnCreatorToolsInteractionPaused;
            LevelPauseGUI.OnUnpauseEvent -=
                OnCreatorToolsInteractionUnpaused;
            creatorToolsDashboard = null;
            creatorToolsStreamRules = null;
            if (creatorToolsInteractions != null)
            {
                creatorToolsInteractions.Dispose();
                creatorToolsInteractions = null;
            }
            CreatorToolsInteractionPresentation.ClearLevelEndSnapshots();
            if (creatorToolsServer == null)
                return;
            creatorToolsServer.Dispose();
            creatorToolsServer = null;
        }

        private string CreatorToolsUrl
        {
            get
            {
                return "http://127.0.0.1:" +
                    CreatorToolsDefaultPort + "/";
            }
        }

        private void BeginCreatorToolsBattleSession()
        {
            creatorToolsBattleSessionActive = true;
            creatorToolsBattleCompleted = false;
            creatorToolsBattleVisible = false;
            creatorToolsBattleSessionId++;
            creatorToolsRevealedIcons = 0;
            creatorToolsTextVisible = false;
            creatorToolsLabelKey = null;
            if (creatorToolsPreviewSetting != null &&
                creatorToolsPreviewSetting.Value)
                creatorToolsPreviewSetting.Value = false;
            PublishCreatorToolsState(true);
        }

        private void SetCreatorToolsBattleVisibility(bool visible)
        {
            // In streaming mode the overlay is a persistent broadcast panel:
            // temporary battle/HUD gaps (defeat, retry and scene hand-offs)
            // must not play its exit animation. The definitive session end
            // still hides it on victory or when returning to the map.
            if (!visible && creatorToolsBattleSessionActive &&
                CreatorToolsKeepOverlayAcrossRetries)
                return;
            if (visible && creatorToolsBattleCompleted)
                return;
            if (!creatorToolsBattleSessionActive ||
                creatorToolsBattleVisible == visible)
                return;
            creatorToolsBattleVisible = visible;
            PublishCreatorToolsState(false);
        }

        private void ResetCreatorToolsBattleRevealForReappear()
        {
            if (!creatorToolsBattleSessionActive ||
                CreatorToolsKeepOverlayAcrossRetries)
                return;

            // The replacement scene restarts the native HUD reveal at zero.
            // Publish that same reset before hiding so the browser cannot
            // begin with the completed count from the previous scene.
            creatorToolsRevealedIcons = 0;
            creatorToolsTextVisible = false;
        }

        private void CompleteCreatorToolsBattleForLogo()
        {
            if (!creatorToolsBattleSessionActive ||
                creatorToolsBattleCompleted ||
                !battleHudPresentationActive ||
                (!battleHudFollowNativeVictoryLayer &&
                 !battleHudHoldOverlayThroughVictory))
                return;

            // WinScreen has started. Keep the native/loadout session alive,
            // but finish the external battle so rating can show the idle logo.
            creatorToolsBattleCompleted = true;
            creatorToolsBattleVisible = false;
            PublishCreatorToolsState(false);
        }

        private void UpdateCreatorToolsBattleReveal(
            int revealedIcons, bool textVisible)
        {
            if (!creatorToolsBattleSessionActive)
                return;
            if (creatorToolsBattleCompleted)
                return;
            if (CreatorToolsKeepOverlayAcrossRetries)
            {
                // BattleResultHud resets its reveal counter while retrying.
                // Never let those transient resets retract an overlay that
                // has already entered; fresh sessions still begin at zero.
                revealedIcons = Math.Max(
                    creatorToolsRevealedIcons, revealedIcons);
                textVisible = creatorToolsTextVisible || textVisible;
            }
            if (creatorToolsRevealedIcons == revealedIcons &&
                creatorToolsTextVisible == textVisible)
                return;
            creatorToolsRevealedIcons = revealedIcons;
            creatorToolsTextVisible = textVisible;
            PublishCreatorToolsState(false);
        }

        private bool CreatorToolsKeepOverlayAcrossRetries
        {
            get
            {
                return creatorToolsRetryBehaviorSetting != null &&
                       creatorToolsRetryBehaviorSetting.Value ==
                       CreatorToolsRetryBehavior.Keep;
            }
        }

        private void EndCreatorToolsBattleSession()
        {
            creatorToolsBattleSessionActive = false;
            creatorToolsBattleCompleted = false;
            creatorToolsBattleVisible = false;
            creatorToolsRevealedIcons = 0;
            creatorToolsTextVisible = false;
            creatorToolsLabelKey = null;
            PublishCreatorToolsState(true);
            PublishCreatorToolsForceConfig(true);
        }

        private void CreatorToolsLanguageChanged()
        {
            creatorToolsLabelKey = null;
            PublishCreatorToolsState(true);
            PublishCreatorToolsForceConfig(true);
            RefreshCreatorToolsMenuLocalization();
        }

        private void PublishCreatorToolsState(bool force)
        {
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning)
                return;

            var json = BuildCreatorToolsStateJson();
            if (!force && json == creatorToolsLastPublishedState)
                return;
            creatorToolsLastPublishedState = json;
            creatorToolsServer.Publish(json);
        }

        private string BuildCreatorToolsStateJson()
        {
            var enabled = creatorToolsEnabledSetting != null &&
                          creatorToolsEnabledSetting.Value;
            var preview = enabled &&
                          !creatorToolsBattleSessionActive &&
                          creatorToolsPreviewSetting.Value;
            var visible = enabled && creatorToolsBattleSessionActive
                ? creatorToolsBattleVisible && !creatorToolsBattleCompleted
                : preview;
            var icons = preview
                ? CreatorToolsPreviewIcons()
                : CreatorToolsBattleIcons();
            var challengeText = preview
                ? LocalizedChallengeLabel(ModifierId.NoDash)
                    .ToUpperInvariant()
                : CreatorToolsBattleChallengeText();
            var revealed = preview
                ? icons.Count
                : creatorToolsRevealedIcons;
            var textVisible = preview || creatorToolsTextVisible;
            var session = preview
                ? -1
                : creatorToolsBattleSessionId;
            var labelRevision = preview
                ? 0
                : creatorToolsLabelRevision;
            var battleActive = creatorToolsBattleSessionActive &&
                               !creatorToolsBattleCompleted;
            var completeRetryExit =
                battleActive &&
                !creatorToolsBattleVisible &&
                battleHudExplicitRestartRequested &&
                creatorToolsRetryBehaviorSetting != null &&
                creatorToolsRetryBehaviorSetting.Value ==
                CreatorToolsRetryBehavior.Reappear;
            var fastRetryExit = completeRetryExit &&
                                !BattleHudUsesPlaneLoadout();

            var builder = new StringBuilder(512);
            builder.Append("{\"type\":\"state\",\"active\":")
                .Append(enabled ? "true" : "false");
            builder.Append(",\"battleActive\":").Append(
                battleActive ? "true" : "false");
            builder.Append(",\"fastRetryExit\":").Append(
                fastRetryExit ? "true" : "false");
            builder.Append(",\"completeExit\":").Append(
                completeRetryExit ? "true" : "false");
            builder.Append(",\"visible\":").Append(
                visible ? "true" : "false");
            builder.Append(",\"preview\":").Append(
                preview ? "true" : "false");
            builder.Append(",\"session\":").Append(session);
            builder.Append(",\"revealed\":").Append(revealed);
            builder.Append(",\"textVisible\":").Append(
                textVisible ? "true" : "false");
            builder.Append(",\"challengeText\":\"")
                .Append(EscapeJson(challengeText)).Append("\"");
            builder.Append(",\"labelRevision\":")
                .Append(labelRevision);
            builder.Append(",\"icons\":[");
            for (var i = 0; i < icons.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append('"').Append(EscapeJson(icons[i])).Append('"');
            }
            builder.Append(']');
            builder.Append(",\"settings\":{");
            builder.Append("\"scale\":")
                .Append(creatorToolsScaleSetting.Value.ToString(
                    "0.0#", CultureInfo.InvariantCulture));
            builder.Append(",\"textFirst\":").Append(
                creatorToolsOrderSetting.Value ==
                CreatorToolsOrder.TextAbove ? "true" : "false");
            builder.Append(",\"alignment\":\"")
                .Append(CreatorToolsAlignmentValue()).Append("\"");
            builder.Append(",\"opacity\":")
                .Append((creatorToolsOpacitySetting.Value / 100f)
                    .ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(",\"logo\":").Append(
                creatorToolsLogoSetting.Value ? "true" : "false");
            builder.Append("}}");
            return builder.ToString();
        }

        private List<string> CreatorToolsPreviewIcons()
        {
            return new List<string>
            {
                RouletteData.Weapons[0].Image,
                RouletteData.Weapons[1].Image,
                RouletteData.Supers[0].Image,
                RouletteData.Charms[0].Image,
                RouletteData.Modifiers[0].Image
            };
        }

        private List<string> CreatorToolsBattleIcons()
        {
            var icons = new List<string>();
            var snapshot = battleHudResultSnapshot;
            if (snapshot == null || snapshot.Boss < 0 ||
                snapshot.Boss >= RouletteData.Bosses.Length)
                return icons;

            var boss = RouletteData.Bosses[snapshot.Boss];
            if (!boss.IsPlane)
            {
                icons.Add(RouletteData.Weapons[ClampIndex(
                    snapshot.Weapon1, RouletteData.Weapons.Length)].Image);
                icons.Add(RouletteData.Weapons[ClampIndex(
                    snapshot.Weapon2, RouletteData.Weapons.Length)].Image);
                icons.Add(RouletteData.Supers[ClampIndex(
                    snapshot.Super, RouletteData.Supers.Length)].Image);
            }
            icons.Add(RouletteData.Charms[ClampIndex(
                snapshot.Charm, RouletteData.Charms.Length)].Image);
            icons.Add(CreatorToolsChallengeIcon(snapshot));
            return icons;
        }

        private string CreatorToolsChallengeIcon(RouletteResult snapshot)
        {
            if (battleHudChallengeSnapshot == ModifierId.None)
                return "weapons/vacio.png";
            if (snapshot.Modifier >= 0 &&
                snapshot.Modifier < RouletteData.Modifiers.Length)
                return RouletteData.Modifiers[snapshot.Modifier].Image;
            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                if (RouletteData.Modifiers[i].Id ==
                    battleHudChallengeSnapshot)
                    return RouletteData.Modifiers[i].Image;
            }
            return "weapons/vacio.png";
        }

        private string CreatorToolsBattleChallengeText()
        {
            return battleHudChallengeSnapshot == ModifierId.None
                ? string.Empty
                : LocalizedChallengeLabel(battleHudChallengeSnapshot)
                    .ToUpperInvariant();
        }

        private string CreatorToolsAlignmentValue()
        {
            if (creatorToolsAlignmentSetting.Value ==
                CreatorToolsAlignment.Left)
                return "left";
            if (creatorToolsAlignmentSetting.Value ==
                CreatorToolsAlignment.Right)
                return "right";
            return "center";
        }

        private static int ClampIndex(int value, int length)
        {
            return Math.Max(0, Math.Min(length - 1, value));
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var builder = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32)
                            builder.Append("\\u")
                                .Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
            return builder.ToString();
        }

        private void UpdateCreatorToolsChallengeLabel()
        {
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning ||
                !creatorToolsBattleSessionActive ||
                battleHudChallengeText == null)
                return;

            var label = CreatorToolsBattleChallengeText();
            if (string.IsNullOrEmpty(label))
            {
                if (creatorToolsLabelKey != string.Empty)
                {
                    creatorToolsLabelKey = string.Empty;
                    creatorToolsLabelRevision = 0;
                    creatorToolsServer.SetChallengeLabel(null, 0);
                    PublishCreatorToolsState(true);
                }
                return;
            }

            var sourceFont = battleHudChallengeText.font;
            var key = label + "|" +
                      (sourceFont == null ? 0 : sourceFont.GetInstanceID()) +
                      "|" + creatorToolsScaleSetting.Value;
            if (key == creatorToolsLabelKey)
                return;

            try
            {
                var png = RenderCreatorToolsLabelPng(
                    battleHudChallengeText, label);
                if (png == null || png.Length == 0)
                    return;
                creatorToolsLabelKey = key;
                creatorToolsLabelRevision++;
                creatorToolsServer.SetChallengeLabel(
                    png, creatorToolsLabelRevision);
                PublishCreatorToolsState(true);
            }
            catch (Exception exception)
            {
                if (!creatorToolsLabelRenderFailureLogged)
                {
                    creatorToolsLabelRenderFailureLogged = true;
                    Logger.LogWarning(
                        "Creator Tools could not render the native challenge " +
                        "label: " + exception.Message);
                }
            }
        }

        private static byte[] RenderCreatorToolsLabelPng(
            Text source, string label)
        {
            if (source == null || source.font == null ||
                string.IsNullOrEmpty(label))
                return null;

            const int renderScale = 4;
            const int renderLayer = 31;
            source.font.RequestCharactersInTexture(
                label, source.fontSize * renderScale, source.fontStyle);

            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            var previousActive = RenderTexture.active;
            try
            {
                canvasObject = new GameObject(
                    "Gilomx Creator Tools Label Canvas",
                    typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = renderLayer;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;

                var labelObject = UnityEngine.Object.Instantiate(
                    source.gameObject);
                labelObject.name = "Gilomx Creator Tools Label";
                labelObject.layer = renderLayer;
                labelObject.transform.SetParent(canvasObject.transform, false);
                var text = labelObject.GetComponent<Text>();
                text.text = label;
                text.fontSize = source.fontSize * renderScale;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;

                var width = Mathf.Clamp(
                    Mathf.CeilToInt(text.preferredWidth + 24f), 16, 2048);
                var height = Mathf.Clamp(
                    Mathf.CeilToInt(text.preferredHeight + 24f), 16, 256);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(width, height);
                var textRect = text.rectTransform;
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(width, height);
                textRect.localScale = Vector3.one;

                cameraObject = new GameObject(
                    "Gilomx Creator Tools Label Camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.aspect = width / (float)height;
                camera.cullingMask = 1 << renderLayer;
                camera.transform.position = new Vector3(0f, 0f, -10f);

                renderTexture = new RenderTexture(
                    width, height, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(
                    width, height, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                if (canvasObject != null)
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
