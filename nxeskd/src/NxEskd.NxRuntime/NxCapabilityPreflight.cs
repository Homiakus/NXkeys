using System.Reflection;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

namespace NxEskd.NxRuntime;

internal sealed class NxCapabilityPreflight(NxServiceContext context)
{
    public bool Validate(DrawingCommand command, DrawingPlan plan)
    {
        var valid = true;
        valid &= RequireTarget(NxReflection.Get(context.Session, "UpdateManager"), "UpdateManager", ["Session", "UpdateManager"]);

        var sheets = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("sheet.collection", "DrawingSheets"));
        valid &= RequireTarget(sheets, "DrawingSheets", ["WorkPart", "DrawingSheets"]);
        valid &= RequireMethod(sheets, "sheet.builder",
            context.ApiMap.Aliases("sheet.builder", "CreateDrawingSheetBuilder", "DrawingSheetBuilder", "CreateSheetBuilder"));

        var views = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews"));
        valid &= RequireTarget(views, "DraftingViews", ["WorkPart", "DraftingViews"]);
        foreach (var type in PlannedViewTypes(plan))
        {
            var (key, aliases) = ViewBuilder(type);
            valid &= RequireMethod(views, key, aliases);
        }

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        if (plan.Operations.Any(operation =>
                operation.OperationId.Equals("note:TECHNICAL_REQUIREMENTS", StringComparison.OrdinalIgnoreCase)))
            valid &= RequireMethod(annotations,
                "note.builder",
                context.ApiMap.Aliases("note.builder", "CreateDraftingNoteBuilder", "CreateNoteBuilder", "DraftingNoteBuilder"),
                IssueSeverity.Warning);

        if (plan.DocumentKind.Equals("assembly_drawing", StringComparison.OrdinalIgnoreCase)
            && plan.Operations.Any(operation =>
                operation.OperationId.Equals("parts_list:AUTO_PARTS_LIST", StringComparison.OrdinalIgnoreCase)))
        {
            var partsLists = NxReflection.Get(annotations,
                context.ApiMap.Aliases("partsList.collection", "PartsLists", "PartsListCollection"));
            valid &= RequireMethod(partsLists ?? annotations,
                "partsList.builder",
                context.ApiMap.Aliases("partsList.builder", "CreatePartsListBuilder", "PartsListBuilder"));
            if (JsonNavigator.GetBool(context.Profile.Root, "$.partsListAndBalloons.balloons.enabled", true))
            {
                var balloons = NxReflection.Get(annotations,
                    context.ApiMap.Aliases("balloon.collection", "Balloons", "IdSymbols", "BalloonNotes"));
                valid &= RequireTarget(balloons, "balloon.collection", ["Annotations", "Balloons/IdSymbols"]);
                valid &= RequireMethod(balloons,
                    "balloon.builder",
                    context.ApiMap.Aliases("balloon.builder", "CreateBalloonBuilder", "CreateIdSymbolBuilder", "CreateBalloonNoteBuilder"));
            }
            valid &= CheckLicense("drafting", ["DRAFTING", "NX_DRAFTING", "drafting"]);
        }

        var flatRequired = plan.Model.IsSheetMetal
                           && plan.Operations.Any(operation =>
                               operation.OperationId.Equals("feature:flat_pattern", StringComparison.OrdinalIgnoreCase));
        if (flatRequired)
        {
            var features = NxReflection.Get(context.WorkPart, "Features");
            var sheetMetal = ResolveSheetMetalManager(features);
            if (!HasManagedOrNamedFlatPattern(features))
                valid &= RequireMethod(sheetMetal,
                    "flatPattern.builder",
                    context.ApiMap.Aliases("flatPattern.builder", "CreateFlatPatternBuilder", "CreateFlatSolidBuilder"));
            valid &= CheckLicense("sheet_metal", ["SHEET_METAL", "NX_SHEET_METAL", "sheet_metal"]);
        }

