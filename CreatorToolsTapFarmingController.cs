using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsTapFarmingObservation
    {
        internal readonly string Feedback;

        internal CreatorToolsTapFarmingObservation(string feedback)
        {
            Feedback = feedback ?? string.Empty;
        }
    }

    /// <summary>
    /// Counts community likes outside Unity and exposes a main-thread bridge
    /// that absorbs player damage before Cuphead changes native boss health.
    /// Native health is never increased, so completed phases cannot regress.
    /// </summary>
    internal sealed class CreatorToolsTapFarmingController
    {
        private const int SchemaVersion = 2;
        private const double MaximumReserveHealth = 1000000000000d;
        private const float BossSnapshotIntervalSeconds = 0.1f;
        private const int DicePalaceProgressSegmentCount = 4;
        private readonly object stateLock = new object();
        private readonly CreatorToolsTapFarmingSettings settings;
        private readonly CreatorToolsLiveEventsCoordinator liveEvents;
        private readonly Action<string> logInfo;

        private string phase = "off";
        private int sessionId;
        private int attempt;
        private int activeLevelInstanceId = -1;
        private string levelId = string.Empty;
        private string bossName = string.Empty;
        private bool dicePalaceTransition;
        private int dicePalaceTransitionLevelInstanceId = -1;
        private bool dicePalaceAttemptActive;
        private bool dicePalaceMainActive;
        private int dicePalaceCompletedSegments;
        private float dicePalaceCurrentSegmentProgress;
        private bool gameplayAvailable;
        private long totalTaps;
        private long unconvertedTaps;
        private long convertedHealth;
        private double reserveHealth;
        private double spentHealth;
        private double spentHealthAtAttemptStart;
        private float bossCurrentHealth;
        private float bossTotalHealth;
        private float currentPhaseProgress;
        private float overallProgress;
        private int phaseIndex;
        private int phaseCount;
        private PhaseProgress[] phaseProgress = new PhaseProgress[0];
        private int revision;
        private string feedback = "ready";
        private bool error;
        private string lastState;
        private CreatorToolsLiveEventLease liveEventLease;

        // Unity object references are only inspected on the main thread. A
        // background stop may atomically clear this handle, but never calls
        // into it; worker projections use the copied primitive fields above.
        private object bossPropertiesMainThread;
        private float nextBossSnapshotAtMainThread;

        internal CreatorToolsTapFarmingController(
            string pluginConfigPath,
            CreatorToolsLiveEventsCoordinator liveEvents,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            settings = CreatorToolsTapFarmingSettings.Load(
                pluginConfigPath, logWarning);
            this.liveEvents = liveEvents;
            this.logInfo = logInfo;
        }

        internal bool Enabled
        {
            get
            {
                lock (stateLock)
                    return phase != "off" && phase != "stopping";
            }
        }

        internal string Phase
        {
            get
            {
                lock (stateLock)
                    return phase;
            }
        }

        internal CreatorToolsTapFarmingObservation ObserveStreamEvent(
            CreatorToolsStreamEvent entry)
        {
            if (entry == null ||
                !string.Equals(entry.Platform, "tiktok",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(entry.Type, "like",
                    StringComparison.OrdinalIgnoreCase) ||
                entry.Count <= 0)
                return new CreatorToolsTapFarmingObservation(string.Empty);

            lock (stateLock)
            {
                if (!CountsTapsLocked())
                    return new CreatorToolsTapFarmingObservation(
                        string.Empty);

                var incoming = (long)entry.Count;
                totalTaps = SaturatingAdd(totalTaps, incoming);
                var combined = SaturatingAdd(unconvertedTaps, incoming);
                var tapsPerConversion = Math.Max(1,
                    settings.TapsPerConversion);
                var conversions = combined / tapsPerConversion;
                unconvertedTaps = combined % tapsPerConversion;
                var points = SaturatingMultiply(conversions,
                    settings.HealthPointsPerConversion);
                if (points > 0L)
                {
                    convertedHealth = SaturatingAdd(
                        convertedHealth, points);
                    reserveHealth = Math.Min(MaximumReserveHealth,
                        reserveHealth + points);
                }
                SetFeedbackLocked("tap_farming_counted", false);
                return new CreatorToolsTapFarmingObservation(
                    "tap_farming_counted");
            }
        }

        internal bool ProcessBackgroundCommand(string query)
        {
            var values = ParseQuery(query);
            string action;
            if (!values.TryGetValue("operation", out action))
                values.TryGetValue("action", out action);
            action = (action ?? string.Empty).Trim().ToLowerInvariant();

            if (action == "activate" || action == "arm" ||
                action == "start")
                return TryActivate(values);
            if (action == "deactivate" || action == "disable" ||
                action == "off" || action == "cancel" ||
                action == "finish" || action == "reset")
                return BeginStop();
            if (action == "save" || action == "configure" ||
                (action.Length == 0 &&
                 (values.ContainsKey("tapsPerConversion") ||
                  values.ContainsKey("healthPointsPerConversion") ||
                  values.ContainsKey("tapsPerHealthPoint"))))
                return SaveSettings(values);

            SetFeedback("invalid_action", true);
            return true;
        }

        internal void Update(
            CreatorToolsServer server, bool gameplayAvailable)
        {
            lock (stateLock)
            {
                if (this.gameplayAvailable != gameplayAvailable)
                {
                    this.gameplayAvailable = gameplayAvailable;
                    TouchLocked();
                }
            }
            TryAttachCurrentLevelMainThread(gameplayAvailable);
            RefreshBossSnapshotMainThread();
            PublishState(server);
        }

        internal void PublishState(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;
            var liveSnapshot = liveEvents == null
                ? new CreatorToolsLiveEventsSnapshot(
                    string.Empty, "idle", 0L, 0)
                : liveEvents.Snapshot;
            lock (stateLock)
            {
                var state = BuildStateLocked(liveSnapshot);
                if (state == lastState)
                    return;
                lastState = state;
                server.SetTapFarmingState(state);
            }
        }

        internal void InvalidateState()
        {
            lock (stateLock)
            {
                lastState = null;
                TouchLocked();
            }
        }

        internal void OnLevelStarted(Level level)
        {
            if (level == null || level.LevelType != Level.Type.Battle)
                return;
            var currentLevel = level.CurrentLevel;
            var isDicePalaceMain =
                currentLevel.ToString() == "DicePalaceMain";
            var logicalLevel = LogicalLevel(currentLevel);
            var levelInstanceId = level.GetInstanceID();
            lock (stateLock)
            {
                if (!CountsTapsLocked())
                    return;

                // Dice Palace swaps Level instances for every internal
                // miniboss. Treat the whole chain as one attempt and rebind
                // immediately when the next board replaces the old Level
                // before its teardown hook arrives.
                if ((phase == "active" || phase == "transition") &&
                    levelInstanceId == activeLevelInstanceId)
                    return;

                // The ending scene can remain exposed briefly through
                // Level.Current after its teardown hook. Do not consume the
                // Dice Palace handoff marker by attaching that same miniboss
                // again; wait for the next Level instance instead.
                if (dicePalaceTransition &&
                    levelInstanceId ==
                        dicePalaceTransitionLevelInstanceId)
                    return;

                var continuesDicePalace = dicePalaceTransition &&
                    logicalLevel == "DicePalace";
                var isDicePalace = logicalLevel == "DicePalace";
                activeLevelInstanceId = levelInstanceId;
                levelId = currentLevel.ToString();
                bossName = levelId;
                if (!continuesDicePalace)
                {
                    attempt++;
                    if (attempt <= 0)
                        attempt = 1;
                    spentHealthAtAttemptStart = spentHealth;
                    dicePalaceAttemptActive = isDicePalace;
                    dicePalaceMainActive = isDicePalaceMain;
                    dicePalaceCompletedSegments = 0;
                    dicePalaceCurrentSegmentProgress = 0f;
                }
                else
                {
                    dicePalaceAttemptActive = true;
                    dicePalaceMainActive = isDicePalaceMain;
                    if (isDicePalaceMain ||
                        dicePalaceCompletedSegments <
                            DicePalaceProgressSegmentCount - 1)
                        dicePalaceCurrentSegmentProgress = 0f;
                }
                dicePalaceTransition = false;
                dicePalaceTransitionLevelInstanceId = -1;
                phase = "active";
                if (dicePalaceAttemptActive)
                    SetDicePalaceProjectionLocked(
                        dicePalaceCurrentSegmentProgress, true);
                else
                    ClearBossProjectionLocked(false);
                SetFeedbackLocked("tap_farming_battle_started", false);
            }
            bossPropertiesMainThread = null;
            nextBossSnapshotAtMainThread = 0f;
        }

        internal void OnLevelDefeated(Level level)
        {
            var instanceId = level == null ? -1 : level.GetInstanceID();
            lock (stateLock)
            {
                if (!MatchesActiveLevelLocked(instanceId) ||
                    (phase != "active" && phase != "transition"))
                    return;
                phase = "collecting";
                activeLevelInstanceId = -1;
                dicePalaceTransition = false;
                dicePalaceTransitionLevelInstanceId = -1;
                dicePalaceMainActive = false;
                if (dicePalaceAttemptActive)
                {
                    dicePalaceCompletedSegments = 0;
                    dicePalaceCurrentSegmentProgress = 0f;
                    SetDicePalaceProjectionLocked(0f, false);
                }
                else
                    ClearBossProjectionLocked(false);
                SetFeedbackLocked(
                    "tap_farming_lost_collecting", false);
            }
            bossPropertiesMainThread = null;
        }

        internal void OnLevelPreWin(Level level)
        {
            if (level == null)
                return;
            var instanceId = level.GetInstanceID();
            var currentLevel = level.CurrentLevel.ToString();
            lock (stateLock)
            {
                if (!MatchesActiveLevelLocked(instanceId) ||
                    (phase != "active" && phase != "transition"))
                    return;

                // Internal Dice Palace miniboss wins only hand control back
                // to the board. Keep counting taps and preserve the virtual
                // reserve; the live event completes exclusively when King
                // Dice's real DicePalaceMain fight is won.
                if (LogicalLevel(level.CurrentLevel) == "DicePalace" &&
                    currentLevel != "DicePalaceMain")
                {
                    dicePalaceAttemptActive = true;
                    dicePalaceMainActive = false;
                    var advancedSegment =
                        dicePalaceCompletedSegments <
                        DicePalaceProgressSegmentCount - 1;
                    if (advancedSegment)
                    {
                        dicePalaceCompletedSegments++;
                        dicePalaceCurrentSegmentProgress = 0f;
                    }
                    dicePalaceTransition = true;
                    dicePalaceTransitionLevelInstanceId = instanceId;
                    phase = "transition";
                    SetDicePalaceProjectionLocked(
                        dicePalaceCurrentSegmentProgress,
                        !advancedSegment);
                    SetFeedbackLocked(
                        "tap_farming_waiting_for_boss", false);
                    return;
                }

                phase = "completed";
                activeLevelInstanceId = -1;
                dicePalaceTransition = false;
                dicePalaceTransitionLevelInstanceId = -1;
                dicePalaceMainActive = false;
                if (dicePalaceAttemptActive)
                {
                    dicePalaceCompletedSegments =
                        DicePalaceProgressSegmentCount;
                    dicePalaceCurrentSegmentProgress = 1f;
                }
                bossCurrentHealth = 0f;
                currentPhaseProgress = 1f;
                overallProgress = 1f;
                if (phaseProgress.Length > 0)
                {
                    for (var i = 0; i < phaseProgress.Length; i++)
                        phaseProgress[i] = new PhaseProgress(
                            i + 1, "complete", 1f);
                    phaseIndex = phaseProgress.Length;
                    phaseCount = phaseProgress.Length;
                }
                SetFeedbackLocked("tap_farming_completed", false);
            }
            bossPropertiesMainThread = null;
        }

        internal void OnLevelEnded(Level level)
        {
            var instanceId = level == null ? -1 : level.GetInstanceID();
            lock (stateLock)
            {
                if (!MatchesActiveLevelLocked(instanceId) ||
                    (phase != "active" && phase != "transition"))
                    return;
                phase = "collecting";
                activeLevelInstanceId = -1;
                if (dicePalaceTransition &&
                    dicePalaceAttemptActive)
                    SetDicePalaceProjectionLocked(
                        dicePalaceCurrentSegmentProgress,
                        dicePalaceCompletedSegments >=
                            DicePalaceProgressSegmentCount - 1 &&
                        dicePalaceCurrentSegmentProgress > 0f);
                else
                    ClearBossProjectionLocked(false);
                SetFeedbackLocked(
                    "tap_farming_waiting_for_boss", false);
            }
            bossPropertiesMainThread = null;
        }

        internal void OnPhaseTransition()
        {
            lock (stateLock)
            {
                if (phase != "active")
                    return;
                phase = "transition";
                SetFeedbackLocked(
                    "tap_farming_phase_transition", false);
            }
        }

        /// <summary>
        /// Runs only inside a player DamageDealer call. Returns false when
        /// the complete hit was consumed and native DealDamage must be
        /// skipped.
        /// </summary>
        internal bool PrepareBossDamage(
            object properties, ref float damage)
        {
            if (properties == null || damage <= 0f)
                return true;

            if (!ReferenceEquals(bossPropertiesMainThread, properties))
            {
                if (!BossProgressReader.IsSupported(properties))
                    return true;
                nextBossSnapshotAtMainThread = 0f;
            }
            bossPropertiesMainThread = properties;
            lock (stateLock)
            {
                if (phase != "active" && phase != "transition")
                    return true;
                if (phase == "transition")
                    phase = "active";

                var absorbed = Math.Min(
                    reserveHealth, Math.Max(0d, (double)damage));
                if (absorbed > 0d)
                {
                    reserveHealth = Math.Max(0d,
                        reserveHealth - absorbed);
                    spentHealth = Math.Min(MaximumReserveHealth,
                        spentHealth + absorbed);
                    damage = (float)Math.Max(0d, damage - absorbed);
                    SetFeedbackLocked(
                        "tap_farming_damage_absorbed", false);
                }
                return damage > 0.00001f;
            }
        }

        internal void ObserveBossDamage(object properties)
        {
            if (properties == null)
                return;
            if (!ReferenceEquals(bossPropertiesMainThread, properties))
            {
                if (!BossProgressReader.IsSupported(properties))
                    return;
                nextBossSnapshotAtMainThread = 0f;
            }
            bossPropertiesMainThread = properties;
            RefreshBossSnapshotMainThread();
        }

        private bool TryActivate(Dictionary<string, string> values)
        {
            int tapsPerConversion;
            int healthPointsPerConversion;
            if (!TryReadConversion(values,
                    settings.TapsPerConversion,
                    settings.HealthPointsPerConversion,
                    out tapsPerConversion,
                    out healthPointsPerConversion))
            {
                SetFeedback("invalid_taps_per_health_point", true);
                return true;
            }

            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked("invalid_action", true);
                    return true;
                }
            }

            CreatorToolsLiveEventLease lease;
            string blockingEvent;
            if (liveEvents == null || !liveEvents.TryAcquire(
                    CreatorToolsLiveEventIds.TapFarming,
                    out lease, out blockingEvent))
            {
                SetFeedback("blocked_by_live_event", true);
                return true;
            }

            var committed = false;
            try
            {
                settings.SetConversion(
                    tapsPerConversion, healthPointsPerConversion);
                settings.Save();
                lock (stateLock)
                {
                    if (phase != "off")
                        return true;
                    liveEventLease = lease;
                    ResetSessionCountersLocked();
                    sessionId++;
                    if (sessionId <= 0)
                        sessionId = 1;
                    phase = "collecting";
                    SetFeedbackLocked("tap_farming_activated", false);
                    committed = true;
                }
            }
            finally
            {
                if (!committed)
                    liveEvents.CompleteRelease(lease);
            }
            if (committed && logInfo != null)
                logInfo("Farmeando taps activado: cada " +
                    tapsPerConversion.ToString(
                        CultureInfo.InvariantCulture) +
                    " taps suma " +
                    healthPointsPerConversion.ToString(
                        CultureInfo.InvariantCulture) +
                    " puntos de vida.");
            return true;
        }

        private bool SaveSettings(Dictionary<string, string> values)
        {
            int tapsPerConversion;
            int healthPointsPerConversion;
            if (!TryReadConversion(values,
                    settings.TapsPerConversion,
                    settings.HealthPointsPerConversion,
                    out tapsPerConversion,
                    out healthPointsPerConversion))
            {
                SetFeedback("invalid_taps_per_health_point", true);
                return true;
            }
            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked(
                        "tap_farming_setting_locked", true);
                    return true;
                }
                settings.SetConversion(
                    tapsPerConversion, healthPointsPerConversion);
                SetFeedbackLocked("tap_farming_settings_saved", false);
            }
            settings.Save();
            return true;
        }

        private bool BeginStop()
        {
            CreatorToolsLiveEventLease lease;
            lock (stateLock)
            {
                if (phase == "off")
                {
                    SetFeedbackLocked("tap_farming_already_off", false);
                    return true;
                }
                if (phase == "stopping")
                    return true;
                phase = "stopping";
                lease = liveEventLease;
                SetFeedbackLocked("tap_farming_stopping", false);
            }
            if (lease != null && liveEvents != null)
                liveEvents.BeginStopping(lease);

            // Tap Farming has no Unity-side actor or queue to tear down.
            // Finish the logical stop on the HTTP/worker thread so another
            // Live Event can be selected even while Cuphead is unfocused.
            lock (stateLock)
            {
                bossPropertiesMainThread = null;
                nextBossSnapshotAtMainThread = 0f;
                if (ReferenceEquals(liveEventLease, lease) &&
                    phase == "stopping")
                {
                    liveEventLease = null;
                    phase = "off";
                    activeLevelInstanceId = -1;
                    dicePalaceTransition = false;
                    dicePalaceTransitionLevelInstanceId = -1;
                    ResetSessionCountersLocked();
                    SetFeedbackLocked(
                        "tap_farming_deactivated", false);
                }
            }
            if (lease != null && liveEvents != null)
                liveEvents.CompleteRelease(lease);
            return true;
        }

        private void RefreshBossSnapshotMainThread()
        {
            var properties = bossPropertiesMainThread;
            if (properties == null)
                return;
            var now = Time.realtimeSinceStartup;
            if (now < nextBossSnapshotAtMainThread)
                return;
            nextBossSnapshotAtMainThread = now +
                BossSnapshotIntervalSeconds;
            BossProgressSnapshot snapshot;
            if (!BossProgressReader.TryRead(properties, out snapshot))
                return;
            lock (stateLock)
            {
                if (phase == "active" || phase == "transition")
                    ApplyBossSnapshotLocked(snapshot);
            }
        }

        private void TryAttachCurrentLevelMainThread(bool levelAvailable)
        {
            if (!levelAvailable)
                return;
            lock (stateLock)
                if (phase != "collecting")
                    return;
            try
            {
                var level = Level.Current;
                if (level != null && !level.Ending &&
                    level.LevelType == Level.Type.Battle)
                    OnLevelStarted(level);
            }
            catch
            {
                // Scene transitions briefly invalidate Level.Current.
            }
        }

        private void ApplyBossSnapshotLocked(BossProgressSnapshot snapshot)
        {
            if (dicePalaceAttemptActive)
            {
                ApplyDicePalaceBossSnapshotLocked(snapshot);
                return;
            }
            if (Mathf.Approximately(
                    bossCurrentHealth, snapshot.CurrentHealth) &&
                Mathf.Approximately(
                    bossTotalHealth, snapshot.TotalHealth) &&
                Mathf.Approximately(
                    currentPhaseProgress,
                    snapshot.CurrentPhaseProgress) &&
                Mathf.Approximately(
                    overallProgress, snapshot.OverallProgress) &&
                phaseIndex == snapshot.PhaseIndex &&
                phaseCount == snapshot.PhaseCount &&
                SamePhases(phaseProgress, snapshot.Phases))
                return;
            bossCurrentHealth = snapshot.CurrentHealth;
            bossTotalHealth = snapshot.TotalHealth;
            currentPhaseProgress = snapshot.CurrentPhaseProgress;
            overallProgress = snapshot.OverallProgress;
            phaseIndex = snapshot.PhaseIndex;
            phaseCount = snapshot.PhaseCount;
            phaseProgress = snapshot.Phases;
            TouchLocked();
        }

        private void ApplyDicePalaceBossSnapshotLocked(
            BossProgressSnapshot snapshot)
        {
            // A route can include more than three minibosses. Once the three
            // global setup segments are complete, keep the fight at 75%
            // until DicePalaceMain actually begins; otherwise an extra board
            // would make the overlay reach 100% and then jump backwards.
            var snapshotProgress =
                dicePalaceCompletedSegments >=
                    DicePalaceProgressSegmentCount - 1 &&
                !dicePalaceMainActive
                    ? 0f
                    : Mathf.Clamp01(snapshot.OverallProgress);
            var progress = Mathf.Max(
                dicePalaceCurrentSegmentProgress,
                snapshotProgress);
            dicePalaceCurrentSegmentProgress = progress;
            var phases = BuildDicePalacePhases(progress, true);
            var completed = Math.Max(0, Math.Min(
                DicePalaceProgressSegmentCount - 1,
                dicePalaceCompletedSegments));
            var globalProgress = Mathf.Clamp01(
                (completed + progress) /
                DicePalaceProgressSegmentCount);
            var currentIndex = Math.Min(
                DicePalaceProgressSegmentCount, completed + 1);

            if (Mathf.Approximately(
                    bossCurrentHealth, snapshot.CurrentHealth) &&
                Mathf.Approximately(
                    bossTotalHealth, snapshot.TotalHealth) &&
                Mathf.Approximately(
                    currentPhaseProgress, progress) &&
                Mathf.Approximately(
                    overallProgress, globalProgress) &&
                phaseIndex == currentIndex &&
                phaseCount == DicePalaceProgressSegmentCount &&
                SamePhases(phaseProgress, phases))
                return;

            bossCurrentHealth = snapshot.CurrentHealth;
            bossTotalHealth = snapshot.TotalHealth;
            currentPhaseProgress = progress;
            overallProgress = globalProgress;
            phaseIndex = currentIndex;
            phaseCount = DicePalaceProgressSegmentCount;
            phaseProgress = phases;
            TouchLocked();
        }

        private void SetDicePalaceProjectionLocked(
            float progress, bool activeSegment)
        {
            progress = Mathf.Clamp01(progress);
            dicePalaceCurrentSegmentProgress = progress;
            var completed = Math.Max(0, Math.Min(
                DicePalaceProgressSegmentCount - 1,
                dicePalaceCompletedSegments));
            bossCurrentHealth = 0f;
            bossTotalHealth = 0f;
            currentPhaseProgress = progress;
            overallProgress = Mathf.Clamp01(
                (completed + progress) /
                DicePalaceProgressSegmentCount);
            phaseIndex = Math.Min(
                DicePalaceProgressSegmentCount, completed + 1);
            phaseCount = DicePalaceProgressSegmentCount;
            phaseProgress = BuildDicePalacePhases(
                progress, activeSegment);
            TouchLocked();
        }

        private PhaseProgress[] BuildDicePalacePhases(
            float progress, bool activeSegment)
        {
            var completed = Math.Max(0, Math.Min(
                DicePalaceProgressSegmentCount - 1,
                dicePalaceCompletedSegments));
            var phases = new PhaseProgress[
                DicePalaceProgressSegmentCount];
            for (var i = 0; i < phases.Length; i++)
            {
                var isComplete = i < completed;
                var isActive = activeSegment && i == completed;
                phases[i] = new PhaseProgress(
                    i + 1,
                    isComplete ? "complete" :
                        isActive ? "active" : "pending",
                    isComplete ? 1f : isActive ? progress : 0f);
            }
            return phases;
        }

        private static bool SamePhases(
            PhaseProgress[] left, PhaseProgress[] right)
        {
            left = left ?? new PhaseProgress[0];
            right = right ?? new PhaseProgress[0];
            if (left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
                if (left[i].Index != right[i].Index ||
                    left[i].Status != right[i].Status ||
                    !Mathf.Approximately(
                        left[i].Progress, right[i].Progress))
                    return false;
            return true;
        }

        private string BuildStateLocked(
            CreatorToolsLiveEventsSnapshot liveSnapshot)
        {
            var ownsEvent = liveEventLease != null &&
                liveSnapshot.ActiveEvent ==
                    CreatorToolsLiveEventIds.TapFarming &&
                liveSnapshot.Epoch == liveEventLease.Epoch;
            var blockedBy = liveSnapshot.ActiveEvent;
            if (ownsEvent)
                blockedBy = string.Empty;
            var tapsPerConversion = Math.Max(1,
                settings.TapsPerConversion);
            var healthPointsPerConversion = Math.Max(1,
                settings.HealthPointsPerConversion);
            var bankedTaps = Math.Max(0d,
                reserveHealth * tapsPerConversion /
                healthPointsPerConversion + unconvertedTaps);
            var legacyTapsPerHealthPoint =
                (double)tapsPerConversion /
                healthPointsPerConversion;
            var spentDuringAttempt = Math.Max(0d,
                spentHealth - spentHealthAtAttemptStart);
            var effectiveHealth =
                CreatorToolsTapFarmingEffectiveHealth.Calculate(
                    phase, bossCurrentHealth, bossTotalHealth,
                    reserveHealth, spentDuringAttempt);

            var builder = new StringBuilder(3072);
            builder.Append("{\"ready\":true,\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"revision\":").Append(revision)
                .Append(",\"phase\":\"");
            CreatorToolsJson.AppendEscaped(builder, phase);
            builder.Append("\",\"sessionId\":").Append(sessionId)
                .Append(",\"attempt\":").Append(attempt)
                .Append(",\"enabled\":")
                .Append(CountsTapsLocked() ? "true" : "false")
                .Append(",\"isLiveEventOwner\":")
                .Append(ownsEvent ? "true" : "false")
                .Append(",\"blockedByLiveEvent\":\"");
            CreatorToolsJson.AppendEscaped(builder, blockedBy);
            builder.Append("\",\"gameplayAvailable\":")
                .Append(gameplayAvailable ? "true" : "false")
                .Append(",\"levelId\":\"");
            CreatorToolsJson.AppendEscaped(builder, levelId);
            builder.Append("\",\"bossName\":\"");
            CreatorToolsJson.AppendEscaped(builder, bossName);
            builder.Append("\",\"conversion\":{\"tapsPerConversion\":")
                .Append(tapsPerConversion)
                .Append(",\"healthPointsPerConversion\":")
                .Append(healthPointsPerConversion)
                .Append(",\"tapsPerHealthPoint\":")
                .Append(FormatNumber(
                    legacyTapsPerHealthPoint, "0.##########"))
                .Append("},\"counters\":{\"totalTaps\":")
                .Append(totalTaps)
                .Append(",\"bankedTaps\":")
                .Append(FormatNumber(bankedTaps, "0.###"))
                .Append(",\"unconvertedTaps\":")
                .Append(unconvertedTaps)
                .Append(",\"convertedHealth\":")
                .Append(convertedHealth)
                .Append(",\"reserveHealth\":")
                .Append(FormatNumber(reserveHealth, "0.###"))
                .Append(",\"spentHealth\":")
                .Append(FormatNumber(spentHealth, "0.###"))
                .Append("},\"boss\":{\"currentHealth\":")
                .Append(FormatNumber(bossCurrentHealth, "0.###"))
                .Append(",\"totalHealth\":")
                .Append(FormatNumber(bossTotalHealth, "0.###"))
                .Append(",\"progress\":")
                .Append(FormatNumber(currentPhaseProgress, "0.####"))
                .Append("},\"effectiveHealth\":{\"available\":")
                .Append(effectiveHealth.Available ? "true" : "false")
                .Append(",\"current\":")
                .Append(FormatNumber(effectiveHealth.Current, "0.###"))
                .Append(",\"total\":")
                .Append(FormatNumber(effectiveHealth.Total, "0.###"))
                .Append(",\"ratio\":")
                .Append(FormatNumber(effectiveHealth.Ratio, "0.####"))
                .Append("},\"phaseIndex\":").Append(phaseIndex)
                .Append(",\"phaseCount\":").Append(phaseCount)
                .Append(",\"overallProgress\":")
                .Append(FormatNumber(overallProgress, "0.####"))
                .Append(",\"phases\":[");
            for (var i = 0; i < phaseProgress.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append("{\"index\":")
                    .Append(phaseProgress[i].Index)
                    .Append(",\"status\":\"");
                CreatorToolsJson.AppendEscaped(
                    builder, phaseProgress[i].Status);
                builder.Append("\",\"progress\":")
                    .Append(FormatNumber(
                        phaseProgress[i].Progress, "0.####"))
                    .Append('}');
            }
            builder.Append("],\"feedback\":\"");
            CreatorToolsJson.AppendEscaped(builder, feedback);
            builder.Append("\",\"error\":")
                .Append(error ? "true" : "false")
                .Append('}');
            return builder.ToString();
        }

        private void ResetSessionCountersLocked()
        {
            totalTaps = 0L;
            unconvertedTaps = 0L;
            convertedHealth = 0L;
            reserveHealth = 0d;
            spentHealth = 0d;
            spentHealthAtAttemptStart = 0d;
            attempt = 0;
            dicePalaceTransition = false;
            dicePalaceTransitionLevelInstanceId = -1;
            dicePalaceAttemptActive = false;
            dicePalaceMainActive = false;
            dicePalaceCompletedSegments = 0;
            dicePalaceCurrentSegmentProgress = 0f;
            levelId = string.Empty;
            bossName = string.Empty;
            ClearBossProjectionLocked(true);
        }

        private void ClearBossProjectionLocked(bool clearLevel)
        {
            bossCurrentHealth = 0f;
            bossTotalHealth = 0f;
            currentPhaseProgress = 0f;
            overallProgress = 0f;
            phaseIndex = 0;
            phaseCount = 0;
            phaseProgress = new PhaseProgress[0];
            if (clearLevel)
            {
                levelId = string.Empty;
                bossName = string.Empty;
            }
            TouchLocked();
        }

        private bool CountsTapsLocked()
        {
            return phase == "collecting" || phase == "active" ||
                phase == "transition";
        }

        private bool MatchesActiveLevelLocked(int instanceId)
        {
            return instanceId >= 0 && activeLevelInstanceId >= 0 &&
                instanceId == activeLevelInstanceId;
        }

        private static string LogicalLevel(Levels level)
        {
            var name = level.ToString();
            return name.StartsWith("DicePalace", StringComparison.Ordinal)
                ? "DicePalace"
                : name;
        }

        private void SetFeedback(string value, bool isError)
        {
            lock (stateLock)
                SetFeedbackLocked(value, isError);
        }

        private void SetFeedbackLocked(string value, bool isError)
        {
            feedback = value ?? string.Empty;
            error = isError;
            TouchLocked();
        }

        private void TouchLocked()
        {
            revision++;
            if (revision < 0)
                revision = 1;
            lastState = null;
        }

        private static long SaturatingAdd(long left, long right)
        {
            if (right <= 0L)
                return left;
            return left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

        private static long SaturatingMultiply(long value, int factor)
        {
            if (value <= 0L || factor <= 0)
                return 0L;
            return value > long.MaxValue / factor
                ? long.MaxValue
                : value * factor;
        }

        private static bool TryReadConversion(
            Dictionary<string, string> values,
            int fallbackTapsPerConversion,
            int fallbackHealthPointsPerConversion,
            out int tapsPerConversion,
            out int healthPointsPerConversion)
        {
            var hasCanonicalTaps =
                values.ContainsKey("tapsPerConversion");
            var hasCanonicalHealth =
                values.ContainsKey("healthPointsPerConversion");
            if (!hasCanonicalTaps && !hasCanonicalHealth)
            {
                string legacyValue;
                if (!values.TryGetValue("tapsPerHealthPoint",
                        out legacyValue))
                {
                    tapsPerConversion = fallbackTapsPerConversion;
                    healthPointsPerConversion =
                        fallbackHealthPointsPerConversion;
                    return true;
                }
                if (!TryParseConversionValue(
                        legacyValue, out tapsPerConversion))
                {
                    tapsPerConversion = fallbackTapsPerConversion;
                    healthPointsPerConversion =
                        fallbackHealthPointsPerConversion;
                    return false;
                }
                healthPointsPerConversion = 1;
                return true;
            }

            tapsPerConversion = fallbackTapsPerConversion;
            healthPointsPerConversion =
                fallbackHealthPointsPerConversion;
            string value;
            if (hasCanonicalTaps &&
                (!values.TryGetValue("tapsPerConversion", out value) ||
                 !TryParseConversionValue(
                    value, out tapsPerConversion)))
                return false;
            if (hasCanonicalHealth &&
                (!values.TryGetValue("healthPointsPerConversion",
                    out value) ||
                 !TryParseConversionValue(
                    value, out healthPointsPerConversion)))
                return false;
            return true;
        }

        private static bool TryParseConversionValue(
            string value, out int result)
        {
            if (!int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out result) ||
                result < CreatorToolsTapFarmingSettings
                    .MinimumConversionValue ||
                result > CreatorToolsTapFarmingSettings
                    .MaximumConversionValue)
            {
                result = 0;
                return false;
            }
            return true;
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
                    key = Uri.UnescapeDataString(key.Replace('+', ' '));
                    value = Uri.UnescapeDataString(
                        value.Replace('+', ' '));
                }
                catch
                {
                    continue;
                }
                if (key.Length <= 64 && value.Length <= 2048)
                    values[key] = value;
            }
            return values;
        }

        private static string FormatNumber(double value, string format)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                value = 0d;
            return Math.Max(0d, value).ToString(
                format, CultureInfo.InvariantCulture);
        }

        private sealed class PhaseProgress
        {
            internal readonly int Index;
            internal readonly string Status;
            internal readonly float Progress;

            internal PhaseProgress(
                int index, string status, float progress)
            {
                Index = index;
                Status = status ?? "pending";
                Progress = Mathf.Clamp01(progress);
            }
        }

        private sealed class BossProgressSnapshot
        {
            internal float CurrentHealth;
            internal float TotalHealth;
            internal float CurrentPhaseProgress;
            internal float OverallProgress;
            internal int PhaseIndex;
            internal int PhaseCount;
            internal PhaseProgress[] Phases = new PhaseProgress[0];
        }

        private static class BossProgressReader
        {
            private static readonly object accessorsLock = new object();
            private static readonly Dictionary<Type, BossAccessor> accessors =
                new Dictionary<Type, BossAccessor>();

            internal static bool TryRead(
                object instance, out BossProgressSnapshot snapshot)
            {
                snapshot = null;
                if (instance == null)
                    return false;
                try
                {
                    var accessor = GetAccessor(instance.GetType());
                    if (accessor == null)
                        return false;
                    return accessor.TryRead(instance, out snapshot);
                }
                catch
                {
                    snapshot = null;
                    return false;
                }
            }

            internal static bool IsSupported(object instance)
            {
                if (instance == null)
                    return false;
                try { return GetAccessor(instance.GetType()) != null; }
                catch { return false; }
            }

            private static BossAccessor GetAccessor(Type type)
            {
                lock (accessorsLock)
                {
                    BossAccessor accessor;
                    if (accessors.TryGetValue(type, out accessor))
                        return accessor;
                    accessor = BossAccessor.Create(type);
                    accessors[type] = accessor;
                    return accessor;
                }
            }

            private sealed class BossAccessor
            {
                private readonly PropertyInfo currentHealth;
                private readonly FieldInfo totalHealth;
                private readonly FieldInfo stateIndex;
                private readonly FieldInfo states;

                private BossAccessor(
                    PropertyInfo currentHealth,
                    FieldInfo totalHealth,
                    FieldInfo stateIndex,
                    FieldInfo states)
                {
                    this.currentHealth = currentHealth;
                    this.totalHealth = totalHealth;
                    this.stateIndex = stateIndex;
                    this.states = states;
                }

                internal static BossAccessor Create(Type type)
                {
                    var current = FindProperty(type, "CurrentHealth");
                    var total = FindField(type, "TotalHealth");
                    var index = FindField(type, "stateIndex");
                    var stateArray = FindField(type, "states");
                    if (current == null || total == null ||
                        index == null || stateArray == null)
                        return null;
                    return new BossAccessor(
                        current, total, index, stateArray);
                }

                internal bool TryRead(
                    object instance, out BossProgressSnapshot snapshot)
                {
                    snapshot = null;
                    var current = Convert.ToSingle(
                        currentHealth.GetValue(instance, null),
                        CultureInfo.InvariantCulture);
                    var total = Convert.ToSingle(
                        totalHealth.GetValue(instance),
                        CultureInfo.InvariantCulture);
                    var currentState = Convert.ToInt32(
                        stateIndex.GetValue(instance),
                        CultureInfo.InvariantCulture);
                    var stateValues = states.GetValue(instance) as Array;
                    if (total <= 0f || stateValues == null ||
                        stateValues.Length == 0)
                        return false;

                    currentState = Math.Max(0,
                        Math.Min(stateValues.Length - 1, currentState));
                    snapshot = BuildSnapshot(
                        current, total, currentState, stateValues);
                    return true;
                }

                private static BossProgressSnapshot BuildSnapshot(
                    float current,
                    float total,
                    int currentState,
                    Array stateValues)
                {
                    var count = stateValues.Length;
                    var names = new string[count];
                    var triggers = new float[count];
                    for (var i = 0; i < count; i++)
                    {
                        var state = stateValues.GetValue(i);
                        names[i] = ReadStateName(state);
                        triggers[i] = Mathf.Clamp01(
                            ReadHealthTrigger(state));
                    }
                    NormalizeGenericNames(names);

                    var groupStarts = new List<int>();
                    var groupNames = new List<string>();
                    for (var i = 0; i < count; i++)
                    {
                        var name = names[i];
                        if (groupNames.Count > 0 &&
                            groupNames[groupNames.Count - 1] == name)
                            continue;
                        groupStarts.Add(i);
                        groupNames.Add(name);
                    }
                    if (groupStarts.Count == 0)
                    {
                        groupStarts.Add(0);
                        groupNames.Add("Phase");
                    }

                    var groupIndex = 0;
                    for (var i = 1; i < groupStarts.Count; i++)
                    {
                        if (currentState < groupStarts[i])
                            break;
                        groupIndex = i;
                    }

                    var ratio = Mathf.Clamp01(current / total);
                    var startRatio = groupIndex == 0
                        ? 1f
                        : triggers[groupStarts[groupIndex]];
                    var endRatio = groupIndex + 1 < groupStarts.Count
                        ? triggers[groupStarts[groupIndex + 1]]
                        : 0f;
                    if (startRatio <= endRatio + 0.0001f)
                    {
                        startRatio = groupIndex == 0 ? 1f :
                            Mathf.Clamp01(1f -
                                (float)groupIndex / groupStarts.Count);
                        endRatio = Mathf.Clamp01(1f -
                            (float)(groupIndex + 1) /
                            groupStarts.Count);
                    }
                    var phaseProgress = Mathf.Clamp01(
                        (startRatio - ratio) /
                        Math.Max(0.0001f, startRatio - endRatio));
                    var overall = Mathf.Clamp01(
                        (groupIndex + phaseProgress) /
                        Math.Max(1f, groupStarts.Count));

                    var phaseItems = new PhaseProgress[groupStarts.Count];
                    for (var i = 0; i < phaseItems.Length; i++)
                    {
                        var progress = i < groupIndex
                            ? 1f
                            : i == groupIndex ? phaseProgress : 0f;
                        phaseItems[i] = new PhaseProgress(
                            i + 1,
                            i < groupIndex ? "complete" :
                                i == groupIndex ? "active" : "pending",
                            progress);
                    }

                    return new BossProgressSnapshot
                    {
                        CurrentHealth = Math.Max(0f, current),
                        TotalHealth = Math.Max(0f, total),
                        CurrentPhaseProgress = phaseProgress,
                        OverallProgress = overall,
                        PhaseIndex = groupIndex + 1,
                        PhaseCount = groupStarts.Count,
                        Phases = phaseItems
                    };
                }

                private static void NormalizeGenericNames(string[] names)
                {
                    for (var i = 0; i < names.Length; i++)
                    {
                        if (!IsGenericName(names[i]))
                            continue;
                        if (i > 0 && !IsGenericName(names[i - 1]))
                        {
                            names[i] = names[i - 1];
                            continue;
                        }
                        for (var next = i + 1;
                             next < names.Length; next++)
                        {
                            if (IsGenericName(names[next]))
                                continue;
                            names[i] = names[next];
                            break;
                        }
                        if (IsGenericName(names[i]))
                            names[i] = "Phase";
                    }
                }

                private static bool IsGenericName(string value)
                {
                    return string.IsNullOrEmpty(value) ||
                        string.Equals(value, "Generic",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "None",
                            StringComparison.OrdinalIgnoreCase);
                }

                private static string ReadStateName(object state)
                {
                    if (state == null)
                        return string.Empty;
                    var field = FindField(state.GetType(), "stateName");
                    if (field == null)
                        return string.Empty;
                    var value = field.GetValue(state);
                    return value == null ? string.Empty : value.ToString();
                }

                private static float ReadHealthTrigger(object state)
                {
                    if (state == null)
                        return 0f;
                    var field = FindField(state.GetType(), "healthTrigger");
                    if (field == null)
                        return 0f;
                    try
                    {
                        return Convert.ToSingle(field.GetValue(state),
                            CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        return 0f;
                    }
                }

                private static PropertyInfo FindProperty(
                    Type type, string name)
                {
                    while (type != null)
                    {
                        var property = type.GetProperty(name,
                            BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);
                        if (property != null)
                            return property;
                        type = type.BaseType;
                    }
                    return null;
                }

                private static FieldInfo FindField(Type type, string name)
                {
                    while (type != null)
                    {
                        var field = type.GetField(name,
                            BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);
                        if (field != null)
                            return field;
                        type = type.BaseType;
                    }
                    return null;
                }
            }
        }
    }
}
