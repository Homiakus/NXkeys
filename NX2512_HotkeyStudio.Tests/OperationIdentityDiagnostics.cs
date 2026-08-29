using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

internal static class OperationIdentityDiagnostics
{
    [ModuleInitializer]
    internal static void ReportCanonicalOperationIdentity()
    {
        var assembly = typeof(Config).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("nx2512-v8-profile.json", StringComparison.OrdinalIgnoreCase));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string json = reader.ReadToEnd();

        Config config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new Config();

        Report("deserialized", config);
        config.ExpandEnvironment();
        Report("expanded", config);
        config.ApplyDefaults();
        Report("defaults", config);
        config.Validate();
        Report("validated", config);
    }

    private static void Report(string stage, Config config)
    {
        var duplicateGroups = config.Operations
            .GroupBy(operation => operation.OperationID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"[operation-identity] stage={stage}; model_count={config.Operations.Count}; distinct_ordinal_ignore_case={config.Operations.Select(operation => operation.OperationID ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count()}; duplicate_groups={duplicateGroups.Count}");
        if (!string.Equals(stage, "defaults", StringComparison.OrdinalIgnoreCase)) return;

        foreach (var group in duplicateGroups)
        {
            Console.WriteLine($"[operation-identity] DUPLICATE '{group.Key}' x{group.Count()}");
            foreach (OperationContract operation in group)
            {
                string applications = string.Join(",", operation.Availability?.Applications ?? new System.Collections.Generic.List<string>());
                string leader = string.Join(" ", operation.Paths?.Leader ?? new System.Collections.Generic.List<string>());
                Console.WriteLine($"[operation-identity]   name='{operation.CommandName}' app='{applications}' adapter='{operation.Adapter?.Kind}:{operation.Adapter?.Value}' leader='{leader}' direct='{operation.Paths?.Direct}' workspace='{operation.Paths?.WorkspaceKey}'");
            }
        }
    }
}
