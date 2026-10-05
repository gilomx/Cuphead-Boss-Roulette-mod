using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class ChatChoice
    {
        internal int Id;
        internal string Name = string.Empty;
        internal string Image = string.Empty;
        internal string Kind = "both";
        internal bool None;
        internal bool Plane;
        internal bool RandomWeapons;
    }

    // Immutable catalog copies are prepared by Unity; voting and transitions
    // operate only on these values and remain available without browser focus.
    internal sealed class ChatChoosesCatalog
    {
        internal readonly Dictionary<string, List<ChatChoice>> Pools =
            new Dictionary<string, List<ChatChoice>>();
    }

    internal sealed class CreatorToolsChatChoosesController
    {
        private static readonly string[] Stages =
            { "boss", "weapon1", "weapon2", "super", "charm", "modifier" };
        private readonly object stateLock = new object();
        private readonly CreatorToolsLiveEventsCoordinator coordinator;
        private readonly Random random;
        private readonly Func<DateTime> clock;
        private readonly Dictionary<string, int> votes = new Dictionary<string, int>();
        private readonly ChatChoice[] selected = new ChatChoice[6];
        private ChatChoosesCatalog catalog;
        private CreatorToolsLiveEventLease lease;
        private List<ChatChoice> choices = new List<ChatChoice>();
        private int[] counts = new int[0];
        private string phase = "off";
        private string feedback = "ready";
        private string outcome = string.Empty;
        private bool error;
        private bool withChallenge;
        private bool mapAvailable;
        private bool plane;
        private int stage;
        private int session;
        private int round;
        private int revision;
        private int winner = -1;
        private DateTime openedAt;
        private DateTime until;

        internal CreatorToolsChatChoosesController(
            CreatorToolsLiveEventsCoordinator coordinator, bool withChallenge,
            Random random = null, Func<DateTime> clock = null)
        {
            this.coordinator = coordinator;
            this.withChallenge = withChallenge;
            this.random = random ?? new Random();
            this.clock = clock ?? delegate { return DateTime.UtcNow; };
        }

        internal bool WithChallenge { get { lock (stateLock) return withChallenge; } }
        internal bool Reserved { get { lock (stateLock) return lease != null; } }
        internal bool ShowingResult
        {
            get { lock (stateLock) return phase == "result" || phase == "countdown" || phase == "waiting_map"; }
        }
        internal bool NeedsCatalog { get { lock (stateLock) return lease == null; } }
        internal ChatChoice[] SelectedOptions { get { lock (stateLock) return (ChatChoice[])selected.Clone(); } }
        internal int CountdownSeconds { get { lock (stateLock) return phase == "countdown" ? Math.Max(0, (int)Math.Ceiling((until - clock()).TotalSeconds)) : 0; } }

        internal void SetCatalog(ChatChoosesCatalog value, bool available)
        {
            lock (stateLock)
            {
                if (lease == null && value != null) catalog = value;
                if (mapAvailable != available) { mapAvailable = available; revision++; }
            }
        }

        internal bool Command(string query)
        {
            var values = ParseQuery(query);
            string action;
            values.TryGetValue("operation", out action);
            lock (stateLock)
            {
                TickLocked();
                if (action == "stop" || action == "finish")
                {
                    ReleaseLocked(); phase = "off"; feedback = "ready"; error = false;
                    votes.Clear(); choices.Clear(); counts = new int[0];
                    Array.Clear(selected, 0, selected.Length); revision++;
                    return true;
                }
                if (action == "save" || action == "start")
                {
                    if (lease != null) return RejectLocked("already_running");
                    string mode;
                    if (!values.TryGetValue("mode", out mode) || (mode != "with" && mode != "without"))
                        return RejectLocked("invalid_action");
                    if (action == "save")
                    {
                        withChallenge = mode == "with"; feedback = "ready"; error = false; revision++;
                        return true;
                    }
                    if (!mapAvailable || catalog == null) return RejectLocked("map_required");
                    CreatorToolsLiveEventLease acquired;
                    string blocker;
                    if (!coordinator.TryAcquire(CreatorToolsLiveEventIds.ChatChooses, out acquired, out blocker))
                        return RejectLocked("blocked_by_live_event");
                    lease = acquired; withChallenge = mode == "with"; session++; round = 0;
                    plane = false; stage = 0; Array.Clear(selected, 0, selected.Length);
                    feedback = "ready"; error = false; OpenRoundLocked();
                    return true;
                }
                int requestSession, requestRound;
                string rawSession, rawRound;
                values.TryGetValue("sessionId", out rawSession); values.TryGetValue("round", out rawRound);
                if (action != "next" || phase != "voting" ||
                    !int.TryParse(rawSession, out requestSession) || requestSession != session ||
                    !int.TryParse(rawRound, out requestRound) || requestRound != round)
                    return RejectLocked("stale_round");
                var maximum = 0;
                for (var i = 0; i < counts.Length; i++) maximum = Math.Max(maximum, counts[i]);
                var leaders = new List<int>();
                for (var i = 0; i < counts.Length; i++) if (counts[i] == maximum) leaders.Add(i);
                winner = leaders[random.Next(leaders.Count)];
                outcome = maximum == 0 ? "no_votes" : leaders.Count > 1 ? "tie" : "most_votes";
                selected[stage] = choices[winner];
                if (stage == 0) plane = selected[0].Plane;
                phase = "reveal"; until = clock().AddSeconds(1.5);
                feedback = "ready"; error = false; revision++;
                return true;
            }
        }

        internal string Observe(CreatorToolsStreamEvent entry)
        {
            if (entry == null || entry.Type != "chat") return string.Empty;
            var text = (entry.ChatText ?? string.Empty).Trim();
            if (text.Length != 1 || text[0] < '1' || text[0] > '6') return string.Empty;
            lock (stateLock)
            {
                TickLocked();
                if (phase != "voting") return string.Empty;
                DateTime received;
                // Old queued messages must never leak into a subsequent round.
                if (!DateTime.TryParse(entry.ReceivedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out received) ||
                    received < openedAt) return string.Empty;
                var option = text[0] - '1';
                if (option >= choices.Count) return string.Empty;
                var identity = !string.IsNullOrEmpty(entry.UserId) ? "id:" + entry.UserId :
                    !string.IsNullOrEmpty(entry.UserName) ? "user:" + entry.UserName.ToLowerInvariant() : string.Empty;
                if (identity.Length == 0) return string.Empty;
                identity = entry.Platform + ":" + identity;
                int previous;
                if (votes.TryGetValue(identity, out previous))
                {
                    if (previous == option) return "chat_vote_unchanged";
                    counts[previous]--;
                }
                else if (votes.Count >= 100000) return string.Empty;
                votes[identity] = option; counts[option]++; revision++;
                return "chat_vote_counted";
            }
        }

        internal void Tick() { lock (stateLock) TickLocked(); }

        private void TickLocked()
        {
            var now = clock();
            if (phase == "reveal" && now >= until)
            {
                stage = NextStageLocked();
                if (stage < Stages.Length) OpenRoundLocked();
                else { phase = "result"; until = now.AddSeconds(5); revision++; }
            }
            else if ((phase == "result" && now >= until) || phase == "waiting_map")
            {
                if (!mapAvailable)
                {
                    if (phase != "waiting_map") { phase = "waiting_map"; revision++; }
                }
                else { phase = "countdown"; until = now.AddSeconds(3); revision++; }
            }
            else if (phase == "countdown" && !mapAvailable)
            { phase = "waiting_map"; revision++; }
        }

        private int NextStageLocked()
        {
            if (stage == 0 && plane) return 4;
            if (stage == 4 && !withChallenge) return 6;
            return stage + 1;
        }

        private void OpenRoundLocked()
        {
            List<ChatChoice> pool;
            if (!catalog.Pools.TryGetValue(Stages[stage], out pool)) pool = new List<ChatChoice>();
            choices = new List<ChatChoice>();
            foreach (var option in pool)
            {
                if (stage == 1 && option.None) continue;
                if (stage == 2 && selected[1] != null && option.Id == selected[1].Id) continue;
                if (stage == 4 && !plane && option.RandomWeapons) continue;
                if (stage == 5 && option.Kind != "both" && option.Kind != (plane ? "plane" : "ground")) continue;
                choices.Add(option);
            }
            for (var i = choices.Count - 1; i > 0; i--)
            { var j = random.Next(i + 1); var item = choices[i]; choices[i] = choices[j]; choices[j] = item; }
            if (choices.Count > 6) choices.RemoveRange(6, choices.Count - 6);
            if (choices.Count == 0)
            { ReleaseLocked(); phase = "off"; RejectLocked("no_options"); return; }
            counts = new int[choices.Count]; votes.Clear(); winner = -1; outcome = string.Empty;
            openedAt = clock(); round++; phase = "voting"; feedback = "ready"; error = false; revision++;
        }

        internal bool TryBeginLoad(out int[] result)
        {
            lock (stateLock)
            {
                TickLocked(); result = null;
                if (phase != "countdown" || !mapAvailable || clock() < until || !coordinator.IsOwner(lease)) return false;
                result = new int[6];
                for (var i = 0; i < result.Length; i++) result[i] = selected[i] == null ? -1 : selected[i].Id;
                phase = "loading"; revision++; return true;
            }
        }

        internal void Loaded(bool success)
        {
            lock (stateLock)
            {
                if (phase != "loading") return;
                if (success) phase = "active";
                else { phase = "completed"; ReleaseLocked(); feedback = "load_failed"; error = true; }
                revision++;
            }
        }

        internal void ReturnedToMap()
        {
            lock (stateLock)
            {
                if (phase != "active") return;
                phase = "completed"; ReleaseLocked(); revision++;
            }
        }

        private void ReleaseLocked()
        {
            if (lease == null) return;
            coordinator.BeginStopping(lease); coordinator.CompleteRelease(lease); lease = null;
        }
        private bool RejectLocked(string code) { feedback = code; error = true; revision++; return false; }

        internal string Snapshot()
        {
            lock (stateLock)
            {
                TickLocked();
                var live = coordinator.Snapshot;
                var builder = new StringBuilder(4096);
                builder.Append("{\"ready\":").Append(catalog != null ? "true" : "false")
                    .Append(",\"schemaVersion\":1,\"revision\":").Append(revision)
                    .Append(",\"sessionId\":").Append(session).Append(",\"round\":").Append(round)
                    .Append(",\"phase\":"); AppendString(builder, phase);
                builder.Append(",\"stage\":"); AppendString(builder, stage < 6 ? Stages[stage] : "result");
                builder.Append(",\"nextStage\":"); AppendString(builder, NextStageLocked() < 6 ? Stages[NextStageLocked()] : "result");
                builder.Append(",\"withChallenge\":").Append(withChallenge ? "true" : "false")
                    .Append(",\"plane\":").Append(plane ? "true" : "false")
                    .Append(",\"mapAvailable\":").Append(mapAvailable ? "true" : "false")
                    .Append(",\"remainingSeconds\":").Append(phase == "reveal" || phase == "result" || phase == "countdown" ?
                        Math.Max(0, (int)Math.Ceiling((until - clock()).TotalSeconds)) : 0)
                    .Append(",\"totalVotes\":").Append(votes.Count)
                    .Append(",\"winnerNumber\":").Append(winner + 1)
                    .Append(",\"outcome\":"); AppendString(builder, outcome);
                builder.Append(",\"blockedByLiveEvent\":"); AppendString(builder, live.ActiveEvent == CreatorToolsLiveEventIds.ChatChooses ? "" : live.ActiveEvent);
                builder.Append(",\"feedback\":"); AppendString(builder, feedback);
                builder.Append(",\"error\":").Append(error ? "true" : "false").Append(",\"options\":[");
                for (var i = 0; i < choices.Count; i++)
                {
                    if (i > 0) builder.Append(','); AppendChoice(builder, choices[i]);
                    builder.Length--; builder.Append(",\"number\":").Append(i + 1).Append(",\"votes\":").Append(counts[i]).Append('}');
                }
                builder.Append("],\"selected\":{");
                var first = true;
                for (var i = 0; i < 6; i++)
                {
                    if (selected[i] == null) continue;
                    if (!first) builder.Append(','); first = false;
                    AppendString(builder, Stages[i]); builder.Append(':'); AppendChoice(builder, selected[i]);
                }
                return builder.Append("}}").ToString();
            }
        }

        private static void AppendChoice(StringBuilder builder, ChatChoice item)
        {
            builder.Append("{\"id\":").Append(item.Id).Append(",\"name\":"); AppendString(builder, item.Name);
            builder.Append(",\"image\":"); AppendString(builder, item.Image);
            builder.Append(",\"none\":").Append(item.None ? "true" : "false").Append('}');
        }
        private static void AppendString(StringBuilder builder, string value)
        { builder.Append('"'); CreatorToolsJson.AppendEscaped(builder, value); builder.Append('"'); }
        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in (query ?? "").Split('&'))
            {
                var split = pair.IndexOf('='); if (split < 0) continue;
                try { result[Uri.UnescapeDataString(pair.Substring(0, split))] = Uri.UnescapeDataString(pair.Substring(split + 1).Replace('+', ' ')); }
                catch { }
            }
            return result;
        }
    }
}
