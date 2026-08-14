using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Planning;

public sealed class DrawingSafetyPolicyAnalyzer
{
    public ValidationReport Analyze(ProfileDocument profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var report = new ValidationReport();

        var allowExisting = JsonNavigator.GetBool(profile.Root,
            "$.output.allowOverwriteExisting", false);
        var allowReleased = JsonNavigator.GetBool(profile.Root,
            "$.output.allowOverwriteReleasedDocument", false);
        if (allowReleased && !allowExisting)
            report.Add(new(
                "SAFETY_RELEASED_OVERWRITE_REQUIRES_EXISTING_OVERWRITE",
                IssueSeverity.Error,
                "Разрешение перезаписи выпущенного документа не имеет смысла без output.allowOverwriteExisting=true.",
                "$.output.allowOverwriteReleasedDocument"));

        var status = JsonNavigator.GetString(profile.Root, "$.job.document.status") ?? string.Empty;
        if (allowReleased && IsReleasedStatus(status))
            report.Add(new(
                "SAFETY_RELEASED_OVERWRITE_ARMED",
                IssueSeverity.ManualReview,
                $"Профиль разрешает перезапись документа со статусом '{status}'. Перед Generate/Update требуется Preview и явное подтверждение пользователя.",
                "$.output.allowOverwriteReleasedDocument"));
        else if (allowExisting)
            report.Add(new(
                "SAFETY_EXISTING_OVERWRITE_ARMED",
                IssueSeverity.Warning,
                "Профиль разрешает перезапись существующего выходного PRT. Runtime создаст recovery backup, но путь необходимо проверить в Preview.",
                "$.output.allowOverwriteExisting"));

        var deleteMissing = JsonNavigator.GetBool(profile.Root,
            "$.execution.idempotency.deleteManagedObjectsMissingFromConfig", false);
        var confirmDeletion = JsonNavigator.GetBool(profile.Root,
            "$.execution.idempotency.confirmManagedDeletion", false);
        if (confirmDeletion && !deleteMissing)
            report.Add(new(
                "SAFETY_REDUNDANT_DELETION_CONFIRMATION",
                IssueSeverity.Warning,
                "confirmManagedDeletion=true, но удаление отсутствующих managed-объектов выключено.",
                "$.execution.idempotency.confirmManagedDeletion"));
        if (deleteMissing && !confirmDeletion)
            report.Add(new(
                "SAFETY_MANAGED_DELETION_NOT_CONFIRMED",
                IssueSeverity.Warning,
                "Удаление stale managed-объектов включено, но не подтверждено. Runtime заблокирует удаление, если найдёт stale-объекты.",
                "$.execution.idempotency.confirmManagedDeletion"));
        if (deleteMissing && confirmDeletion)
            report.Add(new(
                "SAFETY_MANAGED_DELETION_ARMED",
                IssueSeverity.ManualReview,
                "Профиль разрешает удаление stale managed-объектов текущего profile/scope. Выполните Preview и проверьте список STALE.",
                "$.execution.idempotency"));

        var nativeEnabled = JsonNavigator.GetBool(profile.Root,
            "$.output.nativeDrawing.enabled", false);
        var saveMode = JsonNavigator.GetString(profile.Root,
            "$.output.saveMode", "save_current_or_save_as") ?? string.Empty;
        var nativeFile = JsonNavigator.GetString(profile.Root,
            "$.output.nativeDrawing.file");
        if (nativeEnabled && saveMode is "save_as" or "save_current_or_save_as")
        {
            if (string.IsNullOrWhiteSpace(nativeFile))
                report.Add(new(
                    "SAFETY_NATIVE_OUTPUT_PATH_REQUIRED",
                    IssueSeverity.Error,
                    "Для включённого SaveAs не задан output.nativeDrawing.file.",
                    "$.output.nativeDrawing.file"));
            else if (!nativeFile.EndsWith(".prt", StringComparison.OrdinalIgnoreCase))
                report.Add(new(
                    "SAFETY_NATIVE_OUTPUT_EXTENSION_INVALID",
                    IssueSeverity.Error,
                    "output.nativeDrawing.file должен иметь расширение .prt.",
                    "$.output.nativeDrawing.file"));
        }

        return report;
    }

    private static bool IsReleasedStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var normalized = status.Trim().ToLowerInvariant();
        return normalized.Contains("released", StringComparison.Ordinal)
               || normalized.Contains("approved", StringComparison.Ordinal)
               || normalized.Contains("issued", StringComparison.Ordinal)
               || normalized.Contains("frozen", StringComparison.Ordinal)
               || normalized.Contains("obsolete", StringComparison.Ordinal)
               || normalized.StartsWith("rel_", StringComparison.Ordinal)
               || normalized.EndsWith("_rel", StringComparison.Ordinal)
               || normalized.Equals("rel", StringComparison.Ordinal)
               || normalized.Contains("выпущ", StringComparison.Ordinal)
               || normalized.Contains("утверж", StringComparison.Ordinal)
               || normalized.Contains("архив", StringComparison.Ordinal);
    }
}
