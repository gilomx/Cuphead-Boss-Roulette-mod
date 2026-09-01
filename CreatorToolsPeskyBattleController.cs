using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal delegate bool CreatorToolsGiftResolver(
        string giftId, out CreatorToolsGiftCatalogEntry gift);

    internal sealed class CreatorToolsPeskyBattleObservation
    {
        internal readonly string Feedback;
        internal readonly bool StreamAttacksAllowed;

        internal CreatorToolsPeskyBattleObservation(
            string feedback, bool streamAttacksAllowed)
        {
            Feedback = feedback ?? string.Empty;
            StreamAttacksAllowed = streamAttacksAllowed;
        }
    }

    internal sealed class CreatorToolsPeskyBattleController
    {
        private const int SchemaVersion = 1;
        private const int Capacity = 5;
        private const int MaximumCommandsPerUpdate = 64;
        private const float MinimumIntervalSeconds = 1.25f;
        private const float MaximumIntervalSeconds = 3.25f;

        private readonly CreatorToolsPeskyBattleSettings settings;
        private readonly CreatorToolsInteractionQueue queue;
        private readonly CreatorToolsLiveEventsCoordinator liveEvents;
        private readonly CreatorToolsGiftResolver resolveGift;
        private readonly Func<string, bool> isItemAvailable;
        private readonly Action disableFreePesky;
        private readonly Action<string> logInfo;
        private readonly object stateLock = new object();
        private readonly List<Participant> participants =
            new List<Participant>(Capacity);

        private string phase = "off";
        private int sessionId;
        private int attempt;
        private int activeLevelInstanceId = -1;
        private string targetLevel = string.Empty;
        private bool dicePalaceTransition;
        private bool gameplayAvailable;
        private float nextAttackAt = -1f;
        private int revision;
        private string feedback = "ready";
        private bool error;
        private string lastState;
        private bool clearBattleEntriesPending;
        private bool disableFreePeskyPending;
        private bool releaseLiveEventPending;
        private long deferredGameplayGeneration;
        private CreatorToolsLiveEventLease liveEventLease;

        internal CreatorToolsPeskyBattleController(
            string pluginConfigPath,
            CreatorToolsInteractionQueue queue,
            CreatorToolsLiveEventsCoordinator liveEvents,
            CreatorToolsGiftResolver resolveGift,
            Func<string, bool> isItemAvailable,
            Action disableFreePesky,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            settings = CreatorToolsPeskyBattleSettings.Load(
                pluginConfigPath, logWarning);
            this.queue = queue;
            this.liveEvents = liveEvents;
            this.resolveGift = resolveGift;
            this.isItemAvailable = isItemAvailable;
            this.disableFreePesky = disableFreePesky;
            this.logInfo = logInfo;
        }

        internal bool Exclusive
        {
            get
            {
                lock (stateLock)
                    return IsExclusiveLocked();
            }
        }

        internal bool Active
        {
            get
            {
                lock (stateLock)
                    return phase == "active";
            }
        }

        internal bool StreamAttacksAllowed
        {
            get
            {
                lock (stateLock)
                    return !IsExclusiveLocked() ||
                        settings.AllowStreamAttacks;
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

        internal void ProcessCommands(CreatorToolsServer server)
        {
            if (server == null)
                ApplyDeferredMainThreadActions();
            else
                server.ApplyPeskyBattleMainThreadActions(
                    ApplyDeferredMainThreadActions);
            if (server == null || !server.IsRunning)
                return;
            var processed = 0;
            string query;
            while (processed < MaximumCommandsPerUpdate &&
                   server.TryTakePeskyBattleCommand(out query))
            {
                ProcessCommand(ParseQuery(query), false);
                processed++;
            }
        }

        /// <summary>
        /// Applies the pure-data portion of a panel command immediately. Any
        /// queue or gameplay callback is recorded for the next Unity frame.
        /// </summary>
        internal bool ProcessBackgroundCommand(string query)
        {
            ProcessCommand(ParseQuery(query), true);
            return true;
        }

        internal void Update(
            CreatorToolsServer server,
            bool gameplayAvailable,
            bool gameplayDispatchAllowed)
        {
            lock (stateLock)
            {
                if (this.gameplayAvailable != gameplayAvailable)
                {
                    this.gameplayAvailable = gameplayAvailable;
                    TouchLocked();
                }
            }
            UpdateAttackScheduler(gameplayDispatchAllowed);
            PublishState(server);
        }

        /// <summary>
        /// Publishes the pure-data battle projection. Safe for the stream
        /// worker: gift resolution happens outside the battle lock and no
        /// Unity object, queue or executor is inspected here.
        /// </summary>
        internal void PublishState(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;

            // The catalog resolver owns the stream-rule lock. Resolve before
            // taking stateLock so rule evaluation and main-thread dispatch
            // cannot form an inverted lock order with battle publication.
            while (true)
            {
                string giftId;
                lock (stateLock)
                    giftId = settings.GiftId;

                CreatorToolsGiftCatalogEntry gift;
                if (resolveGift == null ||
                    !resolveGift(giftId, out gift))
                    gift = new CreatorToolsGiftCatalogEntry(
                        giftId, string.Empty, string.Empty, 0);

                lock (stateLock)
                {
                    // A main-thread settings command may have changed the
                    // trigger while it was being resolved. Retry rather than
                    // publishing a mixed snapshot.
                    if (settings.GiftId != giftId)
                        continue;
                    var state = BuildStateLocked(gift);
                    if (state == lastState)
                        return;
                    lastState = state;
                    // CreatorToolsServer serializes this setter internally
                    // and does not call back into battle or rule state.
                    server.SetPeskyBattleState(state);
                    return;
                }
            }
        }

        internal CreatorToolsPeskyBattleObservation ObserveStreamEvent(
            CreatorToolsStreamEvent entry)
        {
            lock (stateLock)
            {
                var feedbackCode = ObserveStreamEventLocked(entry);
                return new CreatorToolsPeskyBattleObservation(
                    feedbackCode,
                    !IsExclusiveLocked() || settings.AllowStreamAttacks);
            }
        }

        private string ObserveStreamEventLocked(
            CreatorToolsStreamEvent entry)
        {
            if (entry == null || !IsExclusiveLocked() ||
                entry.Platform != "tiktok" || entry.Type != "gift" ||
                entry.StreakState == "progress" ||
                entry.ItemId != settings.GiftId)
                return string.Empty;

            var identity = ParticipantIdentity(entry);
            if (identity.Length == 0)
            {
                SetFeedbackLocked(
                    "participant_identity_missing", true);
                return "participant_identity_missing";
            }

            var existing = FindParticipant(identity, entry);
            if (existing != null)
            {
                // Once the fight has been requested, the five visible
                // opponents are immutable across retries. A late duplicate
                // gift must not replace a portrait/name during combat.
                if (phase == "recruiting" || phase == "ready")
                {
                    if (identity.IndexOf(":id:",
                            StringComparison.Ordinal) >= 0)
                        existing.Identity = identity;
                    if (existing.Enrich(entry))
                        TouchLocked();
                }
                SetFeedbackLocked(
                    "participant_already_joined", false);
                return "participant_already_joined";
            }

            if (phase != "recruiting" ||
                participants.Count >= Capacity)
                return string.Empty;
            participants.Add(Participant.From(
                participants.Count + 1, identity, entry));
            SetFeedbackLocked("participant_joined", false);
            if (participants.Count == Capacity)
            {
                phase = "ready";
                SetFeedbackLocked("lobby_ready", false);
                return "lobby_ready";
            }
            return "participant_joined";
        }

        internal void OnLevelStarted(Level level)
        {
            if (level == null)
                return;
            var logicalLevel = LogicalLevel(level.CurrentLevel);
            var levelInstanceId = level.GetInstanceID();
            lock (stateLock)
            {
                if (phase != "waiting_level" && phase != "active")
                    return;

                // Cuphead can replace the Level instance before the old
                // instance receives its teardown hook (notably between King
                // Dice boards and after some retries). Rebind the battle here
                // so it cannot remain attached to an object that is no longer
                // current.
                if (phase == "active")
                {
                    if (levelInstanceId == activeLevelInstanceId)
                        return;

                    ClearBattleEntries();
                    activeLevelInstanceId = -1;
                    ResetAttackScheduleLocked();
                    if (targetLevel != logicalLevel)
                    {
                        phase = "waiting_level";
                        dicePalaceTransition = false;
                        SetFeedbackLocked(
                            "waiting_target_level", false);
                        return;
                    }

                    activeLevelInstanceId = levelInstanceId;
                    if (!dicePalaceTransition)
                        attempt++;
                    dicePalaceTransition = false;
                    SetFeedbackLocked("battle_started", false);
                    return;
                }

                if (targetLevel.Length == 0)
                    targetLevel = logicalLevel;
                if (targetLevel != logicalLevel)
                {
                    SetFeedbackLocked("waiting_target_level", false);
                    return;
                }
                phase = "active";
                activeLevelInstanceId = levelInstanceId;
                if (!dicePalaceTransition)
                    attempt++;
                dicePalaceTransition = false;
                ResetAttackScheduleLocked();
                SetFeedbackLocked("battle_started", false);
            }
        }

        internal void OnLevelDefeated(Level level)
        {
            var levelInstanceId = level == null ? -1 : level.GetInstanceID();
            lock (stateLock)
            {
                if (!MatchesActiveLevelLocked(levelInstanceId) ||
                    phase != "active")
                    return;
                phase = "waiting_level";
                activeLevelInstanceId = -1;
                dicePalaceTransition = false;
                ClearBattleEntries();
                ResetAttackScheduleLocked();
                SetFeedbackLocked("battle_lost_retrying", false);
            }
        }

        internal void OnLevelPreWin(Level level)
        {
            if (level == null)
                return;
            var levelInstanceId = level.GetInstanceID();
            var currentLevel = level.CurrentLevel.ToString();
            lock (stateLock)
            {
                if (!MatchesActiveLevelLocked(levelInstanceId) ||
                    phase != "active")
                    return;
                if (targetLevel == "DicePalace" &&
                    currentLevel != "DicePalaceMain")
                {
                    dicePalaceTransition = true;
                    ResetAttackScheduleLocked();
                    return;
                }
                phase = "won";
                activeLevelInstanceId = -1;
                dicePalaceTransition = false;
                ClearBattleEntries();
                ResetAttackScheduleLocked();
                SetFeedbackLocked("battle_won", false);
            }
        }

        internal void OnLevelEnded(Level level)
        {
            var levelInstanceId = level == null ? -1 : level.GetInstanceID();
            lock (stateLock)
            {
                if (!MatchesActiveLevelLocked(levelInstanceId) ||
                    phase != "active")
                    return;
                activeLevelInstanceId = -1;
                ClearBattleEntries();
                ResetAttackScheduleLocked();
                if (dicePalaceTransition)
                {
                    phase = "waiting_level";
                    SetFeedbackLocked(
                        "dice_palace_transition", false);
                    return;
                }
                phase = "waiting_level";
                SetFeedbackLocked("waiting_for_retry", false);
            }
        }

        internal void OnPhaseTransition()
        {
            lock (stateLock)
            {
                if (phase != "active")
                    return;
                queue.ClearPending(
                    CreatorToolsInteractionSource.PeskyBattle);
                ResetAttackScheduleLocked();
                TouchLocked();
            }
        }

        internal void InvalidateState()
        {
            lock (stateLock)
                lastState = null;
        }

        internal void ReportDispatchFeedback(string value, bool isError)
        {
            lock (stateLock)
                SetFeedbackLocked(value, isError);
        }

        internal void Dispose()
        {
            ResetSession("battle_cancelled", false);
            lock (stateLock)
            {
                participants.Clear();
                phase = "off";
                ResetAttackScheduleLocked();
                clearBattleEntriesPending = false;
                disableFreePeskyPending = false;
                releaseLiveEventPending = false;
                liveEventLease = null;
                deferredGameplayGeneration = 0L;
                lastState = null;
            }
        }

        private void ProcessCommand(
            Dictionary<string, string> values,
            bool deferGameplaySideEffects)
        {
            string action;
            if (values.TryGetValue("action", out action))
            {
                action = (action ?? string.Empty).Trim().ToLowerInvariant();
                if (action == "arm")
                {
                    TryArm(values, deferGameplaySideEffects);
                    return;
                }
                ProcessAction(action, deferGameplaySideEffects);
                return;
            }

            string value;
            if (values.TryGetValue("enabled", out value))
            {
                bool enabled;
                if (!TryParseSwitch(value, out enabled))
                {
                    SetFeedback("invalid_setting", true);
                    return;
                }
                if (enabled)
                {
                    TryArm(values, deferGameplaySideEffects);
                    return;
                }
                ProcessAction("off", deferGameplaySideEffects);
                return;
            }
            if (values.TryGetValue("giftId", out value))
            {
                SetGift(value);
                return;
            }
            if (values.TryGetValue("allowStreamAttacks", out value))
            {
                bool enabled;
                if (!TryParseSwitch(value, out enabled))
                {
                    SetFeedback("invalid_setting", true);
                    return;
                }
                lock (stateLock)
                {
                    settings.AllowStreamAttacks = enabled;
                    SetFeedbackLocked(enabled
                        ? "stream_attacks_allowed"
                        : "stream_attacks_blocked", false);
                }
                settings.Save();
                return;
            }
            if (values.ContainsKey("item"))
            {
                SetItem(values);
                return;
            }
            SetFeedback("invalid_setting", true);
        }

        private void TryArm(
            Dictionary<string, string> values,
            bool deferGameplaySideEffects)
        {
            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked("invalid_action", true);
                    return;
                }
            }

            CreatorToolsLiveEventLease lease;
            string blockingEvent;
            if (liveEvents == null || !liveEvents.TryAcquire(
                    CreatorToolsLiveEventIds.PeskyBattle,
                    out lease,
                    out blockingEvent))
            {
                SetFeedback("blocked_by_live_event", true);
                return;
            }

            var committed = false;
            try
            {
                if (!TryApplyArmSettings(values))
                    return;
                committed = ArmSession(
                    deferGameplaySideEffects, lease);
            }
            finally
            {
                // Validation and controller state are intentionally outside
                // the coordinator lock. If either rejects the arm request,
                // return the short-lived reservation with the same epoch.
                if (!committed)
                    liveEvents.CompleteRelease(lease);
            }
        }

        private bool ArmSession(
            bool deferGameplaySideEffects,
            CreatorToolsLiveEventLease lease)
        {
            string giftId;
            var hasItems = false;
            lock (stateLock)
            {
                giftId = settings.GiftId;
                hasItems = settings.EnabledItemCount > 0;
            }
            CreatorToolsGiftCatalogEntry gift;
            if (giftId.Length == 0 || resolveGift == null ||
                !resolveGift(giftId, out gift))
            {
                SetFeedback("gift_required", true);
                return false;
            }
            if (!hasItems)
            {
                SetFeedback("items_required", true);
                return false;
            }
            if (!deferGameplaySideEffects)
                ClearBattleEntries();
            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked("invalid_action", true);
                    return false;
                }
                if (deferGameplaySideEffects)
                {
                    AdvanceDeferredGameplayGenerationLocked();
                    clearBattleEntriesPending = true;
                    disableFreePeskyPending = true;
                }
                releaseLiveEventPending = false;
                liveEventLease = lease;
                participants.Clear();
                targetLevel = string.Empty;
                activeLevelInstanceId = -1;
                attempt = 0;
                sessionId++;
                if (sessionId <= 0)
                    sessionId = 1;
                phase = "recruiting";
                dicePalaceTransition = false;
                ResetAttackScheduleLocked();
                SetFeedbackLocked("battle_armed", false);
            }
            if (!deferGameplaySideEffects && disableFreePesky != null)
                disableFreePesky();
            return true;
        }

        private bool TryApplyArmSettings(
            Dictionary<string, string> values)
        {
            string giftValue;
            string allowValue;
            var hasGift = values.TryGetValue("giftId", out giftValue);
            var hasAllow = values.TryGetValue(
                "allowStreamAttacks", out allowValue);
            string giftId;
            bool allowStreamAttacks;
            lock (stateLock)
            {
                giftId = settings.GiftId;
                allowStreamAttacks = settings.AllowStreamAttacks;
            }

            if (hasGift)
            {
                giftId = CreatorToolsPeskyBattleSettings.NormalizeGiftId(
                    giftValue);
                CreatorToolsGiftCatalogEntry gift;
                if (giftId.Length == 0 || resolveGift == null ||
                    !resolveGift(giftId, out gift))
                {
                    SetFeedback("unknown_gift", true);
                    return false;
                }
                giftId = gift.Id;
            }
            if (hasAllow && !TryParseSwitch(
                    allowValue, out allowStreamAttacks))
            {
                SetFeedback("invalid_setting", true);
                return false;
            }

            CreatorToolsGiftCatalogEntry finalGift;
            if (giftId.Length == 0 || resolveGift == null ||
                !resolveGift(giftId, out finalGift))
            {
                SetFeedback("gift_required", true);
                return false;
            }
            lock (stateLock)
            {
                if (settings.EnabledItemCount == 0)
                {
                    SetFeedbackLocked("items_required", true);
                    return false;
                }
                if (!hasGift && !hasAllow)
                    return true;

                settings.GiftId = finalGift.Id;
                settings.AllowStreamAttacks = allowStreamAttacks;
            }
            settings.Save();
            return true;
        }

        private void ProcessAction(
            string action, bool deferGameplaySideEffects)
        {
            if (action == "cancel" || action == "reset" ||
                action == "off" || action == "disable")
            {
                ResetSession(
                    "battle_cancelled", deferGameplaySideEffects);
                return;
            }
            if (action == "start")
            {
                lock (stateLock)
                {
                    if (phase != "ready")
                    {
                        SetFeedbackLocked("lobby_not_ready", true);
                        return;
                    }
                    phase = "waiting_level";
                    ResetAttackScheduleLocked();
                    SetFeedbackLocked("waiting_for_level", false);
                    return;
                }
            }
            SetFeedback("invalid_action", true);
        }

        private void SetGift(string value)
        {
            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked(
                        "battle_active_setting_locked", true);
                    return;
                }
            }
            var giftId = CreatorToolsPeskyBattleSettings.NormalizeGiftId(value);
            CreatorToolsGiftCatalogEntry gift;
            if (giftId.Length == 0 || resolveGift == null ||
                !resolveGift(giftId, out gift))
            {
                SetFeedback("unknown_gift", true);
                return;
            }
            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked(
                        "battle_active_setting_locked", true);
                    return;
                }
                settings.GiftId = gift.Id;
                SetFeedbackLocked("gift_saved", false);
            }
            settings.Save();
        }

        private void SetItem(Dictionary<string, string> values)
        {
            string item;
            string value;
            bool enabled;
            if (!values.TryGetValue("item", out item) ||
                !IsKnownItem(item) ||
                !values.TryGetValue("itemEnabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetFeedback("invalid_setting", true);
                return;
            }
            lock (stateLock)
            {
                if (phase != "off")
                {
                    SetFeedbackLocked(
                        "battle_active_setting_locked", true);
                    return;
                }
                if (enabled)
                    settings.DisabledItems.Remove(item);
                else if (settings.EnabledItemCount <= 1 &&
                         settings.IsItemEnabled(item))
                {
                    SetFeedbackLocked("items_required", true);
                    return;
                }
                else
                    settings.DisabledItems.Add(item);
                SetFeedbackLocked("items_saved", false);
            }
            settings.Save();
        }

        private void UpdateAttackScheduler(bool gameplayDispatchAllowed)
        {
            lock (stateLock)
            {
                if (!gameplayDispatchAllowed || phase != "active" ||
                    dicePalaceTransition ||
                    !gameplayAvailable)
                {
                    nextAttackAt = -1f;
                    return;
                }
                var now = Time.realtimeSinceStartup;
                if (nextAttackAt < 0f)
                {
                    ScheduleNextAttackLocked(now);
                    return;
                }
                if (now < nextAttackAt)
                    return;
                ScheduleNextAttackLocked(now);

                if (queue == null || participants.Count != Capacity ||
                    queue.PendingCountFor(
                        CreatorToolsInteractionSource.PeskyBattle) > 0)
                    return;
                var availableItems = new List<string>();
                for (var i = 0;
                     i < CreatorToolsInteractionIds.All.Length;
                     i++)
                {
                    var item = CreatorToolsInteractionIds.All[i];
                    if (settings.IsItemEnabled(item) &&
                        (isItemAvailable == null || isItemAvailable(item)))
                        availableItems.Add(item);
                }
                if (availableItems.Count == 0)
                    return;
                var participant = participants[UnityEngine.Random.Range(
                    0, participants.Count)];
                var itemId = availableItems[UnityEngine.Random.Range(
                    0, availableItems.Count)];
                if (queue.Enqueue(
                        itemId, participant.Label, string.Empty, 1, 0f,
                        CreatorToolsInteractionSource.PeskyBattle) <= 0)
                    return;
                if (logInfo != null)
                    logInfo("Batalla Molestosa agrego " + itemId +
                        " para " + participant.Label + ".");
                TouchLocked();
            }
        }

        private void ResetSession(
            string feedbackCode, bool deferGameplaySideEffects)
        {
            CreatorToolsLiveEventLease releasedLease;
            lock (stateLock)
            {
                releasedLease = liveEventLease;
                AdvanceDeferredGameplayGenerationLocked();
                if (deferGameplaySideEffects)
                {
                    clearBattleEntriesPending = true;
                    // If arm and cancel both arrive before Unity resumes,
                    // the final off state must not disable free Pesky mode.
                    disableFreePeskyPending = false;
                    releaseLiveEventPending = releasedLease != null;
                }
                else
                {
                    clearBattleEntriesPending = false;
                    disableFreePeskyPending = false;
                    releaseLiveEventPending = false;
                }
                participants.Clear();
                phase = releasedLease == null ? "off" : "stopping";
                attempt = 0;
                activeLevelInstanceId = -1;
                targetLevel = string.Empty;
                dicePalaceTransition = false;
                ResetAttackScheduleLocked();
                SetFeedbackLocked(feedbackCode, false);
            }
            if (releasedLease != null && liveEvents != null)
                liveEvents.BeginStopping(releasedLease);
            if (deferGameplaySideEffects)
                return;

            ClearBattleEntries();
            lock (stateLock)
            {
                if (ReferenceEquals(liveEventLease, releasedLease))
                {
                    liveEventLease = null;
                    phase = "off";
                    TouchLocked();
                }
            }
            if (releasedLease != null && liveEvents != null)
                liveEvents.CompleteRelease(releasedLease);
        }

        private void ApplyDeferredMainThreadActions()
        {
            bool clearEntries;
            bool disablePesky;
            bool releaseLiveEvent;
            CreatorToolsLiveEventLease releasedLease;
            long generation;
            lock (stateLock)
            {
                clearEntries = clearBattleEntriesPending;
                disablePesky = disableFreePeskyPending;
                releaseLiveEvent = releaseLiveEventPending;
                releasedLease = releaseLiveEvent
                    ? liveEventLease
                    : null;
                generation = deferredGameplayGeneration;
                clearBattleEntriesPending = false;
                disableFreePeskyPending = false;
            }
            if (clearEntries)
                ClearBattleEntries();
            if (disablePesky && disableFreePesky != null)
            {
                lock (stateLock)
                    disablePesky = generation ==
                        deferredGameplayGeneration && IsExclusiveLocked();
                if (disablePesky)
                    disableFreePesky();
            }
            if (!releaseLiveEvent || releasedLease == null)
                return;

            lock (stateLock)
            {
                releaseLiveEvent = releaseLiveEventPending &&
                    generation == deferredGameplayGeneration &&
                    ReferenceEquals(liveEventLease, releasedLease);
                if (releaseLiveEvent)
                {
                    releaseLiveEventPending = false;
                    liveEventLease = null;
                    phase = "off";
                    TouchLocked();
                }
            }
            if (releaseLiveEvent && liveEvents != null)
                liveEvents.CompleteRelease(releasedLease);
        }

        private void AdvanceDeferredGameplayGenerationLocked()
        {
            deferredGameplayGeneration++;
            if (deferredGameplayGeneration <= 0L)
                deferredGameplayGeneration = 1L;
        }

        private void ClearBattleEntries()
        {
            if (queue != null)
                queue.ClearSource(
                    CreatorToolsInteractionSource.PeskyBattle);
        }

        private void ScheduleNextAttackLocked(float now)
        {
            nextAttackAt = now + UnityEngine.Random.Range(
                MinimumIntervalSeconds, MaximumIntervalSeconds);
        }

        private void ResetAttackScheduleLocked()
        {
            nextAttackAt = -1f;
            TouchLocked();
        }

        private bool MatchesActiveLevelLocked(int levelInstanceId)
        {
            return levelInstanceId >= 0 && activeLevelInstanceId >= 0 &&
                levelInstanceId == activeLevelInstanceId;
        }

        private static string LogicalLevel(Levels level)
        {
            var name = level.ToString();
            return name.StartsWith("DicePalace", StringComparison.Ordinal)
                ? "DicePalace"
                : name;
        }

        private Participant FindParticipant(
            string identity, CreatorToolsStreamEvent entry)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant.Identity == identity)
                    return participant;
                if (!string.IsNullOrEmpty(entry.UserId) &&
                    NormalizeIdentity(participant.UserId) ==
                        NormalizeIdentity(entry.UserId))
                    return participant;
                var incomingName = NormalizeIdentity(entry.UserName);
                if (incomingName.Length == 0)
                    incomingName = NormalizeIdentity(
                        entry.UserDisplayName);
                if (incomingName.Length > 0 &&
                    (string.IsNullOrEmpty(entry.UserId) ||
                     string.IsNullOrEmpty(participant.UserId)) &&
                    (NormalizeIdentity(participant.UserName) ==
                        incomingName ||
                     NormalizeIdentity(participant.DisplayName) ==
                        incomingName))
                    return participants[i];
            }
            return null;
        }

        private static string ParticipantIdentity(
            CreatorToolsStreamEvent entry)
        {
            var connection = NormalizeIdentity(entry.ConnectionId);
            var userId = NormalizeIdentity(entry.UserId);
            if (userId.Length > 0)
                return connection + ":id:" + userId;
            var name = NormalizeIdentity(entry.UserName);
            if (name.Length == 0)
                name = NormalizeIdentity(entry.UserDisplayName);
            return name.Length == 0
                ? string.Empty
                : NormalizeIdentity(entry.Platform) + ":name:" + name;
        }

        private static string NormalizeIdentity(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private string BuildStateLocked(CreatorToolsGiftCatalogEntry gift)
        {
            var builder = new StringBuilder(4096);
            builder.Append("{\"ready\":true,\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"revision\":").Append(revision)
                .Append(",\"phase\":\"");
            CreatorToolsJson.AppendEscaped(builder, phase);
            builder.Append("\",\"sessionId\":").Append(sessionId)
                .Append(",\"attempt\":").Append(attempt)
                .Append(",\"capacity\":").Append(Capacity)
                .Append(",\"exclusive\":")
                .Append(IsExclusiveLocked() ? "true" : "false")
                .Append(",\"gameplayAvailable\":")
                .Append(gameplayAvailable ? "true" : "false")
                .Append(",\"targetLevel\":\"");
            CreatorToolsJson.AppendEscaped(builder, targetLevel);
            builder.Append("\",\"trigger\":{\"giftId\":\"");
            CreatorToolsJson.AppendEscaped(builder, gift.Id);
            builder.Append("\",\"giftName\":\"");
            CreatorToolsJson.AppendEscaped(builder, gift.Name);
            builder.Append("\",\"giftImagePath\":\"");
            CreatorToolsJson.AppendEscaped(builder, gift.ImagePath);
            builder.Append("\",\"coinsPerUnit\":")
                .Append(gift.CoinsPerUnit)
                .Append("},\"allowStreamAttacks\":")
                .Append(settings.AllowStreamAttacks ? "true" : "false")
                .Append(",\"participants\":[");
            for (var i = 0; i < participants.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                participants[i].AppendJson(builder);
            }
            builder.Append("],\"items\":[");
            AppendItemsLocked(builder, false);
            builder.Append("],\"disabledItems\":[");
            AppendItemsLocked(builder, true);
            builder.Append("],\"feedback\":\"");
            CreatorToolsJson.AppendEscaped(builder, feedback);
            builder.Append("\",\"error\":")
                .Append(error ? "true" : "false")
                .Append('}');
            return builder.ToString();
        }

        private void AppendItemsLocked(
            StringBuilder builder, bool disabledOnly)
        {
            var first = true;
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
            {
                var item = CreatorToolsInteractionIds.All[i];
                if (disabledOnly && !settings.DisabledItems.Contains(item))
                    continue;
                if (!first)
                    builder.Append(',');
                builder.Append('"');
                CreatorToolsJson.AppendEscaped(builder, item);
                builder.Append('"');
                first = false;
            }
        }

        private void SetFeedback(string value, bool isError)
        {
            lock (stateLock)
                SetFeedbackLocked(value, isError);
        }

        private void SetFeedbackLocked(string value, bool isError)
        {
            feedback = value;
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

        private bool IsExclusiveLocked()
        {
            return phase == "recruiting" || phase == "ready" ||
                phase == "waiting_level" || phase == "active" ||
                phase == "stopping";
        }

        private static bool IsKnownItem(string item)
        {
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
                if (string.Equals(CreatorToolsInteractionIds.All[i], item,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool TryParseSwitch(string value, out bool enabled)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "1" || value == "true" || value == "on")
            {
                enabled = true;
                return true;
            }
            if (value == "0" || value == "false" || value == "off")
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
                    key = Uri.UnescapeDataString(key.Replace('+', ' '));
                    value = Uri.UnescapeDataString(value.Replace('+', ' '));
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

        private sealed class Participant
        {
            internal int Slot;
            internal string Identity;
            internal string UserId;
            internal string UserName;
            internal string DisplayName;
            internal string AvatarUrl;
            internal string JoinedAt;

            internal string Label
            {
                get
                {
                    if (!string.IsNullOrEmpty(DisplayName))
                        return DisplayName;
                    if (!string.IsNullOrEmpty(UserName))
                        return UserName;
                    return "Jugador " + Slot.ToString(
                        CultureInfo.InvariantCulture);
                }
            }

            internal static Participant From(
                int slot, string identity, CreatorToolsStreamEvent entry)
            {
                var participant = new Participant
                {
                    Slot = slot,
                    Identity = identity,
                    JoinedAt = string.IsNullOrEmpty(entry.ReceivedAt)
                        ? DateTime.UtcNow.ToString(
                            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                            CultureInfo.InvariantCulture)
                        : entry.ReceivedAt
                };
                participant.Enrich(entry);
                return participant;
            }

            internal bool Enrich(CreatorToolsStreamEvent entry)
            {
                var changed = false;
                changed |= SetIfPresent(ref UserId, entry.UserId);
                changed |= SetIfPresent(ref UserName, entry.UserName);
                changed |= SetIfPresent(
                    ref DisplayName, entry.UserDisplayName);
                changed |= SetIfPresent(ref AvatarUrl, entry.UserAvatarUrl);
                if (string.IsNullOrEmpty(DisplayName) &&
                    !string.IsNullOrEmpty(UserName))
                {
                    DisplayName = UserName;
                    changed = true;
                }
                return changed;
            }

            internal void AppendJson(StringBuilder builder)
            {
                builder.Append("{\"slot\":").Append(Slot)
                    .Append(",\"userId\":\"");
                CreatorToolsJson.AppendEscaped(builder, UserId);
                builder.Append("\",\"userName\":\"");
                CreatorToolsJson.AppendEscaped(builder, UserName);
                builder.Append("\",\"displayName\":\"");
                CreatorToolsJson.AppendEscaped(builder, DisplayName);
                builder.Append("\",\"avatarUrl\":\"");
                CreatorToolsJson.AppendEscaped(builder, AvatarUrl);
                builder.Append("\",\"joinedAt\":\"");
                CreatorToolsJson.AppendEscaped(builder, JoinedAt);
                builder.Append("\"}");
            }

            private static bool SetIfPresent(
                ref string target, string value)
            {
                value = (value ?? string.Empty).Trim();
                if (value.Length == 0 || target == value)
                    return false;
                target = value;
                return true;
            }
        }
    }
}
