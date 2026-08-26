using System.Globalization;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsJson
    {
        internal static void AppendEscaped(
            StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\').Append(character);
                else if (character == '\n') builder.Append("\\n");
                else if (character == '\r') builder.Append("\\r");
                else if (character == '\t') builder.Append("\\t");
                else if (character < 32)
                    builder.Append("\\u").Append(
                        ((int)character).ToString("x4"));
                else builder.Append(character);
            }
        }

        internal static void AppendNullableString(
            StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                builder.Append("null");
                return;
            }
            builder.Append('"');
            AppendEscaped(builder, value);
            builder.Append('"');
        }

        internal static void AppendDecimal(
            StringBuilder builder, decimal value)
        {
            builder.Append(value.ToString(
                "0.##", CultureInfo.InvariantCulture));
        }
    }
}
