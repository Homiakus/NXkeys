using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Planning;

/// <summary>
/// Reconciles a reusable profile with the analyzed model and removes optional operations
/// that cannot apply. Explicit production profiles remain strict; the shipped full-example
/// profile adapts to the currently opened part so it can be used as a universal starting point.
/// </summary>
public static class PlanApplicabilityFilter
{
    public static DrawingPlan Apply(DrawingPlan plan, ProfileDocument profile, ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(report);

        plan = ReconcileDocumentKind(plan, profile, report);
        if (report.HasErrors) return plan;

        var universalExample = IsUniversalExample(profile);
        var removedOperationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removedViewIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removedSheetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SheetPlan> sheets = plan.Sheets;

        if (!plan.Model.IsSheetMetal)
        {
            var applicableSheets = new List<SheetPlan>();
            foreach (var sheet in plan.Sheets)
            {
                var removed = sheet.Views
                    .Where(view => view.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var view in removed)
                    RemoveView(view.Id, removedViewIds, removedOperationIds);

                var remainingViews = sheet.Views
                    .Where(view => !view.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var flatPatternOnlySheet = remainingViews.Length == 0
                                           && removed.Length > 0
                                           && (sheet.Role.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase)
                                               || sheet.Views.Count == removed.Length);
                if (flatPatternOnlySheet)
                {
                    RemoveSheet(sheet.Id, removedSheetIds, removedOperationIds);
                    continue;
                }

                applicableSheets.Add(sheet with { Views = remainingViews });
            }
            sheets = applicableSheets;

            removedOperationIds.Add("feature:flat_pattern");
            removedOperationIds.Add("table:BEND_TABLE");
            if (removedViewIds.Count > 0)
                report.Add(new(
                    "PLAN_FLAT_PATTERN_SKIPPED",
                    IssueSeverity.Warning,
                    $"Пропущено видов развертки: {removedViewIds.Count}; удалено пустых листов развертки: {removedSheetIds.Count}. " +
                    "Текущая модель не является листовым металлом."));
        }

        sheets = FilterUnavailableDatumSections(
            sheets,
            plan.Model.DatumPlanes ?? Array.Empty<string>(),
            universalExample,
            report,
            removedViewIds,
            removedSheetIds,
            removedOperationIds);
        if (report.HasErrors) return plan with { Sheets = sheets };

        var mappings = JsonNavigator.GetArray(profile.Root, "$.pmiInheritance.viewMapping") ?? [];
        var pmiApplicable = JsonNavigator.GetBool(profile.Root, "$.pmiInheritance.enabled", true)
                            && mappings.Count > 0
                            && plan.Model.PmiCount > 0;
        if (!pmiApplicable)
        {
            removedOperationIds.Add("pmi:inherit");
            if (JsonNavigator.GetBool(profile.Root, "$.pmiInheritance.enabled", true)
                && mappings.Count > 0
                && plan.Model.PmiCount == 0)
                report.Add(new(
                    "PLAN_PMI_SKIPPED",
                    IssueSeverity.Warning,
                    "Наследование PMI пропущено: анализ модели не обнаружил PMI-объектов."));
        }

        if (removedOperationIds.Count == 0) return plan with { Sheets = sheets };

        var operations = plan.Operations
            .Where(operation => !removedOperationIds.Contains(operation.OperationId))
            .Select(operation => operation with
            {
                Dependencies = operation.Dependencies
                    .Where(dependency => !removedOperationIds.Contains(dependency))
                    .ToArray()
            })
            .ToArray();

        return plan with { Sheets = sheets, Operations = operations };
    }

