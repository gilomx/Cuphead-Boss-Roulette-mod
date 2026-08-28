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
        private const float RandomTestMinimumIntervalSeconds = 1.25f;
        private const float RandomTestMaximumIntervalSeconds = 3.25f;
        private const string RandomTestDonor = "gilo.mx";
        private const float MinimumDispatchSeparationSeconds = 0.35f;

        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<ICreatorToolsInteractionExecutor> executors =
            new List<ICreatorToolsInteractionExecutor>();
        private readonly Func<int> getMaximumActive;
        private readonly Action<int> setMaximumActive;
        private readonly Func<bool> getShowGiftImage;
        private readonly Action<bool> setShowGiftImage;
        private readonly Func<bool>
            getPhaseTransitionProtectionEnabled;
        private readonly Action<bool>
            setPhaseTransitionProtectionEnabled;
        private readonly CreatorToolsInteractionQueue interactionQueue =
            new CreatorToolsInteractionQueue();
        private readonly CreatorToolsInteractionQueue peskyQueue =
            new CreatorToolsInteractionQueue();
        private readonly CreatorToolsPeskyModeSettings peskySettings;
        private string lastInteractionState;
        private string lastPeskyState;
        private string lastItem = string.Empty;
        private string interactionFeedback = "ready";
        private bool interactionFeedbackError;
        private string peskyFeedback = "ready";
        private bool peskyFeedbackError;
        private bool randomTestEnabled;
        private int randomTestRevision;
        private int phaseTransitionProtectionRevision;
        private int settingsRevision;
        private bool gameplayLevelActive;
        private bool gameplayLevelLoadPending;
        private bool gameplayAvailabilityObserved;
        private float nextPeskyAt = -1f;
        private float nextRandomTestAt = -1f;
        private float nextInteractionDispatchAt = -1f;
        private float nextPeskyDispatchAt = -1f;
        private int interactionRevision;
        private int peskyRevision;

        internal CreatorToolsInteractionController(
            UnityEngine.MonoBehaviour coroutineHost,
            string pluginConfigPath,
            Func<bool> canPreloadNativeAssets,
            Func<bool> canSpawnInteraction,
            Func<int> getMaximumActive,
            Action<int> setMaximumActive,
            Func<bool> getShowGiftImage,
            Action<bool> setShowGiftImage,
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
            this.getPhaseTransitionProtectionEnabled =
                getPhaseTransitionProtectionEnabled;
            this.setPhaseTransitionProtectionEnabled =
                setPhaseTransitionProtectionEnabled;
            CreatorToolsDonorLabel.SetGiftImagesVisible(ShowGiftImage);
            peskySettings = CreatorToolsPeskyModeSettings.Load(
                pluginConfigPath, logWarning);
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
        }

        internal void Update(CreatorToolsServer server)
        {
            for (var i = 0; i < executors.Count; i++)
                executors[i].Update();
            if (interactionQueue.RemoveFinished() || peskyQueue.RemoveFinished())
                InvalidateState();
            if (server == null || !server.IsRunning)
                return;

            string query;
            bool peskyModeCommand;
            while (server.TryTakeModeCommand(
                out peskyModeCommand, out query))
            {
                var values = ParseQuery(query);
                if (peskyModeCommand)
                    ProcessPeskyCommand(values);
                else
                    ProcessInteractionCommand(values);
            }
            while (server.TryTakeInteractionCommand(out query))
                ProcessInteractionCommand(ParseQuery(query));
            while (server.TryTakePeskyCommand(out query))
                ProcessPeskyCommand(ParseQuery(query));

            var available = AnyItemAvailable();
            if (gameplayLevelActive && available)
                gameplayAvailabilityObserved = true;
            var waitingForInteractions =
                peskySettings.Enabled && interactionQueue.ActiveCount > 0;
            if (peskySettings.Enabled)
            {
                nextRandomTestAt = -1f;
                nextInteractionDispatchAt = -1f;
                if (waitingForInteractions)
                    nextPeskyAt = -1f;
                else
                {
                    UpdatePeskyMode(available);
                    if (available)
                        ProcessQueue(peskyQueue, true);
                    else
                        nextPeskyDispatchAt = -1f;
                }
            }
            else
            {
                nextPeskyAt = -1f;
                nextPeskyDispatchAt = -1f;
                UpdateRandomTest(available);
                if (available)
                    ProcessQueue(interactionQueue, false);
                else
                    nextInteractionDispatchAt = -1f;
            }

            var interactionState = BuildInteractionState(available);
            if (interactionState != lastInteractionState)
            {
                lastInteractionState = interactionState;
                server.SetInteractionsState(interactionState);
            }
            var peskyState = BuildPeskyState(
                available, waitingForInteractions);
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
        }

        internal int StreamQueueAvailableCapacity
        {
            get { return interactionQueue.AvailableCapacity; }
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
                item, donor, giftImagePath, quantity, 0f);
            lastItem = item ?? string.Empty;
            if (added <= 0)
            {
                feedbackCode = "queue_full";
                SetInteractionFeedback(feedbackCode, true);
                return 0;
            }

            feedbackCode = peskySettings.Enabled
                ? "queued_paused"
                : "queued";
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
            nextRandomTestAt = -1f;
            nextInteractionDispatchAt = -1f;
            nextPeskyDispatchAt = -1f;
            InvalidateState();
        }

        internal int ClearActiveForPhaseTransition()
        {
            var cleared = CreatorToolsInteractionPresentation
                .ClearActiveActorsForPhaseTransition();
            interactionQueue.ClearActive();
            peskyQueue.Clear();
            for (var i = 0; i < executors.Count; i++)
                executors[i].EndGameplayLevel();
            nextPeskyAt = -1f;
            nextRandomTestAt = -1f;
            nextInteractionDispatchAt = -1f;
            nextPeskyDispatchAt = -1f;
            InvalidateState();
            return cleared;
        }

        private void ProcessInteractionCommand(
            Dictionary<string, string> values)
        {
            if (values.ContainsKey("maxActive") ||
                values.ContainsKey("showGiftImage"))
            {
                SetInteractionSettings(values);
                return;
            }
            if (values.ContainsKey("randomTestEnabled"))
            {
                SetRandomTestEnabled(values);
                return;
            }
            if (values.ContainsKey(
                "phaseTransitionProtectionEnabled"))
            {
                SetPhaseTransitionProtectionEnabled(values);
                return;
            }
            EnqueueInteraction(values);
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

        private void SetRandomTestEnabled(
            Dictionary<string, string> values)
        {
            string value;
            bool enabled;
            if (!values.TryGetValue("randomTestEnabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetInteractionFeedback("invalid_setting", true);
                return;
            }

            if (enabled && peskySettings.Enabled)
            {
                peskySettings.Enabled = false;
                peskyQueue.Clear();
                ResetPeskySchedule();
                peskySettings.Save();
                SetPeskyFeedback("disabled_by_random_test", false);
            }
            randomTestEnabled = enabled;
            randomTestRevision++;
            nextRandomTestAt = -1f;
            SetInteractionFeedback(
                enabled ? "random_test_enabled" : "random_test_disabled",
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
            if (enabled && peskySettings.Names.Count == 0)
            {
                SetPeskyFeedback("names_required", true);
                return;
            }
            if (enabled && peskySettings.EnabledItemCount == 0)
            {
                SetPeskyFeedback("items_required", true);
                return;
            }

            peskySettings.Enabled = enabled;
            if (enabled && randomTestEnabled)
            {
                randomTestEnabled = false;
                randomTestRevision++;
                nextRandomTestAt = -1f;
                SetInteractionFeedback(
                    "random_test_disabled_by_pesky", false);
            }
            if (!enabled)
                peskyQueue.Clear();
            ResetPeskySchedule();
            peskySettings.Save();
            SetPeskyFeedback(enabled ? "enabled" : "disabled", false);
            if (logInfo != null)
                logInfo("Modo Molestoso " +
                    (enabled ? "activado." : "desactivado."));
        }

        private void SetPeskyNames(Dictionary<string, string> values)
        {
            string value;
            values.TryGetValue("names", out value);
            peskySettings.SetNames((value ?? string.Empty).Split(
                new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.None));
            if (peskySettings.Enabled && peskySettings.Names.Count == 0)
            {
                peskySettings.Enabled = false;
                peskyQueue.Clear();
                ResetPeskySchedule();
                SetPeskyFeedback("disabled_invalid_config", true);
            }
            else
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

            // Keep Random Test's no-backlog behavior: select only when every
            // Pesky entry is active and the shared active limit has room.
            if (TotalActiveCount >= MaximumActive ||
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
            if (availableItems.Count == 0 || peskySettings.Names.Count == 0)
                return;

            var item = availableItems[UnityEngine.Random.Range(
                0, availableItems.Count)];
            var name = peskySettings.Names[UnityEngine.Random.Range(
                0, peskySettings.Names.Count)];
            if (peskyQueue.Enqueue(
                    item, name, string.Empty, 1, 0f) <= 0)
                return;

            lastItem = item;
            InvalidateState();
            if (logInfo != null)
                logInfo("Modo Molestoso agrego " + item +
                    " para " + name + ".");
        }

        private void UpdateRandomTest(bool available)
        {
            if (!randomTestEnabled || !available)
            {
                nextRandomTestAt = -1f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (nextRandomTestAt < 0f)
            {
                ScheduleNextRandomTest(now);
                return;
            }
            if (now < nextRandomTestAt)
                return;

            ScheduleNextRandomTest(now);

            // This intentionally matches the original Random Test: manual
            // entries take priority and no automatic backlog is created.
            if (interactionQueue.ActiveCount >= MaximumActive ||
                interactionQueue.Count != interactionQueue.ActiveCount)
                return;

            var availableItems = new List<string>();
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var candidate = CreatorToolsInteractionIds.All[i];
                var executor = FindExecutor(candidate);
                if (executor != null && executor.IsAvailable(candidate))
                    availableItems.Add(candidate);
            }
            if (availableItems.Count == 0)
                return;

            var item = availableItems[UnityEngine.Random.Range(
                0, availableItems.Count)];
            if (interactionQueue.Enqueue(
                item, RandomTestDonor, string.Empty, 1, 0f) <= 0)
                return;

            lastItem = item;
            InvalidateState();
            if (logInfo != null)
                logInfo("Prueba aleatoria de " + item +
                    " agregada para " + RandomTestDonor + ".");
        }

        private void ScheduleNextRandomTest(float now)
        {
            nextRandomTestAt = now + UnityEngine.Random.Range(
                RandomTestMinimumIntervalSeconds,
                RandomTestMaximumIntervalSeconds);
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

        private void EnqueueInteraction(Dictionary<string, string> values)
        {
            string item;
            if (!values.TryGetValue("item", out item))
            {
                SetInteractionFeedback("unknown_item", true);
                return;
            }

            var executor = FindExecutor(item);
            if (executor == null)
            {
                lastItem = item ?? string.Empty;
                SetInteractionFeedback("unknown_item", true);
                return;
            }
            lastItem = item;

            string donor;
            values.TryGetValue("donor", out donor);
            donor = NormalizeDonor(donor);
            var added = interactionQueue.Enqueue(
                item,
                donor,
                string.Empty,
                ParseQuantity(values),
                ParseDelaySeconds(values));
            if (added > 0)
            {
                SetInteractionFeedback(
                    peskySettings.Enabled ? "queued_paused" : "queued", false);
                if (logInfo != null)
                    logInfo(added + " canje(s) de " + item +
                        " agregados a la cola para " + donor + ".");
                return;
            }

            SetInteractionFeedback("queue_full", true);
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

        private void ProcessQueue(
            CreatorToolsInteractionQueue queue, bool pesky)
        {
            if (TotalActiveCount >= MaximumActive)
                return;

            var now = Time.realtimeSinceStartup;
            var nextDispatchAt = pesky
                ? nextPeskyDispatchAt
                : nextInteractionDispatchAt;
            if (nextDispatchAt >= 0f && now < nextDispatchAt)
                return;

            var entry = queue.Peek();
            if (entry == null || !entry.IsReady)
                return;

            var executor = FindExecutor(entry.Item);
            if (executor == null)
            {
                queue.RejectFirst();
                lastItem = entry.Item;
                if (pesky)
                    SetPeskyFeedback("unknown_item", true);
                else
                    SetInteractionFeedback("unknown_item", true);
                return;
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
                queue.ActivateFirst(handle);
                if (pesky)
                    nextPeskyDispatchAt = now +
                        MinimumDispatchSeparationSeconds;
                else
                    nextInteractionDispatchAt = now +
                        MinimumDispatchSeparationSeconds;
                InvalidateState();
                if (logInfo != null)
                    logInfo((pesky ? "Ejecutando ataque molesto #" :
                        "Ejecutando canje #") + entry.Id + " de " +
                        entry.Donor + ".");
                return;
            }

            if (feedbackCode == "native_assets_loading" ||
                feedbackCode == "requires_gameplay_level")
                return;

            queue.RejectFirst();
            lastItem = entry.Item;
            if (pesky)
                SetPeskyFeedback(feedbackCode, true);
            else
                SetInteractionFeedback(feedbackCode, true);
            if (logWarning != null)
                logWarning(entry.Item + " interaction failed: " +
                    (string.IsNullOrEmpty(error)
                        ? "No diagnostic detail was returned."
                        : error));
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

        private int TotalActiveCount
        {
            get
            {
                return interactionQueue.ActiveCount + peskyQueue.ActiveCount;
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
                .Append(",\"suspendedByPesky\":")
                .Append(peskySettings.Enabled ? "true" : "false")
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
                .Append(",\"randomTestEnabled\":")
                .Append(randomTestEnabled ? "true" : "false")
                .Append(",\"randomTestRevision\":")
                .Append(randomTestRevision)
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

        private string BuildPeskyState(
            bool available, bool waitingForInteractions)
        {
            var running = peskySettings.Enabled && available &&
                !gameplayLevelLoadPending &&
                !waitingForInteractions;
            var startingBattle = peskySettings.Enabled &&
                (gameplayLevelLoadPending ||
                 (gameplayLevelActive &&
                  !gameplayAvailabilityObserved)) &&
                !waitingForInteractions;
            var builder = new StringBuilder(4096);
            builder.Append("{\"ready\":true,\"available\":")
                .Append(available ? "true" : "false")
                .Append(",\"enabled\":")
                .Append(peskySettings.Enabled ? "true" : "false")
                .Append(",\"running\":").Append(running ? "true" : "false")
                .Append(",\"startingBattle\":")
                .Append(startingBattle ? "true" : "false")
                .Append(",\"waitingForInteractions\":")
                .Append(waitingForInteractions ? "true" : "false")
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
                .Append(",\"pausedInteractionCount\":")
                .Append(interactionQueue.Count)
                .Append(",\"pausedInteractionActiveCount\":")
                .Append(interactionQueue.ActiveCount)
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
                var executor = FindExecutor(item);
                if (executor != null && executor.IsAvailable(item))
                    return true;
            }
            return false;
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
            interactionQueue.Dispose();
            peskyQueue.Dispose();
            for (var i = 0; i < executors.Count; i++)
                executors[i].Dispose();
            executors.Clear();
            CreatorToolsGiftImageCache.Clear();
            nextPeskyAt = -1f;
            nextRandomTestAt = -1f;
            nextInteractionDispatchAt = -1f;
            nextPeskyDispatchAt = -1f;
            InvalidateState();
        }
    }
}
