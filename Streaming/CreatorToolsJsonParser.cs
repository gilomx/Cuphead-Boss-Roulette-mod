using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsJsonValue
    {
        internal Dictionary<string, CreatorToolsJsonValue> ObjectValue;
        internal List<CreatorToolsJsonValue> ArrayValue;
        internal string StringValue;
        internal decimal? NumberValue;
        internal bool? BooleanValue;

        internal CreatorToolsJsonValue Property(string name)
        {
            CreatorToolsJsonValue value;
            return ObjectValue != null &&
                ObjectValue.TryGetValue(name, out value) ? value : null;
        }

        internal string String(string name)
        {
            var value = Property(name);
            return value == null ? string.Empty :
                value.StringValue ?? string.Empty;
        }

        internal int Integer(string name, int fallback)
        {
            int value;
            return TryInteger(name, out value) ? value : fallback;
        }

        internal bool TryInteger(string name, out int result)
        {
            result = 0;
            var value = Property(name);
            if (value == null || !value.NumberValue.HasValue ||
                value.NumberValue.Value !=
                    decimal.Truncate(value.NumberValue.Value) ||
                value.NumberValue.Value < int.MinValue ||
                value.NumberValue.Value > int.MaxValue)
                return false;
            result = decimal.ToInt32(value.NumberValue.Value);
            return true;
        }

        internal bool TryBoolean(string name, out bool result)
        {
            result = false;
            var value = Property(name);
            if (value == null || !value.BooleanValue.HasValue)
                return false;
            result = value.BooleanValue.Value;
            return true;
        }
    }

    internal sealed class CreatorToolsJsonParser
    {
        private readonly string json;
        private int position;
        private readonly int maximumStringLength;
        private readonly int maximumArrayLength;

        private CreatorToolsJsonParser(string json, int maximumStringLength, int maximumArrayLength)
        {
            this.json = json;
            this.maximumStringLength = maximumStringLength;
            this.maximumArrayLength = maximumArrayLength;
        }

        internal static bool TryParse(string json, out CreatorToolsJsonValue value,
            int maximumLength = 65536, int maximumStringLength = 8192, int maximumArrayLength = 128)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || json.Length > maximumLength)
                return false;
            try
            {
                var parser = new CreatorToolsJsonParser(json, maximumStringLength, maximumArrayLength);
                value = parser.ReadValue(0);
                parser.SkipWhitespace();
                return value != null && parser.position == json.Length;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private CreatorToolsJsonValue ReadValue(int depth)
        {
            if (depth > 16)
                throw new FormatException();
            SkipWhitespace();
            if (position >= json.Length)
                throw new FormatException();
            if (json[position] == '{') return ReadObject(depth + 1);
            if (json[position] == '[') return ReadArray(depth + 1);
            if (json[position] == '"')
                return new CreatorToolsJsonValue { StringValue = ReadString() };
            if (Match("true"))
                return new CreatorToolsJsonValue { BooleanValue = true };
            if (Match("false"))
                return new CreatorToolsJsonValue { BooleanValue = false };
            if (Match("null")) return new CreatorToolsJsonValue();
            return ReadNumber();
        }

        private CreatorToolsJsonValue ReadObject(int depth)
        {
            position++;
            var values = new Dictionary<string, CreatorToolsJsonValue>(
                StringComparer.Ordinal);
            SkipWhitespace();
            if (Consume('}'))
                return new CreatorToolsJsonValue { ObjectValue = values };
            while (values.Count < 128)
            {
                SkipWhitespace();
                var key = ReadString();
                SkipWhitespace();
                if (!Consume(':') || values.ContainsKey(key))
                    throw new FormatException();
                values[key] = ReadValue(depth);
                SkipWhitespace();
                if (Consume('}'))
                    return new CreatorToolsJsonValue { ObjectValue = values };
                if (!Consume(',')) throw new FormatException();
            }
            throw new FormatException();
        }

        private CreatorToolsJsonValue ReadArray(int depth)
        {
            position++;
            var values = new List<CreatorToolsJsonValue>();
            SkipWhitespace();
            if (Consume(']'))
                return new CreatorToolsJsonValue { ArrayValue = values };
            while (values.Count < maximumArrayLength)
            {
                values.Add(ReadValue(depth));
                SkipWhitespace();
                if (Consume(']'))
                    return new CreatorToolsJsonValue { ArrayValue = values };
                if (!Consume(',')) throw new FormatException();
            }
            throw new FormatException();
        }

        private CreatorToolsJsonValue ReadNumber()
        {
            var start = position;
            while (position < json.Length &&
                "-+0123456789.eE".IndexOf(json[position]) >= 0)
                position++;
            decimal number;
            if (position == start || !Regex.IsMatch(json.Substring(start, position - start),
                    @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant) || !decimal.TryParse(
                    json.Substring(start, position - start),
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out number))
                throw new FormatException();
            return new CreatorToolsJsonValue { NumberValue = number };
        }

        private string ReadString()
        {
            if (!Consume('"')) throw new FormatException();
            var builder = new StringBuilder();
            while (position < json.Length && builder.Length <= maximumStringLength)
            {
                var character = json[position++];
                if (character == '"') return builder.ToString();
                if (character < 32) throw new FormatException();
                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }
                if (position >= json.Length) throw new FormatException();
                character = json[position++];
                if (character == '"' || character == '\\' ||
                    character == '/') builder.Append(character);
                else if (character == 'b') builder.Append('\b');
                else if (character == 'f') builder.Append('\f');
                else if (character == 'n') builder.Append('\n');
                else if (character == 'r') builder.Append('\r');
                else if (character == 't') builder.Append('\t');
                else if (character == 'u')
                {
                    if (position + 4 > json.Length)
                        throw new FormatException();
                    int code;
                    if (!int.TryParse(json.Substring(position, 4),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out code))
                        throw new FormatException();
                    builder.Append((char)code);
                    position += 4;
                }
                else throw new FormatException();
            }
            throw new FormatException();
        }

        private bool Match(string value)
        {
            if (position + value.Length > json.Length ||
                string.CompareOrdinal(json, position, value, 0,
                    value.Length) != 0)
                return false;
            position += value.Length;
            return true;
        }

        private bool Consume(char value)
        {
            if (position >= json.Length || json[position] != value)
                return false;
            position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (position < json.Length &&
                char.IsWhiteSpace(json[position])) position++;
        }
    }
}
