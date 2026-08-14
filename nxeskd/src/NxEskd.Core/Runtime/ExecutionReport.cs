using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Utilities;

namespace NxEskd.Core.Runtime;

public sealed class ExecutionReport
{
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset FinishedAt { get; set; }
    public string ProfilePath { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public DrawingCommand Command { get; init; }
    public string? PartPath { get; set; }
    public bool Success { get; set; }
    public bool RolledBack { get; set; }
    public List<string> CreatedObjects { get; } = [];
    public List<string> UpdatedObjects { get; } = [];
    public List<string> SkippedObjects { get; } = [];
    public List<string> Messages { get; } = [];
    public List<ValidationIssue> Issues { get; } = [];
    public Dictionary<string, object?> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Save(string path)
    {
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, ValidationReport.JsonOptions()));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            PruneOldReports(dir, 50);
        }
    }

    public static void PruneOldReports(string directory, int maxReports = 50)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            var files = Directory.GetFiles(directory, "drawing-report-*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(maxReports);
            foreach (var file in files)
            {
                try { file.Delete(); } catch { }
            }
        }
        catch { }
    }
}