        if (PmiInheritanceApplicable(plan))
        {
            var styleAliases = context.ApiMap.Aliases("pmi.inheritStyle", "InheritPmi", "ViewStyleInheritPmi");
            var modeAliases = context.ApiMap.Aliases("pmi.setMode", "SetInheritPmiMode", "SetPmi");
            var drawingAliases = context.ApiMap.Aliases("pmi.setToDrawing", "SetInheritPmiToDrawing", "SetPmiToDrawing");
            var targetSupportsPmi = AssemblyContainsProperty(styleAliases)
                                    && AssemblyContainsMethod(modeAliases)
                                    && AssemblyContainsMethod(drawingAliases);
            if (!targetSupportsPmi)
            {
                context.Report.Issues.Add(new("NX_CAPABILITY_PMI_MISSING", IssueSeverity.Error,
                    "Не найден подтверждённый View Style API наследования PMI. " +
                    "Автоматическая карта API будет перестроена из фактически загруженных NXOpen-сборок."));
                valid = false;
            }
            valid &= CheckLicense("drafting", ["DRAFTING", "NX_DRAFTING", "drafting"]);
        }

        if (plan.Model.IsSheetMetal
            && plan.Operations.Any(operation =>
                operation.OperationId.Equals("table:BEND_TABLE", StringComparison.OrdinalIgnoreCase))
            && JsonNavigator.GetArray(context.Profile.Root, "$.job.sheetMetalFlatPattern.bends") is { Count: > 0 })
        {
            var tables = NxReflection.Get(annotations, "TableSections", "TabularNotes", "Tables");
            valid &= RequireMethod(tables ?? annotations,
                "table.builder",
                context.ApiMap.Aliases("table.builder", "CreateTableSectionBuilder", "CreateTabularNoteBuilder", "CreateTableBuilder"),
                IssueSeverity.Warning);
        }

        if (JsonNavigator.GetBool(context.Profile.Root, "$.output.pdf.enabled", true))
        {
            var manager = NxReflection.Get(context.WorkPart,
                              context.ApiMap.Aliases("export.plotManager", "PlotManager", "PrintManager"))
                          ?? NxReflection.Get(context.Session, "DexManager");
            valid &= RequireMethod(manager,
                "export.pdfBuilder",
                context.ApiMap.Aliases("export.pdfBuilder",
                    "CreatePrintPdfbuilder", "CreatePrintPdfBuilder", "CreatePrintPDFBuilder", "CreatePdfBuilder"),
                IssueSeverity.Warning);
        }

        if (plan.Model.IsSheetMetal
            && JsonNavigator.GetBool(context.Profile.Root, "$.sheetMetalFlatPattern.dxfExport.enabled", false))
        {
            var dex = NxReflection.Get(context.Session, "DexManager");
            valid &= RequireMethod(dex,
                "export.dxfBuilder",
                context.ApiMap.Aliases("export.dxfBuilder", "CreateDxfdwgCreator", "CreateDxfCreator", "CreateDxfdwgBuilder"));
        }

