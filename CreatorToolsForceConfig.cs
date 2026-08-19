using System;
using System.Collections.Generic;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private bool creatorToolsForceInitialized;
        private bool creatorToolsForceEnabled;
        private int creatorToolsForceBoss;
        private int creatorToolsForceWeapon1;
        private int creatorToolsForceWeapon2;
        private int creatorToolsForceSuper;
        private int creatorToolsForceCharm;
        private int creatorToolsForceModifier;

        private void UpdateCreatorToolsForceConfig()
        {
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning)
                return;

            // Building the web force panel refreshes Cuphead's DLC catalog.
            // During Plugin.Awake that is too early: MapUI has not initialized
            // its native pause/equipment inputs yet. Keep /api/config in its
            // ready:false state until the map (including Equip Card) is ready.
            if (!creatorToolsForceInitialized)
            {
                if (!CanUseRouletteOnMap())
                    return;
                PublishCreatorToolsForceConfig(true);
            }

            string command;
            var changed = false;
            while (creatorToolsServer.TryTakeConfigCommand(out command))
            {
                ApplyCreatorToolsForceCommand(command);
                changed = true;
            }
            if (changed)
                PublishCreatorToolsForceConfig(true);
        }

        private void PublishCreatorToolsForceConfig(bool force)
        {
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning)
                return;
            if (!creatorToolsForceInitialized &&
                !CanUseRouletteOnMap())
                return;
            EnsureCreatorToolsForceDefaults();
            creatorToolsServer.SetConfigState(
                BuildCreatorToolsForceConfigJson());
        }

        private void EnsureCreatorToolsForceDefaults()
        {
            EnsureAvailableContent();
            if (!creatorToolsForceInitialized)
            {
                creatorToolsForceBoss = FirstAvailable(
                    availableBossIndices, 0, false,
                    RouletteData.Bosses.Length - 1);
                creatorToolsForceWeapon1 = FirstAvailable(
                    availableWeaponIndices, 0, true,
                    RouletteData.Weapons.Length - 1);
                creatorToolsForceWeapon2 =
                    RouletteData.Weapons.Length - 1;
                creatorToolsForceSuper =
                    RouletteData.Supers.Length - 1;
                creatorToolsForceCharm =
                    RouletteData.Charms.Length - 1;
                creatorToolsForceModifier =
                    RouletteData.Modifiers.Length - 1;
                creatorToolsForceInitialized = true;
            }
            SanitizeCreatorToolsForceSelection();
        }

        private static int FirstAvailable(List<int> pool, int fallback,
            bool rejectLast, int lastIndex)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (!rejectLast || pool[i] != lastIndex)
                    return pool[i];
            }
            return fallback;
        }

        private void SanitizeCreatorToolsForceSelection()
        {
            if (!availableBossIndices.Contains(creatorToolsForceBoss))
                creatorToolsForceBoss = FirstAvailable(
                    availableBossIndices, 0, false,
                    RouletteData.Bosses.Length - 1);

            var emptyWeapon = RouletteData.Weapons.Length - 1;
            if (!availableWeaponIndices.Contains(
                    creatorToolsForceWeapon1) ||
                creatorToolsForceWeapon1 == emptyWeapon)
                creatorToolsForceWeapon1 = FirstAvailable(
                    availableWeaponIndices, 0, true, emptyWeapon);

            if (!availableWeaponIndices.Contains(
                    creatorToolsForceWeapon2))
                creatorToolsForceWeapon2 = emptyWeapon;
            if (creatorToolsForceWeapon2 == creatorToolsForceWeapon1)
                creatorToolsForceWeapon2 = emptyWeapon;

            if (!availableSuperIndices.Contains(creatorToolsForceSuper))
                creatorToolsForceSuper = RouletteData.Supers.Length - 1;
            if (!availableCharmIndices.Contains(creatorToolsForceCharm))
                creatorToolsForceCharm = RouletteData.Charms.Length - 1;

            var validModifiers = RouletteData.ValidModifierIndices(
                RouletteData.Bosses[creatorToolsForceBoss]);
            var none = RouletteData.Modifiers.Length - 1;
            if (creatorToolsForceModifier != none &&
                !validModifiers.Contains(creatorToolsForceModifier))
                creatorToolsForceModifier = none;
        }

        private void ApplyCreatorToolsForceCommand(string query)
        {
            EnsureCreatorToolsForceDefaults();
            var values = ParseCreatorToolsQuery(query);
            int parsed;
            if (TryCreatorToolsInt(values, "boss", out parsed))
                creatorToolsForceBoss = parsed;
            if (TryCreatorToolsInt(values, "weapon1", out parsed))
                creatorToolsForceWeapon1 = parsed;
            if (TryCreatorToolsInt(values, "weapon2", out parsed))
                creatorToolsForceWeapon2 = parsed;
            if (TryCreatorToolsInt(values, "super", out parsed))
                creatorToolsForceSuper = parsed;
            if (TryCreatorToolsInt(values, "charm", out parsed))
                creatorToolsForceCharm = parsed;
            if (TryCreatorToolsInt(values, "modifier", out parsed))
                creatorToolsForceModifier = parsed;

            string enabled;
            if (values.TryGetValue("enabled", out enabled))
                creatorToolsForceEnabled = enabled == "1" ||
                    string.Equals(enabled, "true",
                        StringComparison.OrdinalIgnoreCase);

            SanitizeCreatorToolsForceSelection();
            uglyMode = creatorToolsForceEnabled
                ? RouletteData.Modifiers[creatorToolsForceModifier].Id !=
                    ModifierId.None
                : challengeSetting.Value;
            Logger.LogInfo(creatorToolsForceEnabled
                ? "Forzado web de ruleta activado."
                : "Forzado web de ruleta desactivado.");
        }

        private static Dictionary<string, string> ParseCreatorToolsQuery(
            string query)
        {
            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
                return values;
            var pairs = query.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var separator = pairs[i].IndexOf('=');
                var rawKey = separator < 0
                    ? pairs[i]
                    : pairs[i].Substring(0, separator);
                var rawValue = separator < 0
                    ? string.Empty
                    : pairs[i].Substring(separator + 1);
                try
                {
                    values[Uri.UnescapeDataString(
                        rawKey.Replace('+', ' '))] =
                        Uri.UnescapeDataString(
                            rawValue.Replace('+', ' '));
                }
                catch
                {
                }
            }
            return values;
        }

        private static bool TryCreatorToolsInt(
            Dictionary<string, string> values, string key, out int value)
        {
            string raw;
            value = 0;
            return values.TryGetValue(key, out raw) &&
                   int.TryParse(raw, out value);
        }

        private RouletteResult CreateCreatorToolsForcedResult()
        {
            if (!creatorToolsForceEnabled)
                return null;
            EnsureCreatorToolsForceDefaults();
            uglyMode = RouletteData.Modifiers[
                creatorToolsForceModifier].Id != ModifierId.None;
            RememberBossResult(creatorToolsForceBoss);
            Logger.LogInfo("Resultado forzado desde /config: " +
                RouletteData.Bosses[creatorToolsForceBoss].Character + ".");
            return new RouletteResult
            {
                Boss = creatorToolsForceBoss,
                Weapon1 = creatorToolsForceWeapon1,
                Weapon2 = creatorToolsForceWeapon2,
                Super = creatorToolsForceSuper,
                Charm = creatorToolsForceCharm,
                Modifier = creatorToolsForceModifier
            };
        }

        private string BuildCreatorToolsForceConfigJson()
        {
            var builder = new StringBuilder(8192);
            builder.Append("{\"ready\":true,\"enabled\":")
                .Append(creatorToolsForceEnabled ? "true" : "false")
                .Append(",\"selection\":{")
                .Append("\"boss\":").Append(creatorToolsForceBoss)
                .Append(",\"weapon1\":")
                .Append(creatorToolsForceWeapon1)
                .Append(",\"weapon2\":")
                .Append(creatorToolsForceWeapon2)
                .Append(",\"super\":").Append(creatorToolsForceSuper)
                .Append(",\"charm\":").Append(creatorToolsForceCharm)
                .Append(",\"modifier\":")
                .Append(creatorToolsForceModifier).Append("},");

            AppendBossConfigOptions(builder);
            builder.Append(',');
            AppendWeaponConfigOptions(builder);
            builder.Append(',');
            AppendSuperConfigOptions(builder);
            builder.Append(',');
            AppendCharmConfigOptions(builder);
            builder.Append(',');
            AppendModifierConfigOptions(builder);
            builder.Append('}');
            return builder.ToString();
        }

        private void AppendBossConfigOptions(StringBuilder builder)
        {
            builder.Append("\"bosses\":[");
            for (var i = 0; i < availableBossIndices.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var index = availableBossIndices[i];
                var boss = RouletteData.Bosses[index];
                builder.Append("{\"id\":").Append(index)
                    .Append(",\"name\":\"")
                    .Append(EscapeJson(LocalizedBossName(boss)))
                    .Append("\",\"plane\":")
                    .Append(boss.IsPlane ? "true" : "false")
                    .Append('}');
            }
            builder.Append(']');
        }

        private void AppendWeaponConfigOptions(StringBuilder builder)
        {
            var empty = RouletteData.Weapons.Length - 1;
            builder.Append("\"weapons\":[");
            for (var i = 0; i < availableWeaponIndices.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var index = availableWeaponIndices[i];
                builder.Append("{\"id\":").Append(index)
                    .Append(",\"name\":\"")
                    .Append(EscapeJson(LocalizedEquipmentName(
                        RouletteData.Weapons[index])))
                    .Append("\",\"empty\":")
                    .Append(index == empty ? "true" : "false")
                    .Append('}');
            }
            builder.Append(']');
        }

        private void AppendSuperConfigOptions(StringBuilder builder)
        {
            builder.Append("\"supers\":[");
            for (var i = 0; i < availableSuperIndices.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var index = availableSuperIndices[i];
                builder.Append("{\"id\":").Append(index)
                    .Append(",\"name\":\"")
                    .Append(EscapeJson(LocalizedEquipmentName(
                        RouletteData.Supers[index])))
                    .Append("\"}");
            }
            builder.Append(']');
        }

        private void AppendCharmConfigOptions(StringBuilder builder)
        {
            builder.Append("\"charms\":[");
            for (var i = 0; i < availableCharmIndices.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var index = availableCharmIndices[i];
                builder.Append("{\"id\":").Append(index)
                    .Append(",\"name\":\"")
                    .Append(EscapeJson(LocalizedEquipmentName(
                        RouletteData.Charms[index])))
                    .Append("\"}");
            }
            builder.Append(']');
        }

        private void AppendModifierConfigOptions(StringBuilder builder)
        {
            builder.Append("\"modifiers\":[");
            var wrote = false;
            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                var modifier = RouletteData.Modifiers[i];
                if (modifier.Id != ModifierId.None &&
                    !ExperimentalFeatures.IsChallengeEnabled(modifier.Id))
                    continue;
                if (wrote) builder.Append(',');
                wrote = true;
                builder.Append("{\"id\":").Append(i)
                    .Append(",\"name\":\"")
                    .Append(EscapeJson(modifier.Id == ModifierId.None
                        ? L(ModText.CommonNone)
                        : LocalizedModifierName(modifier.Id)))
                    .Append("\",\"none\":")
                    .Append(modifier.Id == ModifierId.None
                        ? "true"
                        : "false")
                    .Append(",\"kind\":\"")
                    .Append(modifier.Kind == ModifierKind.Plane
                        ? "plane"
                        : modifier.Kind == ModifierKind.Ground
                            ? "ground"
                            : "both")
                    .Append("\"}");
            }
            builder.Append(']');
        }
    }
}
