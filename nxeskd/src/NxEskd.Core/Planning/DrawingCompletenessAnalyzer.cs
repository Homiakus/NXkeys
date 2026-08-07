using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Planning;

public sealed class DrawingCompletenessAnalyzer
{
    public ValidationReport Analyze(DrawingPlan plan)
    {
        var report = new ValidationReport();
        var views = plan.Sheets.SelectMany(sheet => sheet.Views).ToArray();
        if (views.Length == 0)
        {
            report.Add(new("PLAN_NO_DRAWING_VIEWS", IssueSeverity.Error,
                "План не содержит ни одного чертёжного вида."));
            return report;
        }

        foreach (var view in views.Where(view => !DrawingViewKinds.IsRuntimeSupported(view.Type)))
            report.Add(new("PLAN_VIEW_TYPE_UNSUPPORTED", IssueSeverity.Error,
                $"Тип вида '{view.Type}' для {view.Id} не имеет подтверждённой реализации NX Runtime и не может быть заменён базовым видом.",
                ObjectId: view.Id,
                SuggestedFix: "Используйте поддерживаемый тип вида либо реализуйте отдельный NX adapter и station fixture."));

        var sheetByView = plan.Sheets
            .SelectMany(sheet => sheet.Views.Select(view => (view.Id, SheetId: sheet.Id)))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().SheetId, StringComparer.OrdinalIgnoreCase);
        var viewById = views
            .GroupBy(view => view.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var baseViews = views.Where(IsIndependentView).ToArray();
        if (baseViews.Length == 0)
            report.Add(new("PLAN_BASE_VIEW_MISSING", IssueSeverity.Error,
                "План содержит только зависимые виды; отсутствует главный или иной независимый вид."));

        foreach (var sheet in plan.Sheets.Where(sheet => sheet.Views.Count == 0))
            report.Add(new("PLAN_EMPTY_SHEET", IssueSeverity.Warning,
                $"Лист {sheet.Id} не содержит видов. Проверьте, нужен ли он в документе.", SheetId: sheet.Id));

        if (plan.Model.BodyCount > 0 && IsSpatialModel(plan.Model) && views.Length == 1)
            report.Add(new("PLAN_VIEW_SET_POSSIBLY_INSUFFICIENT", IssueSeverity.ManualReview,
                "Объёмная модель представлена одним видом. Проверьте, однозначно ли он определяет форму изделия.",
                ObjectId: views[0].Id,
                SuggestedFix: "Добавьте проекционный вид, разрез или местный вид только при наличии информационной необходимости."));

        var dependentViews = views.Where(view => DrawingViewKinds.IsDependent(view.Type)).ToArray();
        foreach (var view in dependentViews)
        {
            if (string.IsNullOrWhiteSpace(view.ParentViewId))
            {
                report.Add(new("PLAN_DEPENDENT_VIEW_PARENT_MISSING", IssueSeverity.Error,
                    $"Зависимый вид {view.Id} типа {view.Type} не имеет parentViewId.", ObjectId: view.Id));
                continue;
            }

            if (!viewById.ContainsKey(view.ParentViewId))
            {
                report.Add(new("PLAN_DEPENDENT_VIEW_PARENT_NOT_FOUND", IssueSeverity.Error,
                    $"Родительский вид {view.ParentViewId} для {view.Id} отсутствует в плане.", ObjectId: view.Id));
                continue;
            }

            if (sheetByView.TryGetValue(view.Id, out var childSheet)
                && sheetByView.TryGetValue(view.ParentViewId, out var parentSheet)
                && !childSheet.Equals(parentSheet, StringComparison.OrdinalIgnoreCase))
                report.Add(new("PLAN_DEPENDENT_VIEW_CROSS_SHEET", IssueSeverity.Error,
                    $"Зависимый вид {view.Id} находится на листе {childSheet}, а его parent {view.ParentViewId} — на {parentSheet}.",
                    SheetId: childSheet,
                    ObjectId: view.Id,
                    SuggestedFix: "Разместите parent и dependent view на одном листе либо создайте независимый model view."));
        }

        var projected = views.Where(view => view.Type.Equals(DrawingViewKinds.Projected, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var view in projected.Where(view => string.IsNullOrWhiteSpace(view.ProjectionDirection)))
            report.Add(new("PLAN_PROJECTED_DIRECTION_MISSING", IssueSeverity.Error,
                $"Проекционный вид {view.Id} не имеет направления проецирования.", ObjectId: view.Id));

        if (plan.Model.PmiCount == 0)
            report.Add(new("PLAN_DIMENSION_SOURCE_MISSING", IssueSeverity.ManualReview,
                "В модели нет PMI. Плагин создаст листы и виды, но не может доказать полноту размерной схемы.",
                SuggestedFix: "Добавьте конструкторский PMI или выполните ручное нанесение и проверку размеров."));

        if (plan.DocumentKind.Equals("sheet_metal_drawing", StringComparison.OrdinalIgnoreCase)
            && !views.Any(view => view.Type.Equals(DrawingViewKinds.FlatPattern, StringComparison.OrdinalIgnoreCase)))
            report.Add(new("PLAN_FLAT_PATTERN_VIEW_MISSING", IssueSeverity.Error,
                "Чертёж листовой детали не содержит вида развертки."));

        if (plan.DocumentKind.Equals("assembly_drawing", StringComparison.OrdinalIgnoreCase))
        {
            if (!plan.Operations.Any(operation => operation.ObjectKind.Equals("parts_list", StringComparison.OrdinalIgnoreCase)))
                report.Add(new("PLAN_ASSEMBLY_PARTS_LIST_MISSING", IssueSeverity.ManualReview,
                    "Сборочный чертёж не содержит операции формирования спецификации."));
            if (!views.Any(view => view.Type.Equals(DrawingViewKinds.Base, StringComparison.OrdinalIgnoreCase)))
                report.Add(new("PLAN_ASSEMBLY_BASE_VIEW_MISSING", IssueSeverity.Error,
                    "Сборочный чертёж не содержит основного вида сборки."));
        }

        var duplicateDirections = projected
            .Where(view => !string.IsNullOrWhiteSpace(view.ParentViewId)
                           && !string.IsNullOrWhiteSpace(view.ProjectionDirection))
            .GroupBy(view => $"{view.ParentViewId}|{view.ProjectionDirection}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);
        foreach (var group in duplicateDirections)
            report.Add(new("PLAN_DUPLICATE_PROJECTION_DIRECTION", IssueSeverity.Warning,
                $"Для одной проекционной связи запланировано несколько видов: {string.Join(", ", group.Select(view => view.Id))}."));

        return report;
    }

    private static bool IsIndependentView(ViewPlan view)
        => view.Type.Equals(DrawingViewKinds.Base, StringComparison.OrdinalIgnoreCase)
           || view.Type.Equals(DrawingViewKinds.FlatPattern, StringComparison.OrdinalIgnoreCase)
           || !DrawingViewKinds.IsDependent(view.Type);

    private static bool IsSpatialModel(ModelSnapshot model)
    {
        if (model.BoundingBox is null) return true;
        var dimensions = new[]
        {
            model.BoundingBox.SizeX,
            model.BoundingBox.SizeY,
            model.BoundingBox.SizeZ
        }.Where(value => value > 0.001).OrderByDescending(value => value).ToArray();
        return dimensions.Length >= 3 && dimensions[^1] / dimensions[0] > 0.02;
    }
}