        return valid && !context.Report.Issues.Any(x => x.Severity == IssueSeverity.Error);
    }

    private static string[] PlannedViewTypes(DrawingPlan plan)
        => plan.Sheets.SelectMany(sheet => sheet.Views)
            .Select(view => view.Type)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool PmiInheritanceApplicable(DrawingPlan plan)
        => plan.Model.PmiCount > 0
           && plan.Operations.Any(operation =>
               operation.OperationId.Equals("pmi:inherit", StringComparison.OrdinalIgnoreCase));

    private object? ResolveSheetMetalManager(object? features)
        => NxReflection.Get(features,
               context.ApiMap.Aliases("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager"))
           ?? NxReflection.Get(context.WorkPart,
               context.ApiMap.Aliases("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager"));

    private bool RequireTarget(object? target, string capability, IReadOnlyList<string> path)
    {
        if (target is not null) return true;
        context.Report.Issues.Add(new("NX_CAPABILITY_TARGET_MISSING", IssueSeverity.Error,
            $"NX capability '{capability}' недоступна: объект {string.Join("/", path)} не найден."));
        return false;
    }

    private bool RequireMethod(
        object? target,
        string capability,
        IReadOnlyList<string> aliases,
        IssueSeverity severity = IssueSeverity.Error)
    {
        if (target is not null && HasAnyMethod(target, aliases)) return true;
        context.Report.Issues.Add(new("NX_CAPABILITY_METHOD_MISSING", severity,
            $"NX capability '{capability}' не разрешена. Проверены методы: {string.Join(", ", aliases)}.",
            SuggestedFix: "Запустить Диагностику NX Open API; runtime-карта будет обновлена автоматически."));
        return severity != IssueSeverity.Error;
    }

    private bool CheckLicense(string featureId, IReadOnlyList<string> candidateNames)
    {
        var managers = new[]
        {
            NxReflection.Get(context.Session, "LicenseManager", "Licensing", "License"),
            NxReflection.Get(context.Ui, "LicenseManager", "Licensing")
        }.Where(x => x is not null).ToArray();

        var methods = new[] { "IsLicenseAvailable", "IsFeatureAvailable", "IsLicensed", "HasLicense" };
        foreach (var manager in managers)
        foreach (var candidate in candidateNames)
        {
            try
            {
                var result = NxReflection.InvokeFactory(manager, methods, candidate);
                if (result is bool available)
                {
                    if (available) return true;
                    context.Report.Issues.Add(new("NX_LICENSE_UNAVAILABLE", IssueSeverity.Error,
                        $"Лицензия/capability '{featureId}' недоступна по результату NX API ({candidate})."));
                    return false;
                }
            }
            catch (Exception ex)
            {
                context.Report.Issues.Add(new("NX_LICENSE_QUERY_FAILED", IssueSeverity.Warning,
                    $"Проверка лицензии '{featureId}' завершилась ошибкой: {ex.Message}"));
                return true;
            }
        }

        context.Report.Issues.Add(new("NX_LICENSE_QUERY_UNSUPPORTED", IssueSeverity.ManualReview,
            $"Локальная NX не предоставляет подтверждённый read-only API проверки лицензии '{featureId}'. " +
            "Методы изменения модели проверены до Undo; лицензию следует подтвердить станционным тестом."));
        return true;
    }

    private bool HasManagedOrNamedFlatPattern(object? features)
    {
        var preferred = JsonNavigator.GetArray(context.Profile.Root, "$.sheetMetalFlatPattern.flatPatternSource.preferredFeatureNames")?
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray()
            ?? ["FLAT_PATTERN", "РАЗВЕРТКА"];
        return NxReflection.Enumerate(features).Any(feature =>
            NxObjectTools.IsManaged(feature, "FLAT_PATTERN", context.Profile.ProfileId, "feature", context.ScopeId)
            || preferred.Any(name => string.Equals(NxReflection.GetName(feature), name, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasAnyMethod(object? target, IReadOnlyList<string> names)
    {
        if (target is null) return false;
        var expected = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(method => expected.Contains(method.Name));
    }

    private static bool AssemblyContainsMethod(IReadOnlyList<string> names)
    {
        var expected = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SafeTypes(typeof(NXOpen.Session).Assembly)
            .Where(type => type.Namespace?.StartsWith("NXOpen", StringComparison.Ordinal) == true)
            .Any(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Any(method => expected.Contains(method.Name)));
    }

    private static bool AssemblyContainsProperty(IReadOnlyList<string> names)
    {
        var expected = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SafeTypes(typeof(NXOpen.Session).Assembly)
            .Where(type => type.Namespace?.StartsWith("NXOpen", StringComparison.Ordinal) == true)
            .Any(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Any(property => expected.Contains(property.Name)));
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(type => type is not null).Cast<Type>(); }
    }

    private (string Key, string[] Aliases) ViewBuilder(string type) => type.ToLowerInvariant() switch
    {
        "projected" => ("view.projectedBuilder", context.ApiMap.Aliases("view.projectedBuilder", "CreateProjectedViewBuilder", "ProjectedViewBuilder")),
        "section" or "half_section" or "stepped_section" => ("view.sectionBuilder", context.ApiMap.Aliases("view.sectionBuilder", "CreateSectionViewBuilder", "SectionViewBuilder")),
        "detail" => ("view.detailBuilder", context.ApiMap.Aliases("view.detailBuilder", "CreateDetailViewBuilder", "DetailViewBuilder")),
        _ => ("view.baseBuilder", context.ApiMap.Aliases("view.baseBuilder", "CreateBaseViewBuilder", "BaseViewBuilder"))
    };
}
