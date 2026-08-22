using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsInteractionController : IDisposable
    {
        internal const string GreenZeppelinId = "hilda_green_zeppelin";
        internal const string PurpleZeppelinId = "hilda_purple_zeppelin";

        private const float RandomTestMinimumIntervalSeconds = 1.25f;
        private const float RandomTestMaximumIntervalSeconds = 3.25f;
        private const float MinimumDispatchSeparationSeconds = 0.35f;
        private static readonly string[] RandomTestDonors =
        {
            "Claudia",
            "YeiAndPelos",
            "Yerrisito",
            "Malono",
            "Suches",
            "Elver_hijas"
        };

        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly ZeppelinInteractionExecutor zeppelins;
        private readonly Func<int> getMaximumActive;
        private readonly Action<int> setMaximumActive;
        private readonly CreatorToolsInteractionQueue interactionQueue =
            new CreatorToolsInteractionQueue();
        private string lastState;
        private string lastItem = string.Empty;
        private string feedback = "ready";
        private bool feedbackError;
        private bool randomTestEnabled;
        private int randomTestRevision;
        private float nextRandomTestAt = -1f;
        private float nextDispatchAt = -1f;
        private int revision;

        internal CreatorToolsInteractionController(
            UnityEngine.MonoBehaviour coroutineHost,
            Func<bool> canPreloadNativeAssets,
            Func<bool> canSpawnInteraction,
            Func<int> getMaximumActive,
            Action<int> setMaximumActive,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            this.getMaximumActive = getMaximumActive;
            this.setMaximumActive = setMaximumActive;
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
                ProcessCommand(ParseQuery(query));

            var available = zeppelins.Available;
            UpdateRandomTest(available);
            if (available)
                ProcessQueue();
            else
                nextDispatchAt = -1f;
            var state = BuildState(available);
            if (state == lastState)
                return;
            lastState = state;
            server.SetInteractionsState(state);
        }

        internal void InvalidateState()
        {
            lastState = null;
        }

        internal void EndGameplayLevel()
        {
            zeppelins.ClearActiveSpawns();
            interactionQueue.ClearActive();
            SuspendGameplayLevel();
        }

        internal void SuspendGameplayLevel()
        {
            nextRandomTestAt = -1f;
            nextDispatchAt = -1f;
            lastState = null;
        }

        private void ProcessCommand(Dictionary<string, string> values)
        {
            if (values.ContainsKey("maxActive"))
            {
                SetMaximumActive(values);
                return;
            }
            if (values.ContainsKey("randomTestEnabled"))
            {
                SetRandomTestEnabled(values);
                return;
            }
            Enqueue(values);
        }

        private void SetRandomTestEnabled(
            Dictionary<string, string> values)
        {
            string value;
            bool enabled;
            if (!values.TryGetValue("randomTestEnabled", out value) ||
                !TryParseSwitch(value, out enabled))
            {
                SetFeedback("invalid_setting", true);
                return;
            }

            randomTestEnabled = enabled;
            randomTestRevision++;
            nextRandomTestAt = -1f;
            SetFeedback(
                enabled
                    ? "random_test_enabled"
                    : "random_test_disabled",
                false);
        }

        private void UpdateRandomTest(bool available)
        {
            if (!randomTestEnabled || !available)
            {
                nextRandomTestAt = -1f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (nextRandomTestAt < 0f)
            {
                ScheduleNextRandomTest(now);
                return;
            }
            if (now < nextRandomTestAt)
                return;

            ScheduleNextRandomTest(now);

            // Do not build an automatic backlog. A random test is added only
            // when it can be spawned on this update, after any manual tests.
            if (interactionQueue.ActiveCount >= MaximumActive ||
                interactionQueue.Count != interactionQueue.ActiveCount)
                return;

            var green = UnityEngine.Random.Range(0, 2) == 0;
            var item = green ? GreenZeppelinId : PurpleZeppelinId;
            var variant = green
                ? NativeZeppelinVariant.Green
                : NativeZeppelinVariant.Purple;
            var donor = RandomTestDonors[UnityEngine.Random.Range(
                0, RandomTestDonors.Length)];
            if (interactionQueue.Enqueue(
                item, variant, donor, 1, 0f) <= 0)
                return;

            lastItem = item;
            lastState = null;
            if (logInfo != null)
                logInfo(
                    "Prueba aleatoria de mini zepelin " +
                    variant.ToString().ToLowerInvariant() +
                    " agregada para " + donor + ".");
        }

        private void ScheduleNextRandomTest(float now)
        {
            nextRandomTestAt = now + UnityEngine.Random.Range(
                RandomTestMinimumIntervalSeconds,
                RandomTestMaximumIntervalSeconds);
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
            var delaySeconds = ParseDelaySeconds(values);
            var added = interactionQueue.Enqueue(
                item, variant, donor, quantity, delaySeconds);
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

        private void SetMaximumActive(Dictionary<string, string> values)
        {
            string value;
            int requested;
            if (!values.TryGetValue("maxActive", out value) ||
                !int.TryParse(value, out requested))
            {
                SetFeedback("invalid_setting", true);
                return;
            }

            var normalized = Math.Max(
                1,
                Math.Min(MaximumActiveLimit, requested));
            if (setMaximumActive != null)
                setMaximumActive(normalized);
            SetFeedback("settings_saved", false);
        }

        private void ProcessQueue()
        {
            if (interactionQueue.ActiveCount >= MaximumActive)
                return;

            var now = Time.realtimeSinceStartup;
            if (nextDispatchAt >= 0f && now < nextDispatchAt)
                return;

            var entry = interactionQueue.Peek();
            if (entry == null || !entry.IsReady)
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
                nextDispatchAt = now +
                    MinimumDispatchSeparationSeconds;
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
            if (logWarning != null)
                logWarning(
                    entry.Variant + " zeppelin interaction failed: " +
                    (string.IsNullOrEmpty(error)
                        ? "No diagnostic detail was returned."
                        : error));
        }

        internal const int MaximumActiveLimit = 20;

        private int MaximumActive
        {
            get
            {
                var value = getMaximumActive == null
                    ? 1
                    : getMaximumActive();
                return Math.Max(1, Math.Min(MaximumActiveLimit, value));
            }
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
                .Append(",\"randomTestEnabled\":")
                .Append(randomTestEnabled ? "true" : "false")
                .Append(",\"randomTestRevision\":")
                .Append(randomTestRevision)
                .Append(",\"revision\":").Append(revision)
                .Append(",\"queueCount\":")
                .Append(interactionQueue.Count)
                .Append(",\"activeCount\":")
                .Append(interactionQueue.ActiveCount)
                .Append(",\"maxActive\":")
                .Append(MaximumActive)
                .Append(",\"maxActiveLimit\":")
                .Append(MaximumActiveLimit)
                .Append(",\"maxBatch\":")
                .Append(CreatorToolsInteractionQueue.MaximumBatchSize)
                .Append(",\"maxDelay\":")
                .Append(CreatorToolsInteractionQueue.MaximumDelaySeconds)
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

        private static float ParseDelaySeconds(
            Dictionary<string, string> values)
        {
            string value;
            float delaySeconds;
            if (!values.TryGetValue("delay", out value) ||
                !float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out delaySeconds))
                return 0f;
            return Math.Max(
                0f,
                Math.Min(
                    CreatorToolsInteractionQueue.MaximumDelaySeconds,
                    delaySeconds));
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

        private static bool TryParseSwitch(
            string value,
            out bool enabled)
        {
            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }
            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
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
            randomTestEnabled = false;
            nextRandomTestAt = -1f;
            nextDispatchAt = -1f;
            lastState = null;
        }
    }
}
