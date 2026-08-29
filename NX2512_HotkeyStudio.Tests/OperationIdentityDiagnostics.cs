using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

internal static class OperationIdentityDiagnostics
{
    [ModuleInitializer]
    internal static void ReportCanonicalOperationIdentity()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var assembly = typeof(Config).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("nx2512-v8-profile.json", StringComparison.OrdinalIgnoreCase));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string embeddedJson = reader.ReadToEnd();

        string diskPath = FindRepositoryFile(Path.Combine("config", "nx2512-v8-profile.json"));
        string diskJson = File.ReadAllText(diskPath, Encoding.UTF8);

        Console.WriteLine($"[operation-identity] disk_path={diskPath}");
        Console.WriteLine($"[operation-identity] disk_sha256={Sha256(diskJson)}; embedded_sha256={Sha256(embeddedJson)}; equal={string.Equals(diskJson, embeddedJson, StringComparison.Ordinal)}");

        Config disk = JsonSerializer.Deserialize<Config>(diskJson, options) ?? new Config();
        Report("disk_raw", disk, false);

        Config embedded = JsonSerializer.Deserialize<Config>(embeddedJson, options) ?? new Config();
        Report("embedded_raw", embedded, true);
        embedded.ExpandEnvironment();
        Report("expanded", embedded, false);
        embedded.ApplyDefaults();
        Report("defaults", embedded, false);
        embedded.Validate();
        Report("validated", embedded, false);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        string current = Directory.GetCurrentDirectory();
        for (int depth = 0; depth < 10 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            string candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
        throw new FileNotFoundException("Repository file was not found for operation identity diagnostics.", relativePath);
    }

    private static string Sha256(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void Report(string stage, Config config, bool showDuplicates)
    {
        var duplicateGroups = config.Operations
            .GroupBy(operation => operation.OperationID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"[operation-identity] stage={stage}; model_count={config.Operations.Count}; distinct_ordinal_ignore_case={config.Operations.Select(operation => operation.OperationID ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count()}; duplicate_groups={duplicateGroups.Count}");
        if (!showDuplicates) return;

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
