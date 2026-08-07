using System.Globalization;
using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Planning;

public sealed class DrawingPlanner
{
    private static readonly ScaleCandidate[] ScaleCandidates =
    [
        new(10.0, "10:1"), new(5.0, "5:1"), new(2.0, "2:1"), new(1.0, "1:1"),
        new(0.5, "1:2"), new(0.4, "1:2.5"), new(0.25, "1:4"), new(0.2, "1:5"),
        new(0.1, "1:10"), new(0.05, "1:20"), new(0.02, "1:50"), new(0.01, "1:100")
    ];

    public (DrawingPlan? Plan, ValidationReport Report) Build(
        ProfileDocument profile,
        ModelSnapshot? model = null,
        string? partPath = null,
        ValidationReport? prevalidated = null)
    {
        var report = prevalidated ?? new ProfileValidator().Validate(profile);
        if (report.HasErrors) return (null, report);

        model ??= ModelSnapshot.Empty;
        var variables = VariableExpander.BuildDefault(profile, partPath ?? model.FullPath);
        var sheets = new List<SheetPlan>();
        var sheetArray = JsonNavigator.GetArray(profile.Root, "$.job.sheetPlan")!;
        var titleCells = LoadTitleBlockCells(profile);

        foreach (var sheetNode in sheetArray)
        {
            var sheet = sheetNode!.AsObject();
            var format = sheet["format"]?.GetValue<string?>() ?? "A3";
            var orientation = sheet["orientation"]?.GetValue<string?>() ?? "landscape";
            var requestedScale = sheet["scale"]?.GetValue<string?>() ?? "auto";
            var resolvedScale = ResolveSheetScale(requestedScale, format, orientation, model);
            var views = new List<ViewPlan>();

            foreach (var viewNode in sheet["views"]?.AsArray() ?? [])
            {
                var view = viewNode!.AsObject();
                var placement = view["placement"]?.AsObject();
                var scale = view["scale"]?.AsObject();
                var section = view["section"]?.AsObject();
                var inheritSheet = scale?["inheritSheet"]?.GetValue<bool?>() ?? true;
                var explicitScale = scale?["explicit"]?.GetValue<string?>();
                if (inheritSheet && string.IsNullOrWhiteSpace(explicitScale)
                    && scale?["relativeToMain"]?.GetValue<double?>() is null)
                    explicitScale = resolvedScale;

                views.Add(new ViewPlan(
                    view["id"]?.GetValue<string>() ?? throw new InvalidDataException("View id отсутствует."),
                    view["type"]?.GetValue<string>() ?? "base",
                    view["name"]?.GetValue<string?>() ?? view["id"]?.GetValue<string>() ?? "Вид",
                    view["parentViewId"]?.GetValue<string?>(),
                    view["direction"]?.GetValue<string?>(),
                    view["modelView"]?.GetValue<string?>(),
                    view["fallbackModelView"]?.GetValue<string?>(),
                    new PlacementPlan(
                        placement?["mode"]?.GetValue<string?>() ?? "auto",
                        placement?["preferredAnchor"]?.GetValue<string?>(),
                        placement?["gapMm"]?.GetValue<double?>() ?? 20.0,
                        placement?["x"]?.GetValue<double?>(),
                        placement?["y"]?.GetValue<double?>()),
                    new ScalePlan(
                        inheritSheet,
                        scale?["relativeToMain"]?.GetValue<double?>(),
                        explicitScale),
                    view["hiddenLines"]?.GetValue<string?>() ?? "removed",
                    view["inheritFlatPatternPmi"]?.GetValue<bool?>() ?? false,
                    section?["datumPlaneName"]?.GetValue<string?>(),
                    section?["direction"]?.GetValue<string?>()));
            }

            sheets.Add(new SheetPlan(
                sheet["id"]?.GetValue<string>() ?? throw new InvalidDataException("Sheet id отсутствует."),
                sheet["role"]?.GetValue<string?>() ?? "main",
                sheet["templateId"]?.GetValue<string?>() ?? string.Empty,
                format,
                orientation,
                resolvedScale,
                views,
                titleCells));
        }

        sheets = OrderViewsByDependencies(sheets, report).ToList();
        if (report.HasErrors) return (null, report);

        var plan = new DrawingPlan(
            profile.ProfileId,
            JsonNavigator.GetString(profile.Root, "$.job.documentKind", "part_drawing")!,
            JsonNavigator.GetString(profile.Root, "$.job.document.designation", "UNNAMED")!,
            JsonNavigator.GetString(profile.Root, "$.job.document.name", "Без наименования")!,
            sheets,
            variables,
            JsonNavigator.GetBool(profile.Root, "$.execution.dryRun"))
        {
            Model = model,
            ExecutionPolicy = CompileExecutionPolicy(profile),
            Publication = CompilePublicationPlan(profile)
        };
        plan = plan with { Operations = BuildOperations(profile, plan) };
        return (plan, report);
    }

