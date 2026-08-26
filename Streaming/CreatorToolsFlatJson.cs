using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Small parser for the deliberately flat companion protocol. Keeping the
    /// protocol flat avoids adding a JSON runtime dependency to Unity/Mono
    /// 3.5 while still validating every value at the process boundary.
    /// </summary>
    internal static class CreatorToolsFlatJson
    {
        internal static bool TryParse(
            string json, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json) || json.Length > 65536)
                return false;

            var position = 0;
            SkipWhitespace(json, ref position);
            if (!Consume(json, ref position, '{'))
                return false;
            SkipWhitespace(json, ref position);
            if (Consume(json, ref position, '}'))
                return position == json.Length;

            while (position < json.Length)
            {
                string key;
                if (!TryReadString(json, ref position, out key) ||
                    key.Length == 0 || key.Length > 64)
                    return false;
                SkipWhitespace(json, ref position);
                if (!Consume(json, ref position, ':'))
                    return false;
                SkipWhitespace(json, ref position);

                string value;
                if (!TryReadValue(json, ref position, out value) ||
                    value.Length > 8192)
                    return false;
                values[key] = value;

                SkipWhitespace(json, ref position);
                if (Consume(json, ref position, '}'))
                {
                    SkipWhitespace(json, ref position);
                    return position == json.Length;
                }
                if (!Consume(json, ref position, ','))
                    return false;
                SkipWhitespace(json, ref position);
            }
            return false;
        }

        internal static string Value(
            Dictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value)
                ? value ?? string.Empty
                : string.Empty;
        }

        internal static bool Boolean(
            Dictionary<string, string> values, string key)
        {
            return string.Equals(Value(values, key), "true",
                StringComparison.OrdinalIgnoreCase);
        }

        internal static int Integer(
            Dictionary<string, string> values,
            string key,
            int fallback,
            int minimum,
            int maximum)
        {
            int parsed;
            if (!int.TryParse(Value(values, key), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out parsed))
                parsed = fallback;
            return Math.Max(minimum, Math.Min(maximum, parsed));
        }

        internal static decimal Decimal(
            Dictionary<string, string> values,
            string key,
            decimal minimum,
            decimal maximum)
        {
            decimal parsed;
            if (!decimal.TryParse(Value(values, key), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out parsed))
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, parsed));
        }

        private static bool TryReadValue(
            string json, ref int position, out string value)
        {
            value = string.Empty;
            if (position >= json.Length)
                return false;
            if (json[position] == '"')
                return TryReadString(json, ref position, out value);

            var start = position;
            while (position < json.Length &&
                   json[position] != ',' && json[position] != '}')
                position++;
            value = json.Substring(start, position - start).Trim();
            if (value == "null")
            {
                value = string.Empty;
                return true;
            }
            if (value == "true" || value == "false")
                return true;

            decimal number;
            return decimal.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out number);
        }

        private static bool TryReadString(
            string json, ref int position, out string value)
        {
            value = string.Empty;
            if (!Consume(json, ref position, '"'))
                return false;
            var builder = new StringBuilder();
            while (position < json.Length)
            {
                var character = json[position++];
                if (character == '"')
                {
                    value = builder.ToString();
                    return true;
                }
                if (character != '\\')
                {
                    if (character < 32)
                        return false;
                    builder.Append(character);
                    continue;
                }
                if (position >= json.Length)
                    return false;
                character = json[position++];
                if (character == '"' || character == '\\' ||
                    character == '/')
                    builder.Append(character);
                else if (character == 'b') builder.Append('\b');
                else if (character == 'f') builder.Append('\f');
                else if (character == 'n') builder.Append('\n');
                else if (character == 'r') builder.Append('\r');
                else if (character == 't') builder.Append('\t');
                else if (character == 'u')
                {
                    if (position + 4 > json.Length)
                        return false;
                    int code;
                    if (!int.TryParse(json.Substring(position, 4),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out code))
                        return false;
                    builder.Append((char)code);
                    position += 4;
                }
                else
                    return false;
            }
            return false;
        }

        private static void SkipWhitespace(string value, ref int position)
        {
            while (position < value.Length &&
                   char.IsWhiteSpace(value[position]))
                position++;
        }

        private static bool Consume(
            string value, ref int position, char expected)
        {
            if (position >= value.Length || value[position] != expected)
                return false;
            position++;
            return true;
        }
    }
}
