using System.Text.Json;
using System.Text.Json.Serialization;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Utilities;

namespace NxEskd.Core.Runtime;

public enum PackageDocumentStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped
}

public sealed record PackageDocumentExecutionResult(
    bool Success,
    string? Message = null,
    IReadOnlyDictionary<string, object?>? Metrics = null);

public interface IDrawingPackageDocumentExecutor
{
    Task<PackageDocumentExecutionResult> ExecuteAsync(
        DrawingPackageDocumentPlan document,
        string nativeOutputPath,
        CancellationToken cancellationToken = default);
}

public sealed record PackageDocumentState(
    string DocumentId,
    PackageDocumentStatus Status,
    string OutputPath,
    string? Message,
    DateTimeOffset UpdatedUtc);

public sealed class DrawingPackageExecutionReport
{
    public required string PackageId { get; init; }
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FinishedUtc { get; set; }
    public bool Published { get; set; }
    public bool Success { get; set; }
    public Dictionary<string, PackageDocumentState> Documents { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<ValidationIssue> Issues { get; } = [];
}

public sealed class DrawingPackageExecutor
{
    public async Task<DrawingPackageExecutionReport> ExecuteAsync(
        DrawingPackagePlan plan,
        IDrawingPackageDocumentExecutor executor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(executor);

        var report = new DrawingPackageExecutionReport { PackageId = plan.PackageId };
        var stagingRoot = Path.Combine(plan.OutputDirectory, ".staging", Sanitize(plan.PackageId));
        var journal = LoadJournal(plan.Publication.JournalPath, plan.PackageId);
        Directory.CreateDirectory(plan.OutputDirectory);
        if (plan.Publication.AtomicPublish) Directory.CreateDirectory(stagingRoot);

        foreach (var id in plan.ExecutionOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!plan.DocumentsById.TryGetValue(id, out var document) || !document.Enabled) continue;

            var finalPath = Path.GetFullPath(document.NativeOutputPath);
            var executionPath = plan.Publication.AtomicPublish
                ? StagingPath(plan.OutputDirectory, stagingRoot, finalPath)
                : finalPath;

            if (plan.Publication.ResumeFromJournal
                && journal.Documents.TryGetValue(id, out var previous)
                && previous.Status == PackageDocumentStatus.Succeeded
                && IsNonEmptyFile(executionPath))
            {
                report.Documents[id] = previous with
                {
                    OutputPath = executionPath,
                    Message = "Возобновлено по журналу: ранее успешно подготовленный файл подтверждён.",
                    UpdatedUtc = DateTimeOffset.UtcNow
                };
                continue;
            }

            var failedDependency = document.Dependencies.FirstOrDefault(dependency =>
                !report.Documents.TryGetValue(dependency, out var state)
                || state.Status != PackageDocumentStatus.Succeeded);
            if (!string.IsNullOrWhiteSpace(failedDependency))
            {
                var state = NewState(document, PackageDocumentStatus.Skipped, executionPath,
                    $"Зависимость '{failedDependency}' не завершена успешно.");
                report.Documents[id] = state;
                journal.Documents[id] = state;
                SaveJournal(plan, journal);
                if (plan.Publication.StopOnFailure) break;
                continue;
            }

            if (plan.Publication.DryRun)
            {
                var state = NewState(document, PackageDocumentStatus.Skipped, executionPath,
                    "Dry run: документ проверен и включён в порядок выполнения, NX-обработка не запускалась.");
                report.Documents[id] = state;
                journal.Documents[id] = state;
                SaveJournal(plan, journal);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(executionPath)!);
            var running = NewState(document, PackageDocumentStatus.Running, executionPath, null);
            report.Documents[id] = running;
            journal.Documents[id] = running;
            SaveJournal(plan, journal);

            PackageDocumentExecutionResult result;
            try
            {
                result = await executor.ExecuteAsync(document, executionPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = new PackageDocumentExecutionResult(false, ex.ToString());
            }

            var success = result.Success && IsNonEmptyFile(executionPath);
            var completed = NewState(
                document,
                success ? PackageDocumentStatus.Succeeded : PackageDocumentStatus.Failed,
                executionPath,
                success ? result.Message : result.Message ?? "Исполнитель не создал непустой нативный файл.");
            report.Documents[id] = completed;
            journal.Documents[id] = completed;
            SaveJournal(plan, journal);

            if (!success)
            {
                report.Issues.Add(new(
                    "PKG_DOCUMENT_EXECUTION_FAILED",
                    IssueSeverity.Error,
                    $"Документ '{id}' не подготовлен: {completed.Message}",
                    ObjectId: id));
                if (plan.Publication.StopOnFailure) break;
            }
        }

        var enabled = plan.Documents.Where(document => document.Enabled).Select(document => document.DocumentId).ToArray();
        var allSucceeded = enabled.All(id =>
            report.Documents.TryGetValue(id, out var state)
            && state.Status == PackageDocumentStatus.Succeeded);

        if (!plan.Publication.DryRun && allSucceeded)
        {
            if (plan.Publication.AtomicPublish)
            {
                try
                {
                    PublishAtomically(plan, stagingRoot);
                    report.Published = true;
                }
                catch (Exception ex)
                {
                    report.Issues.Add(new(
                        "PKG_ATOMIC_PUBLISH_FAILED",
                        IssueSeverity.Error,
                        "Не удалось опубликовать комплект; выполнен файловый rollback: " + ex.Message));
                }
            }
            else report.Published = true;
        }
        else if (!plan.Publication.DryRun && plan.Publication.RequireAllDocuments)
        {
            report.Issues.Add(new(
                "PKG_INCOMPLETE_NOT_PUBLISHED",
                IssueSeverity.Error,
                "Комплект не опубликован: не все обязательные документы подготовлены успешно."));
        }

        report.FinishedUtc = DateTimeOffset.UtcNow;
        report.Success = plan.Publication.DryRun
            ? !report.Issues.Any(issue => issue.Severity == IssueSeverity.Error)
            : report.Published && !report.Issues.Any(issue => issue.Severity == IssueSeverity.Error);
        journal.Completed = report.Success;
        journal.Published = report.Published;
        journal.UpdatedUtc = report.FinishedUtc;
        SaveJournal(plan, journal);
        return report;
    }

