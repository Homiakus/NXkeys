using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxOperationPreconditionEvaluator(NxServiceContext context, DrawingPlan plan)
{
    private bool _nxUpdateCompleted;

    public void MarkNxUpdateCompleted() => _nxUpdateCompleted = true;

    public bool Validate(DrawingOperation operation)
    {
        var valid = true;
        foreach (var precondition in operation.Preconditions)
        {
            var result = Evaluate(operation, precondition);
            context.Report.Metrics[$"precondition.{operation.OperationId}.{precondition}"] = result.Success;
            if (result.Success) continue;

            valid = false;
            context.Report.Issues.Add(new(
                "NX_OPERATION_PRECONDITION_FAILED",
                IssueSeverity.Error,
                $"Операция '{operation.OperationId}' не может быть выполнена: precondition '{precondition}' не подтверждено. {result.Message}",
                ObjectId: operation.TargetId));
        }
        return valid;
    }

    private EvaluationResult Evaluate(DrawingOperation operation, string precondition)
        => precondition.ToLowerInvariant() switch
        {
            DrawingOperationPreconditions.SheetMetalCapability => Result(
                plan.Model.IsSheetMetal,
                "Анализ модели не подтвердил листовой металл."),
            DrawingOperationPreconditions.StationaryFaceResolved => Result(
                HasAnyGeometry("Faces", "GetFaces"),
                "В телах модели не найдена грань-кандидат для стационарной грани Flat Pattern."),
            DrawingOperationPreconditions.XDirectionResolved => Result(
                HasAnyGeometry("Edges", "GetEdges"),
                "В телах модели не найдено ребро-кандидат для направления X Flat Pattern."),
            DrawingOperationPreconditions.TemplateAvailable => TemplateAvailable(operation),
            DrawingOperationPreconditions.FormatSupported => FormatSupported(operation),
            DrawingOperationPreconditions.RequiredTitleValuesResolved => RequiredTitleValuesConfigured(operation),
            DrawingOperationPreconditions.SourceModelViewResolved => SourceModelViewResolved(operation),
            DrawingOperationPreconditions.ScaleResolved => ScaleResolved(operation),
            DrawingOperationPreconditions.PlacementResolved => PlacementResolved(operation),
            DrawingOperationPreconditions.PmiCapability => Result(
                !context.Report.Issues.Any(issue => issue.Code == "NX_CAPABILITY_PMI_MISSING"),
                "Preflight не подтвердил NX API наследования PMI."),
            DrawingOperationPreconditions.SourceIdentityResolved => Result(
                !string.IsNullOrWhiteSpace(context.WorkPart.FullPath),
                "WorkPart не сохранён и не имеет устойчивого пути."),
            DrawingOperationPreconditions.DraftingLicense => Result(
                !context.Report.Issues.Any(issue => issue.Code == "NX_LICENSE_UNAVAILABLE"),
                "NX preflight сообщил об отсутствии требуемой лицензии."),
            DrawingOperationPreconditions.AssemblySnapshotAvailable => Result(
                plan.Model.IsAssembly,
                "ModelSnapshot не подтверждает сборочную структуру."),
            DrawingOperationPreconditions.TokensResolved => TokensResolved(),
            DrawingOperationPreconditions.BendDataAvailable => Result(
                JsonNavigator.GetArray(context.Profile.Root, "$.job.sheetMetalFlatPattern.bends") is { Count: > 0 },
                "Не заданы подтверждённые строки $.job.sheetMetalFlatPattern.bends."),
            DrawingOperationPreconditions.NxUpdateCompleted => Result(
                _nxUpdateCompleted,
                "NX UpdateManager ещё не завершил обновление в текущей транзакции."),
            DrawingOperationPreconditions.NoBlockingDiagnostics => Result(
                !context.Report.Issues.Any(issue => issue.Severity == IssueSeverity.Error),
                "В отчёте уже присутствует блокирующая диагностика."),
            _ => Result(false, "Неизвестное precondition не имеет runtime-обработчика.")
        };

    private EvaluationResult TemplateAvailable(DrawingOperation operation)
    {
        var sheet = FindSheet(operation);
        if (sheet is null) return Result(false, "Не найден SheetPlan операции.");
        var templates = JsonNavigator.GetArray(context.Profile.Root, "$.templateCatalog.templates") ?? [];
        var template = templates.Select(node => node?.AsObject()).FirstOrDefault(node =>
            string.Equals(node?["id"]?.GetValue<string?>(), sheet.TemplateId, StringComparison.OrdinalIgnoreCase));
        var raw = template?["file"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(raw)) return Result(false, $"Шаблон '{sheet.TemplateId}' не задаёт файл.");

        try
        {
            var variables = VariableExpander.BuildDefault(context.Profile, context.WorkPart.FullPath,
                context.RootDirectory);
            var expanded = new VariableExpander(variables).Expand(raw, false);
            var candidates = new[]
            {
                Path.GetFullPath(expanded, context.Profile.BaseDirectory),
                Path.GetFullPath(expanded, context.RootDirectory),
                Path.Combine(context.RootDirectory, "templates", Path.GetFileName(expanded))
            };
            return Result(candidates.Any(File.Exists),
                $"Файл шаблона '{expanded}' не найден ни относительно профиля, ни относительно NX_ESKD_ROOT.");
        }
        catch (Exception ex)
        {
            return Result(false, "Ошибка разрешения пути шаблона: " + ex.Message);
        }
    }

    private EvaluationResult FormatSupported(DrawingOperation operation)
    {
        var sheet = FindSheet(operation);
        return sheet is null
            ? Result(false, "Не найден SheetPlan операции.")
            : Result(sheet.Format is "A0" or "A1" or "A2" or "A3" or "A4",
                $"Формат '{sheet.Format}' не поддерживается runtime.");
    }

    private EvaluationResult RequiredTitleValuesConfigured(DrawingOperation operation)
    {
        var sheet = FindSheet(operation);
        if (sheet is null) return Result(false, "Не найден SheetPlan операции.");
        var incomplete = sheet.TitleBlockValues.Where(value => value.Required)
            .Where(value => string.IsNullOrWhiteSpace(value.SourceKind)
                            && string.IsNullOrWhiteSpace(value.SourceName)
                            && string.IsNullOrWhiteSpace(value.JsonPath)
                            && string.IsNullOrWhiteSpace(value.LiteralValue))
            .Select(value => value.Label)
            .ToArray();
        return Result(incomplete.Length == 0,
            incomplete.Length == 0
                ? string.Empty
                : "Не настроены источники обязательных ячеек: " + string.Join(", ", incomplete));
    }

    private EvaluationResult SourceModelViewResolved(DrawingOperation operation)
    {
        var view = FindView(operation);
        if (view is null) return Result(false, "Не найден ViewPlan операции.");
        if (DrawingViewKinds.IsDependent(view.Type)
            || view.Type.Equals(DrawingViewKinds.FlatPattern, StringComparison.OrdinalIgnoreCase))
            return Result(true, string.Empty);

        var candidates = new[] { view.ModelView, view.FallbackModelView }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var resolved = candidates.Any(candidate => plan.Model.ModelViews.Contains(candidate,
            StringComparer.OrdinalIgnoreCase));
        return Result(resolved,
            candidates.Length == 0
                ? "Для независимого вида не задан modelView/fallbackModelView."
                : "В ModelSnapshot отсутствуют model views: " + string.Join(", ", candidates));
    }

    private EvaluationResult ScaleResolved(DrawingOperation operation)
    {
        var view = FindView(operation);
        if (view is null) return Result(false, "Не найден ViewPlan операции.");
        if (view.Scale.RelativeToMain is > 0) return Result(true, string.Empty);
        return Result(TryParseScale(view.Scale.ExplicitScale, out _),
            $"Масштаб вида {view.Id} не разрешён в положительное отношение.");
    }

    private EvaluationResult PlacementResolved(DrawingOperation operation)
    {
        var view = FindView(operation);
        if (view is null) return Result(false, "Не найден ViewPlan операции.");
        var mode = view.Placement.Mode.ToLowerInvariant();
        var valid = mode switch
        {
            "fixed" => view.Placement.X is not null && view.Placement.Y is not null,
            "auto" or "aligned_auto" => true,
            _ => false
        };
        return Result(valid,
            $"Режим размещения '{view.Placement.Mode}' не разрешён или не содержит координат.");
    }

    private EvaluationResult TokensResolved()
    {
        var variables = VariableExpander.BuildDefault(context.Profile, context.WorkPart.FullPath,
            context.RootDirectory);
        var expander = new VariableExpander(variables);
        var unresolved = EnumerateStrings(JsonNavigator.Get(context.Profile.Root, "$.technicalRequirements"))
            .Concat(EnumerateStrings(JsonNavigator.Get(context.Profile.Root, "$.job.technicalRequirements")))
            .Select(value => expander.Expand(value, false))
            .Where(value => Regex.IsMatch(value, @"\$\{[^}]+\}|\{\{[^}]+\}\}"))
            .Take(5)
            .ToArray();
        return Result(unresolved.Length == 0,
            unresolved.Length == 0
                ? string.Empty
                : "Остались неразрешённые токены: " + string.Join(" | ", unresolved));
    }

    private bool HasAnyGeometry(params string[] collectionNames)
    {
        foreach (var body in NxReflection.Enumerate(NxReflection.Get(context.WorkPart, "Bodies")))
            if (NxReflection.Enumerate(NxReflection.GetOrInvoke(body, collectionNames)).Any())
                return true;
        return false;
    }

    private SheetPlan? FindSheet(DrawingOperation operation)
    {
        if (operation.Payload.TryGetValue("sheetId", out var payloadSheetId)
            && payloadSheetId is not null)
        {
            var byPayload = plan.Sheets.FirstOrDefault(sheet =>
                sheet.Id.Equals(payloadSheetId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (byPayload is not null) return byPayload;
        }

        return plan.Sheets.FirstOrDefault(sheet =>
            sheet.Id.Equals(operation.TargetId, StringComparison.OrdinalIgnoreCase)
            || sheet.Views.Any(view => view.Id.Equals(operation.TargetId, StringComparison.OrdinalIgnoreCase)));
    }

    private ViewPlan? FindView(DrawingOperation operation)
        => plan.Sheets.SelectMany(sheet => sheet.Views).FirstOrDefault(view =>
            view.Id.Equals(operation.TargetId, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EnumerateStrings(JsonNode? node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                yield return text;
                break;
            case JsonObject obj:
                foreach (var child in obj.SelectMany(pair => EnumerateStrings(pair.Value)))
                    yield return child;
                break;
            case JsonArray array:
                foreach (var child in array.SelectMany(EnumerateStrings))
                    yield return child;
                break;
        }
    }

    private static bool TryParseScale(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var parts = raw.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0].Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var numerator)
            || !double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var denominator)
            || numerator <= 0 || denominator <= 0)
            return false;
        value = numerator / denominator;
        return double.IsFinite(value) && value > 0;
    }

    private static EvaluationResult Result(bool success, string message)
        => new(success, message);

    private sealed record EvaluationResult(bool Success, string Message);
}
