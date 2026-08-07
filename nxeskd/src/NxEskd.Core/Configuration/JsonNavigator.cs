using System.Globalization;
using System.Text.Json.Nodes;

namespace NxEskd.Core.Configuration;

public static class JsonNavigator
{
    public static JsonNode? Get(JsonNode? root, string path)
    {
        if (root is null) return null;
        if (string.IsNullOrWhiteSpace(path) || path == "$") return root;
        if (!path.StartsWith("$.", StringComparison.Ordinal))
            throw new ArgumentException($"Поддерживаются пути вида $.section.value: {path}", nameof(path));

        JsonNode? current = root;
        foreach (var segment in Split(path[2..]))
        {
            if (current is null) return null;
            if (segment.Index is int index)
            {
                var arr = current[segment.Name]?.AsArray();
                current = arr is not null && index >= 0 && index < arr.Count ? arr[index] : null;
            }
            else
            {
                current = current[segment.Name];
            }
        }
        return current;
    }

    public static string? GetString(JsonNode? root, string path, string? fallback = null)
        => Get(root, path)?.GetValue<string?>() ?? fallback;

    public static bool GetBool(JsonNode? root, string path, bool fallback = false)
    {
        var node = Get(root, path);
        if (node is null) return fallback;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var b)) return b;
            if (value.TryGetValue<string>(out var s) && bool.TryParse(s, out b)) return b;
        }
        return fallback;
    }

    public static int GetInt(JsonNode? root, string path, int fallback = 0)
    {
        var node = Get(root, path);
        if (node is null) return fallback;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l) && l is >= int.MinValue and <= int.MaxValue) return (int)l;
            if (value.TryGetValue<string>(out var s)
                && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return i;
        }
        return fallback;
    }

    public static double GetDouble(JsonNode? root, string path, double fallback = 0)
    {
        var node = Get(root, path);
        if (node is null) return fallback;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var d)) return d;
            if (value.TryGetValue<string>(out var s) && double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
        }
        return fallback;
    }

    public static JsonArray? GetArray(JsonNode? root, string path) => Get(root, path) as JsonArray;
    public static JsonObject? GetObject(JsonNode? root, string path) => Get(root, path) as JsonObject;

    public static void Set(JsonNode root, string path, JsonNode? value)
    {
        if (!path.StartsWith("$.", StringComparison.Ordinal))
            throw new ArgumentException("Путь должен начинаться с $.", nameof(path));

        var segments = Split(path[2..]).ToArray();
        JsonNode current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (segment.Index is not null)
                throw new NotSupportedException("Запись в массив по JSON-пути в редакторе не поддерживается напрямую.");
            var obj = current.AsObject();
            obj[segment.Name] ??= new JsonObject();
            current = obj[segment.Name]!;
        }

        var last = segments[^1];
        if (last.Index is null)
            current.AsObject()[last.Name] = value;
        else
        {
            var array = current.AsObject()[last.Name]?.AsArray() ?? throw new InvalidOperationException("Массив не найден.");
            array[last.Index.Value] = value;
        }
    }

    private static IEnumerable<(string Name, int? Index)> Split(string path)
    {
        foreach (var raw in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bracket = raw.IndexOf('[');
            if (bracket < 0)
            {
                yield return (raw, null);
                continue;
            }
            var name = raw[..bracket];
            var end = raw.IndexOf(']', bracket + 1);
            var indexText = raw[(bracket + 1)..end];
            yield return (name, int.Parse(indexText, CultureInfo.InvariantCulture));
        }
    }
}
