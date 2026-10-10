using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class YouTubeConnectionBridge
    {
        private readonly object gate = new object();
        private string state = "{\"ready\":false,\"status\":\"disconnected\",\"authorized\":false,\"messageCode\":\"companion_starting\"}";
        private long nextRevision, minimumRevision;
        private string pendingCommand, sentCommand;
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
                acceptsEvents = false; minimumRevision = ++nextRevision;
                pendingCommand = "youtube:" + action + ":" + minimumRevision.ToString(CultureInfo.InvariantCulture);
                sentCommand = null;
            }
            return string.Empty;
        }
        internal string TakeCommand()
        {
            lock (gate)
            {
                if (pendingCommand == null || pendingCommand == sentCommand) return null;
                sentCommand = pendingCommand; return pendingCommand;
            }
        }
        internal void Restarting()
        {
            lock (gate)
            {
                acceptsEvents = false; sentCommand = null;
                if (pendingCommand == null) minimumRevision = 0;
                state = "{\"ready\":false,\"status\":\"reconnecting\",\"authorized\":false,\"messageCode\":\"companion_starting\"}";
            }
        }
        internal bool AcceptStatus(Dictionary<string, string> values)
        {
            var status = CreatorToolsFlatJson.Value(values, "state");
            long revision;
            if (CreatorToolsFlatJson.Integer(values, "protocolVersion", 0, 0, 100) != 1 ||
                CreatorToolsFlatJson.Value(values, "kind") != "status" ||
                (status != "disconnected" && status != "connecting" && status != "connected" && status != "reconnecting" && status != "error") ||
                !long.TryParse(CreatorToolsFlatJson.Value(values, "controlRevision"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out revision)) return false;
            lock (gate)
            {
                if (revision < minimumRevision) return false;
                var authorized = CreatorToolsFlatJson.Boolean(values, "authorized");
                acceptsEvents = authorized && status == "connected";
                if (revision == minimumRevision) pendingCommand = null;
                var builder = new StringBuilder("{\"ready\":true,\"authorized\":").Append(authorized ? "true" : "false");
                foreach (var field in new[] { "state", "account", "messageCode", "verificationUri", "expiresAt" })
                {
                    var value = CreatorToolsFlatJson.Value(values, field);
                    if (value.Length > (field == "verificationUri" ? 2048 : 160)) value = string.Empty;
                    if (field == "verificationUri" && !IsAuthorizationUri(value)) value = string.Empty;
                    builder.Append(",\"").Append(field == "state" ? "status" : field).Append("\":\"");
                    CreatorToolsJson.AppendEscaped(builder, value); builder.Append('"');
                }
                state = builder.Append('}').ToString(); return true;
            }
        }
        internal bool AcceptsEvents { get { lock (gate) return acceptsEvents; } }
        internal static bool IsAuthorizationUri(string value)
        {
            Uri uri;
            return value.Length == 0 || Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                uri.Scheme == "https" && uri.Host == "accounts.google.com" && uri.Port == 443 &&
                uri.AbsolutePath == "/o/oauth2/v2/auth" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
        }
    }
}
