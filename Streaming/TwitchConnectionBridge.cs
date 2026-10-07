using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    // Only public account/authorization state crosses into legacy Mono or the browser.
    internal sealed class TwitchConnectionBridge
    {
        private readonly object gate = new object();
        private string state = "{\"ready\":false,\"status\":\"disconnected\",\"authorized\":false," +
            "\"account\":\"\",\"messageCode\":\"companion_starting\",\"userCode\":\"\",\"verificationUri\":\"\",\"expiresAt\":\"\"}";
        private long nextRevision;
        private long minimumRevision;
        private string pendingCommand;
        private string sentCommand;
        private bool acceptsEvents;

        internal string GetState()
        {
            lock (gate) return state.Substring(0, state.Length - 1) +
                ",\"commandPending\":" + (pendingCommand == null ? "false" : "true") + "}";
        }

        internal string Command(string action)
        {
            if (action != "connect" && action != "disconnect" && action != "cancel") return "invalid_command";
            lock (gate)
            {
                acceptsEvents = false;
                minimumRevision = ++nextRevision;
                pendingCommand = "twitch:" + action + ":" + minimumRevision.ToString(CultureInfo.InvariantCulture);
                sentCommand = null;
            }
            return string.Empty;
        }

        internal string TakeCommand()
        {
            lock (gate)
            {
                if (pendingCommand == null || pendingCommand == sentCommand) return null;
                sentCommand = pendingCommand;
                return pendingCommand;
            }
        }

        internal void Restarting()
        {
            lock (gate)
            {
                acceptsEvents = false; sentCommand = null;
                if (pendingCommand == null) minimumRevision = 0;
                state = "{\"ready\":false,\"status\":\"reconnecting\",\"authorized\":false," +
                    "\"account\":\"\",\"messageCode\":\"companion_starting\",\"userCode\":\"\",\"verificationUri\":\"\",\"expiresAt\":\"\"}";
            }
        }

        internal bool AcceptStatus(Dictionary<string, string> values)
        {
            var status = CreatorToolsFlatJson.Value(values, "state");
            if (CreatorToolsFlatJson.Integer(values, "protocolVersion", 0, 0, 100) != 1 ||
                CreatorToolsFlatJson.Value(values, "kind") != "status" ||
                (status != "disconnected" && status != "connecting" && status != "connected" &&
                 status != "reconnecting" && status != "error")) return false;
            long revision;
            if (!long.TryParse(CreatorToolsFlatJson.Value(values, "controlRevision"),
                NumberStyles.None, CultureInfo.InvariantCulture, out revision)) return false;
            lock (gate)
            {
                if (revision < minimumRevision) return false;
                acceptsEvents = CreatorToolsFlatJson.Boolean(values, "authorized");
                if (revision == minimumRevision) pendingCommand = null;
                var builder = new StringBuilder("{\"ready\":true,\"authorized\":")
                    .Append(acceptsEvents ? "true" : "false");
                var fields = new[] { "state", "account", "messageCode", "userCode", "verificationUri", "expiresAt" };
                foreach (var field in fields)
                {
                    var value = CreatorToolsFlatJson.Value(values, field);
                    if (value.Length > (field == "verificationUri" ? 256 : 80)) value = string.Empty;
                    if (field == "verificationUri" && !IsVerificationUri(value)) value = string.Empty;
                    builder.Append(",\"").Append(field == "state" ? "status" : field).Append("\":\"");
                    CreatorToolsJson.AppendEscaped(builder, value); builder.Append('"');
                }
                state = builder.Append('}').ToString();
                return true;
            }
        }

        internal bool AcceptsEvents { get { lock (gate) return acceptsEvents; } }

        internal static bool IsVerificationUri(string value)
        {
            Uri uri;
            return value.Length == 0 || Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                uri.Scheme == "https" && uri.Host == "www.twitch.tv" && uri.Port == 443 &&
                uri.AbsolutePath == "/activate" && uri.UserInfo.Length == 0;
        }
    }
}