    private static PackageDocumentState NewState(
        DrawingPackageDocumentPlan document,
        PackageDocumentStatus status,
        string path,
        string? message)
        => new(document.DocumentId, status, path, message, DateTimeOffset.UtcNow);

    private static string StagingPath(string outputRoot, string stagingRoot, string finalPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(outputRoot), Path.GetFullPath(finalPath));
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            throw new InvalidOperationException($"Файл комплекта находится вне outputDirectory: {finalPath}");
        return Path.GetFullPath(relative, stagingRoot);
    }

    private static void PublishAtomically(DrawingPackagePlan plan, string stagingRoot)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var backupRoot = Path.Combine(plan.OutputDirectory, ".publish-backup", transactionId);
        var published = new List<(string Final, string? Backup)>();
        var completed = false;
        try
        {
            foreach (var id in plan.ExecutionOrder)
            {
                if (!plan.DocumentsById.TryGetValue(id, out var document) || !document.Enabled) continue;
                var finalPath = Path.GetFullPath(document.NativeOutputPath);
                var staged = StagingPath(plan.OutputDirectory, stagingRoot, finalPath);
                if (!IsNonEmptyFile(staged))
                    throw new IOException($"Staging-файл отсутствует или пуст: {staged}");

                Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                string? backup = null;
                if (File.Exists(finalPath))
                {
                    var relative = Path.GetRelativePath(plan.OutputDirectory, finalPath);
                    backup = Path.GetFullPath(relative, backupRoot);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Move(finalPath, backup, overwrite: true);
                }

                // Register the current file before the final move so a failure at this exact point
                // also restores the previous published version.
                published.Add((finalPath, backup));
                File.Move(staged, finalPath, overwrite: false);
            }
            completed = true;
        }
        catch
        {
            foreach (var item in published.AsEnumerable().Reverse())
            {
                if (File.Exists(item.Final)) File.Delete(item.Final);
                if (item.Backup is not null && File.Exists(item.Backup))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Final)!);
                    File.Move(item.Backup, item.Final, overwrite: true);
                }
            }
            throw;
        }
        finally
        {
            TryDeleteDirectory(backupRoot);
            if (completed) TryDeleteDirectory(stagingRoot);
        }
    }

    private static bool IsNonEmptyFile(string path)
        => File.Exists(path) && new FileInfo(path).Length > 0;

    private static PackageJournal LoadJournal(string path, string packageId)
    {
        if (!File.Exists(path)) return new PackageJournal(packageId);
        try
        {
            var result = JsonSerializer.Deserialize<PackageJournal>(File.ReadAllText(path), JsonOptions());
            return result is not null && result.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)
                ? result
                : new PackageJournal(packageId);
        }
        catch
        {
            return new PackageJournal(packageId);
        }
    }

    private static void SaveJournal(DrawingPackagePlan plan, PackageJournal journal)
    {
        journal.UpdatedUtc = DateTimeOffset.UtcNow;
        AtomicFile.WriteAllText(
            plan.Publication.JournalPath,
            JsonSerializer.Serialize(journal, JsonOptions()),
            createBackup: true);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Staging cleanup is best effort; the execution report and journal remain authoritative.
        }
    }

    public sealed class PackageJournal
    {
        public PackageJournal(string packageId) => PackageId = packageId;

        public string PackageId { get; init; }
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public bool Completed { get; set; }
        public bool Published { get; set; }
        public Dictionary<string, PackageDocumentState> Documents { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
