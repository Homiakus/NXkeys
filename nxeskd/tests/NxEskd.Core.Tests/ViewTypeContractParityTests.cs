using System.Text.Json.Nodes;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class ViewTypeContractParityTests
{
    [Fact]
    public void SchemaViewTypesMatchRuntimeSupportedKinds()
    {
        var root = LocateRepositoryRoot();
        var schemaPath = Path.Combine(root, "config", "nx-eskd-profile.schema.json");
        var schema = JsonNode.Parse(File.ReadAllText(schemaPath))?.AsObject()
                     ?? throw new InvalidDataException("Schema root is missing.");
        var typeNode = schema["properties"]?["job"]?["properties"]?["sheetPlan"]?["items"]?
            ["properties"]?["views"]?["items"]?["properties"]?["type"]?["enum"]?.AsArray()
                       ?? throw new InvalidDataException("View type enum is missing from schema.");
        var schemaTypes = typeNode.Select(node => node?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runtimeTypes = DrawingViewKinds.RuntimeSupported
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(schemaTypes.SetEquals(runtimeTypes),
            $"Schema=[{string.Join(",", schemaTypes.Order())}], Runtime=[{string.Join(",", runtimeTypes.Order())}]");
    }

    private static string LocateRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "config", "nx-eskd-profile.schema.json")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
