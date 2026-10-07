using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsStreamRulesController
    {
        private const int SchemaVersion = 6;
        private const int MinimumSupportedSchemaVersion = 1;
        private const string GiftEventType = "gift";
        private const string LikeEventType = "like";
        private const string FollowEventType = "follow";
        private const string CommunityGiftPlaceholderId = "0";
        private const string CommunityGiftPlaceholderName = "Community Gift";
        private const string CommunityGiftPlaceholderImagePath =
            "/assets/creator-tools/gifts/images/0.webp";
        private const int MaximumRules = 100;
        private const int MaximumCommandsPerUpdate = 64;
        private const int MaximumRuleNameLength = 64;
        private const int MaximumEvery = 1000000;
        private const int MaximumQuantity = 50;
        private const int MaximumCooldownSeconds = 3600;
        private const int MaximumRuntimeViewerKeys = 100000;

        private readonly string settingsPath;
        private readonly string communityGiftPath;
        private readonly Func<bool> getInteractionsEnabled;
        private readonly Func<DateTime> utcNow;
        private readonly Action<string> logWarning;
        private readonly object ruleStateLock = new object();
        private readonly Dictionary<string, GiftEntry> gifts =
            new Dictionary<string, GiftEntry>(StringComparer.Ordinal);
        private readonly List<StreamRule> rules = new List<StreamRule>();
        private readonly Dictionary<string, long> accumulators =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> userCooldowns =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> globalCooldowns =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly HashSet<string> followedViewers =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly CreatorToolsStreamDispatchBacklog dispatchBacklog =
            new CreatorToolsStreamDispatchBacklog();

        private string catalogVersion = string.Empty;
        private bool catalogReady;
        private long nextId = 1;
        private long revision;
        private string feedback = "ready";
        private bool error;
        private string lastPublishedState;
        private bool stateDirty = true;
        private CommunityGiftState communityGift =
            CommunityGiftState.Placeholder();

        internal CreatorToolsStreamRulesController(
            string assetsDirectory,
            string pluginConfigPath,
            Func<bool> getInteractionsEnabled,
            Action<string> logWarning,
            Func<DateTime> utcNow = null)
        {
            this.getInteractionsEnabled = getInteractionsEnabled;
            this.logWarning = logWarning;
            this.utcNow = utcNow ?? delegate { return DateTime.UtcNow; };
            var configDirectory = Path.GetDirectoryName(
                string.IsNullOrEmpty(pluginConfigPath)
                    ? string.Empty
                    : Path.GetFullPath(pluginConfigPath));
            if (string.IsNullOrEmpty(configDirectory))
                configDirectory = Environment.CurrentDirectory;
            settingsPath = Path.Combine(configDirectory,
                "mx.gilomx.cuphead.bossroulette.stream-rules.json");
            communityGiftPath = Path.Combine(configDirectory,
                "mx.gilomx.cuphead.bossroulette.community-gift.json");

            var catalogPath = Path.Combine(
                Path.Combine(
                    Path.Combine(
                        Path.GetFullPath(assetsDirectory ?? string.Empty),
                        "creator-tools"),
                    "gifts"),
                "catalog.json");
            catalogReady = TryLoadCatalog(catalogPath);
            if (!catalogReady)
            {
                feedback = "catalog_unavailable";
                error = true;
            }
            LoadCommunityGift();
            LoadSettings();
            MigrateLoadedCommunityGiftRules();
        }

        internal int Update(
            CreatorToolsServer server,
            CreatorToolsInteractionController interactions,
            bool streamAttacksAllowed)
        {
            if (server != null && server.IsRunning)
            {
                var processed = 0;
                string query;
                while (processed < MaximumCommandsPerUpdate &&
                       server.TryTakeStreamRuleCommand(out query))
                {
                    lock (ruleStateLock)
                        ProcessCommand(ParseQuery(query));
                    processed++;
                }

                PublishState(server);
            }
            // HTTP CRUD can purge runtime state while Unity remains
            // unfocused. Serialize backlog mutation with evaluation, but
            // keep all gameplay queue access on this main-thread call.
            lock (ruleStateLock)
                return InteractionsEnabled && streamAttacksAllowed
                    ? dispatchBacklog.Drain(interactions, UtcNow())
                    : 0;
        }

        internal void PublishState(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;
            lock (ruleStateLock)
            {
                if (!stateDirty)
                    return;
                var state = BuildState();
                if (state != lastPublishedState)
                {
                    lastPublishedState = state;
                    server.SetStreamRulesState(state);
                }
                stateDirty = false;
            }
        }

        internal long BacklogCount
        {
            get
            {
                lock (ruleStateLock)
                    return dispatchBacklog.PendingCount;
            }
        }

        internal long ClearBacklog()
        {
            lock (ruleStateLock)
                return dispatchBacklog.Clear();
        }

        internal void ResetRuntimeState()
        {
            lock (ruleStateLock)
            {
                dispatchBacklog.Clear();
                accumulators.Clear();
                userCooldowns.Clear();
                globalCooldowns.Clear();
                followedViewers.Clear();
                lastPublishedState = null;
                stateDirty = true;
            }
        }

        internal void InvalidateState()
        {
            lock (ruleStateLock)
            {
                lastPublishedState = null;
                stateDirty = true;
            }
        }

        internal string ProcessServerCommand(string query)
        {
            lock (ruleStateLock)
            {
                ProcessCommand(ParseQuery(query));
                var state = BuildState();
                lastPublishedState = state;
                stateDirty = false;
                return state;
            }
        }

        /// <summary>
        /// Evaluates every compatible rule independently. Gift accumulators
        /// are scoped by rule plus connection. Like accumulators also include
        /// the viewer, so every user keeps a separate session-only remainder.
        /// Follow identities are remembered for the whole session so a later
        /// unfollow/follow cycle cannot dispatch twice.
        /// </summary>
        internal CreatorToolsStreamEvaluation Evaluate(
            CreatorToolsStreamEvent streamEvent,
            bool streamAttacksAllowed)
        {
            lock (ruleStateLock)
            {
                ObserveCommunityGift(streamEvent);
                // Read the volatile master mirror while holding the same lock
                // used by ResetRuntimeState. A stale snapshot taken before
                // this lock could otherwise recreate backlog just after the
                // user disabled and cleared interactions.
                if (!InteractionsEnabled)
                    return new CreatorToolsStreamEvaluation
                    {
                        MessageCode = "interactions_disabled"
                    };
                if (!streamAttacksAllowed)
                    return new CreatorToolsStreamEvaluation
                    {
                        MessageCode =
                            "stream_attacks_blocked_by_pesky_battle"
                    };
                return EvaluateLocked(streamEvent);
            }
        }

        /// <summary>
        /// Resolves simulator input against the installed catalog so the
        /// browser cannot provide a forged gift name, image or coin value.
        /// </summary>
        internal bool TryResolveSimulationGift(
            CreatorToolsStreamEvent streamEvent)
        {
            if (streamEvent == null)
                return false;
            lock (ruleStateLock)
            {
                GiftEntry gift;
                if (!catalogReady || streamEvent.Platform != "tiktok" ||
                    streamEvent.Type != "gift" ||
                    !gifts.TryGetValue(
                        streamEvent.ItemId ?? string.Empty, out gift))
                    return false;

                streamEvent.ItemId = gift.Id;
                streamEvent.ItemName = gift.Name;
                streamEvent.ItemImageUrl = communityGift.Learned &&
                    gift.Id == communityGift.GiftId
                    ? communityGift.ImagePath
                    : "/assets/creator-tools/gifts/images/" +
                      gift.Id + ".webp";
                streamEvent.Count = Math.Max(
                    1, Math.Min(1000000, streamEvent.Count));
                streamEvent.UnitValue = gift.CoinsPerUnit;
                streamEvent.TotalValue = Math.Min(
                    1000000000m,
                    (decimal)gift.CoinsPerUnit * streamEvent.Count);
                streamEvent.Unit = "coin";
                streamEvent.Currency = string.Empty;
                return true;
            }
        }

        internal bool TryResolveGift(
            string giftId, out CreatorToolsGiftCatalogEntry resolved)
        {
            resolved = null;
            lock (ruleStateLock)
            {
                GiftEntry gift;
                if (!catalogReady || !gifts.TryGetValue(
                    giftId ?? string.Empty, out gift))
                    return false;
                resolved = new CreatorToolsGiftCatalogEntry(
                    gift.Id, gift.Name, gift.ImagePath,
                    gift.CoinsPerUnit,
                    communityGift.Learned &&
                        (gift.Id == communityGift.GiftId ||
                         gift.Id == communityGift.PreviousGiftId)
                        ? communityGift.ImagePath
                        : "/assets/creator-tools/gifts/images/" +
                          gift.Id + ".webp");
                return true;
            }
        }

        private CreatorToolsStreamEvaluation EvaluateLocked(
            CreatorToolsStreamEvent streamEvent)
        {
            var result = new CreatorToolsStreamEvaluation();
            var eventType = RuleEventType(streamEvent);
            if (!catalogReady || streamEvent == null ||
                !IsSupportedEventType(streamEvent.Platform, eventType))
                return result;
            if (streamEvent.Type == GiftEventType &&
                string.IsNullOrEmpty(streamEvent.ItemId))
            {
                result.MessageCode = "gift_id_missing";
                return result;
            }

            var viewerKey = BuildViewerKey(streamEvent);
            if ((streamEvent.Type == LikeEventType ||
                 streamEvent.Type == FollowEventType) &&
                viewerKey.Length == 0)
            {
                result.MessageCode = "user_identity_missing";
                return result;
            }

            if (streamEvent.Type == FollowEventType)
            {
                var hasEnabledFollowRule = false;
                for (var i = 0; i < rules.Count; i++)
                {
                    if (rules[i].Enabled &&
                        rules[i].Platform == streamEvent.Platform &&
                        rules[i].EventType == FollowEventType)
                    {
                        hasEnabledFollowRule = true;
                        break;
                    }
                }
                if (!hasEnabledFollowRule)
                    return result;
                var followKey = BuildConnectionViewerKey(
                    streamEvent.Platform + ":" + streamEvent.ConnectionId, viewerKey);
                if (!TryRememberFollowViewer(followKey))
                {
                    result.MessageCode = "follow_already_seen";
                    return result;
                }
            }

            var matchedNames = new List<string>();
            var interactionIds = new List<string>();
            var pending = new List<PendingRuleDispatch>();
            var thresholdObserved = false;
            // Bits are measured by value, not by the number of Cheer messages.
            var amount = eventType == "currency"
                ? (long)Math.Min(1000000000m, Math.Max(0m, decimal.Floor(streamEvent.TotalValue)))
                : Math.Max(0L, streamEvent.Count);
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!rule.Enabled || rule.Platform != streamEvent.Platform || rule.EventType != eventType ||
                    (rule.EventType == GiftEventType &&
                     rule.GiftId != streamEvent.ItemId) ||
                    (rule.EventType == "redemption" && !string.Equals(
                        rule.RewardName, (streamEvent.ItemName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)))
                    continue;
                thresholdObserved = true;
                long triggers;
                if (rule.EventType == FollowEventType)
                    triggers = 1;
                else
                {
                    var accumulatorKey = BuildAccumulatorKey(
                        rule.Id,
                        streamEvent.ConnectionId,
                        rule.EventType == LikeEventType
                            ? viewerKey
                            : string.Empty);
                    long remainder;
                    accumulators.TryGetValue(accumulatorKey, out remainder);
                    var total = Math.Min(long.MaxValue - amount,
                        Math.Max(0L, remainder)) + amount;
                    triggers = total / rule.Every;
                    SetAccumulatorRemainder(
                        accumulatorKey, total % rule.Every);
                }
                if (triggers <= 0)
                    continue;

                result.MatchedRules++;
                matchedNames.Add(rule.Name);
                interactionIds.Add(rule.Interaction);
                var requestedLong = triggers > long.MaxValue / rule.Quantity
                    ? long.MaxValue
                    : triggers * (long)rule.Quantity;
                var giftImagePath = string.Empty;
                GiftEntry gift;
                if (rule.EventType == GiftEventType &&
                    gifts.TryGetValue(rule.GiftId, out gift))
                    giftImagePath = gift.ImagePath;
                pending.Add(new PendingRuleDispatch(
                    rule, triggers, rule.Quantity, giftImagePath));
                result.DeferredInteractions = SaturatingAdd(
                    result.DeferredInteractions,
                    Math.Max(0L, requestedLong));
            }

            ScheduleDispatches(
                pending, streamEvent, viewerKey, UtcNow());

            result.RuleNames = string.Join(", ", matchedNames.ToArray());
            result.InteractionIds = string.Join(", ",
                interactionIds.ToArray());
            if (result.MatchedRules > 0 &&
                result.DeferredInteractions > 0)
                result.MessageCode = "rules_waiting_queue";
            else if (result.MatchedRules > 0 &&
                     string.IsNullOrEmpty(result.MessageCode))
                result.MessageCode = "rules_matched";
            else if (thresholdObserved)
                result.MessageCode = "threshold_pending";
            return result;
        }

        private void ScheduleDispatches(
            IList<PendingRuleDispatch> pending,
            CreatorToolsStreamEvent streamEvent,
            string viewerKey,
            DateTime now)
        {
            if (pending == null || pending.Count == 0)
                return;

            if (streamEvent.Type == GiftEventType)
            {
                // Every rule attached to one gift event remains a bundle.
                // Its shared gift cooldown is the longest configured by the
                // rules that actually crossed their threshold.
                var due = now;
                var globalKey = BuildGlobalCooldownKey(
                    pending[0].Rule, streamEvent.ConnectionId);
                due = Later(due, GetCooldown(globalCooldowns, globalKey));
                var globalSeconds = 0;
                for (var i = 0; i < pending.Count; i++)
                {
                    var rule = pending[i].Rule;
                    globalSeconds = Math.Max(
                        globalSeconds, rule.GlobalCooldownSeconds);
                    if (rule.UserCooldownSeconds <= 0 ||
                        viewerKey.Length == 0)
                        continue;
                    due = Later(due, GetCooldown(
                        userCooldowns,
                        BuildAccumulatorKey(rule.Id,
                            streamEvent.ConnectionId, viewerKey)));
                }
                var intervalSeconds = globalSeconds;
                long maximumTriggers = 0;
                for (var i = 0; i < pending.Count; i++)
                {
                    maximumTriggers = Math.Max(
                        maximumTriggers, pending[i].TriggerCount);
                    if (viewerKey.Length > 0)
                        intervalSeconds = Math.Max(intervalSeconds,
                            pending[i].Rule.UserCooldownSeconds);
                }
                var lastBundleDue = AdvanceDue(
                    due, maximumTriggers - 1, intervalSeconds);
                if (globalSeconds > 0)
                    SetCooldown(globalCooldowns, globalKey,
                        AdvanceDue(lastBundleDue, 1, globalSeconds));
                for (var i = 0; i < pending.Count; i++)
                {
                    var rule = pending[i].Rule;
                    if (rule.UserCooldownSeconds > 0 &&
                        viewerKey.Length > 0)
                    {
                        var lastRuleDue = AdvanceDue(
                            due, pending[i].TriggerCount - 1,
                            intervalSeconds);
                        SetCooldown(userCooldowns,
                            BuildAccumulatorKey(rule.Id,
                                streamEvent.ConnectionId, viewerKey),
                            AdvanceDue(lastRuleDue, 1,
                                rule.UserCooldownSeconds));
                    }
                    AddScheduledDispatchSeries(
                        pending[i], streamEvent, due, intervalSeconds);
                }
                return;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var dispatch = pending[i];
                var rule = dispatch.Rule;
                var due = now;
                var globalKey = BuildGlobalCooldownKey(
                    rule, streamEvent.ConnectionId);
                due = Later(due, GetCooldown(globalCooldowns, globalKey));
                var userKey = BuildAccumulatorKey(
                    rule.Id, streamEvent.ConnectionId, viewerKey);
                if (rule.UserCooldownSeconds > 0)
                    due = Later(due,
                        GetCooldown(userCooldowns, userKey));
                var intervalSeconds = Math.Max(
                    rule.GlobalCooldownSeconds,
                    rule.UserCooldownSeconds);
                var lastDue = AdvanceDue(
                    due, dispatch.TriggerCount - 1, intervalSeconds);
                if (rule.GlobalCooldownSeconds > 0)
                    SetCooldown(globalCooldowns, globalKey,
                        AdvanceDue(lastDue, 1,
                            rule.GlobalCooldownSeconds));
                if (rule.UserCooldownSeconds > 0)
                    SetCooldown(userCooldowns, userKey,
                        AdvanceDue(lastDue, 1,
                            rule.UserCooldownSeconds));
                AddScheduledDispatchSeries(
                    dispatch, streamEvent, due, intervalSeconds);
            }
        }

        private void AddScheduledDispatchSeries(
            PendingRuleDispatch dispatch,
            CreatorToolsStreamEvent streamEvent,
            DateTime due,
            int intervalSeconds)
        {
            dispatchBacklog.AddSeries(
                dispatch.Rule.Id,
                streamEvent.ConnectionId,
                dispatch.Rule.Interaction,
                dispatch.GiftImagePath,
                streamEvent.UserName,
                dispatch.QuantityPerTrigger,
                dispatch.TriggerCount,
                due,
                intervalSeconds,
                dispatch.Rule.DurationSeconds,
                dispatch.Rule.CountdownSeconds);
            // Evaluation can run while Unity is suspended. The worker records
            // an absolute UTC due time; Update is still the sole main-thread
            // boundary that materializes it into Unity's gameplay clock.
        }

        private static DateTime GetCooldown(
            IDictionary<string, DateTime> values, string key)
        {
            DateTime value;
            return key != null && values.TryGetValue(key, out value)
                ? value
                : DateTime.MinValue;
        }

        private static DateTime Later(DateTime left, DateTime right)
        {
            return left >= right ? left : right;
        }

        private static DateTime AdvanceDue(
            DateTime due, long intervals, int intervalSeconds)
        {
            if (intervals <= 0 || intervalSeconds <= 0)
                return due;
            var seconds = intervals * (double)intervalSeconds;
            var maximumSeconds = (DateTime.MaxValue - due).TotalSeconds;
            return seconds >= maximumSeconds
                ? DateTime.MaxValue
                : due.AddSeconds(seconds);
        }

        private static long SaturatingAdd(long left, long right)
        {
            if (right <= 0)
                return left;
            return left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

        private static void SetCooldown(
            IDictionary<string, DateTime> values,
            string key,
            DateTime value)
        {
            if (string.IsNullOrEmpty(key))
                return;
            if (!values.ContainsKey(key) &&
                values.Count >= MaximumRuntimeViewerKeys)
            {
                string oldestKey = null;
                var oldest = DateTime.MaxValue;
                foreach (var pair in values)
                {
                    if (pair.Value >= oldest)
                        continue;
                    oldest = pair.Value;
                    oldestKey = pair.Key;
                }
                if (oldestKey != null)
                    values.Remove(oldestKey);
            }
            values[key] = value;
        }

        private bool TryRememberFollowViewer(string key)
        {
            if (followedViewers.Contains(key))
                return false;
            if (followedViewers.Count >= MaximumRuntimeViewerKeys)
            {
                string expired = null;
                foreach (var candidate in followedViewers)
                {
                    expired = candidate;
                    break;
                }
                if (expired != null)
                    followedViewers.Remove(expired);
            }
            followedViewers.Add(key);
            return true;
        }

        private void SetAccumulatorRemainder(string key, long remainder)
        {
            if (remainder <= 0L)
            {
                accumulators.Remove(key);
                return;
            }
            if (!accumulators.ContainsKey(key) &&
                accumulators.Count >= MaximumRuntimeViewerKeys)
            {
                string expired = null;
                foreach (var candidate in accumulators.Keys)
                {
                    expired = candidate;
                    break;
                }
                if (expired != null)
                    accumulators.Remove(expired);
            }
            accumulators[key] = remainder;
        }

        private void ProcessCommand(Dictionary<string, string> values)
        {
            var action = Value(values, "action").Trim().ToLowerInvariant();
            if (!catalogReady)
            {
                SetFeedback("catalog_unavailable", true);
                return;
            }
            if (action == "create")
            {
                if (rules.Count >= MaximumRules)
                {
                    SetFeedback("rules_limit", true);
                    return;
                }
                StreamRule rule;
                if (!TryBuildRule(values, nextId, out rule))
                    return;
                var candidate = CloneRules();
                candidate.Add(rule);
                TryCommit(candidate, IncrementRuleId(nextId), "created");
                return;
            }

            long id;
            if (!long.TryParse(Value(values, "id"),
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out id))
            {
                SetFeedback("invalid_rule", true);
                return;
            }
            var index = FindRuleIndex(id);
            if (index < 0)
            {
                SetFeedback("rule_not_found", true);
                return;
            }

            if (action == "update")
            {
                StreamRule rule;
                if (!TryBuildRule(values, id, out rule))
                    return;
                var resetRuntimeState =
                    rules[index].Platform != rule.Platform ||
                    rules[index].RewardName != rule.RewardName ||
                    rules[index].EventType != rule.EventType ||
                    rules[index].GiftId != rule.GiftId ||
                    rules[index].Every != rule.Every ||
                    rules[index].Interaction != rule.Interaction ||
                    rules[index].Quantity != rule.Quantity ||
                    rules[index].UserCooldownSeconds !=
                        rule.UserCooldownSeconds ||
                    rules[index].GlobalCooldownSeconds !=
                        rule.GlobalCooldownSeconds ||
                    rules[index].DurationSeconds != rule.DurationSeconds ||
                    rules[index].CountdownSeconds != rule.CountdownSeconds ||
                    (rules[index].Enabled && !rule.Enabled);
                var candidate = CloneRules();
                candidate[index] = rule;
                if (TryCommit(candidate, nextId, "updated") &&
                    resetRuntimeState)
                    ResetRuleRuntimeState(id);
            }
            else if (action == "toggle")
            {
                bool enabled;
                if (!TryReadBoolean(Value(values, "enabled"), out enabled))
                {
                    SetFeedback("invalid_rule", true);
                    return;
                }
                var candidate = CloneRules();
                candidate[index].Enabled = enabled;
                if (TryCommit(candidate, nextId,
                        enabled ? "enabled" : "disabled") && !enabled)
                    ResetRuleRuntimeState(id);
            }
            else if (action == "duplicate")
            {
                if (rules.Count >= MaximumRules)
                {
                    SetFeedback("rules_limit", true);
                    return;
                }
                var copy = rules[index].Clone(nextId);
                copy.Name = NormalizeRuleName(copy.Name + " (copia)");
                var candidate = CloneRules();
                candidate.Insert(index + 1, copy);
                TryCommit(candidate, IncrementRuleId(nextId), "duplicated");
            }
            else if (action == "delete")
            {
                var candidate = CloneRules();
                candidate.RemoveAt(index);
                if (TryCommit(candidate, nextId, "deleted"))
                    ResetRuleRuntimeState(id);
            }
            else
                SetFeedback("invalid_action", true);
        }

        private bool TryBuildRule(
            Dictionary<string, string> values,
            long id,
            out StreamRule rule)
        {
            rule = null;
            var name = NormalizeRuleName(Value(values, "name"));
            var platform = Value(values, "platform").Trim().ToLowerInvariant();
            if (platform.Length == 0) platform = "tiktok";
            var rewardName = Value(values, "rewardName").Trim();
            var eventType = Value(values, "eventType").Trim()
                .ToLowerInvariant();
            if (eventType.Length == 0)
                eventType = GiftEventType;
            var giftId = Value(values, "giftId").Trim();
            var interaction = Value(values, "interaction").Trim();
            GiftEntry gift = null;
            if (name.Length == 0 || !IsSupportedEventType(platform, eventType) ||
                (eventType == "redemption" && (rewardName.Length == 0 || rewardName.Length > MaximumRuleNameLength)) ||
                (eventType == GiftEventType &&
                 !gifts.TryGetValue(giftId, out gift)) ||
                !IsKnownInteraction(interaction))
            {
                SetFeedback("invalid_rule", true);
                return false;
            }

            bool enabled;
            if (!TryReadBoolean(Value(values, "enabled"), out enabled))
                enabled = true;
            int every;
            int quantity;
            int userCooldown;
            int globalCooldown;
            if (!TryReadBoundedInt(
                    Value(values, "every"), 1, MaximumEvery, out every) ||
                !TryReadBoundedInt(Value(values, "quantity"),
                    1, MaximumQuantity, out quantity) ||
                !TryReadOptionalBoundedInt(
                    Value(values, "userCooldownSeconds"),
                    0, MaximumCooldownSeconds, out userCooldown) ||
                !TryReadOptionalBoundedInt(
                    Value(values, "globalCooldownSeconds"),
                    0, MaximumCooldownSeconds, out globalCooldown))
            {
                SetFeedback("invalid_rule", true);
                return false;
            }
            if (eventType == FollowEventType)
                every = 1;

            int duration, countdown;
            if (!CreatorToolsTimedChallenge.TryDuration(Value(values, "durationSeconds"), out duration) ||
                !CreatorToolsTimedChallenge.TryCountdown(Value(values, "countdownSeconds"), out countdown))
            {
                SetFeedback("invalid_rule", true);
                return false;
            }
            rule = new StreamRule
            {
                Id = id,
                Name = name,
                Enabled = enabled,
                Platform = platform,
                RewardName = eventType == "redemption" ? rewardName : string.Empty,
                EventType = eventType,
                GiftId = eventType == GiftEventType
                    ? gift.Id
                    : string.Empty,
                GiftName = eventType == GiftEventType
                    ? gift.Name
                    : string.Empty,
                Every = every,
                Interaction = interaction,
                Quantity = quantity,
                UserCooldownSeconds = userCooldown,
                GlobalCooldownSeconds = globalCooldown,
                DurationSeconds = duration,
                CountdownSeconds = countdown
            };
            return true;
        }

        private bool TryCommit(
            List<StreamRule> candidateRules,
            long candidateNextId,
            string successFeedback)
        {
            if (!SaveSettings(candidateRules, candidateNextId))
            {
                SetFeedback("save_failed", true);
                return false;
            }
            rules.Clear();
            rules.AddRange(candidateRules);
            nextId = candidateNextId;
            SetFeedback(successFeedback, false);
            return true;
        }

        private void SetFeedback(string value, bool isError)
        {
            feedback = value;
            error = isError;
            revision++;
            stateDirty = true;
        }

        private string BuildState()
        {
            var builder = new StringBuilder(4096);
            builder.Append("{\"ready\":")
                .Append(catalogReady ? "true" : "false")
                .Append(",\"schemaVersion\":")
                .Append(SchemaVersion)
                .Append(",\"revision\":")
                .Append(revision.ToString(CultureInfo.InvariantCulture))
                .Append(",\"engineActive\":")
                .Append(catalogReady && InteractionsEnabled
                    ? "true"
                    : "false")
                .Append(",\"catalogVersion\":\"");
            AppendJson(builder, catalogVersion);
            builder.Append("\",\"feedback\":\"");
            AppendJson(builder, feedback);
            builder.Append("\",\"error\":")
                .Append(error ? "true" : "false")
                .Append(",\"maxRules\":")
                .Append(MaximumRules)
                .Append(",\"maxEvery\":")
                .Append(MaximumEvery)
                .Append(",\"maxQuantity\":")
                .Append(MaximumQuantity)
                .Append(",\"maxCooldownSeconds\":")
                .Append(MaximumCooldownSeconds)
                .Append(",\"communityGift\":{")
                .Append("\"learned\":")
                .Append(communityGift.Learned ? "true" : "false")
                .Append(",\"giftId\":\"");
            AppendJson(builder, communityGift.GiftId);
            builder.Append("\",\"name\":\"");
            AppendJson(builder, communityGift.Name);
            builder.Append("\",\"imagePath\":\"");
            AppendJson(builder, communityGift.ImagePath);
            builder.Append("\",\"placeholderImagePath\":\"");
            AppendJson(builder, CommunityGiftPlaceholderImagePath);
            builder.Append("\",\"coinsPerUnit\":")
                .Append(communityGift.CoinsPerUnit)
                .Append('}')
                .Append(",\"rules\":[");
            for (var i = 0; i < rules.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AppendRuleJson(builder, rules[i], true);
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private bool InteractionsEnabled
        {
            get
            {
                return getInteractionsEnabled != null &&
                       getInteractionsEnabled();
            }
        }

        private void AppendRuleJson(
            StringBuilder builder, StreamRule rule, bool includeGift)
        {
            builder.Append("{\"id\":")
                .Append(rule.Id.ToString(CultureInfo.InvariantCulture))
                .Append(",\"name\":\"");
            AppendJson(builder, rule.Name);
            builder.Append("\",\"enabled\":")
                .Append(rule.Enabled ? "true" : "false")
                .Append(",\"platform\":\"");
            AppendJson(builder, rule.Platform);
            builder.Append("\"")
                .Append(",\"connectionId\":\"all\"")
                .Append(",\"eventType\":\"");
            AppendJson(builder, rule.EventType);
            builder.Append("\"")
                .Append(",\"giftId\":\"");
            AppendJson(builder, rule.GiftId);
            builder.Append("\",\"giftName\":\"");
            AppendJson(builder, rule.GiftName);
            builder.Append("\",\"rewardName\":\"");
            AppendJson(builder, rule.RewardName);
            builder.Append("\",\"every\":")
                .Append(rule.Every)
                .Append(",\"interaction\":\"");
            AppendJson(builder, rule.Interaction);
            builder.Append("\",\"quantity\":")
                .Append(rule.Quantity)
                .Append(",\"userCooldownSeconds\":")
                .Append(rule.UserCooldownSeconds)
                .Append(",\"globalCooldownSeconds\":")
                .Append(rule.GlobalCooldownSeconds)
                .Append(",\"durationSeconds\":").Append(rule.DurationSeconds)
                .Append(",\"countdownSeconds\":").Append(rule.CountdownSeconds);
            if (includeGift)
            {
                GiftEntry gift;
                if (rule.EventType == GiftEventType &&
                    gifts.TryGetValue(rule.GiftId, out gift))
                {
                    builder.Append(",\"coinsPerUnit\":")
                        .Append(gift.CoinsPerUnit);
                }
            }
            builder.Append('}');
        }

        private bool TryLoadCatalog(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                var json = File.ReadAllText(path, Encoding.UTF8);
                catalogVersion = ReadStringProperty(
                    json, "catalogVersion", 0, json.Length);
                var expression = new Regex(
                    "\\\"giftId\\\"\\s*:\\s*\\\"(?<id>\\d+)\\\"" +
                    "\\s*,\\s*\\\"name\\\"\\s*:\\s*" +
                    "\\\"(?<name>(?:\\\\.|[^\\\"])*)\\\"" +
                    "[\\s\\S]*?\\\"coinsPerUnit\\\"\\s*:\\s*" +
                    "(?<coins>\\d+)",
                    RegexOptions.CultureInvariant);
                var matches = expression.Matches(json);
                var catalogDirectory = Path.GetDirectoryName(
                    Path.GetFullPath(path)) ?? string.Empty;
                var imageDirectory = Path.Combine(
                    catalogDirectory, "images");
                for (var i = 0; i < matches.Count; i++)
                {
                    int coins;
                    if (!int.TryParse(matches[i].Groups["coins"].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out coins))
                        continue;
                    var id = matches[i].Groups["id"].Value;
                    if (!gifts.ContainsKey(id))
                        gifts.Add(id, new GiftEntry(
                            id,
                            UnescapeJson(matches[i].Groups["name"].Value),
                            coins,
                            Path.GetFullPath(Path.Combine(
                                imageDirectory, id + ".webp"))));
                }
                return catalogVersion.Length > 0 && gifts.Count > 0;
            }
            catch (Exception exception)
            {
                Warn("No se pudo leer el catalogo de regalos: " +
                    exception.Message);
                return false;
            }
        }

        private void LoadCommunityGift()
        {
            if (!File.Exists(communityGiftPath))
                return;
            try
            {
                var json = File.ReadAllText(communityGiftPath, Encoding.UTF8);
                int version;
                int coins;
                var giftId = ReadStringProperty(
                    json, "giftId", 0, json.Length);
                var previousGiftId = ReadStringProperty(
                    json, "previousGiftId", 0, json.Length);
                var name = ReadStringProperty(
                    json, "name", 0, json.Length);
                var imagePath = ReadStringProperty(
                    json, "imagePath", 0, json.Length);
                var observedAt = ReadStringProperty(
                    json, "observedAt", 0, json.Length);
                if (!TryReadIntProperty(json, "version", out version) ||
                    version != 1 || !IsPositiveNumericId(giftId) ||
                    !TryReadIntProperty(json, "coinsPerUnit", out coins) ||
                    coins < 1)
                    // Cuphead's legacy Mono omits InvalidDataException. A
                    // reference to it prevents the controller constructor
                    // from being JIT-compiled even when this branch is not
                    // reached, so keep the failure type in mscorlib.
                    throw new InvalidOperationException(
                        "El registro del Community Gift no es valido.");

                communityGift = new CommunityGiftState(
                    giftId,
                    IsPositiveNumericId(previousGiftId)
                        ? previousGiftId
                        : CommunityGiftPlaceholderId,
                    string.IsNullOrEmpty(name)
                        ? CommunityGiftPlaceholderName
                        : name,
                    NormalizeCommunityImagePath(imagePath),
                    coins,
                    observedAt);
                RegisterCommunityGift(communityGift.GiftId);
                if (communityGift.PreviousGiftId !=
                    CommunityGiftPlaceholderId)
                    RegisterCommunityGift(communityGift.PreviousGiftId);
            }
            catch (Exception exception)
            {
                Warn("No se pudo leer el Community Gift guardado: " +
                    exception.Message);
                communityGift = CommunityGiftState.Placeholder();
            }
        }

        private void ObserveCommunityGift(CreatorToolsStreamEvent streamEvent)
        {
            if (streamEvent == null || !streamEvent.IsCommunityGift ||
                streamEvent.Platform != "tiktok" ||
                streamEvent.Type != GiftEventType)
                return;

            var giftId = (streamEvent.ItemId ?? string.Empty).Trim();
            if (!IsPositiveNumericId(giftId) ||
                giftId == communityGift.GiftId)
                return;

            var name = (streamEvent.ItemName ?? string.Empty).Trim();
            if (name.Length == 0)
                name = CommunityGiftPlaceholderName;
            if (name.Length > 160)
                name = name.Substring(0, 160);
            var coins = streamEvent.UnitValue >= 1m
                ? (int)Math.Min(int.MaxValue, streamEvent.UnitValue)
                : 1;
            var candidate = new CommunityGiftState(
                giftId,
                communityGift.GiftId,
                name,
                NormalizeCommunityImagePath(streamEvent.ItemImageUrl),
                coins,
                UtcNow().ToString(
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture));
            if (!SaveCommunityGift(candidate))
                return;

            var candidateRules = CloneRules();
            var migratedRuleIds = new List<long>();
            for (var i = 0; i < candidateRules.Count; i++)
            {
                var rule = candidateRules[i];
                if (rule.EventType != GiftEventType ||
                    (rule.GiftId != CommunityGiftPlaceholderId &&
                     rule.GiftId != communityGift.GiftId))
                    continue;
                rule.GiftId = candidate.GiftId;
                rule.GiftName = candidate.Name;
                migratedRuleIds.Add(rule.Id);
            }
            if (migratedRuleIds.Count > 0 &&
                !SaveSettings(candidateRules, nextId))
                return;

            communityGift = candidate;
            RegisterCommunityGift(candidate.GiftId);
            rules.Clear();
            rules.AddRange(candidateRules);
            for (var i = 0; i < migratedRuleIds.Count; i++)
                ResetRuleRuntimeState(migratedRuleIds[i]);
            revision++;
            stateDirty = true;
        }

        private void MigrateLoadedCommunityGiftRules()
        {
            if (!communityGift.Learned)
                return;
            var migrated = false;
            var candidateRules = CloneRules();
            for (var i = 0; i < candidateRules.Count; i++)
            {
                var rule = candidateRules[i];
                if (rule.EventType != GiftEventType ||
                    (rule.GiftId != CommunityGiftPlaceholderId &&
                     rule.GiftId != communityGift.PreviousGiftId))
                    continue;
                rule.GiftId = communityGift.GiftId;
                rule.GiftName = communityGift.Name;
                migrated = true;
            }
            if (!migrated || !SaveSettings(candidateRules, nextId))
                return;
            rules.Clear();
            rules.AddRange(candidateRules);
        }

        private void RegisterCommunityGift(string giftId)
        {
            gifts[giftId] = new GiftEntry(
                giftId,
                communityGift.Name,
                communityGift.CoinsPerUnit,
                communityGift.ImagePath);
        }

        private bool SaveCommunityGift(CommunityGiftState value)
        {
            try
            {
                var directory = Path.GetDirectoryName(communityGiftPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var builder = new StringBuilder(512);
                builder.Append("{\n  \"version\": 1,\n  \"giftId\": \"");
                AppendJson(builder, value.GiftId);
                builder.Append("\",\n  \"previousGiftId\": \"");
                AppendJson(builder, value.PreviousGiftId);
                builder.Append("\",\n  \"name\": \"");
                AppendJson(builder, value.Name);
                builder.Append("\",\n  \"imagePath\": \"");
                AppendJson(builder, value.ImagePath);
                builder.Append("\",\n  \"coinsPerUnit\": ")
                    .Append(value.CoinsPerUnit)
                    .Append(",\n  \"observedAt\": \"");
                AppendJson(builder, value.ObservedAt);
                builder.Append("\"\n}\n");

                var temporaryPath = communityGiftPath + ".tmp";
                File.WriteAllText(temporaryPath, builder.ToString(),
                    new UTF8Encoding(false));
                if (File.Exists(communityGiftPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, communityGiftPath,
                            communityGiftPath + ".bak", true);
                        return true;
                    }
                    catch
                    {
                        File.Copy(communityGiftPath,
                            communityGiftPath + ".bak", true);
                        File.Delete(communityGiftPath);
                    }
                }
                File.Move(temporaryPath, communityGiftPath);
                return true;
            }
            catch (Exception exception)
            {
                Warn("No se pudo guardar el Community Gift: " +
                    exception.Message);
                return false;
            }
        }

        private static bool IsPositiveNumericId(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "0")
                return false;
            for (var i = 0; i < value.Length; i++)
                if (value[i] < '0' || value[i] > '9')
                    return false;
            return true;
        }

        private static string NormalizeCommunityImagePath(string value)
        {
            Uri uri;
            value = (value ?? string.Empty).Trim();
            return value.Length <= 2048 &&
                   Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                   uri.Scheme == Uri.UriSchemeHttps
                ? value
                : CommunityGiftPlaceholderImagePath;
        }

        private void LoadSettings()
        {
            if (TryLoadSettingsFile(settingsPath))
                return;
            var backupPath = settingsPath + ".bak";
            if (TryLoadSettingsFile(backupPath))
            {
                Warn("Las reglas principales no pudieron leerse; se " +
                    "recupero el respaldo.");
                SaveSettings();
                return;
            }
            if (File.Exists(settingsPath) || File.Exists(backupPath))
                Warn("La configuracion de reglas de stream no era valida; " +
                    "se iniciara vacia.");
            rules.Clear();
            nextId = 1;
        }

        private bool TryLoadSettingsFile(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                int version;
                long storedNextId;
                if (!TryReadIntProperty(json, "version", out version) ||
                    version < MinimumSupportedSchemaVersion ||
                    version > SchemaVersion ||
                    !TryReadLongProperty(json, "nextId", out storedNextId))
                    return false;

                var loaded = new List<StreamRule>();
                var expression = new Regex(
                    "\\{\\\"id\\\":(?<id>\\d+)," +
                    "\\\"name\\\":\\\"(?<name>(?:\\\\.|[^\\\"])*)\\\"," +
                    "\\\"enabled\\\":(?<enabled>true|false)," +
                    "\\\"platform\\\":\\\"(?<platform>tiktok|twitch)\\\"," +
                    "\\\"connectionId\\\":\\\"all\\\"," +
                    "\\\"eventType\\\":\\\"(?<eventType>[a-z_]+)\\\"," +
                    "\\\"giftId\\\":\\\"(?<giftId>\\d*)\\\"," +
                    "\\\"giftName\\\":\\\"(?<giftName>(?:\\\\.|[^\\\"])*)\\\"," +
                    "(?:\\\"rewardName\\\":\\\"(?<rewardName>(?:\\\\.|[^\\\"])*)\\\",)?" +
                    "\\\"every\\\":(?<every>\\d+)," +
                    "\\\"interaction\\\":\\\"(?<interaction>[^\\\"]+)\\\"," +
                    "\\\"quantity\\\":(?<quantity>\\d+)" +
                    "(?:,\\\"userCooldownSeconds\\\":(?<userCooldown>\\d+))?" +
                    "(?:,\\\"globalCooldownSeconds\\\":(?<globalCooldown>\\d+))?" +
                    "(?:,\\\"durationSeconds\\\":(?<duration>\\d+))?" +
                    "(?:,\\\"countdownSeconds\\\":(?<countdown>\\d+))?\\}",
                    RegexOptions.CultureInvariant);
                var matches = expression.Matches(json);
                // Reject an unsupported or malformed rule instead of silently
                // migrating a partially matched file and losing saved rules.
                if (matches.Count != Regex.Matches(json, "\\{\\s*\\\"id\\\"\\s*:").Count)
                    return false;
                var ids = new HashSet<long>();
                for (var i = 0; i < matches.Count; i++)
                {
                    long id;
                    int every;
                    int quantity;
                    int userCooldown;
                    int globalCooldown;
                    int duration, countdown;
                    var eventType =
                        matches[i].Groups["eventType"].Value;
                    var platform = matches[i].Groups["platform"].Value;
                    var rewardName = UnescapeJson(matches[i].Groups["rewardName"].Value).Trim();
                    var giftId = matches[i].Groups["giftId"].Value;
                    var interaction = matches[i].Groups["interaction"].Value;
                    if (!long.TryParse(matches[i].Groups["id"].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out id) ||
                        !int.TryParse(matches[i].Groups["every"].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out every) ||
                        !int.TryParse(matches[i].Groups["quantity"].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out quantity) ||
                        !TryReadOptionalBoundedInt(
                            matches[i].Groups["userCooldown"].Value,
                            0, MaximumCooldownSeconds,
                            out userCooldown) ||
                        !TryReadOptionalBoundedInt(
                            matches[i].Groups["globalCooldown"].Value,
                            0, MaximumCooldownSeconds,
                            out globalCooldown) ||
                        id <= 0 || !ids.Add(id) ||
                        every < 1 || every > MaximumEvery ||
                        quantity < 1 || quantity > MaximumQuantity ||
                        !IsSupportedEventType(platform, eventType) ||
                        (eventType == "redemption" && (rewardName.Length == 0 || rewardName.Length > MaximumRuleNameLength)) ||
                        (eventType == GiftEventType &&
                         !gifts.ContainsKey(giftId)) ||
                        (eventType == FollowEventType && every != 1) ||
                        !IsKnownInteraction(interaction) ||
                        !CreatorToolsTimedChallenge.TryDuration(matches[i].Groups["duration"].Value, out duration) ||
                        !CreatorToolsTimedChallenge.TryCountdown(matches[i].Groups["countdown"].Value, out countdown))
                        return false;
                    loaded.Add(new StreamRule
                    {
                        Id = id,
                        Name = NormalizeRuleName(UnescapeJson(
                            matches[i].Groups["name"].Value)),
                        Enabled = matches[i].Groups["enabled"].Value == "true",
                        Platform = platform,
                        RewardName = eventType == "redemption" ? rewardName : string.Empty,
                        EventType = eventType,
                        GiftId = eventType == GiftEventType
                            ? giftId
                            : string.Empty,
                        GiftName = eventType == GiftEventType
                            ? UnescapeJson(matches[i]
                                .Groups["giftName"].Value)
                            : string.Empty,
                        Every = every,
                        Interaction = interaction,
                        Quantity = quantity,
                        UserCooldownSeconds = userCooldown,
                        GlobalCooldownSeconds = globalCooldown,
                        DurationSeconds = duration,
                        CountdownSeconds = countdown
                    });
                }
                if (loaded.Count > MaximumRules ||
                    !Regex.IsMatch(
                        json,
                        "\\\"rules\\\"\\s*:\\s*\\[",
                        RegexOptions.CultureInvariant))
                    return false;
                rules.Clear();
                rules.AddRange(loaded);
                nextId = Math.Max(1, storedNextId);
                for (var i = 0; i < rules.Count; i++)
                    nextId = Math.Max(nextId, rules[i].Id + 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SaveSettings()
        {
            return SaveSettings(rules, nextId);
        }

        private bool SaveSettings(
            IList<StreamRule> rulesToSave,
            long nextIdToSave)
        {
            try
            {
                var directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var builder = new StringBuilder(4096);
                builder.Append("{\n  \"version\": ")
                    .Append(SchemaVersion)
                    .Append(",\n  \"nextId\": ")
                    .Append(nextIdToSave.ToString(
                        CultureInfo.InvariantCulture))
                    .Append(",\n  \"rules\": [");
                for (var i = 0; i < rulesToSave.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append("\n    ");
                    AppendRuleJson(builder, rulesToSave[i], false);
                }
                if (rulesToSave.Count > 0)
                    builder.Append('\n');
                builder.Append("  ]\n}\n");

                var temporaryPath = settingsPath + ".tmp";
                File.WriteAllText(temporaryPath, builder.ToString(),
                    new UTF8Encoding(false));
                if (File.Exists(settingsPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, settingsPath,
                            settingsPath + ".bak", true);
                        return true;
                    }
                    catch
                    {
                        File.Copy(settingsPath, settingsPath + ".bak", true);
                        File.Delete(settingsPath);
                    }
                }
                File.Move(temporaryPath, settingsPath);
                return true;
            }
            catch (Exception exception)
            {
                Warn("No se pudieron guardar las reglas de stream: " +
                    exception.Message);
                return false;
            }
        }

        private int FindRuleIndex(long id)
        {
            for (var i = 0; i < rules.Count; i++)
                if (rules[i].Id == id)
                    return i;
            return -1;
        }

        private List<StreamRule> CloneRules()
        {
            var cloned = new List<StreamRule>(rules.Count);
            for (var i = 0; i < rules.Count; i++)
                cloned.Add(rules[i].Clone(rules[i].Id));
            return cloned;
        }

        private void ResetRuleRuntimeState(long id)
        {
            var prefix = id.ToString(CultureInfo.InvariantCulture) + ":";
            var keys = new List<string>();
            foreach (var key in accumulators.Keys)
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    keys.Add(key);
            for (var i = 0; i < keys.Count; i++)
                accumulators.Remove(keys[i]);
            keys.Clear();
            foreach (var key in userCooldowns.Keys)
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    keys.Add(key);
            for (var i = 0; i < keys.Count; i++)
                userCooldowns.Remove(keys[i]);
            // Gift cooldowns are shared by every rule that maps the same
            // gift. A rule edit invalidates those ephemeral reservations;
            // persisted configuration remains untouched.
            globalCooldowns.Clear();
            dispatchBacklog.RemoveRule(id);
        }

        private static long IncrementRuleId(long value)
        {
            return value >= long.MaxValue ? long.MaxValue : value + 1;
        }

        private static bool IsKnownInteraction(string value)
        {
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
                if (string.Equals(value, CreatorToolsInteractionIds.All[i],
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsSupportedEventType(string platform, string value)
        {
            if (platform == "tiktok")
                return value == GiftEventType || value == LikeEventType || value == FollowEventType;
            return platform == "twitch" && (value == FollowEventType || value == "currency" ||
                value == "subscription" || value == "subscription_gift" || value == "resubscription" || value == "redemption");
        }

        private static string RuleEventType(CreatorToolsStreamEvent entry)
        {
            if (entry == null) return string.Empty;
            if (entry.Platform != "twitch") return entry.Type;
            if (entry.Type == "currency") return entry.Unit == "bit" ? "currency" : string.Empty;
            if (entry.Type == "subscription")
            {
                if (entry.RawEventType == "channel.subscription.gift") return "subscription_gift";
                if (entry.RawEventType == "channel.subscription.message") return "resubscription";
            }
            return entry.Type;
        }

        private static string BuildViewerKey(
            CreatorToolsStreamEvent streamEvent)
        {
            var userId = (streamEvent.UserId ?? string.Empty).Trim();
            if (userId.Length > 0)
                return "id:" + userId;
            var userName = (streamEvent.UserName ?? string.Empty).Trim();
            return userName.Length == 0
                ? string.Empty
                : "name:" + userName.ToLowerInvariant();
        }

        private static string BuildConnectionViewerKey(
            string connectionId,
            string viewerKey)
        {
            return (connectionId ?? string.Empty) + "\n" + viewerKey;
        }

        private static string BuildAccumulatorKey(
            long ruleId,
            string connectionId,
            string viewerKey)
        {
            return ruleId.ToString(CultureInfo.InvariantCulture) + ":" +
                (connectionId ?? string.Empty) + "\n" +
                (viewerKey ?? string.Empty);
        }

        private static string BuildGlobalCooldownKey(
            StreamRule rule,
            string connectionId)
        {
            return (connectionId ?? string.Empty) + "\n" +
                (rule.EventType == GiftEventType
                    ? "gift:" + rule.GiftId
                    : "rule:" + rule.Id.ToString(
                        CultureInfo.InvariantCulture));
        }

        private DateTime UtcNow()
        {
            var value = utcNow();
            return value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();
        }

        private static string NormalizeRuleName(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length > MaximumRuleNameLength)
                value = value.Substring(0, MaximumRuleNameLength);
            return value;
        }

        private static bool TryReadBoolean(string value, out bool result)
        {
            result = false;
            value = (value ?? string.Empty).Trim();
            if (value == "1" || string.Equals(value, "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }
            return value == "0" || string.Equals(value, "false",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadOptionalBoundedInt(
            string value, int minimum, int maximum, out int result)
        {
            result = 0;
            return string.IsNullOrEmpty(value) ||
                TryReadBoundedInt(value, minimum, maximum, out result);
        }

        private static bool TryReadBoundedInt(
            string value, int minimum, int maximum, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer,
                       CultureInfo.InvariantCulture, out result) &&
                   result >= minimum && result <= maximum;
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
                if (key.Length <= 64 && value.Length <= 1024)
                    values[key] = value;
            }
            return values;
        }

        private static string Value(
            Dictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value)
                ? value
                : string.Empty;
        }

        private static string ReadStringProperty(
            string json, string property, int start, int end)
        {
            var marker = "\"" + property + "\"";
            var position = json.IndexOf(marker, start,
                Math.Max(0, end - start), StringComparison.Ordinal);
            if (position < 0)
                return string.Empty;
            position = json.IndexOf(':', position + marker.Length);
            if (position < 0 || position >= end)
                return string.Empty;
            position++;
            while (position < end && char.IsWhiteSpace(json[position]))
                position++;
            if (position >= end || json[position] != '"')
                return string.Empty;
            position++;
            var builder = new StringBuilder();
            var escaped = false;
            while (position < end)
            {
                var character = json[position++];
                if (!escaped && character == '"')
                    return UnescapeJson(builder.ToString());
                if (!escaped && character == '\\')
                    escaped = true;
                else
                {
                    if (escaped)
                        builder.Append('\\');
                    builder.Append(character);
                    escaped = false;
                }
            }
            return string.Empty;
        }

        private static bool TryReadIntProperty(
            string json, string property, out int value)
        {
            long parsed;
            var result = TryReadLongProperty(json, property, out parsed) &&
                parsed >= int.MinValue && parsed <= int.MaxValue;
            value = result ? (int)parsed : 0;
            return result;
        }

        private static bool TryReadLongProperty(
            string json, string property, out long value)
        {
            value = 0;
            var marker = "\"" + property + "\"";
            var position = json.IndexOf(marker, StringComparison.Ordinal);
            if (position < 0)
                return false;
            position = json.IndexOf(':', position + marker.Length);
            if (position < 0)
                return false;
            position++;
            while (position < json.Length && char.IsWhiteSpace(json[position]))
                position++;
            var start = position;
            while (position < json.Length && char.IsDigit(json[position]))
                position++;
            return position > start && long.TryParse(
                json.Substring(start, position - start),
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out value);
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

        private static string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value ?? string.Empty;
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(character);
                    continue;
                }
                character = value[++i];
                if (character == 'n') builder.Append('\n');
                else if (character == 'r') builder.Append('\r');
                else if (character == 't') builder.Append('\t');
                else if (character == 'b') builder.Append('\b');
                else if (character == 'f') builder.Append('\f');
                else if (character == 'u' && i + 4 < value.Length)
                {
                    int code;
                    if (int.TryParse(value.Substring(i + 1, 4),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out code))
                    {
                        builder.Append((char)code);
                        i += 4;
                    }
                }
                else builder.Append(character);
            }
            return builder.ToString();
        }

        private void Warn(string message)
        {
            if (logWarning != null)
                logWarning(message);
        }

        private sealed class GiftEntry
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly int CoinsPerUnit;
            internal readonly string ImagePath;

            internal GiftEntry(
                string id,
                string name,
                int coinsPerUnit,
                string imagePath)
            {
                Id = id;
                Name = name;
                CoinsPerUnit = coinsPerUnit;
                ImagePath = imagePath ?? string.Empty;
            }
        }

        private sealed class CommunityGiftState
        {
            internal readonly string GiftId;
            internal readonly string PreviousGiftId;
            internal readonly string Name;
            internal readonly string ImagePath;
            internal readonly int CoinsPerUnit;
            internal readonly string ObservedAt;

            internal CommunityGiftState(
                string giftId,
                string previousGiftId,
                string name,
                string imagePath,
                int coinsPerUnit,
                string observedAt)
            {
                GiftId = giftId;
                PreviousGiftId = previousGiftId;
                Name = name;
                ImagePath = imagePath;
                CoinsPerUnit = coinsPerUnit;
                ObservedAt = observedAt ?? string.Empty;
            }

            internal bool Learned
            {
                get { return GiftId != CommunityGiftPlaceholderId; }
            }

            internal static CommunityGiftState Placeholder()
            {
                return new CommunityGiftState(
                    CommunityGiftPlaceholderId,
                    CommunityGiftPlaceholderId,
                    CommunityGiftPlaceholderName,
                    CommunityGiftPlaceholderImagePath,
                    1,
                    string.Empty);
            }
        }

        private sealed class StreamRule
        {
            internal long Id;
            internal string Name;
            internal bool Enabled;
            internal string Platform;
            internal string RewardName;
            internal string EventType;
            internal string GiftId;
            internal string GiftName;
            internal int Every;
            internal string Interaction;
            internal int Quantity;
            internal int UserCooldownSeconds;
            internal int GlobalCooldownSeconds;
            internal int DurationSeconds = CreatorToolsTimedChallenge.DefaultDuration;
            internal int CountdownSeconds = CreatorToolsTimedChallenge.DefaultCountdown;

            internal StreamRule Clone(long id)
            {
                return new StreamRule
                {
                    Id = id,
                    Name = Name,
                    Enabled = Enabled,
                    Platform = Platform,
                    RewardName = RewardName,
                    EventType = EventType,
                    GiftId = GiftId,
                    GiftName = GiftName,
                    Every = Every,
                    Interaction = Interaction,
                    Quantity = Quantity,
                    UserCooldownSeconds = UserCooldownSeconds,
                    GlobalCooldownSeconds = GlobalCooldownSeconds,
                    DurationSeconds = DurationSeconds,
                    CountdownSeconds = CountdownSeconds
                };
            }
        }

        private sealed class PendingRuleDispatch
        {
            internal readonly StreamRule Rule;
            internal readonly long TriggerCount;
            internal readonly int QuantityPerTrigger;
            internal readonly string GiftImagePath;

            internal PendingRuleDispatch(
                StreamRule rule,
                long triggerCount,
                int quantityPerTrigger,
                string giftImagePath)
            {
                Rule = rule;
                TriggerCount = triggerCount;
                QuantityPerTrigger = quantityPerTrigger;
                GiftImagePath = giftImagePath ?? string.Empty;
            }
        }
    }
}
