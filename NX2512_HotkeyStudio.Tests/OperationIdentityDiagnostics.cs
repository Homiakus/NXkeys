using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NX2512_HotkeyStudio.Models;

internal static class OperationIdentityDiagnostics
{
    [ModuleInitializer]
    internal static void ReportCanonicalOperationIdentity()
    {
        Config config = Config.LoadEmbedded();
        var duplicateGroups = config.Operations
            .GroupBy(operation => operation.OperationID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"[operation-identity] model_count={config.Operations.Count}; distinct_ordinal_ignore_case={config.Operations.Select(operation => operation.OperationID ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count()}; duplicate_groups={duplicateGroups.Count}");
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
