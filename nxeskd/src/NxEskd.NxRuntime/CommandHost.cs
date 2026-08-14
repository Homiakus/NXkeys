using System.Diagnostics;
using System.Reflection;
using System.Text;
using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Runtime;

namespace NxEskd.NxRuntime;

public static class CommandHost
{
    public static int Run(DrawingCommand command)
    {
        try
        {
            WriteDiagnosticHeader(command.ToString());
            var root = LocateRoot();
            var profile = LocateActiveProfile(root);
            return Execute(command, profile, root);
        }
        catch (Exception ex)
        {
            Show("ЕСКД-генератор", ex.ToString(), isError: true);
            return 1;
        }
    }

    public static int Execute(DrawingCommand command, string profilePath, string root)
    {
        IExecutionAdapter adapter = command == DrawingCommand.Inventory
            ? new NxInventoryAdapter(root)
            : new NxExecutionAdapter(root);
        var report = new DrawingEngine().Run(command, profilePath, adapter);
        var reportPath = ResolveReportPath(profilePath, adapter.CurrentPartPath, report, root);
        try
        {
            report.Save(reportPath);
        }
        catch (Exception ex)
        {
            var fallback = FallbackReportPath(report);
            try
            {
                report.Save(fallback);
                reportPath = fallback;
            }
            catch (Exception exFallback)
            {
                report.Issues.Add(new("REPORT_SAVE_FAILED", IssueSeverity.Error,
                    $"Не удалось сохранить отчёт '{reportPath}': {ex.Message}; fallback '{fallback}': {exFallback.Message}"));
            }
        }

        var summary = new StringBuilder()
            .AppendLine(report.Success ? "Операция завершена успешно." : "Операция завершена с ошибками.")
            .AppendLine($"Команда: {command}")
            .AppendLine($"Ошибок: {report.Issues.Count(i => i.Severity == IssueSeverity.Error)}")
            .AppendLine($"Предупреждений: {report.Issues.Count(i => i.Severity is IssueSeverity.Warning or IssueSeverity.ManualReview)}")
            .AppendLine($"Отчет: {reportPath}");
        Show("ЕСКД-генератор NX", summary.ToString(), !report.Success);
        return report.Success ? 0 : 2;
    }