    private static IReadOnlyList<SheetPlan> FilterUnavailableDatumSections(
        IReadOnlyList<SheetPlan> sheets,
        IReadOnlyList<string> datumPlanes,
        bool universalExample,
        ValidationReport report,
        ISet<string> removedViewIds,
        ISet<string> removedSheetIds,
        ISet<string> removedOperationIds)
    {
        var available = datumPlanes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = sheets.SelectMany(sheet => sheet.Views)
            .Where(view => IsSection(view.Type)
                           && !string.IsNullOrWhiteSpace(view.SectionDatumPlane)
                           && !available.Contains(view.SectionDatumPlane))
            .ToArray();
        if (missing.Length == 0) return sheets;

        if (!universalExample)
        {
            foreach (var view in missing)
                report.Add(new(
                    "PLAN_SECTION_DATUM_MISSING",
                    IssueSeverity.Error,
                    $"Для разреза '{view.Id}' отсутствует datum plane '{view.SectionDatumPlane}'.",
                    ObjectId: view.Id));
            return sheets;
        }

        var missingIds = missing.Select(view => view.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var view in missing)
            RemoveView(view.Id, removedViewIds, removedOperationIds);

        var filteredSheets = new List<SheetPlan>();
        foreach (var sheet in sheets)
        {
            var views = sheet.Views.Where(view => !missingIds.Contains(view.Id)).ToArray();
            if (views.Length == 0 && sheet.Views.Count > 0)
            {
                RemoveSheet(sheet.Id, removedSheetIds, removedOperationIds);
                continue;
            }
            filteredSheets.Add(sheet with { Views = views });
        }

        report.Add(new(
            "PLAN_SECTION_DATUM_SKIPPED",
            IssueSeverity.Warning,
            "Из универсального example-профиля исключены разрезы без существующей datum plane: " +
            string.Join(", ", missing.Select(view => $"{view.Id}→{view.SectionDatumPlane}")) + "."));
        return filteredSheets;
    }

    private static DrawingPlan ReconcileDocumentKind(
        DrawingPlan plan,
        ProfileDocument profile,
        ValidationReport report)
    {
        var detected = plan.Model.IsAssembly
            ? "assembly_drawing"
            : plan.Model.IsSheetMetal
                ? "sheet_metal_drawing"
                : "part_drawing";
        if (plan.DocumentKind.Equals(detected, StringComparison.OrdinalIgnoreCase)) return plan;

        if (!IsUniversalExample(profile))
        {
            report.Add(new(
                "PLAN_DOCUMENT_KIND_MODEL_MISMATCH",
                IssueSeverity.Error,
                $"Профиль требует '{plan.DocumentKind}', но модель определена как '{detected}'. " +
                "Исправьте job.documentKind либо используйте универсальный example-профиль."));
            return plan;
        }

        report.Add(new(
            "PLAN_DOCUMENT_KIND_ADJUSTED",
            IssueSeverity.Warning,
            $"Универсальный example-профиль адаптирован к модели: '{plan.DocumentKind}' → '{detected}'."));
        return plan with { DocumentKind = detected };
    }

    private static bool IsUniversalExample(ProfileDocument profile)
    {
        var profileStatus = JsonNavigator.GetString(profile.Root, "$.profileStatus", string.Empty) ?? string.Empty;
        if (profileStatus.Equals("active", StringComparison.OrdinalIgnoreCase)
            || profileStatus.Equals("production", StringComparison.OrdinalIgnoreCase)
            || profileStatus.Equals("strict", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return profileStatus.Equals("example", StringComparison.OrdinalIgnoreCase)
               || profile.ProfileId.Contains("FULL_EXAMPLE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSection(string type)
        => type.Equals("section", StringComparison.OrdinalIgnoreCase)
           || type.Equals("half_section", StringComparison.OrdinalIgnoreCase)
           || type.Equals("stepped_section", StringComparison.OrdinalIgnoreCase);

    private static void RemoveView(string id, ISet<string> removedViewIds, ISet<string> removedOperationIds)
    {
        removedViewIds.Add(id);
        removedOperationIds.Add("view:" + id);
    }

    private static void RemoveSheet(string id, ISet<string> removedSheetIds, ISet<string> removedOperationIds)
    {
        removedSheetIds.Add(id);
        removedOperationIds.Add("sheet:" + id);
        removedOperationIds.Add("title_block:" + id);
    }
}
