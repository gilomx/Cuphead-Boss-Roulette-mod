using System;
using System.Collections.Generic;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsInteractionController : IDisposable
    {
        internal const string GreenZeppelinId = "hilda_green_zeppelin";
        internal const string PurpleZeppelinId = "hilda_purple_zeppelin";

        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly ZeppelinInteractionExecutor zeppelins;
        private readonly CreatorToolsInteractionQueue interactionQueue =
            new CreatorToolsInteractionQueue();
        private string lastState;
        private string lastItem = string.Empty;
        private string feedback = "ready";
        private bool feedbackError;
        private int revision;

        internal CreatorToolsInteractionController(
            UnityEngine.MonoBehaviour coroutineHost,
            Func<bool> canPreloadNativeAssets,
            Func<bool> canSpawnInteraction,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            zeppelins = new ZeppelinInteractionExecutor(
                coroutineHost,
                canPreloadNativeAssets,
                canSpawnInteraction,
                logInfo,
                logWarning);
        }

        internal void Update(CreatorToolsServer server)
        {
            zeppelins.Update();
            interactionQueue.RemoveFinished();
            if (server == null || !server.IsRunning)
                return;

            string query;
            while (server.TryTakeInteractionCommand(out query))
                Enqueue(ParseQuery(query));

            var available = zeppelins.Available;
            if (available)
                ProcessQueue();
            var state = BuildState(available);
            if (state == lastState)
                return;
            lastState = state;
            server.SetInteractionsState(state);
        }

        private void Enqueue(Dictionary<string, string> values)
        {
            string item;
            if (!values.TryGetValue("item", out item))
            {
                SetFeedback("unknown_item", true);
                return;
            }

            NativeZeppelinVariant variant;
            if (!TryResolveVariant(item, out variant))
            {
                lastItem = item ?? string.Empty;
                SetFeedback("unknown_item", true);
                return;
            }
            lastItem = item;

            string donor;
            values.TryGetValue("donor", out donor);
            donor = NormalizeDonor(donor);

            var quantity = ParseQuantity(values);
            var added = interactionQueue.Enqueue(
                item, variant, donor, quantity);
            if (added > 0)
            {
                SetFeedback("queued", false);
                if (logInfo != null)
                    logInfo(
                        added + " canje(s) de mini zepelin " +
                        variant.ToString().ToLowerInvariant() +
                        " agregados a la cola para " + donor + ".");
                return;
            }

            SetFeedback("queue_full", true);
        }

        private void ProcessQueue()
        {
            if (interactionQueue.ActiveCount >=
                CreatorToolsInteractionQueue.MaximumActive)
                return;

            var entry = interactionQueue.Peek();
            if (entry == null)
                return;

            FlyingBlimpLevelEnemy spawned;
            string feedbackCode;
            string error;
            if (zeppelins.TrySpawn(
                entry.Variant,
                entry.Donor,
                out spawned,
                out feedbackCode,
                out error))
            {
                interactionQueue.ActivateFirst(spawned);
                if (logInfo != null)
                    logInfo(
                        "Ejecutando canje #" + entry.Id + " de " +
                        entry.Donor + ".");
                return;
            }

            if (feedbackCode == "native_assets_loading" ||
                feedbackCode == "requires_gameplay_level")
                return;

            interactionQueue.RejectFirst();
            lastItem = entry.Item;
            SetFeedback(feedbackCode, true);
            if (!string.IsNullOrEmpty(error) && logWarning != null)
                logWarning(
                    entry.Variant + " zeppelin interaction failed: " +
                    error);
        }

        private void SetFeedback(string value, bool error)
        {
            feedback = value;
            feedbackError = error;
            revision++;
            lastState = null;
        }

        private string BuildState(bool available)
        {
            var builder = new StringBuilder(4096);
            builder.Append("{\"ready\":true,\"available\":")
                .Append(available ? "true" : "false")
                .Append(",\"item\":\"")
                .Append(GreenZeppelinId)
                .Append("\",\"items\":[\"")
                .Append(GreenZeppelinId)
                .Append("\",\"")
                .Append(PurpleZeppelinId)
                .Append("\"],\"lastItem\":\"");
            AppendJson(builder, lastItem);
            builder.Append("\",\"feedback\":\"");
            AppendJson(builder, feedback);
            builder.Append("\",\"error\":")
                .Append(feedbackError ? "true" : "false")
                .Append(",\"revision\":").Append(revision)
                .Append(",\"queueCount\":")
                .Append(interactionQueue.Count)
                .Append(",\"activeCount\":")
                .Append(interactionQueue.ActiveCount)
                .Append(",\"maxActive\":")
                .Append(CreatorToolsInteractionQueue.MaximumActive)
                .Append(",\"maxBatch\":")
                .Append(CreatorToolsInteractionQueue.MaximumBatchSize)
                .Append(",\"queue\":");
            interactionQueue.AppendJson(builder);
            builder.Append('}');
            return builder.ToString();
        }

        private static bool TryResolveVariant(
            string item,
            out NativeZeppelinVariant variant)
        {
            if (item == GreenZeppelinId)
            {
                variant = NativeZeppelinVariant.Green;
                return true;
            }
            if (item == PurpleZeppelinId)
            {
                variant = NativeZeppelinVariant.Purple;
                return true;
            }
            variant = NativeZeppelinVariant.Purple;
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
            return Math.Max(
                1,
                Math.Min(
                    CreatorToolsInteractionQueue.MaximumBatchSize,
                    quantity));
        }

        private static string NormalizeDonor(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "DONADOR";
            value = value.Trim();
            if (value.Length > 32)
                value = value.Substring(0, 32);
            return value;
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
                    values[Uri.UnescapeDataString(
                        key.Replace('+', ' '))] =
                        Uri.UnescapeDataString(value.Replace('+', ' '));
                }
                catch { }
            }
            return values;
        }

        private static void AppendJson(StringBuilder builder, string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\');
                builder.Append(character);
            }
        }

        public void Dispose()
        {
            interactionQueue.Dispose();
            zeppelins.Dispose();
            lastState = null;
        }
    }
}
