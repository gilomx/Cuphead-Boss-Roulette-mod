using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal delegate bool CreatorToolsGiftResolver(
        string giftId, out CreatorToolsGiftCatalogEntry gift);

    internal sealed class CreatorToolsPeskyBattleController
    {
        private const int SchemaVersion = 1;
        private const int Capacity = 5;
        private const int MaximumCommandsPerUpdate = 64;
        private const float MinimumIntervalSeconds = 1.25f;
        private const float MaximumIntervalSeconds = 3.25f;

        private readonly CreatorToolsPeskyBattleSettings settings;
        private readonly CreatorToolsInteractionQueue queue;
        private readonly CreatorToolsGiftResolver resolveGift;
        private readonly Func<string, bool> isItemAvailable;
        private readonly Action disableFreePesky;
        private readonly Action<string> logInfo;
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

        internal CreatorToolsPeskyBattleController(
            string pluginConfigPath,
            CreatorToolsInteractionQueue queue,
            CreatorToolsGiftResolver resolveGift,
            Func<string, bool> isItemAvailable,
            Action disableFreePesky,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            settings = CreatorToolsPeskyBattleSettings.Load(
                pluginConfigPath, logWarning);
            this.queue = queue;
            this.resolveGift = resolveGift;
            this.isItemAvailable = isItemAvailable;
            this.disableFreePesky = disableFreePesky;
            this.logInfo = logInfo;
        }

        internal bool Exclusive
        {
            get
            {
                return phase == "recruiting" || phase == "ready" ||
                    phase == "waiting_level" || phase == "active";
            }
        }

        internal bool Active
        {
            get { return phase == "active"; }
        }

        internal bool StreamAttacksAllowed
        {
            get { return !Exclusive || settings.AllowStreamAttacks; }
        }

        internal string Phase
        {
            get { return phase; }
        }

        internal void ProcessCommands(CreatorToolsServer server)
        {
            if (server == null || !server.IsRunning)
                return;
            var processed = 0;
            string query;
            while (processed < MaximumCommandsPerUpdate &&
                   server.TryTakePeskyBattleCommand(out query))
            {
                ProcessCommand(ParseQuery(query));
                processed++;
            }
        }

        internal void Update(
            CreatorToolsServer server, bool gameplayAvailable)
        {
            this.gameplayAvailable = gameplayAvailable;
            UpdateAttackScheduler();
            if (server == null || !server.IsRunning)
                return;
            var state = BuildState();
            if (state == lastState)
                return;
            lastState = state;
            server.SetPeskyBattleState(state);
        }

        internal string ObserveStreamEvent(CreatorToolsStreamEvent entry)
        {
            if (entry == null || !Exclusive ||
                entry.Platform != "tiktok" || entry.Type != "gift" ||
                entry.StreakState == "progress" ||
                entry.ItemId != settings.GiftId)
                return string.Empty;

            var identity = ParticipantIdentity(entry);
            if (identity.Length == 0)
            {
                SetFeedback("participant_identity_missing", true);
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
                        Touch();
                }
                SetFeedback("participant_already_joined", false);
                return "participant_already_joined";
            }

            if (phase != "recruiting" || participants.Count >= Capacity)
                return string.Empty;
            participants.Add(Participant.From(
                participants.Count + 1, identity, entry));
            SetFeedback("participant_joined", false);
            if (participants.Count == Capacity)
            {
                phase = "ready";
                SetFeedback("lobby_ready", false);
                return "lobby_ready";
            }
            return "participant_joined";
        }

        internal void OnLevelStarted(Level level)
        {
            if (level == null ||
                (phase != "waiting_level" && phase != "active"))
                return;
            var logicalLevel = LogicalLevel(level.CurrentLevel);

            // Cuphead can replace the Level instance before the old instance
            // receives its teardown hook (notably between King Dice boards and
            // after some retries). Rebind the battle here so it cannot remain
            // attached to an object that is no longer current.
            if (phase == "active")
            {
                var replacementId = level.GetInstanceID();
                if (replacementId == activeLevelInstanceId)
                    return;

                ClearBattleEntries();
                activeLevelInstanceId = -1;
                ResetAttackSchedule();
                if (targetLevel != logicalLevel)
                {
                    phase = "waiting_level";
                    dicePalaceTransition = false;
                    SetFeedback("waiting_target_level", false);
                    return;
                }

                activeLevelInstanceId = replacementId;
                if (!dicePalaceTransition)
                    attempt++;
                dicePalaceTransition = false;
                SetFeedback("battle_started", false);
                return;
            }

            if (targetLevel.Length == 0)
                targetLevel = logicalLevel;
            if (targetLevel != logicalLevel)
            {
                SetFeedback("waiting_target_level", false);
                return;
            }
            phase = "active";
            activeLevelInstanceId = level.GetInstanceID();
            if (!dicePalaceTransition)
                attempt++;
            dicePalaceTransition = false;
            ResetAttackSchedule();
            SetFeedback("battle_started", false);
        }

        internal void OnLevelDefeated(Level level)
        {
            if (!MatchesActiveLevel(level) || phase != "active")
                return;
            phase = "waiting_level";
            activeLevelInstanceId = -1;
            dicePalaceTransition = false;
            ClearBattleEntries();
            ResetAttackSchedule();
            SetFeedback("battle_lost_retrying", false);
        }

        internal void OnLevelPreWin(Level level)
        {
            if (!MatchesActiveLevel(level) || phase != "active")
                return;
            if (targetLevel == "DicePalace" &&
                level.CurrentLevel.ToString() != "DicePalaceMain")
            {
                dicePalaceTransition = true;
                ResetAttackSchedule();
                return;
            }
            phase = "won";
            activeLevelInstanceId = -1;
            dicePalaceTransition = false;
            ClearBattleEntries();
            ResetAttackSchedule();
            SetFeedback("battle_won", false);
        }

        internal void OnLevelEnded(Level level)
        {
            if (!MatchesActiveLevel(level) || phase != "active")
                return;
            activeLevelInstanceId = -1;
            ClearBattleEntries();
            ResetAttackSchedule();
            if (dicePalaceTransition)
            {
                phase = "waiting_level";
                SetFeedback("dice_palace_transition", false);
                return;
            }
            phase = "waiting_level";
            SetFeedback("waiting_for_retry", false);
        }

        internal void OnPhaseTransition()
        {
            if (!Active)
                return;
            queue.ClearPending(
                CreatorToolsInteractionSource.PeskyBattle);
            ResetAttackSchedule();
            Touch();
        }

        internal void InvalidateState()
        {
            lastState = null;
        }

        internal void ReportDispatchFeedback(string value, bool isError)
        {
            SetFeedback(value, isError);
        }

        internal void Dispose()
        {
            ClearBattleEntries();
            participants.Clear();
            phase = "off";
            ResetAttackSchedule();
            lastState = null;
        }

        private void ProcessCommand(Dictionary<string, string> values)
        {
            string action;
            if (values.TryGetValue("action", out action))
            {
                action = (action ?? string.Empty).Trim().ToLowerInvariant();
                if (action == "arm" && phase != "off")
                {
                    SetFeedback("invalid_action", true);
                    return;
                }
                if (action == "arm" && !TryApplyArmSettings(values))
                    return;
                ProcessAction(action);
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
                if (enabled && phase != "off")
                {
                    SetFeedback("invalid_action", true);
                    return;
                }
                if (enabled && !TryApplyArmSettings(values))
                    return;
                ProcessAction(enabled ? "arm" : "off");
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
                settings.AllowStreamAttacks = enabled;
                settings.Save();
                SetFeedback(enabled
                    ? "stream_attacks_allowed"
                    : "stream_attacks_blocked", false);
                return;
            }
            if (values.ContainsKey("item"))
            {
                SetItem(values);
                return;
            }
            SetFeedback("invalid_setting", true);
        }

        private bool TryApplyArmSettings(
            Dictionary<string, string> values)
        {
            string giftValue;
            string allowValue;
            var hasGift = values.TryGetValue("giftId", out giftValue);
            var hasAllow = values.TryGetValue(
                "allowStreamAttacks", out allowValue);
            var giftId = settings.GiftId;
            var allowStreamAttacks = settings.AllowStreamAttacks;

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
            if (settings.EnabledItemCount == 0)
            {
                SetFeedback("items_required", true);
                return false;
            }
            if (!hasGift && !hasAllow)
                return true;

            settings.GiftId = finalGift.Id;
            settings.AllowStreamAttacks = allowStreamAttacks;
            settings.Save();
            return true;
        }

        private void ProcessAction(string action)
        {
            if (action == "cancel" || action == "reset" ||
                action == "off" || action == "disable")
            {
                ResetSession("battle_cancelled");
                return;
            }
            if (action == "arm")
            {
                CreatorToolsGiftCatalogEntry gift;
                if (settings.GiftId.Length == 0 || resolveGift == null ||
                    !resolveGift(settings.GiftId, out gift))
                {
                    SetFeedback("gift_required", true);
                    return;
                }
                if (settings.EnabledItemCount == 0)
                {
                    SetFeedback("items_required", true);
                    return;
                }
                ClearBattleEntries();
                participants.Clear();
                targetLevel = string.Empty;
                activeLevelInstanceId = -1;
                attempt = 0;
                sessionId++;
                if (sessionId <= 0)
                    sessionId = 1;
                phase = "recruiting";
                dicePalaceTransition = false;
                ResetAttackSchedule();
                if (disableFreePesky != null)
                    disableFreePesky();
                SetFeedback("battle_armed", false);
                return;
            }
            if (action == "start")
            {
                if (phase != "ready")
                {
                    SetFeedback("lobby_not_ready", true);
                    return;
                }
                phase = "waiting_level";
                ResetAttackSchedule();
                SetFeedback("waiting_for_level", false);
                return;
            }
            SetFeedback("invalid_action", true);
        }

        private void SetGift(string value)
        {
            if (phase != "off")
            {
                SetFeedback("battle_active_setting_locked", true);
                return;
            }
            var giftId = CreatorToolsPeskyBattleSettings.NormalizeGiftId(value);
            CreatorToolsGiftCatalogEntry gift;
            if (giftId.Length == 0 || resolveGift == null ||
                !resolveGift(giftId, out gift))
            {
                SetFeedback("unknown_gift", true);
                return;
            }
            settings.GiftId = gift.Id;
            settings.Save();
            SetFeedback("gift_saved", false);
        }

        private void SetItem(Dictionary<string, string> values)
        {
            if (phase != "off")
            {
                SetFeedback("battle_active_setting_locked", true);
                return;
            }
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
            if (enabled)
                settings.DisabledItems.Remove(item);
            else if (settings.EnabledItemCount <= 1 &&
                     settings.IsItemEnabled(item))
            {
                SetFeedback("items_required", true);
                return;
            }
            else
                settings.DisabledItems.Add(item);
            settings.Save();
            SetFeedback("items_saved", false);
        }

        private void UpdateAttackScheduler()
        {
            if (!Active || dicePalaceTransition || !gameplayAvailable)
            {
                nextAttackAt = -1f;
                return;
            }
            var now = Time.realtimeSinceStartup;
            if (nextAttackAt < 0f)
            {
                ScheduleNextAttack(now);
                return;
            }
            if (now < nextAttackAt)
                return;
            ScheduleNextAttack(now);

            if (queue == null || participants.Count != Capacity ||
                queue.PendingCountFor(
                    CreatorToolsInteractionSource.PeskyBattle) > 0)
                return;
            var availableItems = new List<string>();
            for (var i = 0; i < CreatorToolsInteractionIds.All.Length; i++)
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
            Touch();
        }

        private void ResetSession(string feedbackCode)
        {
            ClearBattleEntries();
            participants.Clear();
            phase = "off";
            attempt = 0;
            activeLevelInstanceId = -1;
            targetLevel = string.Empty;
            dicePalaceTransition = false;
            ResetAttackSchedule();
            SetFeedback(feedbackCode, false);
        }

        private void ClearBattleEntries()
        {
            if (queue != null)
                queue.ClearSource(
                    CreatorToolsInteractionSource.PeskyBattle);
        }

        private void ScheduleNextAttack(float now)
        {
            nextAttackAt = now + UnityEngine.Random.Range(
                MinimumIntervalSeconds, MaximumIntervalSeconds);
        }

        private void ResetAttackSchedule()
        {
            nextAttackAt = -1f;
            Touch();
        }

        private bool MatchesActiveLevel(Level level)
        {
            return level != null && activeLevelInstanceId >= 0 &&
                level.GetInstanceID() == activeLevelInstanceId;
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

        private string BuildState()
        {
            CreatorToolsGiftCatalogEntry gift;
            if (resolveGift == null ||
                !resolveGift(settings.GiftId, out gift))
                gift = new CreatorToolsGiftCatalogEntry(
                    settings.GiftId, string.Empty, string.Empty, 0);
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
                .Append(Exclusive ? "true" : "false")
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
            AppendItems(builder, false);
            builder.Append("],\"disabledItems\":[");
            AppendItems(builder, true);
            builder.Append("],\"feedback\":\"");
            CreatorToolsJson.AppendEscaped(builder, feedback);
            builder.Append("\",\"error\":")
                .Append(error ? "true" : "false")
                .Append('}');
            return builder.ToString();
        }

        private void AppendItems(StringBuilder builder, bool disabledOnly)
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
            feedback = value;
            error = isError;
            Touch();
        }

        private void Touch()
        {
            revision++;
            if (revision < 0)
                revision = 1;
            lastState = null;
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
