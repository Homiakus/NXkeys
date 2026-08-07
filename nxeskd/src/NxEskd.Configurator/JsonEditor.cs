using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NxEskd.Configurator;

internal static partial class JsonEditor
{
    private readonly record struct Segment(string Name, int? Index);

    public static void Set(JsonObject root, string path, JsonNode? value)
    {
        var segments = Parse(path).ToArray();
        JsonNode current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            current = Resolve(current, segment) ?? throw new InvalidOperationException($"Не найден промежуточный путь {path}.");
        }
        var last = segments[^1];
        if (current is JsonObject obj)
        {
            if (last.Index is null) obj[last.Name] = value;
            else
            {
                var array = obj[last.Name]?.AsArray() ?? throw new InvalidOperationException("Массив не найден.");
                array[last.Index.Value] = value;
            }
        }
        else if (current is JsonArray array && last.Index is int index)
            array[index] = value;
        else throw new InvalidOperationException("Невозможно записать значение по пути " + path);
    }

    private static JsonNode? Resolve(JsonNode current, Segment segment)
    {
        if (current is JsonObject obj)
        {
            var child = obj[segment.Name];
            return segment.Index is int index ? child?.AsArray()[index] : child;
        }
        if (current is JsonArray array && segment.Index is int arrayIndex) return array[arrayIndex];
        return null;
    }

    private static IEnumerable<Segment> Parse(string path)
    {
        var body = path.StartsWith("$.") ? path[2..] : path;
        foreach (var raw in body.Split('.'))
        {
            var match = SegmentRegex().Match(raw);
            if (!match.Success) throw new FormatException("Некорректный JSON path: " + path);
            yield return new Segment(match.Groups["name"].Value,
                match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : null);
        }
    }

    [GeneratedRegex(@"^(?<name>[^\[]+)(?:\[(?<index>\d+)\])?$")]
    private static partial Regex SegmentRegex();
}