    public static string ResolveReportPath(string profilePath, string? partPath, ExecutionReport report, string root)
    {
        try
        {
            var profile = ProfileLoader.Load(profilePath);
            var raw = JsonNavigator.GetString(profile.Root, "$.output.executionReport.file");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var vars = VariableExpander.BuildDefault(profile, partPath, root);
                var expanded = new VariableExpander(vars).Expand(raw, false);
                var targetPath = Path.GetFullPath(expanded, profile.BaseDirectory);
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                    var testFile = Path.Combine(dir, "." + Guid.NewGuid().ToString("N") + ".tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    return targetPath;
                }
            }
        }
        catch (Exception ex)
        {
            report.Issues.Add(new("REPORT_PATH_INVALID", IssueSeverity.Warning,
                $"Настроенный путь отчёта недоступен ({ex.Message}). Используется LocalAppData fallback."));
        }
        return FallbackReportPath(report);
    }

    public static string FallbackReportPath(ExecutionReport report)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "reports", "nxeskd");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"drawing-report-{DateTime.Now:yyyyMMdd-HHmmss}-{report.RunId[..Math.Min(8, report.RunId.Length)]}.json");
    }

    public static void WriteDiagnosticHeader(string commandName)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("──────────────────────────────────────────────");
            sb.AppendLine($"[NxEskd] command            = {commandName}");
            sb.AppendLine($"[NxEskd] product version    = {BuildInfo.Version}");

            try
            {
                var nxOpenAsm = typeof(Session).Assembly;
                var nxOpenPath = nxOpenAsm.Location;
                var nxOpenDir  = Path.GetDirectoryName(nxOpenPath) ?? "(unknown)";
                var fvi        = FileVersionInfo.GetVersionInfo(nxOpenPath);
                sb.AppendLine($"[NxEskd] NXOpen ref dir     = {nxOpenDir}");
                sb.AppendLine($"[NxEskd] NXOpen.dll version = {fvi.FileVersion ?? "(unknown)"}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[NxEskd] NXOpen ref         = (error: {ex.Message})");
            }

            try
            {
                var session = Session.GetSession();
                var sessionType = session.GetType();
                var versionProp = sessionType.GetProperty("NXVersion",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var nxVersion = versionProp?.GetValue(session)?.ToString() ?? "(NXVersion not available)";
                sb.AppendLine($"[NxEskd] NX session version = {nxVersion}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[NxEskd] NX session         = (error: {ex.Message})");
            }

            sb.AppendLine($"[NxEskd] started at (UTC)   = {DateTimeOffset.UtcNow:O}");
            sb.AppendLine("──────────────────────────────────────────────");

            var header = sb.ToString();

            try
            {
                var listing = NxReflection.Get(Session.GetSession(), "ListingWindow");
                _ = NxReflection.InvokeCommand(listing, ["Open"]);
                _ = NxReflection.InvokeCommand(listing, ["WriteFullline", "WriteLine"], header);
            }
            catch { }

            try
            {
                var logDir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys", "logs", "nxeskd");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "startup.log");
                File.AppendAllText(logPath, header, new UTF8Encoding(false));
            }
            catch { }
        }
        catch { }
    }

    public static string LocateRoot()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(CommandHost).Assembly.Location)!;
        var candidates = new[]
        {
            assemblyDir,
            Path.GetFullPath(Path.Combine(assemblyDir, "..")),
            Path.GetFullPath(Path.Combine(assemblyDir, "..", "..")),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys")
        };
        return candidates.FirstOrDefault(c => Directory.Exists(Path.Combine(c, "config")) || Directory.Exists(Path.Combine(c, "templates"))) ?? assemblyDir;
    }

    public static string LocateActiveProfile(string root)
    {
        var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "profiles", "nxeskd", "active-profile.json");
        if (File.Exists(user)) return user;
        var packaged = Path.Combine(root, "config", "active-profile.json");
        if (File.Exists(packaged)) return packaged;
        var example = Path.Combine(root, "config", "active-profile.example.json");
        if (!File.Exists(example))
        {
            var repoExample = Path.Combine(root, "nxeskd", "config", "active-profile.example.json");
            if (File.Exists(repoExample)) example = repoExample;
        }
        if (!File.Exists(example)) throw new FileNotFoundException("Не найден активный профиль ЕСКД.", example);
        Directory.CreateDirectory(Path.GetDirectoryName(user)!);
        File.Copy(example, user, overwrite: false);
        return user;
    }

    private static void Show(string title, string message, bool isError)
    {
        Exception? primaryError = null;
        try
        {
            var ui = UI.GetUI();
            var box = NxReflection.Get(ui, "NXMessageBox", "MessageBox");
            var dialogType = box?.GetType().Assembly.GetType("NXOpen.NXMessageBox+DialogType");
            object? enumValue = null;
            if (dialogType?.IsEnum == true)
                enumValue = Enum.Parse(dialogType, isError ? "Error" : "Information", true);
            if (NxReflection.InvokeCommand(box, ["Show"], title, enumValue, message)) return;
            primaryError = new InvalidOperationException("NXMessageBox.Show не найден или не подтвердил выполнение.");
        }
        catch (Exception ex)
        {
            primaryError = ex;
        }

        try
        {
            var listing = NxReflection.Get(Session.GetSession(), "ListingWindow");
            _ = NxReflection.InvokeCommand(listing, ["Open"]);
            if (NxReflection.InvokeCommand(listing, ["WriteFullline", "WriteLine"], $"[{title}] {message}")) return;
        }
        catch { }

        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "logs", "nxeskd");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "command-host-fallback.log");
            File.AppendAllText(path,
                $"{DateTimeOffset.Now:O} [{title}] MessageBoxError={primaryError}\n{message}\n\n",
                new UTF8Encoding(false));
        }
        catch { }
    }
}
