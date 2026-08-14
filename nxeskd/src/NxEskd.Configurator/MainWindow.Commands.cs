using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NXKeys.Protocol;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;
using NxEskd.Core.Utilities;

namespace NxEskd.Configurator;

public partial class MainWindow
{
    private async void Request(DrawingCommand command)
    {
        string capabilityId = command switch
        {
            DrawingCommand.Preview => "nxeskd.preview",
            DrawingCommand.Validate => "nxeskd.validate",
            DrawingCommand.Inventory => "nxeskd.inventory",
            DrawingCommand.Generate => "nxeskd.generate",
            DrawingCommand.Update => "nxeskd.update",
            _ => "nxeskd.preview"
        };

        await ExecuteCapabilityAsync(capabilityId, command == DrawingCommand.Preview);
    }

    private async Task ExecuteCapabilityAsync(string capabilityId, bool dryRun = false)
    {
        if (!SaveCurrent()) return;

        bool isGenerateOrUpdate = string.Equals(capabilityId, "nxeskd.generate", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(capabilityId, "nxeskd.update", StringComparison.OrdinalIgnoreCase);

        if (isGenerateOrUpdate && !ConfirmRiskyExecution()) return;

        // Switch to the relevant workflow tab
        if (capabilityId == "nxeskd.preview")
            WorkflowTabs.SelectedIndex = 1;
        else if (capabilityId == "nxeskd.validate" || capabilityId == "nxeskd.inventory")
            WorkflowTabs.SelectedIndex = 2;
        else if (isGenerateOrUpdate)
            WorkflowTabs.SelectedIndex = 3;

        string bridgeRoot = Environment.GetEnvironmentVariable("NXKEYS_BRIDGE_ROOT")
                            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "bridge");

        string pendingDir = Path.Combine(bridgeRoot, "pending");
        string completedDir = Path.Combine(bridgeRoot, "completed");
        string failedDir = Path.Combine(bridgeRoot, "failed");

        Directory.CreateDirectory(pendingDir);
        Directory.CreateDirectory(completedDir);
        Directory.CreateDirectory(failedDir);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string requestId = $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";

        var request = new NxCommandRequest
        {
            SchemaVersion = NxProtocolConstants.SchemaVersion,
            RequestId = requestId,
            Action = NxProtocolActions.RunCapability,
            CapabilityId = capabilityId,
            CommandId = capabilityId,
            WorkflowId = _workflowId,
            ExpectedPartId = _nxPartPath ?? string.Empty,
            ProfileId = _document.ProfilePath,
            ProfileSha256 = File.Exists(_document.ProfilePath) ? Hashing.Sha256File(_document.ProfilePath) : string.Empty,
            CreatedUtc = now.ToString("O"),
            ExpiresUtc = now.AddMinutes(5).ToString("O"),
            SourceProcessId = Environment.ProcessId,
            ConfirmationAccepted = true
        };

        // Sign request if session credentials exist
        string? sessionId = Environment.GetEnvironmentVariable(NxBridgeSecurityEnvironment.SessionIdVariable);
        string? secretBase64 = Environment.GetEnvironmentVariable(NxBridgeSecurityEnvironment.SessionSecretVariable);
        string? configPath = Environment.GetEnvironmentVariable(NxBridgeSecurityEnvironment.ConfigPathVariable);

        if (!string.IsNullOrWhiteSpace(sessionId) && !string.IsNullOrWhiteSpace(secretBase64))
        {
            try
            {
                byte[] secret = Convert.FromBase64String(secretBase64);
                string digest = File.Exists(configPath)
                    ? NxBridgePermissionSet.FromProfileFile(configPath).ProfileDigest
                    : Hashing.Sha256(_document.CurrentJson);
                NxRequestAuthenticator.Sign(request, sessionId, Guid.NewGuid().ToString("N"), secret, digest, 1);
            }
            catch { }
        }

        // Show Progress UI
        ExecutionProgressBar.Visibility = Visibility.Visible;
        ExecutionProgressBar.IsIndeterminate = true;
        ExecutionProgressPhase.Visibility = Visibility.Visible;
        ExecutionProgressPhase.Text = $"Запуск операции {capabilityId} в NX...";
        Status($"Выполняется {capabilityId}...");
        ExecutionLogBox.Text = $"[{DateTime.Now:HH:mm:ss}] Запрос {requestId} отправлен в очередь NXKeys...\n";

        // Write request atomically
        string requestPath = Path.Combine(pendingDir, requestId + ".request.json");
        string tempRequestPath = requestPath + ".tmp";
        File.WriteAllText(tempRequestPath, JsonSerializer.Serialize(request, NxProtocolJson.WriteOptions));
        File.Move(tempRequestPath, requestPath, true);

        // Async wait for result
        NxCommandResult? result = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            result = await Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    string compPath = Path.Combine(completedDir, requestId + ".result.json");
                    if (File.Exists(compPath))
                    {
                        try
                        {
                            using var fs = new FileStream(compPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            return JsonSerializer.Deserialize<NxCommandResult>(fs, NxProtocolJson.ReadOptions);
                        }
                        catch { }
                    }

                    string failPath = Path.Combine(failedDir, requestId + ".result.json");
                    if (File.Exists(failPath))
                    {
                        try
                        {
                            using var fs = new FileStream(failPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            return JsonSerializer.Deserialize<NxCommandResult>(fs, NxProtocolJson.ReadOptions);
                        }
                        catch { }
                    }

                    await Task.Delay(250, cts.Token);
                }
                return null;
            }, cts.Token);
        }
        catch (OperationCanceledException) { }

        // Finalize UI
        ExecutionProgressBar.IsIndeterminate = false;
        ExecutionProgressBar.Value = 100;
        ExecutionProgressBar.Visibility = Visibility.Collapsed;
        ExecutionProgressPhase.Visibility = Visibility.Collapsed;

        if (result != null)
        {
            bool success = result.Success;
            ResultStatusTitle.Text = success ? "Операция успешно завершена" : "Операция завершена с замечаниями";
            ResultStatusDescription.Text = result.Message;

            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Результат: {result.Status} (фаза: {result.Phase})");
            sb.AppendLine($"Сообщение: {result.Message}");
            if (!string.IsNullOrWhiteSpace(result.IssueCode))
                sb.AppendLine($"Код проблемы: {result.IssueCode}");
            if (!string.IsNullOrWhiteSpace(result.RecommendedAction))
                sb.AppendLine($"Рекомендация: {result.RecommendedAction}");
            if (!string.IsNullOrWhiteSpace(result.ReportName))
            {
                sb.AppendLine($"Отчёт: {result.ReportName}");
                string reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "reports", "nxeskd");
                _lastReportPath = Path.Combine(reportsDir, result.ReportName);
                if (!File.Exists(_lastReportPath))
                {
                    var found = Directory.Exists(reportsDir)
                        ? Directory.GetFiles(reportsDir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                        : null;
                    if (found != null) _lastReportPath = found;
                }
                OpenReportButton.IsEnabled = File.Exists(_lastReportPath);
            }

            ExecutionLogBox.Text = sb.ToString();
            Status(success ? "Операция завершена успешно." : $"Завершено со статусом: {result.Status}");

            if (capabilityId == "nxeskd.preview")
            {
                PopulatePreviewOperations();
                PreviewSummaryText.Text = $"Предпросмотр завершён: {_planOperations.Count} запланированных операций.";
                PreviewHashText.Text = $"Хэш плана: {result.PreviewHash ?? Hashing.Sha256(_document.CurrentJson)[..16]}";
            }
        }
        else
        {
            ResultStatusTitle.Text = "Таймаут ожидания ответа NX";
            ResultStatusDescription.Text = "NX не вернул результат в течение 90 секунд. Проверьте состояние NX и сессии Bridge.";
            ExecutionLogBox.Text += $"[{DateTime.Now:HH:mm:ss}] Истёк таймаут ожидания обработки запроса {requestId}.\n";
            Status("Превышено время ожидания ответа NX.");

            if (capabilityId == "nxeskd.preview")
            {
                PopulatePreviewOperations();
            }
        }
    }

    private void PopulatePreviewOperations()
    {
        _planOperations.Clear();
        try
        {
            var profile = ProfileLoader.Load(_document.ProfilePath);
            var planner = new DrawingPlanner();
            var (plan, report) = planner.Build(profile, null, _nxPartPath);
            if (plan != null && plan.Operations != null)
            {
                foreach (var op in plan.Operations)
                {
                    _planOperations.Add(new PlanOperationDisplayItem
                    {
                        OperationId = op.OperationId,
                        ObjectKind = op.ObjectKind,
                        ChangeKind = op.ChangeKind,
                        TargetId = op.TargetId,
                        DependenciesText = op.Dependencies != null && op.Dependencies.Count > 0
                            ? string.Join(", ", op.Dependencies)
                            : "—"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _planOperations.Add(new PlanOperationDisplayItem
            {
                OperationId = "PREVIEW_ERROR",
                ObjectKind = "Error",
                ChangeKind = "Failed",
                TargetId = "Plan",
                DependenciesText = ex.Message
            });
        }
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _lastReportPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть отчёт: " + ex.Message, "Отчёт", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private bool ConfirmRiskyExecution()
    {
        var actions = new List<string>();
        if (JsonNavigator.GetBool(_document.Root, "$.output.allowOverwriteExisting", false))
            actions.Add("разрешена перезапись существующего выходного PRT");
        if (JsonNavigator.GetBool(_document.Root, "$.output.allowOverwriteReleasedDocument", false))
            actions.Add("разрешена перезапись выпущенного или утверждённого документа");
        if (JsonNavigator.GetBool(_document.Root,
                "$.execution.idempotency.deleteManagedObjectsMissingFromConfig", false))
            actions.Add(JsonNavigator.GetBool(_document.Root,
                    "$.execution.idempotency.confirmManagedDeletion", false)
                ? "разрешено удаление stale managed-объектов текущего profile/scope"
                : "запрошено удаление stale managed-объектов, но подтверждение профиля отсутствует");

        if (actions.Count == 0) return true;

        var message = new StringBuilder()
            .AppendLine("Для этого запуска включены опасные разрешения:")
            .AppendLine()
            .AppendLine(string.Join(Environment.NewLine, actions.Select(action => "• " + action)))
            .AppendLine()
            .AppendLine("Продолжать только после проверки Preview и целевых путей файлов.")
            .ToString();
        return MessageBox.Show(this, message,
                   "Подтверждение опасных операций",
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Warning,
                   MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private bool ConfirmDiscardOrSave()
    {
        if (!_document.IsDirty(RawJsonBox.Text)) return true;
        var answer = MessageBox.Show(this,
            "Профиль изменён. Сохранить изменения перед продолжением?",
            "Несохранённые изменения",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return answer switch
        {
            MessageBoxResult.Yes => SaveCurrent(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Generate);
    private void Update_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Update);
    private void ValidateNx_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Validate);
    private void Preview_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Preview);
    private void Inventory_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Inventory);
}
