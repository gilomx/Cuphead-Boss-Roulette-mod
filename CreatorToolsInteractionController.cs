using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsInteractionController : IDisposable
    {
        private const float PeskyMinimumIntervalSeconds = 1.25f;
        private const float PeskyMaximumIntervalSeconds = 3.25f;
        private const float MinimumDispatchSeparationSeconds = 0.35f;
        private const int MaximumCommandsPerUpdate = 64;

        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<ICreatorToolsInteractionExecutor> executors =
            new List<ICreatorToolsInteractionExecutor>();
        private readonly Func<int> getMaximumActive;
        private readonly Action<int> setMaximumActive;
        private readonly Func<bool> getShowGiftImage;
        private readonly Action<bool> setShowGiftImage;
        private readonly Func<bool> getInteractionsEnabled;
        private readonly Action<bool> setInteractionsEnabled;
        private readonly Func<long> getStreamBacklogCount;
        private readonly Action clearStreamBacklog;
        private readonly Action resetStreamRuntimeState;
        private readonly Func<bool>
            getPhaseTransitionProtectionEnabled;
        private readonly Action<bool>
            setPhaseTransitionProtectionEnabled;
        private readonly CreatorToolsInteractionQueue interactionQueue =
            new CreatorToolsInteractionQueue();
        private readonly CreatorToolsInteractionQueue peskyQueue =
            new CreatorToolsInteractionQueue();
        private readonly CreatorToolsPeskyModeSettings peskySettings;
        private readonly CreatorToolsLiveEventsCoordinator liveEvents;
        private readonly CreatorToolsPeskyBattleController peskyBattle;
        private readonly CreatorToolsTapFarmingController tapFarming;
        private readonly object liveEventsPublishLock = new object();
        private string lastInteractionState;
        private string lastPeskyState;
        private string lastLiveEventsState;
        private string lastItem = string.Empty;
        private string interactionFeedback = "ready";
        private bool interactionFeedbackError;
        private string peskyFeedback = "ready";
        private bool peskyFeedbackError;
        private int phaseTransitionProtectionRevision;
        private int settingsRevision;
        private int masterRevision;
        private int queueControlRevision;
        private bool queuePaused;
        private bool gameplayLevelActive;
        private bool gameplayLevelLoadPending;
        private bool gameplayAvailabilityObserved;
        private float nextPeskyAt = -1f;
        private float nextAnyDispatchAt = -1f;
        private float nextInteractionDispatchAt = -1f;
        private float nextPeskyDispatchAt = -1f;
        private bool preferPeskyNext;
        private bool preferPeskyBattleNext = true;
        private int interactionRevision;
        private int peskyRevision;
        private long lastProcessedInteractionControlSequence;

        internal CreatorToolsInteractionController(
            UnityEngine.MonoBehaviour coroutineHost,
            string pluginConfigPath,
            Func<bool> canPreloadNativeAssets,
            Func<bool> canSpawnInteraction,
            Func<int> getMaximumActive,
            Action<int> setMaximumActive,
            Func<bool> getShowGiftImage,
            Action<bool> setShowGiftImage,
            Func<bool> getInteractionsEnabled,
            Action<bool> setInteractionsEnabled,
            Func<long> getStreamBacklogCount,
            Action clearStreamBacklog,
            Action resetStreamRuntimeState,
            CreatorToolsGiftResolver resolveGift,
            Func<bool> getPhaseTransitionProtectionEnabled,
            Action<bool> setPhaseTransitionProtectionEnabled,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            this.getMaximumActive = getMaximumActive;
            this.setMaximumActive = setMaximumActive;
            this.getShowGiftImage = getShowGiftImage;
            this.setShowGiftImage = setShowGiftImage;
            this.getInteractionsEnabled = getInteractionsEnabled;
            this.setInteractionsEnabled = setInteractionsEnabled;
            this.getStreamBacklogCount = getStreamBacklogCount;
            this.clearStreamBacklog = clearStreamBacklog;
            this.resetStreamRuntimeState = resetStreamRuntimeState;
            this.getPhaseTransitionProtectionEnabled =
                getPhaseTransitionProtectionEnabled;
            this.setPhaseTransitionProtectionEnabled =
                setPhaseTransitionProtectionEnabled;
            CreatorToolsDonorLabel.SetGiftImagesVisible(ShowGiftImage);
            peskySettings = CreatorToolsPeskyModeSettings.Load(
                pluginConfigPath, logWarning);
            liveEvents = new CreatorToolsLiveEventsCoordinator();
            executors.Add(new ZeppelinInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            executors.Add(new HomingCarrotInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            executors.Add(new CagneyHomingPlantInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            executors.Add(new FrogsFireflyInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            executors.Add(new RobotHomingBombInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            executors.Add(new BaronessHeadTossInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            executors.Add(new DragonFireballsInteractionExecutor(
                coroutineHost, canPreloadNativeAssets, canSpawnInteraction,
                logInfo, logWarning));
            peskyBattle = new CreatorToolsPeskyBattleController(
                pluginConfigPath, interactionQueue, liveEvents, resolveGift,
                IsItemAvailable, DisableFreePeskyForBattle,
                logInfo, logWarning);
            tapFarming = new CreatorToolsTapFarmingController(
                pluginConfigPath, liveEvents, logInfo, logWarning);
        }

        internal CreatorToolsLiveEventsCoordinator LiveEvents
        {
            get { return liveEvents; }
        }

        internal void Update(
            CreatorToolsServer server, bool gameplayDispatchAllowed)
        {
            for (var i = 0; i < executors.Count; i++)
                executors[i].Update();
            if (interactionQueue.RemoveFinished() || peskyQueue.RemoveFinished())
                InvalidateState();
            if (server == null || !server.IsRunning)
                return;

            string query;
            bool backgroundApplied;
            bool isTest;
            int deferredTestQuantity;
            long commandSequence;
            long testGeneration;
            peskyBattle.ProcessCommands(server);
            var processedCommands = 0;
            while (processedCommands < MaximumCommandsPerUpdate &&
                   server.TryTakeInteractionCommand(
                       interactionQueue.AvailableCapacity > 0,
                       out query,
                       out backgroundApplied,
                       out isTest,
                       out deferredTestQuantity,
                       out commandSequence,
                       out testGeneration))
            {
                var commandQuery = query;
                var commandValues = ParseQuery(commandQuery);
                if (isTest)
                {
                    var commandQuantity = deferredTestQuantity;
                    var commandGeneration = testGeneration;
                    server.ProcessInteractionTestCommand(
                        commandQuery,
                        commandQuantity,
                        commandGeneration,
                        delegate
                        {
                            return ProcessInteractionCommand(
                                commandValues,
                                false,
                                commandQuantity);
                        });
                }
                else
                {
                    ProcessInteractionCommand(
                        commandValues, backgroundApplied, 0);
                }
                if (commandSequence >
                    lastProcessedInteractionControlSequence)
                    lastProcessedInteractionControlSequence =
                        commandSequence;
                processedCommands++;
            }
            var processedPeskyCommands = 0;
            while (processedPeskyCommands < MaximumCommandsPerUpdate &&
                   server.TryTakePeskyCommand(out query))
            {
                ProcessPeskyCommand(ParseQuery(query));
                processedPeskyCommands++;
            }

            var interactionsEnabled = InteractionsEnabled;
            var gameplayAvailable = AnyItemAvailable();
            var interactionsAvailable =
                interactionsEnabled && gameplayAvailable;
            if (gameplayLevelActive && gameplayAvailable)
                gameplayAvailabilityObserved = true;

            var canDispatchInteractions = gameplayDispatchAllowed &&
                gameplayAvailable &&
                (peskyBattle.Active ||
                 (interactionsEnabled && !queuePaused));
            if (!canDispatchInteractions)
                nextInteractionDispatchAt = -1f;

            var canDispatchPesky = false;
            if (peskySettings.Enabled && gameplayDispatchAllowed)
            {
                UpdatePeskyMode(gameplayAvailable);
                canDispatchPesky = gameplayAvailable;
                if (!canDispatchPesky)
                    nextPeskyDispatchAt = -1f;
            }
            else
            {
                nextPeskyAt = -1f;
                nextPeskyDispatchAt = -1f;
            }
            ProcessReadyQueues(
                canDispatchInteractions, canDispatchPesky);
            peskyBattle.Update(
                server, gameplayAvailable, gameplayDispatchAllowed);
            tapFarming.Update(server, gameplayLevelActive);
            PublishLiveEventsState(server);

            PublishInteractionState(server, interactionsAvailable);
            var peskyState = BuildPeskyState(gameplayAvailable);
            if (peskyState != lastPeskyState)
            {
                lastPeskyState = peskyState;
                server.SetPeskyState(peskyState);
            }
        }

        internal void InvalidateState()
        {
            lastInteractionState = null;
            lastPeskyState = null;
            lock (liveEventsPublishLock)
                lastLiveEventsState = null;
            peskyBattle.InvalidateState();
            tapFarming.InvalidateState();
        }

        internal int StreamQueueAvailableCapacity
        {
            get
            {
                return InteractionsEnabled
                    ? interactionQueue.AvailableCapacity
                    : 0;
            }
        }

        internal bool InteractionsEnabled
        {
            get
            {
                return getInteractionsEnabled != null &&
                       getInteractionsEnabled();
            }
        }

        internal bool StreamAttacksAllowed
        {
            get { return peskyBattle.StreamAttacksAllowed; }
        }

        internal CreatorToolsPeskyBattleObservation ObservePeskyBattleEvent(
            CreatorToolsStreamEvent streamEvent)
        {
            return peskyBattle.ObserveStreamEvent(streamEvent);
        }

        internal CreatorToolsTapFarmingObservation ObserveTapFarmingEvent(
            CreatorToolsStreamEvent streamEvent)
        {
            return tapFarming.ObserveStreamEvent(streamEvent);
        }

        /// <summary>
        /// Republishes the physical gameplay queue after stream backlog items
        /// are materialized later in the same Unity frame.
        /// </summary>
        internal void PublishInteractionState(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;
            PublishInteractionState(
                server, InteractionsEnabled && AnyItemAvailable());
        }

        private void PublishInteractionState(
            CreatorToolsServer server, bool interactionsAvailable)
        {
            var interactionState = BuildInteractionState(
                interactionsAvailable);
            if (interactionState == lastInteractionState)
                return;
            lastInteractionState = interactionState;
            server.SetInteractionsState(
                interactionState,
                masterRevision,
                queueControlRevision,
                lastProcessedInteractionControlSequence);
        }

        internal void PublishPeskyBattleState(CreatorToolsServer server)
        {
            peskyBattle.PublishState(server);
            tapFarming.PublishState(server);
            PublishLiveEventsState(server);
        }

        internal bool ProcessPeskyBattleCommandInBackground(
            string query, CreatorToolsServer server)
        {
            var handled = peskyBattle.ProcessBackgroundCommand(query);
            peskyBattle.PublishState(server);
            PublishLiveEventsState(server);
            return handled;
        }

        internal bool ProcessTapFarmingCommandInBackground(
            string query, CreatorToolsServer server)
        {
            var handled = tapFarming.ProcessBackgroundCommand(query);
            tapFarming.PublishState(server);
            PublishLiveEventsState(server);
            return handled;
        }

        internal void PublishLiveEventsState(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;
            // Unity, the stream worker and HTTP command handlers can all
            // publish this projection. Serialize snapshot + write so an
            // older revision cannot overtake a newer one in the panel.
            lock (liveEventsPublishLock)
            {
                var snapshot = liveEvents.Snapshot;
                var stoppingEvent = snapshot.Status == "stopping"
                    ? snapshot.ActiveEvent
                    : string.Empty;
                var builder = new StringBuilder(256);
                builder.Append("{\"ready\":true,\"schemaVersion\":1," +
                    "\"revision\":")
                    .Append(snapshot.Revision)
                    .Append(",\"activeEvent\":\"");
                CreatorToolsJson.AppendEscaped(
                    builder, snapshot.ActiveEvent);
                builder.Append("\",\"status\":\"");
                CreatorToolsJson.AppendEscaped(builder, snapshot.Status);
                builder.Append("\",\"stoppingEvent\":\"");
                CreatorToolsJson.AppendEscaped(builder, stoppingEvent);
                builder.Append("\",\"feedback\":\"ready\"," +
                    "\"error\":false}");
                var state = builder.ToString();
                if (state == lastLiveEventsState)
                    return;
                lastLiveEventsState = state;
                server.SetLiveEventsState(state);
            }
        }

        internal void PeskyBattleLevelStarted(Level level)
        {
            peskyBattle.OnLevelStarted(level);
            tapFarming.OnLevelStarted(level);
        }

        internal void PeskyBattleLevelDefeated(Level level)
        {
            peskyBattle.OnLevelDefeated(level);
            tapFarming.OnLevelDefeated(level);
        }

        internal void PeskyBattleLevelPreWin(Level level)
        {
            peskyBattle.OnLevelPreWin(level);
            tapFarming.OnLevelPreWin(level);
        }

        internal void PeskyBattleLevelEnded(Level level)
        {
            peskyBattle.OnLevelEnded(level);
            tapFarming.OnLevelEnded(level);
        }

        internal void TapFarmingMapEntered()
        {
            tapFarming.OnMapEntered();
        }

        internal bool PrepareTapFarmingBossDamage(
            object properties, ref float damage)
        {
            return tapFarming.PrepareBossDamage(properties, ref damage);
        }

        internal void ObserveTapFarmingBossDamage(object properties)
        {
            tapFarming.ObserveBossDamage(properties);
        }

        /// <summary>
        /// Main-thread entry point for evaluated stream rules. Network and
        /// companion threads never call the gameplay queue directly.
        /// </summary>
        internal int EnqueueStreamInteraction(
            string item,
            string donor,
            string giftImagePath,
            int quantity,
            out string feedbackCode)
        {
            if (!InteractionsEnabled)
            {
                feedbackCode = "interactions_disabled";
                return 0;
            }
            var executor = FindExecutor(item);
            if (executor == null)
            {
                lastItem = item ?? string.Empty;
                feedbackCode = "unknown_item";
                SetInteractionFeedback(feedbackCode, true);
                return 0;
            }

            donor = NormalizeDonor(donor);
            var added = interactionQueue.Enqueue(
                item, donor, giftImagePath, quantity, 0f,
                CreatorToolsInteractionSource.Stream);
            lastItem = item ?? string.Empty;
            if (added <= 0)
            {
                feedbackCode = "queue_full";
                SetInteractionFeedback(feedbackCode, true);
                return 0;
            }

            feedbackCode = queuePaused ? "queued_paused" : "queued";
            SetInteractionFeedback(feedbackCode, false);
            InvalidateState();
            if (logInfo != null)
                logInfo(added + " canje(s) de " + item +
                    " agregados desde una regla de stream para " +
                    donor + ".");
            return added;
        }

        internal bool GameplayLevelLoadPending
        {
            get { return gameplayLevelLoadPending; }
        }

        internal bool GameplayLevelActive
        {
            get { return gameplayLevelActive; }
        }

        internal void EndGameplayLevel()
        {
            interactionQueue.ClearActive();
            peskyQueue.Clear();
            for (var i = 0; i < executors.Count; i++)
                executors[i].EndGameplayLevel();
            SuspendGameplayLevel();
        }

        internal void BeginGameplayLevel(bool confirmLoad)
        {
            if (confirmLoad)
                gameplayLevelLoadPending = false;
            gameplayLevelActive = true;
            gameplayAvailabilityObserved = false;
            InvalidateState();
        }

        internal void ConfirmGameplayLevelStart()
        {
            if (!gameplayLevelLoadPending)
                return;
            gameplayLevelLoadPending = false;
            InvalidateState();
        }

        internal bool BeginGameplayLevelLoad()
        {
            if (gameplayLevelLoadPending)
                return false;
            gameplayLevelLoadPending = true;
            InvalidateState();
            return true;
        }

        internal void CancelGameplayLevelLoad()
        {
            if (!gameplayLevelLoadPending)
                return;
            gameplayLevelLoadPending = false;
            InvalidateState();
        }

        internal void SuspendGameplayLevel()
        {
            gameplayLevelActive = false;
            gameplayAvailabilityObserved = false;
            peskyQueue.Clear();
            nextPeskyAt = -1f;
            nextAnyDispatchAt = -1f;
            nextInteractionDispatchAt = -1f;
            nextPeskyDispatchAt = -1f;
            preferPeskyNext = false;
            InvalidateState();
        }

        internal int ClearActiveForPhaseTransition()
        {
            var cleared = CreatorToolsInteractionPresentation
                .ClearActiveActorsForPhaseTransition();
            interactionQueue.ClearActive();
            peskyQueue.Clear();
            peskyBattle.OnPhaseTransition();
            tapFarming.OnPhaseTransition();
            for (var i = 0; i < executors.Count; i++)
                executors[i].EndGameplayLevel();
            nextPeskyAt = -1f;
            nextAnyDispatchAt = -1f;
            nextInteractionDispatchAt = -1f;
            nextPeskyDispatchAt = -1f;
            preferPeskyNext = false;
            InvalidateState();
            return cleared;
        }

        private int ProcessInteractionCommand(
            Dictionary<string, string> values,
            bool backgroundApplied,
            int deferredTestQuantity)
        {
            if (values.ContainsKey("interactionsEnabled"))
            {
                SetInteractionsEnabled(values, backgroundApplied);
                return 0;
            }
            if (values.ContainsKey("queuePaused"))
            {
                SetQueuePaused(values);
                return 0;
            }
            if (values.ContainsKey("clearPending"))
            {
                ClearPendingInteractions(backgroundApplied);
                return 0;
            }
            if (values.ContainsKey("maxActive") ||
                values.ContainsKey("showGiftImage"))
            {
                SetInteractionSettings(values);
                return 0;
            }
            if (values.ContainsKey(
                "phaseTransitionProtectionEnabled"))
            {
                SetPhaseTransitionProtectionEnabled(values);
                return 0;
            }
            return EnqueueInteraction(values, deferredTestQuantity);
        }

        private void SetInteractionsEnabled(
            Dictionary<string, string> values,
            bool backgroundApplied)
        {
            string value;
            bool enabled;
            if (!values.TryGetValue("interactionsEnabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetInteractionFeedback("invalid_setting", true);
                return;
            }

            // A background-applied master command already updated the
            // authoritative volatile mirror and persisted setting. Replaying
            // an older command here could otherwise overwrite a newer HTTP
            // request while the queues are being caught up.
            if (!backgroundApplied && setInteractionsEnabled != null)
                setInteractionsEnabled(enabled);
            if (!enabled)
            {
                interactionQueue.ClearPending(
                    CreatorToolsInteractionSource.Manual);
                interactionQueue.ClearPending(
                    CreatorToolsInteractionSource.Stream);
                if (!backgroundApplied && resetStreamRuntimeState != null)
                    resetStreamRuntimeState();
                queuePaused = false;
                nextInteractionDispatchAt = -1f;
            }
            masterRevision++;
            queueControlRevision++;
            SetInteractionFeedback(
                enabled
                    ? "interactions_enabled"
                    : "interactions_disabled",
                false);
        }

        private void SetQueuePaused(Dictionary<string, string> values)
        {
            string value;
            bool paused;
            if (!values.TryGetValue("queuePaused", out value) ||
                !TryParseSwitch(value, out paused) ||
                !InteractionsEnabled)
            {
                SetInteractionFeedback(
                    InteractionsEnabled
                        ? "invalid_setting"
                        : "interactions_disabled",
                    !InteractionsEnabled ? false : true);
                return;
            }

            queuePaused = paused;
            nextInteractionDispatchAt = -1f;
            queueControlRevision++;
            SetInteractionFeedback(
                paused ? "queue_paused" : "queue_resumed", false);
        }

        private void ClearPendingInteractions(bool backgroundApplied)
        {
            var cleared = (long)interactionQueue.ClearPending(
                CreatorToolsInteractionSource.Manual);
            cleared += interactionQueue.ClearPending(
                CreatorToolsInteractionSource.Stream);
            if (!backgroundApplied && clearStreamBacklog != null)
            {
                var backlogBefore = getStreamBacklogCount == null
                    ? 0L
                    : Math.Max(0L, getStreamBacklogCount());
                clearStreamBacklog();
                cleared += backlogBefore;
            }
            queueControlRevision++;
            SetInteractionFeedback(
                cleared > 0L || backgroundApplied
                    ? "pending_cleared"
                    : "pending_empty",
                false);
        }

        private void SetPhaseTransitionProtectionEnabled(
            Dictionary<string, string> values)
        {
            string value;
            bool enabled;
            if (!values.TryGetValue(
                    "phaseTransitionProtectionEnabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetInteractionFeedback("invalid_setting", true);
                return;
            }

            if (setPhaseTransitionProtectionEnabled != null)
                setPhaseTransitionProtectionEnabled(enabled);
            phaseTransitionProtectionRevision++;
            SetInteractionFeedback(
                enabled
                    ? "phase_transition_protection_enabled"
                    : "phase_transition_protection_disabled",
                false);
        }

        private void ProcessPeskyCommand(
            Dictionary<string, string> values)
        {
            if (values.ContainsKey("names"))
                SetPeskyNames(values);
            else if (values.ContainsKey("item"))
                SetPeskyItem(values);
            else if (values.ContainsKey("enabled"))
                SetPeskyEnabled(values);
            else
                SetPeskyFeedback("invalid_setting", true);
        }

        private void SetPeskyEnabled(Dictionary<string, string> values)
        {
            string value;
            bool enabled;
            if (!values.TryGetValue("enabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetPeskyFeedback("invalid_setting", true);
                return;
            }
            if (enabled && peskyBattle.Exclusive)
            {
                SetPeskyFeedback("blocked_by_pesky_battle", true);
                return;
            }
            if (enabled && peskySettings.EnabledItemCount == 0)
            {
                SetPeskyFeedback("items_required", true);
                return;
            }

            peskySettings.Enabled = enabled;
            if (!enabled)
                peskyQueue.Clear();
            ResetPeskySchedule();
            peskySettings.Save();
            SetPeskyFeedback(enabled ? "enabled" : "disabled", false);
            if (logInfo != null)
                logInfo("Modo Molestoso " +
                    (enabled ? "activado." : "desactivado."));
        }

        private void DisableFreePeskyForBattle()
        {
            if (!peskySettings.Enabled && peskyQueue.Count == 0)
                return;
            peskySettings.Enabled = false;
            peskyQueue.Clear();
            ResetPeskySchedule();
            peskySettings.Save();
            SetPeskyFeedback("disabled_by_pesky_battle", false);
            if (logInfo != null)
                logInfo("Modo Molestoso libre se desactivo para Batalla " +
                    "Molestosa.");
        }

        private void SetPeskyNames(Dictionary<string, string> values)
        {
            string value;
            values.TryGetValue("names", out value);
            peskySettings.SetNames((value ?? string.Empty).Split(
                new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.None));
            SetPeskyFeedback("names_saved", false);
            peskySettings.Save();
        }

        private void SetPeskyItem(Dictionary<string, string> values)
        {
            string item;
            string value;
            bool enabled;
            if (!values.TryGetValue("item", out item) ||
                FindExecutor(item) == null ||
                !values.TryGetValue("itemEnabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetPeskyFeedback("invalid_setting", true);
                return;
            }

            if (enabled)
                peskySettings.DisabledItems.Remove(item);
            else
                peskySettings.DisabledItems.Add(item);
            if (peskySettings.Enabled && peskySettings.EnabledItemCount == 0)
            {
                peskySettings.Enabled = false;
                peskyQueue.Clear();
                ResetPeskySchedule();
                SetPeskyFeedback("disabled_invalid_config", true);
            }
            else
                SetPeskyFeedback("items_saved", false);
            peskySettings.Save();
        }

        private void UpdatePeskyMode(bool available)
        {
            if (!peskySettings.Enabled || !available)
            {
                nextPeskyAt = -1f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (nextPeskyAt < 0f)
            {
                ScheduleNextPesky(now);
                return;
            }
            if (now < nextPeskyAt)
                return;

            ScheduleNextPesky(now);

            // Avoid an automatic backlog: select only when every Pesky entry
            // is active and this queue's active limit has room.
            if (peskyQueue.ActiveCount >= MaximumActive ||
                peskyQueue.Count != peskyQueue.ActiveCount)
                return;

            var availableItems = new List<string>();
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var candidate = CreatorToolsInteractionIds.All[i];
                var executor = FindExecutor(candidate);
                if (peskySettings.IsItemEnabled(candidate) &&
                    executor != null && executor.IsAvailable(candidate))
                    availableItems.Add(candidate);
            }
            if (availableItems.Count == 0)
                return;

            var item = availableItems[UnityEngine.Random.Range(
                0, availableItems.Count)];
            var name = peskySettings.Names.Count == 0
                ? string.Empty
                : peskySettings.Names[UnityEngine.Random.Range(
                    0, peskySettings.Names.Count)];
            if (peskyQueue.Enqueue(
                    item, name, string.Empty, 1, 0f,
                    CreatorToolsInteractionSource.Pesky) <= 0)
                return;

            lastItem = item;
            InvalidateState();
            if (logInfo != null)
                logInfo("Modo Molestoso agrego " + item +
                    (name.Length == 0 ? "." : " para " + name + "."));
        }

        private void ScheduleNextPesky(float now)
        {
            nextPeskyAt = now + UnityEngine.Random.Range(
                PeskyMinimumIntervalSeconds, PeskyMaximumIntervalSeconds);
        }

        private void ResetPeskySchedule()
        {
            nextPeskyAt = -1f;
            nextPeskyDispatchAt = -1f;
            InvalidateState();
        }

        private int EnqueueInteraction(
            Dictionary<string, string> values,
            int deferredTestQuantity)
        {
            if (!InteractionsEnabled)
            {
                SetInteractionFeedback("interactions_disabled", false);
                return 0;
            }
            string item;
            if (!values.TryGetValue("item", out item))
            {
                SetInteractionFeedback("unknown_item", true);
                return 0;
            }

            var executor = FindExecutor(item);
            if (executor == null)
            {
                lastItem = item ?? string.Empty;
                SetInteractionFeedback("unknown_item", true);
                return 0;
            }
            lastItem = item;

            string donor;
            values.TryGetValue("donor", out donor);
            donor = NormalizeDonor(donor);
            var requested = deferredTestQuantity > 0
                ? Math.Max(
                    1,
                    Math.Min(
                        CreatorToolsInteractionQueue.MaximumBatchSize,
                        deferredTestQuantity))
                : ParseQuantity(values);
            var added = interactionQueue.Enqueue(
                item,
                donor,
                string.Empty,
                requested,
                ParseDelaySeconds(values),
                CreatorToolsInteractionSource.Manual);
            if (added > 0)
            {
                SetInteractionFeedback(
                    queuePaused ? "queued_paused" : "queued",
                    false);
                if (logInfo != null)
                    logInfo(added + " canje(s) de " + item +
                        " agregados a la cola para " + donor + ".");
                return deferredTestQuantity > 0
                    ? Math.Max(0, requested - added)
                    : 0;
            }

            if (deferredTestQuantity > 0)
            {
                SetInteractionFeedback(
                    queuePaused ? "queued_paused" : "queued", false);
                return requested;
            }
            SetInteractionFeedback("queue_full", true);
            return 0;
        }

        private void SetInteractionSettings(
            Dictionary<string, string> values)
        {
            string value;
            var maximumActive = MaximumActive;
            var showGiftImage = ShowGiftImage;
            if (values.TryGetValue("maxActive", out value))
            {
                int requested;
                if (!int.TryParse(value, out requested))
                {
                    SetInteractionFeedback("invalid_setting", true);
                    return;
                }
                maximumActive = Math.Max(
                    1, Math.Min(MaximumActiveLimit, requested));
            }
            if (values.TryGetValue("showGiftImage", out value) &&
                !TryParseSwitch(value, out showGiftImage))
            {
                SetInteractionFeedback("invalid_setting", true);
                return;
            }

            if (setMaximumActive != null)
                setMaximumActive(maximumActive);
            if (setShowGiftImage != null)
                setShowGiftImage(showGiftImage);
            CreatorToolsDonorLabel.SetGiftImagesVisible(showGiftImage);
            settingsRevision++;
            SetInteractionFeedback("settings_saved", false);
        }

        private void ProcessReadyQueues(
            bool canDispatchInteractions, bool canDispatchPesky)
        {
            if (!peskyBattle.Active)
                preferPeskyBattleNext = true;
            if (!canDispatchInteractions && !canDispatchPesky)
                return;

            var now = Time.realtimeSinceStartup;
            if (nextAnyDispatchAt >= 0f && now < nextAnyDispatchAt)
                return;

            if (preferPeskyNext)
            {
                if (canDispatchPesky &&
                    ProcessQueue(peskyQueue, true))
                {
                    preferPeskyNext = false;
                    return;
                }
                if (canDispatchInteractions &&
                    ProcessQueue(interactionQueue, false))
                    preferPeskyNext = true;
                return;
            }

            if (canDispatchInteractions &&
                ProcessQueue(interactionQueue, false))
            {
                preferPeskyNext = true;
                return;
            }
            if (canDispatchPesky && ProcessQueue(peskyQueue, true))
                preferPeskyNext = false;
        }

        private bool ProcessQueue(
            CreatorToolsInteractionQueue queue, bool pesky)
        {
            if (queue.ActiveCount >= MaximumActive)
                return false;

            var now = Time.realtimeSinceStartup;
            if (nextAnyDispatchAt >= 0f && now < nextAnyDispatchAt)
                return false;
            var nextDispatchAt = pesky
                ? nextPeskyDispatchAt
                : nextInteractionDispatchAt;
            if (nextDispatchAt >= 0f && now < nextDispatchAt)
                return false;

            Func<CreatorToolsInteractionQueue.Entry, bool> canDispatch =
                delegate(CreatorToolsInteractionQueue.Entry candidate)
                {
                    return candidate.IsReady &&
                        CanDispatchEntry(candidate, pesky);
                };
            if (pesky)
                return TryDispatchEntry(
                    queue, queue.Peek(canDispatch), true, now);

            CreatorToolsInteractionQueue.Entry battleEntry;
            CreatorToolsInteractionQueue.Entry regularEntry;
            queue.PeekBySource(
                canDispatch,
                CreatorToolsInteractionSource.PeskyBattle,
                out battleEntry,
                out regularEntry);
            var entry = SelectSharedQueueEntry(
                queue, battleEntry, regularEntry);
            if (entry == null)
                return false;
            var alternate = entry == battleEntry
                ? regularEntry
                : battleEntry;
            if (TryDispatchEntry(queue, entry, false, now))
                return true;
            // A temporarily unavailable lane must not head-of-line block the
            // other one; the shared dispatch clock advances only on success.
            return alternate != null &&
                TryDispatchEntry(queue, alternate, false, now);
        }

        private CreatorToolsInteractionQueue.Entry SelectSharedQueueEntry(
            CreatorToolsInteractionQueue queue,
            CreatorToolsInteractionQueue.Entry battleEntry,
            CreatorToolsInteractionQueue.Entry regularEntry)
        {
            if (battleEntry == null)
                return regularEntry;
            if (regularEntry == null)
                return battleEntry;

            var remainingCapacity = MaximumActive - queue.ActiveCount;
            if (remainingCapacity == 1)
            {
                // Do not let either lane consume the final slot while only
                // the other lane is represented among active entries.
                var activeBattle = queue.ActiveCountFor(
                    CreatorToolsInteractionSource.PeskyBattle);
                var activeRegular = queue.ActiveCount - activeBattle;
                if (activeBattle == 0 && activeRegular > 0)
                    return battleEntry;
                if (activeRegular == 0 && activeBattle > 0)
                    return regularEntry;
            }
            return preferPeskyBattleNext
                ? battleEntry
                : regularEntry;
        }

        private bool TryDispatchEntry(
            CreatorToolsInteractionQueue queue,
            CreatorToolsInteractionQueue.Entry entry,
            bool pesky,
            float now)
        {
            if (entry == null || !entry.IsReady)
                return false;

            var executor = FindExecutor(entry.Item);
            if (executor == null)
            {
                queue.Reject(entry);
                lastItem = entry.Item;
                SetDispatchFeedback(
                    entry, pesky, "unknown_item", true);
                return false;
            }

            ICreatorToolsInteractionHandle handle;
            string feedbackCode;
            string error;
            if (executor.TrySpawn(
                entry.Item,
                entry.Donor,
                entry.GiftImagePath,
                out handle,
                out feedbackCode, out error))
            {
                queue.Activate(entry, handle);
                if (!pesky)
                    preferPeskyBattleNext = entry.Source !=
                        CreatorToolsInteractionSource.PeskyBattle;
                if (pesky)
                    nextPeskyDispatchAt = now +
                        MinimumDispatchSeparationSeconds;
                else
                    nextInteractionDispatchAt = now +
                        MinimumDispatchSeparationSeconds;
                nextAnyDispatchAt = now +
                    MinimumDispatchSeparationSeconds;
                InvalidateState();
                if (logInfo != null)
                    logInfo((entry.Source ==
                            CreatorToolsInteractionSource.PeskyBattle
                        ? "Ejecutando ataque de Batalla Molestosa #"
                        : pesky ? "Ejecutando ataque molesto #"
                        : "Ejecutando canje #") + entry.Id + " de " +
                        entry.Donor + ".");
                return true;
            }

            if (feedbackCode == "native_assets_loading" ||
                feedbackCode == "requires_gameplay_level" ||
                feedbackCode == "interaction_type_active")
                return false;

            queue.Reject(entry);
            lastItem = entry.Item;
            SetDispatchFeedback(entry, pesky, feedbackCode, true);
            if (logWarning != null)
                logWarning(entry.Item + " interaction failed: " +
                    (string.IsNullOrEmpty(error)
                        ? "No diagnostic detail was returned."
                        : error));
            return false;
        }

        private bool CanDispatchEntry(
            CreatorToolsInteractionQueue.Entry entry, bool pesky)
        {
            if (entry == null)
                return false;
            var exclusiveExecutor = FindExecutor(entry.Item) as
                ICreatorToolsExclusiveInteractionExecutor;
            if (exclusiveExecutor != null &&
                exclusiveExecutor.BlocksConcurrentSpawn(entry.Item))
                return false;
            if (pesky)
                return peskySettings.Enabled;
            if (entry.Source == CreatorToolsInteractionSource.PeskyBattle)
                return peskyBattle.Active;
            if (!InteractionsEnabled || queuePaused)
                return false;
            if (entry.Source == CreatorToolsInteractionSource.Stream)
                return peskyBattle.StreamAttacksAllowed;
            return true;
        }

        private void SetDispatchFeedback(
            CreatorToolsInteractionQueue.Entry entry,
            bool pesky,
            string value,
            bool error)
        {
            if (entry != null && entry.Source ==
                    CreatorToolsInteractionSource.PeskyBattle)
                peskyBattle.ReportDispatchFeedback(value, error);
            else if (pesky)
                SetPeskyFeedback(value, error);
            else
                SetInteractionFeedback(value, error);
        }

        internal const int MaximumActiveLimit = 20;

        private int MaximumActive
        {
            get
            {
                var value = getMaximumActive == null ? 1 : getMaximumActive();
                return Math.Max(1, Math.Min(MaximumActiveLimit, value));
            }
        }

        private bool ShowGiftImage
        {
            get
            {
                return getShowGiftImage == null || getShowGiftImage();
            }
        }

        private void SetInteractionFeedback(string value, bool error)
        {
            interactionFeedback = value;
            interactionFeedbackError = error;
            interactionRevision++;
            InvalidateState();
        }

        private void SetPeskyFeedback(string value, bool error)
        {
            peskyFeedback = value;
            peskyFeedbackError = error;
            peskyRevision++;
            InvalidateState();
        }

        private string BuildInteractionState(bool available)
        {
            var builder = new StringBuilder(4096);
            builder.Append("{\"ready\":true,\"available\":")
                .Append(available ? "true" : "false")
                .Append(",\"interactionsEnabled\":")
                .Append(InteractionsEnabled ? "true" : "false")
                .Append(",\"masterRevision\":")
                .Append(masterRevision)
                .Append(",\"queuePaused\":")
                .Append(queuePaused ? "true" : "false")
                .Append(",\"queueControlRevision\":")
                .Append(queueControlRevision)
                .Append(",\"pendingClearProjected\":false")
                .Append(",\"item\":\"")
                .Append(CreatorToolsInteractionIds.All[0])
                .Append("\",\"items\":[");
            AppendItemList(builder);
            builder.Append("],\"lastItem\":\"");
            AppendJson(builder, lastItem);
            builder.Append("\",\"feedback\":\"");
            AppendJson(builder, interactionFeedback);
            builder.Append("\",\"error\":")
                .Append(interactionFeedbackError ? "true" : "false")
                .Append(",\"phaseTransitionProtectionEnabled\":")
                .Append(getPhaseTransitionProtectionEnabled == null ||
                    getPhaseTransitionProtectionEnabled()
                        ? "true"
                        : "false")
                .Append(",\"phaseTransitionProtectionRevision\":")
                .Append(phaseTransitionProtectionRevision)
                .Append(",\"showGiftImage\":")
                .Append(ShowGiftImage ? "true" : "false")
                .Append(",\"settingsRevision\":")
                .Append(settingsRevision)
                .Append(",\"revision\":").Append(interactionRevision)
                .Append(",\"queueCount\":").Append(interactionQueue.Count)
                .Append(",\"activeCount\":").Append(interactionQueue.ActiveCount)
                .Append(",\"pendingCount\":")
                .Append(interactionQueue.PendingCount)
                .Append(",\"backlogCount\":")
                .Append(getStreamBacklogCount == null
                    ? 0L
                    : Math.Max(0L, getStreamBacklogCount()))
                .Append(",\"deferredTestCount\":0")
                .Append(",\"maxActive\":").Append(MaximumActive)
                .Append(",\"maxActiveLimit\":").Append(MaximumActiveLimit)
                .Append(",\"maxBatch\":")
                .Append(CreatorToolsInteractionQueue.MaximumBatchSize)
                .Append(",\"maxDelay\":")
                .Append(CreatorToolsInteractionQueue.MaximumDelaySeconds)
                .Append(",\"queue\":");
            interactionQueue.AppendJson(builder);
            builder.Append('}');
            return builder.ToString();
        }

        private string BuildPeskyState(bool available)
        {
            var running = peskySettings.Enabled && available &&
                !gameplayLevelLoadPending;
            var startingBattle = peskySettings.Enabled &&
                (gameplayLevelLoadPending ||
                 (gameplayLevelActive &&
                  !gameplayAvailabilityObserved));
            var builder = new StringBuilder(4096);
            builder.Append("{\"ready\":true,\"available\":")
                .Append(available ? "true" : "false")
                .Append(",\"enabled\":")
                .Append(peskySettings.Enabled ? "true" : "false")
                .Append(",\"blockedByPeskyBattle\":")
                .Append(peskyBattle.Exclusive ? "true" : "false")
                .Append(",\"running\":").Append(running ? "true" : "false")
                .Append(",\"startingBattle\":")
                .Append(startingBattle ? "true" : "false")
                .Append(",\"revision\":").Append(peskyRevision)
                .Append(",\"feedback\":\"");
            AppendJson(builder, peskyFeedback);
            builder.Append("\",\"error\":")
                .Append(peskyFeedbackError ? "true" : "false")
                .Append(",\"minimumInterval\":")
                .Append(PeskyMinimumIntervalSeconds.ToString(
                    "0.##", CultureInfo.InvariantCulture))
                .Append(",\"maximumInterval\":")
                .Append(PeskyMaximumIntervalSeconds.ToString(
                    "0.##", CultureInfo.InvariantCulture))
                .Append(",\"names\":[");
            for (var i = 0; i < peskySettings.Names.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append('"');
                AppendJson(builder, peskySettings.Names[i]);
                builder.Append('"');
            }
            builder.Append("],\"items\":[");
            AppendItemList(builder);
            builder.Append("],\"disabledItems\":[");
            var first = true;
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var item = CreatorToolsInteractionIds.All[i];
                if (!peskySettings.DisabledItems.Contains(item))
                    continue;
                if (!first)
                    builder.Append(',');
                builder.Append('"');
                AppendJson(builder, item);
                builder.Append('"');
                first = false;
            }
            builder.Append("],\"queueCount\":").Append(peskyQueue.Count)
                .Append(",\"activeCount\":").Append(peskyQueue.ActiveCount)
                .Append(",\"maxActive\":").Append(MaximumActive)
                .Append(",\"queue\":");
            peskyQueue.AppendJson(builder);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendItemList(StringBuilder builder)
        {
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append('"');
                AppendJson(builder, CreatorToolsInteractionIds.All[i]);
                builder.Append('"');
            }
        }

        private ICreatorToolsInteractionExecutor FindExecutor(string item)
        {
            for (var i = 0; i < executors.Count; i++)
                if (executors[i].Supports(item))
                    return executors[i];
            return null;
        }

        private bool AnyItemAvailable()
        {
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var item = CreatorToolsInteractionIds.All[i];
                if (IsItemAvailable(item))
                    return true;
            }
            return false;
        }

        private bool IsItemAvailable(string item)
        {
            var executor = FindExecutor(item);
            return executor != null && executor.IsAvailable(item);
        }

        private static int ParseQuantity(
            Dictionary<string, string> values)
        {
            string value;
            int quantity;
            if (!values.TryGetValue("quantity", out value) ||
                !int.TryParse(value, out quantity))
                return 1;
            return Math.Max(1, Math.Min(
                CreatorToolsInteractionQueue.MaximumBatchSize, quantity));
        }

        private static float ParseDelaySeconds(
            Dictionary<string, string> values)
        {
            string value;
            float delaySeconds;
            if (!values.TryGetValue("delay", out value) ||
                !float.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out delaySeconds))
                return 0f;
            return Math.Max(0f, Math.Min(
                CreatorToolsInteractionQueue.MaximumDelaySeconds,
                delaySeconds));
        }

        private static string NormalizeDonor(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "DONADOR";
            value = value.Trim();
            if (value.Length > CreatorToolsPeskyModeSettings.MaximumNameLength)
                value = value.Substring(0,
                    CreatorToolsPeskyModeSettings.MaximumNameLength);
            return value;
        }

        private static bool TryParseSwitch(string value, out bool enabled)
        {
            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }
            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }
            enabled = false;
            return false;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
                return values;
            var pairs = query.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var separator = pairs[i].IndexOf('=');
                var key = separator < 0
                    ? pairs[i]
                    : pairs[i].Substring(0, separator);
                var value = separator < 0
                    ? string.Empty
                    : pairs[i].Substring(separator + 1);
                try
                {
                    values[Uri.UnescapeDataString(key.Replace('+', ' '))] =
                        Uri.UnescapeDataString(value.Replace('+', ' '));
                }
                catch { }
            }
            return values;
        }

        private static void AppendJson(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\').Append(character);
                else if (character == '\n')
                    builder.Append("\\n");
                else if (character == '\r')
                    builder.Append("\\r");
                else if (character == '\t')
                    builder.Append("\\t");
                else if (character < 32)
                    builder.Append("\\u")
                        .Append(((int)character).ToString("x4"));
                else
                    builder.Append(character);
            }
        }

        public void Dispose()
        {
            peskyBattle.Dispose();
            interactionQueue.Dispose();
            peskyQueue.Dispose();
            for (var i = 0; i < executors.Count; i++)
                executors[i].Dispose();
            executors.Clear();
            CreatorToolsGiftImageCache.Clear();
            nextPeskyAt = -1f;
            nextAnyDispatchAt = -1f;
            nextInteractionDispatchAt = -1f;
            nextPeskyDispatchAt = -1f;
            preferPeskyNext = false;
            preferPeskyBattleNext = true;
            InvalidateState();
        }
    }
}
