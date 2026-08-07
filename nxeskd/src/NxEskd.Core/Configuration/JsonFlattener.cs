using System.Text.Json.Nodes;

namespace NxEskd.Core.Configuration;

public sealed record FlatSetting(string Path, string Name, string Value, string ValueType, string? Description, bool IsEditable);

public static class JsonFlattener
{
    public static IReadOnlyList<FlatSetting> Flatten(JsonNode node, string path = "$", string? inheritedDescription = null)
    {
        var result = new List<FlatSetting>();
        Walk(node, path, inheritedDescription, result);
        return result;
    }

    private static void Walk(JsonNode? node, string path, string? description, List<FlatSetting> result)
    {
        switch (node)
        {
            case JsonObject obj:
                var ownDescription = obj["_description"]?.GetValue<string?>() ?? description;
                foreach (var pair in obj)
                {
                    if (pair.Key == "_description") continue;
                    Walk(pair.Value, path == "$" ? "$." + pair.Key : path + "." + pair.Key, ownDescription, result);
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                    Walk(arr[i], $"{path}[{i}]", description, result);
                break;
            case JsonValue value:
                result.Add(new(path, path.Split('.').Last(), ToText(value), DetectType(value), description, !path.Contains("._description", StringComparison.Ordinal)));
                break;
            case null:
                result.Add(new(path, path.Split('.').Last(), "null", "null", description, true));
                break;
        }
    }

    public static JsonNode? ParseValue(string text, string valueType)
    {
        if (text == "null") return null;
        return valueType switch
        {
            "boolean" when bool.TryParse(text, out var b) => JsonValue.Create(b),
            "integer" when long.TryParse(text, out var l) => JsonValue.Create(l),
            "number" when double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) => JsonValue.Create(d),
            _ => JsonValue.Create(text)
        };
    }

    private static string ToText(JsonValue value)
    {
        if (value.TryGetValue<string>(out var s)) return s;
        return value.ToJsonString();
    }

    private static string DetectType(JsonValue value)
    {
        if (value.TryGetValue<bool>(out _)) return "boolean";
        if (value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _)) return "integer";
        if (value.TryGetValue<double>(out _) || value.TryGetValue<decimal>(out _)) return "number";
        return "string";
    }
}
