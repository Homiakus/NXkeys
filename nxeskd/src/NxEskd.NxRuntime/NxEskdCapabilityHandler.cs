using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NXKeys.Protocol;
using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;
using NxEskd.Core.Utilities;

namespace NxEskd.NxRuntime;

public static class NxEskdCapabilityHandler
{
    public const string ComponentId = "nxeskd";
    public const string ComponentVersion = "1.1.0";

    public static NxCommandResult ExecuteCapability(NxCommandRequest request, string? sessionDirectory = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        CommandHost.WriteDiagnosticHeader("Capability:" + request.CapabilityId);

        var result = new NxCommandResult
        {
            SchemaVersion = NxProtocolConstants.SchemaVersion,
            RequestId = request.RequestId,
            WorkflowId = request.WorkflowId,
            CompletedUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        try
        {
            var capabilityId = (request.CapabilityId ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(capabilityId) && !string.IsNullOrWhiteSpace(request.CommandId))
                capabilityId = request.CommandId.Trim().ToLowerInvariant();

            DrawingCommand command = capabilityId switch
            {
                "nxeskd.inventory" => DrawingCommand.Inventory,
                "nxeskd.validate" => DrawingCommand.Validate,
                "nxeskd.preview" => DrawingCommand.Preview,
                "nxeskd.generate" => DrawingCommand.Generate,
                "nxeskd.update" => DrawingCommand.Update,
                "nxeskd.cancel" => DrawingCommand.None,
                "nxeskd.open_workflow" => DrawingCommand.Preview,
                _ => throw new InvalidOperationException($"Неизвестный идентификатор capability: {request.CapabilityId}")
            };

            if (capabilityId == "nxeskd.cancel")
            {
                result.Status = "cancelled";
                result.Phase = "cancelled";
                result.Percent = 100;
                result.Message = "Операция отменена пользователем.";
                return result;
            }

            var root = CommandHost.LocateRoot();
            string profilePath;
            if (!string.IsNullOrWhiteSpace(request.ProfileId) && File.Exists(request.ProfileId))
            {
                profilePath = Path.GetFullPath(request.ProfileId);
            }
            else
            {
                profilePath = CommandHost.LocateActiveProfile(root);
            }

            if (!File.Exists(profilePath))
                throw new FileNotFoundException("Профиль ЕСКД не найден.", profilePath);

            // Verify profile digest if supplied
            if (!string.IsNullOrWhiteSpace(request.ProfileSha256))
            {
                var actualSha = Hashing.Sha256File(profilePath);
                if (!string.Equals(request.ProfileSha256, actualSha, StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = "blocked";
                    result.Phase = "preflight";
                    result.IssueCode = "PROFILE_DIGEST_MISMATCH";
                    result.RecommendedAction = "Повторить предпросмотр с обновленным профилем.";
                    result.Message = "Профиль был изменен после создания запроса.";
                    return result;
                }
            }

            // Verify WorkPart if expected part ID is specified
            var session = Session.GetSession();
            var workPart = session?.Parts?.Work;
            if (!string.IsNullOrWhiteSpace(request.ExpectedPartId))
            {
                if (workPart == null)
                {
                    result.Status = "blocked";
                    result.Phase = "preflight";
                    result.IssueCode = "WORK_PART_MISSING";
                    result.RecommendedAction = "Откройте требуемую деталь в NX.";
                    result.Message = "В сессии NX отсутствует активная рабочая деталь.";
                    return result;
                }

                var currentPartPath = workPart.FullPath;
                if (!string.Equals(Path.GetFullPath(currentPartPath), Path.GetFullPath(request.ExpectedPartId), StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = "blocked";
                    result.Phase = "preflight";
                    result.IssueCode = "WORK_PART_CHANGED";
                    result.RecommendedAction = "Повторите предпросмотр для текущей активной детали.";
                    result.Message = $"Активная деталь NX ({currentPartPath}) не совпадает с ожидаемой ({request.ExpectedPartId}).";
                    return result;
                }
            }

            // Execute via Engine
            result.Phase = "executing";
            result.Percent = 25;

            IExecutionAdapter adapter = command == DrawingCommand.Inventory
                ? new NxInventoryAdapter(root)
                : new NxExecutionAdapter(root);

            var report = new DrawingEngine().Run(command, profilePath, adapter);

            // Save report
            var reportPath = CommandHost.ResolveReportPath(profilePath, adapter.CurrentPartPath, report, root);
            try
            {
                report.Save(reportPath);
                result.ReportName = Path.GetFileName(reportPath);
                if (File.Exists(reportPath))
                    result.ReportSha256 = Hashing.Sha256File(reportPath);
            }
            catch { }

            if (report.Metrics.TryGetValue("plan.hash", out object? planHashObj) && planHashObj is string planHashStr)
            {
                result.PreviewHash = planHashStr;
            }

            result.Phase = "completed";
            result.Percent = 100;
            result.Status = report.Success ? "completed" : "blocked";
            result.ManualReviewRequired = report.Issues.Any(i => i.Severity == IssueSeverity.ManualReview);
            result.RollbackAttempted = report.RolledBack;
            result.RollbackVerified = report.RolledBack && !report.Issues.Any(i => i.Code == "NX_ROLLBACK_FAILED");
            result.Message = report.Success
                ? "Операция ЕСКД выполнена успешно."
                : string.Join("; ", report.Issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message));

            if (!report.Success)
            {
                var firstError = report.Issues.FirstOrDefault(i => i.Severity == IssueSeverity.Error);
                result.IssueCode = firstError?.Code ?? "ESKD_OPERATION_FAILED";
                result.RecommendedAction = "Проверьте предупреждения и отчёт в окне ЕСКД.";
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Status = "failed";
            result.Phase = "failed";
            result.Percent = 100;
            result.IssueCode = "UNHANDLED_EXCEPTION";
            result.RecommendedAction = "Проверьте журнал диагностики NXKeys.";
            result.Message = ex.Message;
            return result;
        }
    }
}
