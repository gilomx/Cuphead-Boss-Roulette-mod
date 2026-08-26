using System.Globalization;
using System.Text.Json;

namespace LaPichiRuleta.TikFinity.TikFinity;

internal static class JsonFieldReader
{
    internal static string? String(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!TryGetPath(root, path, out var value))
                continue;

            var text = ScalarString(value);
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return null;
    }

    internal static string? Url(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!TryGetPath(root, path, out var value))
                continue;

            var url = FirstString(value);
            if (!string.IsNullOrWhiteSpace(url))
                return url.Trim();
        }

        return null;
    }

    internal static int? Integer(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!TryGetPath(root, path, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            var text = ScalarString(value);
            if (int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                return number;
            }
        }

        return null;
    }

    internal static decimal? Decimal(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!TryGetPath(root, path, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                return number;

            var text = ScalarString(value);
            if (decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                return number;
            }
        }

        return null;
    }

    internal static bool? Boolean(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!TryGetPath(root, path, out var value))
                continue;

            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return value.GetBoolean();

            var text = ScalarString(value);
            if (bool.TryParse(text, out var boolean))
                return boolean;
            if (text == "1")
                return true;
            if (text == "0")
                return false;
        }

        return null;
    }

    internal static bool TryGetProperty(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetPath(
        JsonElement root,
        string path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.'))
        {
            if (!TryGetProperty(value, segment, out value))
                return false;
        }

        return true;
    }

    private static string? FirstString(JsonElement value)
    {
        var scalar = ScalarString(value);
        if (!string.IsNullOrWhiteSpace(scalar))
            return scalar;

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var nested = FirstString(item);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "urlList", "url_list", "urls", "url", "uri" })
            {
                if (TryGetProperty(value, name, out var nestedValue))
                {
                    var nested = FirstString(nestedValue);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }
            }
        }

        return null;
    }

    private static string? ScalarString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }
}
