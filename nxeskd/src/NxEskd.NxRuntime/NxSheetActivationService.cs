using NxEskd.Core.Diagnostics;

namespace NxEskd.NxRuntime;

internal sealed class NxSheetActivationService(NxServiceContext context)
{
    public bool Activate(object sheet, string sheetId)
    {
        var collection = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("sheet.collection", "DrawingSheets"));
        if (collection is null)
        {
            context.Report.Issues.Add(new(
                "NX_TARGET_SHEET_COLLECTION_MISSING",
                IssueSeverity.Error,
                $"Не найдена коллекция листов для активации {sheetId}.",
                SheetId: sheetId));
            return false;
        }

        var activated = TryActivate(collection, sheet) || TryActivate(sheet, sheet);
        if (!activated)
        {
            context.Report.Issues.Add(new(
                "NX_TARGET_SHEET_ACTIVATION_FAILED",
                IssueSeverity.Error,
                $"NX не подтвердил активацию целевого листа {sheetId}. Операции видов и аннотаций заблокированы.",
                SheetId: sheetId));
            return false;
        }

        var current = NxReflection.Get(collection,
                          "CurrentDrawingSheet", "CurrentSheet", "ActiveSheet")
                      ?? NxReflection.Get(context.WorkPart,
                          "CurrentDrawingSheet", "CurrentSheet", "ActiveSheet");
        if (current is null) return true;
        if (IsSameNxObject(current, sheet)) return true;

        var expectedName = NxReflection.GetName(sheet) ?? sheetId;
        var actualName = NxReflection.GetName(current);
        if (string.Equals(expectedName, actualName, StringComparison.OrdinalIgnoreCase)) return true;

        context.Report.Issues.Add(new(
            "NX_TARGET_SHEET_POSTCONDITION_FAILED",
            IssueSeverity.Error,
            $"После активации NX сообщает текущий лист '{actualName ?? "unknown"}', ожидался '{expectedName}'.",
            SheetId: sheetId));
        return false;
    }

    private static bool TryActivate(object target, object sheet)
    {
        try
        {
            if (!IsSameNxObject(target, sheet)
                && NxReflection.InvokeCommand(target,
                    ["SetCurrentDrawingSheet", "SetCurrentSheet", "Activate"], sheet))
                return true;
            return NxReflection.InvokeCommand(sheet, ["Open", "Activate"]);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSameNxObject(object? a, object? b)
    {
        if (a is null || b is null) return false;
        if (ReferenceEquals(a, b)) return true;
        var tagA = NxReflection.Get(a, "Tag");
        var tagB = NxReflection.Get(b, "Tag");
        if (tagA is not null && tagB is not null && Equals(tagA, tagB)) return true;
        var nameA = NxReflection.GetName(a);
        var nameB = NxReflection.GetName(b);
        return !string.IsNullOrWhiteSpace(nameA) && string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase);
    }
}
