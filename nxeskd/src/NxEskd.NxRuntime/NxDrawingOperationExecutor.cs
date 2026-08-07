using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxDrawingOperationExecutor(
    NxServiceContext context,
    DrawingPlan plan,
    Action ensureCurrentWorkPart,
    Action updateModel)
{
    private readonly Dictionary<string, object> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processedViewSheets = new(StringComparer.OrdinalIgnoreCase);

    public ValidationReport Execute()
    {
        var runtimeGuard = ValidateRuntimeViewKinds();
        if (runtimeGuard.HasErrors)
        {
            foreach (var issue in runtimeGuard.Issues) context.Report.Issues.Add(issue);
            return runtimeGuard;
        }

        var (operations, scheduleReport) = new DrawingOperationScheduler().Build(plan.Operations);
        if (scheduleReport.HasErrors)
        {
            foreach (var issue in scheduleReport.Issues) context.Report.Issues.Add(issue);
            return scheduleReport;
        }

        var attributes = new NxAttributeService(context);
        var sheetService = new NxSheetService(context);
        var sheetActivation = new NxSheetActivationService(context);
        var viewService = new NxViewService(context);
        var titleBlock = new NxTitleBlockService(context, attributes);
        var preconditions = new NxOperationPreconditionEvaluator(context, plan);
        ValidationReport? validation = null;

        foreach (var operation in operations)
        {
            ensureCurrentWorkPart();
            context.Report.Messages.Add(
                $"EXEC {operation.OperationId}: {operation.ChangeKind} {operation.ObjectKind}/{operation.TargetId}");

            if (operation.ObjectKind.Equals("validation", StringComparison.OrdinalIgnoreCase))
            {
                updateModel();
                preconditions.MarkNxUpdateCompleted();
            }

            if (!preconditions.Validate(operation)) break;

            switch (operation.ObjectKind.ToLowerInvariant())
            {
                case "setup":
                    ExecuteSetup(operation);
                    break;
                case "attributes":
                    attributes.WriteCanonicalAttributes(plan);
                    break;
                case "feature":
                    ExecuteFeature(operation);
                    break;
                case "sheet":
                    ExecuteSheet(operation, sheetService);
                    break;
                case "title_block":
                    ExecuteTitleBlock(operation, titleBlock, sheetActivation);
                    break;
                case "view":
                    ExecuteViewsForSheet(operation, viewService, sheetActivation);
                    break;
                case "pmi":
                    new NxPmiService(context, viewService.Views).Inherit(plan);
                    new NxAnnotationLayoutService(context, viewService.Views).Arrange();
                    break;
                case "parts_list":
                    new NxPartsListService(context).CreateOrUpdate(plan);
                    break;
                case "note":
                    new NxTechnicalRequirementsService(context).CreateOrUpdate(plan);
                    break;
                case "table":
                    new NxTableService(context).CreateBendTableIfRequired(plan);
                    break;
                case "reconciliation":
                    new NxReconciliationService(context).Reconcile(plan);
                    break;
                case "validation":
                    validation = new NxValidationService(context).Validate(plan);
                    break;
                case "output":
                    context.Report.Messages.Add(
                        "Публикация отложена до успешного завершения NX-транзакции и выполняется DrawingEngine.Export.");
                    break;
                default:
                    context.Report.Issues.Add(new(
                        "NX_OPERATION_KIND_UNSUPPORTED",
                        IssueSeverity.Error,
                        $"Runtime не поддерживает тип операции '{operation.ObjectKind}' ({operation.OperationId}).",
                        ObjectId: operation.OperationId));
                    break;
            }

            ensureCurrentWorkPart();
            if (context.Report.Issues.Any(issue => issue.Severity == IssueSeverity.Error)) break;
        }

        if (validation is null && !context.Report.Issues.Any(issue => issue.Severity == IssueSeverity.Error))
        {
            updateModel();
            preconditions.MarkNxUpdateCompleted();
            validation = new NxValidationService(context).Validate(plan);
        }

        context.Report.Metrics["execution.operationCount"] = operations.Count;
        return validation ?? new ValidationReport();
    }

    private ValidationReport ValidateRuntimeViewKinds()
    {
        var report = new ValidationReport();
        foreach (var view in plan.Sheets.SelectMany(sheet => sheet.Views)
                     .Where(view => !DrawingViewKinds.IsRuntimeSupported(view.Type)))
            report.Add(new(
                "NX_VIEW_TYPE_UNSUPPORTED",
                IssueSeverity.Error,
                $"NX Runtime не поддерживает тип вида '{view.Type}' для {view.Id}. " +
                "Автоматическая подмена базовым видом запрещена.",
                ObjectId: view.Id));
        return report;
    }

    private void ExecuteSetup(DrawingOperation operation)
    {
        switch (operation.OperationId.ToLowerInvariant())
        {
            case "setup:layers":
                new NxLayerService(context).EnsureLayers(plan);
                break;
            case "setup:styles":
                new NxStyleService(context).ApplyDraftingPreferences();
                break;
            default:
                context.Report.Issues.Add(new(
                    "NX_SETUP_OPERATION_UNSUPPORTED",
                    IssueSeverity.Error,
                    $"Неизвестная подготовительная операция '{operation.OperationId}'.",
                    ObjectId: operation.OperationId));
                break;
        }
    }

    private void ExecuteFeature(DrawingOperation operation)
    {
        if (operation.TargetId.Equals("FLAT_PATTERN", StringComparison.OrdinalIgnoreCase))
        {
            new NxFlatPatternService(context).EnsureFlatPattern(plan);
            return;
        }

        context.Report.Issues.Add(new(
            "NX_FEATURE_OPERATION_UNSUPPORTED",
            IssueSeverity.Error,
            $"Неизвестная feature-операция '{operation.OperationId}'.",
            ObjectId: operation.OperationId));
    }

    private void ExecuteSheet(DrawingOperation operation, NxSheetService sheetService)
    {
        var sheetPlan = plan.Sheets.FirstOrDefault(sheet =>
            sheet.Id.Equals(operation.TargetId, StringComparison.OrdinalIgnoreCase));
        if (sheetPlan is null)
        {
            context.Report.Issues.Add(new(
                "NX_OPERATION_SHEET_PLAN_MISSING",
                IssueSeverity.Error,
                $"Операция {operation.OperationId} ссылается на отсутствующий лист {operation.TargetId}.",
                ObjectId: operation.OperationId));
            return;
        }

        var sheetNumber = plan.Sheets.ToList().FindIndex(sheet =>
            sheet.Id.Equals(sheetPlan.Id, StringComparison.OrdinalIgnoreCase)) + 1;
        _sheets[sheetPlan.Id] = sheetService.EnsureSheet(sheetPlan, sheetNumber);
    }

    private void ExecuteTitleBlock(
        DrawingOperation operation,
        NxTitleBlockService titleBlock,
        NxSheetActivationService sheetActivation)
    {
        var sheetPlan = plan.Sheets.FirstOrDefault(sheet =>
            sheet.Id.Equals(operation.TargetId, StringComparison.OrdinalIgnoreCase));
        if (sheetPlan is null || !_sheets.TryGetValue(operation.TargetId, out var sheet))
        {
            context.Report.Issues.Add(new(
                "NX_OPERATION_TITLE_SHEET_MISSING",
                IssueSeverity.Error,
                $"Для операции {operation.OperationId} не подготовлен лист {operation.TargetId}.",
                ObjectId: operation.OperationId));
            return;
        }

        if (!sheetActivation.Activate(sheet, sheetPlan.Id)) return;
        var sheetNumber = plan.Sheets.ToList().FindIndex(item =>
            item.Id.Equals(sheetPlan.Id, StringComparison.OrdinalIgnoreCase)) + 1;
        titleBlock.Fill(sheet, sheetPlan, plan, sheetNumber);
    }

    private void ExecuteViewsForSheet(
        DrawingOperation operation,
        NxViewService viewService,
        NxSheetActivationService sheetActivation)
    {
        var sheetId = ReadPayloadString(operation, "sheetId")
                      ?? plan.Sheets.FirstOrDefault(sheet => sheet.Views.Any(view =>
                          view.Id.Equals(operation.TargetId, StringComparison.OrdinalIgnoreCase)))?.Id;
        if (string.IsNullOrWhiteSpace(sheetId))
        {
            context.Report.Issues.Add(new(
                "NX_OPERATION_VIEW_SHEET_MISSING",
                IssueSeverity.Error,
                $"Не удалось определить лист для операции вида {operation.OperationId}.",
                ObjectId: operation.OperationId));
            return;
        }

        if (!_processedViewSheets.Add(sheetId)) return;
        if (!_sheets.TryGetValue(sheetId, out var targetSheet))
        {
            context.Report.Issues.Add(new(
                "NX_OPERATION_VIEW_TARGET_SHEET_MISSING",
                IssueSeverity.Error,
                $"Для видов листа {sheetId} отсутствует подтверждённый объект DrawingSheet.",
                SheetId: sheetId,
                ObjectId: operation.OperationId));
            return;
        }
        if (!sheetActivation.Activate(targetSheet, sheetId)) return;

        var sheetPlan = plan.Sheets.First(sheet =>
            sheet.Id.Equals(sheetId, StringComparison.OrdinalIgnoreCase));
        viewService.CreateOrUpdateViews(sheetPlan);
    }

    private static string? ReadPayloadString(DrawingOperation operation, string name)
        => operation.Payload.TryGetValue(name, out var value) ? value?.ToString() : null;
}
