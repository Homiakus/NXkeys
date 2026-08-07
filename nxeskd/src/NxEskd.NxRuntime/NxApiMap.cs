using System.Text.Json.Nodes;

namespace NxEskd.NxRuntime;

internal sealed class NxApiMap
{
    private readonly JsonObject _configuredAliases;
    private readonly JsonObject _synchronizedAliases;

    private NxApiMap(JsonObject root, JsonObject synchronizedAliases)
    {
        _configuredAliases = root["aliases"] as JsonObject ?? new JsonObject();
        _synchronizedAliases = synchronizedAliases;
    }

    public static NxApiMap Load(string rootDirectory)
    {
        var path = Path.Combine(rootDirectory, "config", "nx2512-api-map.json");
        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject()
            : new JsonObject();
        var synchronized = NxApiSynchronizer.LoadOrRefresh(rootDirectory, root);
        return new NxApiMap(root, synchronized);
    }

    public string[] Aliases(string key, params string[] defaults)
    {
        // Runtime-discovered names have priority, but neither a generated nor a stale
        // configured map is allowed to suppress conservative built-in fallbacks.
        return Read(_synchronizedAliases, key)
            .Concat(Read(_configuredAliases, key))
            .Concat(defaults)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> Read(JsonObject aliases, string key)
        => (aliases[key] as JsonArray)?
               .Select(x => x?.GetValue<string?>())
               .Where(x => !string.IsNullOrWhiteSpace(x))
               .Cast<string>()
           ?? [];
}
