using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class TwitchRewardBridge
    {
        private const int MaximumCost = 1000000000;
        private const int MinimumCooldownSeconds = 60;
        private const int MaximumCooldownSeconds = 604800;
        private const int RequestSeconds = 20;
        private readonly object gate = new object();
        private readonly Func<DateTime> clock;
        private readonly Dictionary<string, Reward> rewards = new Dictionary<string, Reward>(StringComparer.Ordinal);
        private Dictionary<string, DesiredReward> desired = new Dictionary<string, DesiredReward>(StringComparer.Ordinal);
        private string desiredJson;
        private string broadcasterId = string.Empty;
        private string broadcasterLogin = string.Empty;
        private bool canManage;
        private bool catalogLoaded;
        private bool catalogRequested;
        private DateTime refreshAt;
        private DateTime retrySyncAt;
        private string catalogError = string.Empty;
        private string syncError = string.Empty;
        private Request queued;
        private Request inFlight;

        internal TwitchRewardBridge(Func<DateTime> clock = null)
        {
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        internal void SetConnection(string channel, bool mayManage, string login = "")
        {
            channel = ValidId(channel, 32, true) ? channel : string.Empty;
            lock (gate)
            {
                if (channel != broadcasterId)
                {
                    Complete(queued, Error("session_changed"));
                    Complete(inFlight, Error("session_changed"));
                    queued = null;
                    inFlight = null;
                    rewards.Clear();
                    catalogLoaded = false;
                    catalogError = syncError = string.Empty;
                    refreshAt = retrySyncAt = DateTime.MinValue;
                    broadcasterId = channel;
                }
                if (canManage != mayManage) retrySyncAt = DateTime.MinValue;
                canManage = channel.Length != 0 && mayManage;
                broadcasterLogin = channel.Length != 0 && ValidLogin(login) ? login : string.Empty;
            }
        }

        internal void SetDesired(string json)
        {
            lock (gate)
            {
                if (json == desiredJson) return;
                CreatorToolsJsonValue state;
                if (!CreatorToolsJsonParser.TryParse(json, out state) || state.ArrayValue == null) return;
                var next = new Dictionary<string, DesiredReward>(StringComparer.Ordinal);
                foreach (var value in state.ArrayValue)
                {
                    var id = value.String("id");
                    var channel = value.String("broadcasterId");
                    bool enabled;
                    if (!ValidId(id, 96, false) || !ValidId(channel, 32, true) || !value.TryBoolean("enabled", out enabled)) continue;
                    var item = new DesiredReward { Id = id, BroadcasterId = channel, Enabled = enabled };
                    next[item.Key] = item;
                }
                // Disable a reward removed from the last rule during this run.
                // Preserve other broadcasters' entries until their account reconnects.
                foreach (var pair in desired)
                {
                    if (!next.ContainsKey(pair.Key))
                        next[pair.Key] = new DesiredReward { Id = pair.Value.Id, BroadcasterId = pair.Value.BroadcasterId, Removed = true };
                }
                desired = next;
                desiredJson = json;
                retrySyncAt = DateTime.MinValue;
            }
        }

        internal string GetState()
        {
            lock (gate)
            {
                catalogRequested = true;
                var builder = new StringBuilder("{\"ready\":").Append(catalogLoaded ? "true" : "false")
                    .Append(",\"canManage\":").Append(canManage ? "true" : "false");
                AppendString(builder, "broadcasterId", broadcasterId);
                AppendString(builder, "broadcasterLogin", broadcasterLogin);
                var error = broadcasterId.Length == 0 ? "not_connected" : catalogError;
                if (error.Length != 0) AppendString(builder, "error", error);
                builder.Append(",\"rewards\":[");
                var first = true;
                foreach (var reward in rewards.Values)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    reward.AppendJson(builder, broadcasterId);
                }
                builder.Append("],\"pendingRewardIds\":[");
                first = true;
                foreach (var item in desired.Values)
                {
                    if (!IsPending(item)) continue;
                    if (!first) builder.Append(',');
                    first = false;
                    builder.Append('"');
                    CreatorToolsJson.AppendEscaped(builder, item.Id);
                    builder.Append('"');
                }
                builder.Append(']');
                if (!first)
                {
                    var pendingError = broadcasterId.Length == 0 ? "not_connected" : !canManage ? "manage_permission_required"
                        : syncError.Length != 0 ? syncError : catalogError;
                    if (pendingError.Length != 0) AppendString(builder, "syncError", pendingError);
                }
                return builder.Append('}').ToString();
            }
        }

        internal string Command(string body)
        {
            CreatorToolsJsonValue command;
            if (!CreatorToolsJsonParser.TryParse(body, out command, 2048, 256)) return Error("invalid_request");
            var operation = command.String("operation");
            var channel = command.String("broadcasterId");
            var id = command.String("id");
            var title = command.String("title").Trim();
            int? cooldownSeconds, maxPerStream, maxPerUserPerStream;
            int cost;
            if (!ValidId(channel, 32, true) || !command.TryInteger("cost", out cost) || cost < 1 || cost > MaximumCost ||
                (operation == "create" ? title.Length < 1 || title.Length > 45 : operation != "update" || !ValidId(id, 96, false)) ||
                !TryReadLimit(command, "cooldownSeconds", MinimumCooldownSeconds, MaximumCooldownSeconds, out cooldownSeconds) ||
                !TryReadLimit(command, "maxPerStream", 1, int.MaxValue, out maxPerStream) ||
                !TryReadLimit(command, "maxPerUserPerStream", 1, int.MaxValue, out maxPerUserPerStream))
                return Error("invalid_request");

            lock (gate)
            {
                if (broadcasterId.Length == 0) return Error("not_connected");
                if (broadcasterId != channel) return Error("wrong_broadcaster");
                if (!canManage) return Error("manage_permission_required");
                if (queued != null || inFlight != null && inFlight.IsUserCommand) return Error("busy");
                Reward reward;
                if (operation == "update" && (!rewards.TryGetValue(id, out reward) || !reward.Manageable))
                    return Error("reward_not_manageable");

                var request = new Request(operation, channel, clock().AddSeconds(RequestSeconds))
                {
                    RewardId = id, Title = title, Cost = cost, CooldownSeconds = cooldownSeconds,
                    MaxPerStream = maxPerStream, MaxPerUserPerStream = maxPerUserPerStream
                };
                queued = request;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (request.Result == null)
                {
                    var remaining = RequestSeconds * 1000 - (int)timer.ElapsedMilliseconds;
                    if (remaining <= 0) break;
                    Monitor.Wait(gate, remaining, false);
                }
                if (request.Result != null) return request.Result;
                if (queued == request) queued = null;
                return Error("twitch_unavailable");
            }
        }

        internal string TakeCommand()
        {
            lock (gate)
            {
                var now = clock();
                if (inFlight != null && now >= inFlight.Deadline)
                    FinishRequest(false, "twitch_unavailable", null, now);
                if (inFlight != null || broadcasterId.Length == 0) return null;
                if (queued != null)
                {
                    inFlight = queued;
                    queued = null;
                    inFlight.Deadline = now.AddSeconds(RequestSeconds);
                }
                else
                {
                    var hasDesired = false;
                    foreach (var item in desired.Values)
                    {
                        if (item.BroadcasterId != broadcasterId) continue;
                        hasDesired = true;
                        Reward reward;
                        if (catalogLoaded && canManage && now >= retrySyncAt && rewards.TryGetValue(item.Id, out reward) &&
                            reward.Manageable && reward.Enabled != item.Enabled)
                        {
                            inFlight = new Request("sync", broadcasterId, now.AddSeconds(RequestSeconds))
                                { RewardId = item.Id, Enabled = item.Enabled };
                            break;
                        }
                        if (item.Removed && catalogLoaded &&
                            (!rewards.TryGetValue(item.Id, out reward) || !reward.Enabled || !reward.Manageable && canManage))
                        {
                            desired.Remove(item.Key);
                            break;
                        }
                    }
                    if (inFlight == null && (hasDesired || catalogRequested) && now >= refreshAt)
                    {
                        inFlight = new Request("list", broadcasterId, now.AddSeconds(RequestSeconds));
                        catalogRequested = false;
                    }
                }
                return inFlight == null ? null : inFlight.ToJson();
            }
        }

        internal void AcceptResult(CreatorToolsJsonValue message)
        {
            lock (gate)
            {
                if (inFlight == null || message.Integer("protocolVersion", 0) != 1 ||
                    message.String("requestId") != inFlight.Id || message.String("operation") != inFlight.Operation) return;
                var error = message.String("error");
                bool ok;
                if (!message.TryBoolean("ok", out ok) || ok && message.String("broadcasterId") != broadcasterId)
                {
                    FinishRequest(false, "twitch_unavailable", null, clock());
                    return;
                }
                var payload = message.String(inFlight.Operation == "list" ? "rewardsJson" : "rewardJson");
                if (!ok && !ValidId(error, 64, false)) error = "twitch_unavailable";
                FinishRequest(ok, error, payload, clock());
            }
        }

        private void FinishRequest(bool ok, string error, string payload, DateTime now)
        {
            var request = inFlight;
            inFlight = null;
            CreatorToolsJsonValue value = null;
            if (ok && !CreatorToolsJsonParser.TryParse(payload, out value, 49152, 256, 51))
            {
                ok = false;
                error = "twitch_unavailable";
            }
            if (request.Operation == "list")
            {
                if (ok && value.ArrayValue != null && value.ArrayValue.Count <= 50)
                {
                    var list = new Dictionary<string, Reward>(StringComparer.Ordinal);
                    foreach (var entry in value.ArrayValue)
                    {
                        var reward = Reward.Read(entry);
                        if (reward == null || list.ContainsKey(reward.Id)) { ok = false; break; }
                        list.Add(reward.Id, reward);
                    }
                    if (ok)
                    {
                        rewards.Clear();
                        foreach (var entry in list) rewards.Add(entry.Key, entry.Value);
                        catalogLoaded = true;
                    }
                }
                else ok = false;
                catalogError = ok ? string.Empty : error.Length == 0 ? "twitch_unavailable" : error;
                refreshAt = now.AddSeconds(ok ? 25 : 10);
                return;
            }

            var updated = ok ? Reward.Read(value) : null;
            if (updated != null && (request.Operation == "create" || updated.Id == request.RewardId))
            {
                rewards[updated.Id] = updated;
                if (request.Operation == "sync") syncError = string.Empty;
                var response = new StringBuilder("{\"ok\":true,\"reward\":");
                updated.AppendJson(response, broadcasterId);
                Complete(request, response.Append('}').ToString());
            }
            else
            {
                if (error.Length == 0) error = "twitch_unavailable";
                if (request.Operation == "sync")
                {
                    syncError = error;
                    retrySyncAt = now.AddSeconds(10);
                    if (error == "reward_not_found" || error == "reward_not_manageable") refreshAt = DateTime.MinValue;
                }
                Complete(request, Error(error));
            }
        }

        private bool IsPending(DesiredReward item)
        {
            if (broadcasterId.Length != 0 && item.BroadcasterId != broadcasterId) return false;
            Reward reward;
            return !rewards.TryGetValue(item.Id, out reward) || reward.Enabled != item.Enabled;
        }

        private void Complete(Request request, string result)
        {
            if (request == null || !request.IsUserCommand) return;
            request.Result = result;
            Monitor.PulseAll(gate);
        }

        internal static string Error(string code)
        {
            var json = new StringBuilder("{\"ok\":false");
            AppendString(json, "error", code);
            return json.Append('}').ToString();
        }

        private static bool ValidId(string value, int limit, bool decimalOnly)
        {
            if (string.IsNullOrEmpty(value) || value.Length > limit) return false;
            foreach (var character in value)
            {
                if (character >= '0' && character <= '9') continue;
                if (decimalOnly || !(character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z' || character == '-' || character == '_')) return false;
            }
            return true;
        }

        private static bool ValidLogin(string value)
        {
            return ValidId(value, 25, false) && value.IndexOf('-') < 0;
        }

        private static bool TryReadLimit(CreatorToolsJsonValue json, string key, int minimum, int maximum, out int? limit)
        {
            limit = null;
            if (json.Property(key) == null) return true;
            int value;
            if (!json.TryInteger(key, out value) || value != 0 && (value < minimum || value > maximum)) return false;
            limit = value;
            return true;
        }

        private static void AppendString(StringBuilder json, string key, string value)
        {
            json.Append(",\"").Append(key).Append("\":\"");
            CreatorToolsJson.AppendEscaped(json, value);
            json.Append('"');
        }

        private sealed class DesiredReward
        {
            internal string Id;
            internal string BroadcasterId;
            internal bool Enabled;
            internal bool Removed;
            internal string Key { get { return BroadcasterId + ":" + Id; } }
        }

        private sealed class Reward
        {
            internal string Id;
            internal string Title;
            internal int Cost;
            internal bool Enabled;
            internal bool Manageable;
            internal int CooldownSeconds;
            internal int MaxPerStream;
            internal int MaxPerUserPerStream;

            internal static Reward Read(CreatorToolsJsonValue value)
            {
                var id = value.String("id");
                var title = value.String("title");
                int cost;
                bool enabled, manageable;
                int? cooldownSeconds, maxPerStream, maxPerUserPerStream;
                return ValidId(id, 96, false) && title.Length > 0 && title.Length <= 90 &&
                    value.TryInteger("cost", out cost) && cost >= 1 && cost <= MaximumCost &&
                    value.TryBoolean("enabled", out enabled) && value.TryBoolean("manageable", out manageable) &&
                    TryReadLimit(value, "cooldownSeconds", 1, MaximumCooldownSeconds, out cooldownSeconds) &&
                    TryReadLimit(value, "maxPerStream", 1, int.MaxValue, out maxPerStream) &&
                    TryReadLimit(value, "maxPerUserPerStream", 1, int.MaxValue, out maxPerUserPerStream)
                    ? new Reward {
                        Id = id, Title = title, Cost = cost, Enabled = enabled, Manageable = manageable,
                        CooldownSeconds = cooldownSeconds.GetValueOrDefault(), MaxPerStream = maxPerStream.GetValueOrDefault(),
                        MaxPerUserPerStream = maxPerUserPerStream.GetValueOrDefault()
                    } : null;
            }

            internal void AppendJson(StringBuilder json, string channel)
            {
                json.Append("{\"cost\":").Append(Cost.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"cooldownSeconds\":").Append(CooldownSeconds.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"maxPerStream\":").Append(MaxPerStream.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"maxPerUserPerStream\":").Append(MaxPerUserPerStream.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"enabled\":").Append(Enabled ? "true" : "false")
                    .Append(",\"manageable\":").Append(Manageable ? "true" : "false");
                AppendString(json, "id", Id);
                AppendString(json, "title", Title);
                AppendString(json, "broadcasterId", channel);
                json.Append('}');
            }
        }

        private sealed class Request
        {
            internal readonly string Id = Guid.NewGuid().ToString("N");
            internal readonly string Operation;
            internal readonly string BroadcasterId;
            internal DateTime Deadline;
            internal string RewardId;
            internal string Title;
            internal int Cost;
            internal int? CooldownSeconds;
            internal int? MaxPerStream;
            internal int? MaxPerUserPerStream;
            internal bool Enabled;
            internal string Result;
            internal bool IsUserCommand { get { return Operation == "create" || Operation == "update"; } }

            internal Request(string operation, string broadcasterId, DateTime deadline)
            {
                Operation = operation;
                BroadcasterId = broadcasterId;
                Deadline = deadline;
            }

            internal string ToJson()
            {
                var json = new StringBuilder("{\"kind\":\"twitch_reward_command\"");
                AppendString(json, "operation", Operation);
                AppendString(json, "requestId", Id);
                AppendString(json, "broadcasterId", BroadcasterId);
                if (Operation == "create") AppendString(json, "title", Title);
                if (Operation == "update" || Operation == "sync") AppendString(json, "rewardId", RewardId);
                if (IsUserCommand)
                {
                    json.Append(",\"cost\":").Append(Cost.ToString(CultureInfo.InvariantCulture));
                    AppendLimit(json, "cooldownSeconds", CooldownSeconds);
                    AppendLimit(json, "maxPerStream", MaxPerStream);
                    AppendLimit(json, "maxPerUserPerStream", MaxPerUserPerStream);
                }
                if (Operation == "sync") json.Append(",\"enabled\":").Append(Enabled ? "true" : "false");
                return json.Append('}').ToString();
            }

            private static void AppendLimit(StringBuilder json, string name, int? value)
            {
                if (value.HasValue)
                    json.Append(",\"").Append(name).Append("\":").Append(value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