    private static IReadOnlyList<SheetPlan> OrderViewsByDependencies(
        IReadOnlyList<SheetPlan> sheets,
        ValidationReport report)
    {
        var nodes = sheets.SelectMany((sheet, sheetIndex) =>
                sheet.Views.Select(view => new ViewNode(view, sheet.Id, sheetIndex)))
            .ToDictionary(x => x.View.Id, StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string id, Stack<string> path)
        {
            if (!nodes.TryGetValue(id, out var node)) return;
            if (state.TryGetValue(id, out var currentState))
            {
                if (currentState == 2) return;
                if (currentState == 1)
                {
                    var cycle = string.Join(" -> ", path.Reverse().Append(id));
                    if (reportedCycles.Add(cycle))
                        report.Add(new("CFG_DEPENDENCY_CYCLE", IssueSeverity.Error,
                            "Обнаружен цикл зависимостей видов: " + cycle, ObjectId: id));
                    return;
                }
            }

            state[id] = 1;
            path.Push(id);
            if (!string.IsNullOrWhiteSpace(node.View.ParentViewId)
                && nodes.TryGetValue(node.View.ParentViewId, out var parent))
            {
                if (parent.SheetIndex > node.SheetIndex)
                    report.Add(new("CFG_PARENT_VIEW_LATER_SHEET", IssueSeverity.Error,
                        $"Вид '{node.View.Id}' на листе '{node.SheetId}' зависит от родителя '{parent.View.Id}' на более позднем листе '{parent.SheetId}'.",
                        ObjectId: node.View.Id, SheetId: node.SheetId));
                Visit(parent.View.Id, path);
            }
            path.Pop();
            state[id] = 2;
            order.Add(id);
        }

        foreach (var id in nodes.Keys) Visit(id, new Stack<string>());
        var rank = order.Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index, StringComparer.OrdinalIgnoreCase);
        return sheets.Select(sheet => sheet with
        {
            Views = sheet.Views.OrderBy(view => rank.TryGetValue(view.Id, out var index) ? index : int.MaxValue).ToArray()
        }).ToArray();
    }

    private static IReadOnlyList<DrawingOperation> BuildOperations(ProfileDocument profile, DrawingPlan plan)
    {
        var operations = new List<DrawingOperation>();
        var flatPatternRequired = plan.DocumentKind.Equals("sheet_metal_drawing", StringComparison.OrdinalIgnoreCase)
                                  || plan.Sheets.SelectMany(x => x.Views).Any(x => x.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase));
        if (flatPatternRequired)
            operations.Add(Operation("feature:flat_pattern", "FLAT_PATTERN", "feature", "ensure", [],
                ["sheet_metal_capability", "stationary_face_resolved", "x_direction_resolved"]));

        foreach (var sheet in plan.Sheets)
        {
            var sheetOperation = "sheet:" + sheet.Id;
            operations.Add(Operation(sheetOperation, sheet.Id, "sheet", "ensure", [],
                ["template_available", "format_supported"]));
            operations.Add(Operation("title_block:" + sheet.Id, sheet.Id, "title_block", "update",
                [sheetOperation], ["required_title_values_resolved"]));

            foreach (var view in sheet.Views)
            {
                var dependencies = new List<string> { sheetOperation };
                if (!string.IsNullOrWhiteSpace(view.ParentViewId)) dependencies.Add("view:" + view.ParentViewId);
                if (view.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase)) dependencies.Add("feature:flat_pattern");
                operations.Add(Operation("view:" + view.Id, view.Id, "view", "ensure", dependencies,
                    ["source_model_view_resolved", "scale_resolved", "placement_resolved"],
                    new Dictionary<string, object?>
                    {
                        ["type"] = view.Type,
                        ["sheetId"] = sheet.Id,
                        ["modelView"] = view.ModelView,
                        ["scale"] = view.Scale.ExplicitScale,
                        ["parentViewId"] = view.ParentViewId,
                        ["direction"] = view.ProjectionDirection
                    }));
            }
        }

        var viewOperations = operations.Where(x => x.ObjectKind == "view").Select(x => x.OperationId).ToArray();
        if (JsonNavigator.GetBool(profile.Root, "$.pmiInheritance.enabled", true))
            operations.Add(Operation("pmi:inherit", "PMI", "pmi", "reconcile", viewOperations,
                ["pmi_capability", "source_identity_resolved"]));
        if (plan.DocumentKind.Equals("assembly_drawing", StringComparison.OrdinalIgnoreCase)
            && JsonNavigator.GetBool(profile.Root, "$.partsListAndBalloons.partsList.enabled", true))
            operations.Add(Operation("parts_list:AUTO_PARTS_LIST", "AUTO_PARTS_LIST", "parts_list", "reconcile",
                viewOperations, ["drafting_license", "assembly_snapshot_available"]));
        if (JsonNavigator.GetBool(profile.Root, "$.technicalRequirements.enabled", true))
            operations.Add(Operation("note:TECHNICAL_REQUIREMENTS", "TECHNICAL_REQUIREMENTS", "note", "reconcile",
                plan.Sheets.Select(x => "sheet:" + x.Id).ToArray(), ["tokens_resolved"]));
        if (JsonNavigator.GetBool(profile.Root, "$.sheetMetalFlatPattern.bendTable.enabled", false))
            operations.Add(Operation("table:BEND_TABLE", "BEND_TABLE", "table", "reconcile",
                flatPatternRequired ? ["feature:flat_pattern"] : [], ["bend_data_available"]));

        var mutationIds = operations.Select(x => x.OperationId).ToArray();
        operations.Add(Operation("validation:postconditions", "drawing", "validation", "validate",
            mutationIds, ["nx_update_completed"]));
        if (JsonNavigator.GetBool(profile.Root, "$.output.savePart", true)
            || JsonNavigator.GetBool(profile.Root, "$.output.pdf.enabled", true)
            || JsonNavigator.GetBool(profile.Root, "$.sheetMetalFlatPattern.dxfExport.enabled", false))
            operations.Add(Operation("output:publish", "drawing", "output", "publish",
                ["validation:postconditions"], ["no_blocking_diagnostics"]));
        return operations;
    }

    private static DrawingOperation Operation(
        string operationId,
        string targetId,
        string objectKind,
        string changeKind,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<string> preconditions,
        IReadOnlyDictionary<string, object?>? payload = null)
        => new(operationId, targetId, objectKind, changeKind, dependencies, preconditions,
            payload ?? new Dictionary<string, object?>());

    private static string ResolveSheetScale(
        string requested,
        string format,
        string orientation,
        ModelSnapshot model)
    {
        if (!requested.Equals("auto", StringComparison.OrdinalIgnoreCase)) return requested;
        if (model.BoundingBox is null || model.BoundingBox.SizeX <= 0 || model.BoundingBox.SizeY <= 0) return "1:1";

        var (sheetWidth, sheetHeight) = SheetSize(format, orientation);
        var usableWidth = Math.Max(1, sheetWidth - 45);
        var usableHeight = Math.Max(1, sheetHeight - 40);
        var maxScale = Math.Min(usableWidth / model.BoundingBox.SizeX, usableHeight / model.BoundingBox.SizeY);
        foreach (var candidate in ScaleCandidates)
        {
            if (candidate.Factor <= maxScale) return candidate.Text;
        }
        return "1:100";
    }

    private static (double Width, double Height) SheetSize(string format, string orientation)
    {
        var result = format.ToUpperInvariant() switch
        {
            "A0" => (1189.0, 841.0),
            "A1" => (841.0, 594.0),
            "A2" => (594.0, 420.0),
            "A3" => (420.0, 297.0),
            _ => (297.0, 210.0)
        };
        if (orientation.Equals("portrait", StringComparison.OrdinalIgnoreCase) && result.Item1 > result.Item2)
            return (result.Item2, result.Item1);
        return result;
    }

    private static IReadOnlyList<TitleBlockValuePlan> LoadTitleBlockCells(ProfileDocument profile)
    {
        var active = JsonNavigator.GetString(profile.Root, "$.titleBlocks.activeDefinition");
        var definitions = JsonNavigator.GetArray(profile.Root, "$.titleBlocks.definitions") ?? [];
        var definition = definitions.Select(x => x?.AsObject()).FirstOrDefault(x => string.Equals(x?["id"]?.GetValue<string?>(), active, StringComparison.OrdinalIgnoreCase));
        var result = new List<TitleBlockValuePlan>();
        foreach (var cellNode in definition?["cells"]?.AsArray() ?? [])
        {
            var cell = cellNode!.AsObject();
            var value = cell["value"]?.AsObject();
            result.Add(new(
                cell["label"]?.GetValue<string>() ?? string.Empty,
                cell["required"]?.GetValue<bool?>() ?? false,
                value?["source"]?.GetValue<string?>(),
                value?["name"]?.GetValue<string?>() ?? value?["function"]?.GetValue<string?>(),
                value?["path"]?.GetValue<string?>() ?? value?["fallbackPath"]?.GetValue<string?>(),
                value?["value"]?.GetValue<string?>()));
        }
        return result;
    }

    private static DrawingExecutionPolicy CompileExecutionPolicy(ProfileDocument profile)
        => new(
            JsonNavigator.GetBool(profile.Root, "$.execution.preserveManualObjects", true),
            JsonNavigator.GetBool(profile.Root, "$.execution.preserveManualViewPositions", true),
            JsonNavigator.GetBool(profile.Root, "$.execution.preserveManualDimensions", true),
            JsonNavigator.GetBool(profile.Root, "$.execution.preserveManualNotes", true),
            JsonNavigator.GetBool(profile.Root, "$.execution.idempotency.deleteManagedObjectsMissingFromConfig", false),
            JsonNavigator.GetBool(profile.Root, "$.execution.idempotency.confirmManagedDeletion", false));

    private static DrawingPublicationPlan CompilePublicationPlan(ProfileDocument profile)
        => new(
            JsonNavigator.GetBool(profile.Root, "$.output.savePart", true),
            JsonNavigator.GetBool(profile.Root, "$.output.updateBeforeSave", true),
            JsonNavigator.GetString(profile.Root, "$.output.saveMode", "save_current_or_save_as")!,
            JsonNavigator.GetBool(profile.Root, "$.output.nativeDrawing.enabled", false),
            JsonNavigator.GetString(profile.Root, "$.output.nativeDrawing.file"),
            JsonNavigator.GetBool(profile.Root, "$.output.allowOverwriteExisting", false),
            JsonNavigator.GetBool(profile.Root, "$.output.allowOverwriteReleasedDocument", false),
            JsonNavigator.GetBool(profile.Root, "$.output.pdf.enabled", false),
            JsonNavigator.GetString(profile.Root, "$.output.pdf.file"),
            JsonNavigator.GetBool(profile.Root, "$.sheetMetalFlatPattern.dxfExport.enabled", false),
            JsonNavigator.GetString(profile.Root, "$.sheetMetalFlatPattern.dxfExport.file"));

    private readonly record struct ScaleCandidate(double Factor, string Text);
    private sealed record ViewNode(ViewPlan View, string SheetId, int SheetIndex);
}
