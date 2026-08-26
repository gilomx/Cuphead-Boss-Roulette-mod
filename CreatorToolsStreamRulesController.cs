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
        private const int SchemaVersion = 1;
        private const int MaximumRules = 100;
        private const int MaximumCommandsPerUpdate = 64;
        private const int MaximumRuleNameLength = 64;
        private const int MaximumEvery = 1000000;
        private const int MaximumQuantity = 50;

        private readonly string settingsPath;
        private readonly Action<string> logWarning;
        private readonly object ruleStateLock = new object();
        private readonly Dictionary<string, GiftEntry> gifts =
            new Dictionary<string, GiftEntry>(StringComparer.Ordinal);
        private readonly List<StreamRule> rules = new List<StreamRule>();
        private readonly Dictionary<string, long> accumulators =
            new Dictionary<string, long>(StringComparer.Ordinal);
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

        internal CreatorToolsStreamRulesController(
            string assetsDirectory,
            string pluginConfigPath,
            Action<string> logWarning)
        {
            this.logWarning = logWarning;
            var configDirectory = Path.GetDirectoryName(
                string.IsNullOrEmpty(pluginConfigPath)
                    ? string.Empty
                    : Path.GetFullPath(pluginConfigPath));
            if (string.IsNullOrEmpty(configDirectory))
                configDirectory = Environment.CurrentDirectory;
            settingsPath = Path.Combine(configDirectory,
                "mx.gilomx.cuphead.bossroulette.stream-rules.json");

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
            LoadSettings();
        }

        internal int Update(
            CreatorToolsServer server,
            CreatorToolsInteractionController interactions)
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

                lock (ruleStateLock)
                {
                    if (stateDirty)
                    {
                        var state = BuildState();
                        if (state != lastPublishedState)
                        {
                            lastPublishedState = state;
                            server.SetStreamRulesState(state);
                        }
                        stateDirty = false;
                    }
                }
            }
            // HTTP CRUD can purge runtime state while Unity remains
            // unfocused. Serialize backlog mutation with evaluation, but
            // keep all gameplay queue access on this main-thread call.
            lock (ruleStateLock)
                return dispatchBacklog.Drain(interactions);
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
        /// Evaluates every compatible rule independently. Accumulators are
        /// session-only and scoped by rule plus connection; each keeps its
        /// remainder after crossing an every-N threshold.
        /// </summary>
        internal CreatorToolsStreamEvaluation Evaluate(
            CreatorToolsStreamEvent streamEvent,
            CreatorToolsInteractionController interactions)
        {
            lock (ruleStateLock)
                return EvaluateLocked(streamEvent, interactions);
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
                streamEvent.ItemImageUrl =
                    "/assets/creator-tools/gifts/images/" +
                    gift.Id + ".png";
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

        private CreatorToolsStreamEvaluation EvaluateLocked(
            CreatorToolsStreamEvent streamEvent,
            CreatorToolsInteractionController interactions)
        {
            var result = new CreatorToolsStreamEvaluation();
            if (!catalogReady || streamEvent == null || interactions == null ||
                streamEvent.Platform != "tiktok" ||
                streamEvent.Type != "gift")
                return result;
            if (string.IsNullOrEmpty(streamEvent.ItemId))
            {
                result.MessageCode = "gift_id_missing";
                return result;
            }

            var matchedNames = new List<string>();
            var interactionIds = new List<string>();
            var thresholdObserved = false;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!rule.Enabled || rule.GiftId != streamEvent.ItemId)
                    continue;
                thresholdObserved = true;
                var accumulatorKey = rule.Id.ToString(
                    CultureInfo.InvariantCulture) + ":" +
                    (streamEvent.ConnectionId ?? string.Empty);
                long remainder;
                accumulators.TryGetValue(accumulatorKey, out remainder);
                var total = Math.Min(long.MaxValue - streamEvent.Count,
                    Math.Max(0L, remainder)) + streamEvent.Count;
                var triggers = total / rule.Every;
                accumulators[accumulatorKey] = total % rule.Every;
                if (triggers <= 0)
                    continue;

                result.MatchedRules++;
                matchedNames.Add(rule.Name);
                interactionIds.Add(rule.Interaction);
                var requestedLong = triggers * (long)rule.Quantity;
                var canDispatchImmediately = !dispatchBacklog.HasEntries;
                var pending = dispatchBacklog.Add(
                    rule.Id,
                    streamEvent.ConnectionId,
                    rule.Interaction,
                    gifts[rule.GiftId].ImagePath,
                    streamEvent.UserName,
                    requestedLong);
                var queuedForRule = 0;
                if (canDispatchImmediately)
                {
                    string feedbackCode;
                    queuedForRule = dispatchBacklog.DrainEntry(
                        pending, interactions, out feedbackCode);
                    dispatchBacklog.RemoveIfComplete(pending);
                    if (queuedForRule == 0 &&
                        string.IsNullOrEmpty(result.MessageCode))
                        result.MessageCode = feedbackCode;
                }
                result.QueuedInteractions += queuedForRule;
                result.DeferredInteractions += Math.Max(
                    0L, requestedLong - queuedForRule);
            }

            result.RuleNames = string.Join(", ", matchedNames.ToArray());
            result.InteractionIds = string.Join(", ",
                interactionIds.ToArray());
            if (result.QueuedInteractions > 0)
                result.MessageCode = result.DeferredInteractions > 0
                    ? "rules_partially_queued"
                    : "rules_queued";
            else if (result.MatchedRules > 0 &&
                result.DeferredInteractions > 0)
                result.MessageCode = "rules_waiting_queue";
            else if (result.MatchedRules > 0 &&
                     string.IsNullOrEmpty(result.MessageCode))
                result.MessageCode = "rules_matched";
            else if (thresholdObserved)
                result.MessageCode = "threshold_pending";
            return result;
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
                    rules[index].GiftId != rule.GiftId ||
                    rules[index].Every != rule.Every ||
                    rules[index].Interaction != rule.Interaction ||
                    rules[index].Quantity != rule.Quantity ||
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
            var giftId = Value(values, "giftId").Trim();
            var interaction = Value(values, "interaction").Trim();
            GiftEntry gift;
            if (name.Length == 0 || !gifts.TryGetValue(giftId, out gift) ||
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
            if (!TryReadBoundedInt(
                    Value(values, "every"), 1, MaximumEvery, out every) ||
                !TryReadBoundedInt(Value(values, "quantity"),
                    1, MaximumQuantity, out quantity))
            {
                SetFeedback("invalid_rule", true);
                return false;
            }

            rule = new StreamRule
            {
                Id = id,
                Name = name,
                Enabled = enabled,
                GiftId = gift.Id,
                GiftName = gift.Name,
                Every = every,
                Interaction = interaction,
                Quantity = quantity
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
                .Append(catalogReady ? "true" : "false")
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

        private void AppendRuleJson(
            StringBuilder builder, StreamRule rule, bool includeGift)
        {
            builder.Append("{\"id\":")
                .Append(rule.Id.ToString(CultureInfo.InvariantCulture))
                .Append(",\"name\":\"");
            AppendJson(builder, rule.Name);
            builder.Append("\",\"enabled\":")
                .Append(rule.Enabled ? "true" : "false")
                .Append(",\"platform\":\"tiktok\"")
                .Append(",\"connectionId\":\"all\"")
                .Append(",\"eventType\":\"gift\"")
                .Append(",\"giftId\":\"");
            AppendJson(builder, rule.GiftId);
            builder.Append("\",\"giftName\":\"");
            AppendJson(builder, rule.GiftName);
            builder.Append("\",\"every\":")
                .Append(rule.Every)
                .Append(",\"interaction\":\"");
            AppendJson(builder, rule.Interaction);
            builder.Append("\",\"quantity\":")
                .Append(rule.Quantity);
            if (includeGift)
            {
                GiftEntry gift;
                if (gifts.TryGetValue(rule.GiftId, out gift))
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
                                imageDirectory, id + ".png"))));
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
                    version != SchemaVersion ||
                    !TryReadLongProperty(json, "nextId", out storedNextId))
                    return false;

                var loaded = new List<StreamRule>();
                var expression = new Regex(
                    "\\{\\\"id\\\":(?<id>\\d+)," +
                    "\\\"name\\\":\\\"(?<name>(?:\\\\.|[^\\\"])*)\\\"," +
                    "\\\"enabled\\\":(?<enabled>true|false)," +
                    "\\\"platform\\\":\\\"tiktok\\\"," +
                    "\\\"connectionId\\\":\\\"all\\\"," +
                    "\\\"eventType\\\":\\\"gift\\\"," +
                    "\\\"giftId\\\":\\\"(?<giftId>\\d+)\\\"," +
                    "\\\"giftName\\\":\\\"(?<giftName>(?:\\\\.|[^\\\"])*)\\\"," +
                    "\\\"every\\\":(?<every>\\d+)," +
                    "\\\"interaction\\\":\\\"(?<interaction>[^\\\"]+)\\\"," +
                    "\\\"quantity\\\":(?<quantity>\\d+)\\}",
                    RegexOptions.CultureInvariant);
                var matches = expression.Matches(json);
                var ids = new HashSet<long>();
                for (var i = 0; i < matches.Count; i++)
                {
                    long id;
                    int every;
                    int quantity;
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
                        id <= 0 || !ids.Add(id) ||
                        every < 1 || every > MaximumEvery ||
                        quantity < 1 || quantity > MaximumQuantity ||
                        !gifts.ContainsKey(giftId) ||
                        !IsKnownInteraction(interaction))
                        return false;
                    loaded.Add(new StreamRule
                    {
                        Id = id,
                        Name = NormalizeRuleName(UnescapeJson(
                            matches[i].Groups["name"].Value)),
                        Enabled = matches[i].Groups["enabled"].Value == "true",
                        GiftId = giftId,
                        GiftName = UnescapeJson(
                            matches[i].Groups["giftName"].Value),
                        Every = every,
                        Interaction = interaction,
                        Quantity = quantity
                    });
                }
                if (loaded.Count > MaximumRules ||
                    json.IndexOf("\"rules\":[",
                        StringComparison.Ordinal) < 0)
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

        private sealed class StreamRule
        {
            internal long Id;
            internal string Name;
            internal bool Enabled;
            internal string GiftId;
            internal string GiftName;
            internal int Every;
            internal string Interaction;
            internal int Quantity;

            internal StreamRule Clone(long id)
            {
                return new StreamRule
                {
                    Id = id,
                    Name = Name,
                    Enabled = Enabled,
                    GiftId = GiftId,
                    GiftName = GiftName,
                    Every = Every,
                    Interaction = Interaction,
                    Quantity = Quantity
                };
            }
        }
    }
}
