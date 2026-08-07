using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxSheetTargetService(NxServiceContext context)
{
    public object? Activate(DrawingPlan plan, params string[] preferredRoles)
    {
        var targetPlan = ResolveTargetPlan(plan, preferredRoles);
        if (targetPlan is null)
        {
            context.Report.Issues.Add(new("NX_TARGET_SHEET_PLAN_MISSING", IssueSeverity.Error,
                "Не удалось определить целевой лист для размещения аннотации или таблицы."));
            return null;
        }

        var collection = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("sheet.collection", "DrawingSheets"));
        var sheet = NxReflection.Enumerate(collection).FirstOrDefault(item =>
            NxObjectTools.IsManaged(item, targetPlan.Id, context.Profile.ProfileId, "sheet", context.ScopeId));
        if (sheet is null)
        {
            context.Report.Issues.Add(new("NX_TARGET_SHEET_MISSING", IssueSeverity.Error,
                $"Управляемый целевой лист '{targetPlan.Id}' не найден после выполнения плана.",
                SheetId: targetPlan.Id));
            return null;
        }

        var activated = false;
        try
        {
            activated = NxReflection.InvokeCommand(collection,
                ["SetCurrentDrawingSheet", "SetCurrentSheet", "Activate"], sheet);
        }
        catch (Exception ex)
        {
            context.Log.Warn($"Активация листа {targetPlan.Id} через коллекцию: {ex.Message}");
        }

        if (!activated)
        {
            try { activated = NxReflection.InvokeCommand(sheet, ["Open", "Activate"]); }
            catch (Exception ex) { context.Log.Warn($"Активация листа {targetPlan.Id}: {ex.Message}"); }
        }

        if (!activated)
        {
            context.Report.Issues.Add(new("NX_TARGET_SHEET_ACTIVATION_FAILED", IssueSeverity.Error,
                $"NX API не подтвердил активацию целевого листа '{targetPlan.Id}'.",
                SheetId: targetPlan.Id));
            return null;
        }

        context.Report.Messages.Add($"Целевой лист активирован: {targetPlan.Id} ({targetPlan.Role}).");
        return sheet;
    }

    private static SheetPlan? ResolveTargetPlan(DrawingPlan plan, IReadOnlyList<string> preferredRoles)
    {
        foreach (var role in preferredRoles.Where(role => !string.IsNullOrWhiteSpace(role)))
        {
            var match = plan.Sheets.FirstOrDefault(sheet =>
                sheet.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return plan.Sheets.FirstOrDefault();
    }
}
