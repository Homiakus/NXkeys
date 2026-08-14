using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Runtime;

public sealed class DrawingEngine
{
    public ExecutionReport Run(DrawingCommand command, string profilePath, IExecutionAdapter adapter)
    {
        var report = new ExecutionReport { ProfilePath = Path.GetFullPath(profilePath), Command = command, PartPath = adapter.CurrentPartPath };
        try
        {
            var profile = ProfileLoader.Load(profilePath);
            report = new ExecutionReport
            {
                ProfilePath = Path.GetFullPath(profilePath),
                ProfileId = profile.ProfileId,
                Command = command,
                PartPath = adapter.CurrentPartPath
            };
            if (profile.WasMigrated)
            {
                report.Metrics["profile.originalSchemaVersion"] = profile.OriginalSchemaVersion;
                report.Metrics["profile.effectiveSchemaVersion"] = profile.SchemaVersion;
                report.Messages.Add($"Профиль мигрирован в памяти: {profile.OriginalSchemaVersion} → {profile.SchemaVersion}. " +
                                    "Исходный файл не изменён; сохранение выполняется отдельно и создаёт .bak.");
                foreach (var note in profile.MigrationNotes) report.Messages.Add("Миграция профиля: " + note);
            }

            if (command == DrawingCommand.Inventory)
            {
                adapter.Inventory(profile, report);
                report.Success = !report.Issues.Any(i => i.Severity == IssueSeverity.Error);
                return Finish(report);
            }

            var prevalidation = new ProfileValidator().Validate(profile);
            var safety = new DrawingSafetyPolicyAnalyzer().Analyze(profile);
            foreach (var issue in safety.Issues) prevalidation.Add(issue);
            report.Issues.AddRange(prevalidation.Issues);
            if (prevalidation.HasErrors)
            {
                report.Success = false;
                return Finish(report);
            }

            var model = adapter.AnalyzeModel(profile, report);
            foreach (var pair in model.ToMetrics()) report.Metrics["model." + pair.Key] = pair.Value;

            var planner = new DrawingPlanner();
            var (plan, planReport) = planner.Build(profile, model, adapter.CurrentPartPath, prevalidation);
            if (plan is not null)
            {
                plan = PlanApplicabilityFilter.Apply(plan, profile, planReport);
                plan = DrawingOperationPlanNormalizer.Normalize(plan);
                var (_, scheduleReport) = new DrawingOperationScheduler().Build(plan.Operations);
                foreach (var issue in scheduleReport.Issues) planReport.Add(issue);
                var completeness = new DrawingCompletenessAnalyzer().Analyze(plan);
                foreach (var issue in completeness.Issues) planReport.Add(issue);
            }

            foreach (var issue in planReport.Issues)
                if (!report.Issues.Contains(issue)) report.Issues.Add(issue);
            if (planReport.HasErrors || plan is null)
            {
                report.Success = false;
                return Finish(report);
            }
            report.Metrics["plan.operationCount"] = plan.Operations.Count;
            report.Metrics["plan.sheetCount"] = plan.Sheets.Count;
            report.Metrics["plan.viewCount"] = plan.Sheets.Sum(sheet => sheet.Views.Count);
            report.Metrics["plan.hash"] = plan.ComputeHash();

            switch (command)
            {
                case DrawingCommand.Validate:
                    var validation = adapter.ValidateResult(profile, plan, report);
                    report.Issues.AddRange(validation.Issues);
                    report.Success = !validation.HasErrors && !report.Issues.Any(i => i.Severity == IssueSeverity.Error);
                    break;
                case DrawingCommand.Preview:
                    adapter.Preview(profile, plan, report);
                    report.Success = !report.Issues.Any(i => i.Severity == IssueSeverity.Error);
                    break;
                case DrawingCommand.Generate:
                case DrawingCommand.Update:
                    if (plan.DryRun)
                    {
                        adapter.Preview(profile, plan, report);
                        report.Messages.Add("execution.dryRun=true: NX-объекты не изменялись.");
                        report.Success = !report.Issues.Any(i => i.Severity == IssueSeverity.Error);
                        break;
                    }

                    var result = adapter.Execute(command, profile, plan, report);
                    report.Issues.AddRange(result.Issues);
                    if (!report.Issues.Any(i => i.Severity == IssueSeverity.Error))
                    {
                        adapter.Export(profile, plan, report);
                        report.Success = !report.Issues.Any(i => i.Severity == IssueSeverity.Error);
                    }
                    else report.Success = false;
                    break;
                default:
                    report.Issues.Add(new("CMD_NONE", IssueSeverity.Error, "Команда не задана."));
                    report.Success = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            report.Issues.Add(new("UNHANDLED", IssueSeverity.Error, ex.ToString()));
            report.Success = false;
        }
        return Finish(report);
    }

    private static ExecutionReport Finish(ExecutionReport report)
    {
        report.FinishedAt = DateTimeOffset.Now;
        report.Metrics["durationMs"] = (report.FinishedAt - report.StartedAt).TotalMilliseconds;
        return report;
    }
}
