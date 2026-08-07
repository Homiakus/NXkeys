using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxNativePartPublisher(NxServiceContext context)
{
    public bool Save(DrawingPlan plan)
    {
        var publication = plan.Publication;
        if (!publication.SavePart) return true;

        var nativeEnabled = publication.NativeDrawingEnabled;
        var rawTarget = publication.NativeDrawingFile;
        var saveMode = string.IsNullOrWhiteSpace(publication.SaveMode)
            ? "save_current_or_save_as"
            : publication.SaveMode;
        var currentPath = NxReflection.Get(context.WorkPart, "FullPath")?.ToString();

        if (!nativeEnabled || string.IsNullOrWhiteSpace(rawTarget)
            || saveMode.Equals("save_current", StringComparison.OrdinalIgnoreCase))
            return SaveCurrent(currentPath);

        string targetPath;
        try
        {
            var variables = VariableExpander.BuildDefault(context.Profile, currentPath, context.RootDirectory);
            foreach (var pair in plan.ResolvedVariables) variables[pair.Key] = pair.Value;
            targetPath = Path.GetFullPath(new VariableExpander(variables).Expand(rawTarget), context.Profile.BaseDirectory);
        }
        catch (Exception ex)
        {
            context.Report.Issues.Add(new("NX_NATIVE_PATH_RESOLUTION_FAILED", IssueSeverity.Error,
                "Не удалось определить путь нативного чертежа: " + ex.Message,
                JsonPath: "$.output.nativeDrawing.file"));
            return false;
        }

        if (!targetPath.EndsWith(".prt", StringComparison.OrdinalIgnoreCase))
        {
            context.Report.Issues.Add(new("NX_NATIVE_PATH_EXTENSION_INVALID", IssueSeverity.Error,
                $"Нативный чертёж должен иметь расширение .prt: {targetPath}",
                JsonPath: "$.output.nativeDrawing.file"));
            return false;
        }

        if (!string.IsNullOrWhiteSpace(currentPath)
            && PathsEqual(currentPath, targetPath))
            return SaveCurrent(currentPath);

        if (saveMode.Equals("save_current_or_save_as", StringComparison.OrdinalIgnoreCase)
            || saveMode.Equals("save_as", StringComparison.OrdinalIgnoreCase))
            return SaveAs(targetPath, plan);

        context.Report.Issues.Add(new("NX_SAVE_MODE_UNSUPPORTED", IssueSeverity.Error,
            $"Неподдерживаемый output.saveMode: {saveMode}", JsonPath: "$.output.saveMode"));
        return false;
    }

    private bool SaveCurrent(string? currentPath)
    {
        try
        {
            if (!NxReflection.InvokeCommand(context.WorkPart, ["Save"]))
                throw new MissingMethodException(context.WorkPart.GetType().FullName, "Save");
            var path = string.IsNullOrWhiteSpace(currentPath)
                ? NxReflection.Get(context.WorkPart, "FullPath")?.ToString()
                : currentPath;
            context.Report.UpdatedObjects.Add("part:" + (path ?? NxReflection.GetName(context.WorkPart) ?? "WorkPart"));
            return true;
        }
        catch (Exception ex)
        {
            context.Report.Issues.Add(new("NX_SAVE_FAILED", IssueSeverity.Error,
                "Сохранение детали завершилось ошибкой: " + ex.Message));
            return false;
        }
    }

    private bool SaveAs(string targetPath, DrawingPlan plan)
    {
        var publication = plan.Publication;
        string? recoveryBackup = null;
        try
        {
            var directory = Path.GetDirectoryName(targetPath)
                            ?? throw new InvalidOperationException("Не удалось определить каталог нативного чертежа.");
            Directory.CreateDirectory(directory);

            if (File.Exists(targetPath))
            {
                if (!publication.AllowOverwriteExisting)
                {
                    context.Report.Issues.Add(new("NX_OUTPUT_OVERWRITE_CONFIRMATION_REQUIRED", IssueSeverity.Error,
                        $"Выходной файл уже существует: {targetPath}. Перезапись заблокирована, пока " +
                        "$.output.allowOverwriteExisting не установлено в true после проверки Preview.",
                        JsonPath: "$.output.allowOverwriteExisting"));
                    return false;
                }

                var status = JsonNavigator.GetString(context.Profile.Root, "$.job.document.status") ?? string.Empty;
                if (IsReleasedStatus(status)
                    && !publication.AllowOverwriteReleasedDocument)
                {
                    context.Report.Issues.Add(new("NX_RELEASED_DOCUMENT_OVERWRITE_BLOCKED", IssueSeverity.Error,
                        $"Документ имеет статус '{status}'. Перезапись существующего PRT запрещена без отдельного " +
                        "$.output.allowOverwriteReleasedDocument=true.",
                        JsonPath: "$.output.allowOverwriteReleasedDocument"));
                    return false;
                }

                recoveryBackup = targetPath + ".pre-save.bak";
                File.Copy(targetPath, recoveryBackup, overwrite: true);
                context.Report.Messages.Add("Создана резервная копия существующего PRT: " + recoveryBackup);
            }

            if (!NxReflection.InvokeCommand(context.WorkPart, ["SaveAs"], targetPath))
                throw new MissingMethodException(context.WorkPart.GetType().FullName, "SaveAs(string)");

            var actualPath = NxReflection.Get(context.WorkPart, "FullPath")?.ToString();
            if (!string.IsNullOrWhiteSpace(actualPath) && !PathsEqual(actualPath, targetPath))
                throw new InvalidOperationException(
                    $"После SaveAs NX сообщает путь '{actualPath}', ожидался '{targetPath}'.");
            if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
                throw new IOException($"NX не создал непустой файл после SaveAs: {targetPath}");

            context.Report.UpdatedObjects.Add("part-save-as:" + targetPath);
            context.Report.Metrics["output.nativeDrawingPath"] = targetPath;
            if (recoveryBackup is not null)
                context.Report.Metrics["output.previousNativeDrawingBackup"] = recoveryBackup;
            context.Log.Info("Нативный чертёж сохранён: " + targetPath);
            return true;
        }
        catch (Exception ex)
        {
            TryRestoreBackup(targetPath, recoveryBackup);
            context.Report.Issues.Add(new("NX_SAVE_AS_FAILED", IssueSeverity.Error,
                "Сохранение нативного чертежа через SaveAs завершилось ошибкой: " + ex.Message,
                JsonPath: "$.output.nativeDrawing.file"));
            return false;
        }
    }

    private void TryRestoreBackup(string targetPath, string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath)) return;
        try
        {
            File.Copy(backupPath, targetPath, overwrite: true);
            context.Report.Messages.Add("Предыдущий PRT восстановлен из резервной копии после ошибки SaveAs.");
        }
        catch (Exception restoreException)
        {
            context.Report.Issues.Add(new("NX_OUTPUT_BACKUP_RESTORE_FAILED", IssueSeverity.Error,
                $"Не удалось восстановить '{targetPath}' из '{backupPath}': {restoreException.Message}"));
        }
    }

    private static bool IsReleasedStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var normalized = status.Trim().ToLowerInvariant();
        return normalized.Contains("released", StringComparison.Ordinal)
               || normalized.Contains("approved", StringComparison.Ordinal)
               || normalized.Contains("выпущ", StringComparison.Ordinal)
               || normalized.Contains("утверж", StringComparison.Ordinal)
               || normalized.Contains("архив", StringComparison.Ordinal);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
