using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxValidationService(NxServiceContext context)
{
    private readonly Dictionary<string, Action<DrawingPlan, ValidationReport>> _rules =
        new(StringComparer.OrdinalIgnoreCase);

    public ValidationReport Validate(DrawingPlan plan)
    {
        var report = new ValidationReport();
        RegisterRules();

        if (context.WorkPart is null)
        {
            report.Add(new("NX_NO_WORK_PART", IssueSeverity.Error, "Рабочая деталь не открыта."));
            return report;
        }

        var configured = LoadConfiguredRules();
        foreach (var rule in configured.Where(x => x.Enabled))
        {
            if (!_rules.TryGetValue(rule.Id, out var execute))
            {
                report.Add(new("VAL_RULE_UNIMPLEMENTED", IssueSeverity.ManualReview,
                    $"Проверка '{rule.Id}' включена в профиле, но для неё нет подтверждённой NX-реализации. " +
                    "Она не считается пройденной и требует ручного контроля.",
                    JsonPath: rule.JsonPath,
                    ObjectId: rule.Id,
                    SuggestedFix: "Реализовать правило в NxValidationService и добавить NX integration fixture."));
                continue;
            }

            var local = new ValidationReport();
            try
            {
                execute(plan, local);
            }
            catch (Exception ex)
            {
                local.Add(new("VAL_RULE_EXECUTION_FAILED", IssueSeverity.Error,
                    $"Проверка '{rule.Id}' завершилась внутренней ошибкой: {ex.Message}",
                    JsonPath: rule.JsonPath,
                    ObjectId: rule.Id));
            }

            foreach (var issue in local.Issues)
                report.Add(issue with { Severity = MaxSeverity(issue.Severity, rule.Severity) });
        }

        var maximumWarnings = JsonNavigator.GetInt(context.Profile.Root, "$.validation.maximumWarnings", 50);
        var warningCount = report.Issues.Count(x => x.Severity is IssueSeverity.Warning or IssueSeverity.ManualReview);
        if (maximumWarnings >= 0 && warningCount > maximumWarnings)
            report.Add(new("VAL_WARNING_LIMIT", IssueSeverity.Error,
                $"Количество предупреждений/ручных проверок {warningCount} превышает допустимое {maximumWarnings}."));

        if (JsonNavigator.GetBool(context.Profile.Root, "$.validation.failOnWarnings", false)
            && report.Issues.Any(x => x.Severity is IssueSeverity.Warning or IssueSeverity.ManualReview))
            report.Add(new("VAL_WARNINGS_BLOCK_RELEASE", IssueSeverity.Error,
                "validation.failOnWarnings=true: предупреждения блокируют сохранение и экспорт."));
        return report;
    }

    private void RegisterRules()
    {
        if (_rules.Count > 0) return;
        _rules["VAL_JSON_SCHEMA"] = (_, _) => { }; // Executed by ProfileValidator before model analysis.
        _rules["VAL_TEMPLATE_EXISTS"] = ValidateTemplatesExist;
        _rules["VAL_REQUIRED_TITLE_BLOCK_LABELS"] = ValidateRequiredTitleBlockLabels;
        _rules["VAL_REQUIRED_VALUES"] = ValidateRequiredAttributes;
        _rules["VAL_FORMAT_ALLOWED"] = ValidateFormats;
        _rules["VAL_SCALE_ALLOWED"] = ValidateScales;
        _rules["VAL_OBJECTS_INSIDE_BORDER"] = ValidateObjectsInsideBorder;
        _rules["VAL_VIEW_OVERLAP"] = ValidateViewOverlap;
        _rules["VAL_EMPTY_VIEW"] = ValidateEmptyViews;
        _rules["VAL_OUT_OF_DATE_VIEW"] = ValidateOutOfDateViews;
        _rules["VAL_DIM_ASSOCIATIVITY"] = ValidateAssociativity;
        _rules["VAL_UNRESOLVED_PMI_MAPPING"] = ValidateUnresolvedPmiMappings;
        _rules["VAL_FLAT_PATTERN_VALID"] = ValidateFlatPattern;
        _rules["VAL_TECH_REQ_UNRESOLVED_TOKEN"] = ValidateTechnicalRequirementTokens;
        _rules["VAL_TEXT_TOO_SMALL"] = ValidateTextStyles;
        _rules["VAL_LINE_STYLE_MAPPING"] = ValidateLineStyles;
    }

    private IReadOnlyList<ConfiguredRule> LoadConfiguredRules()
    {
        var nodes = JsonNavigator.GetArray(context.Profile.Root, "$.validation.checks") ?? [];
        return nodes.Select((node, index) =>
        {
            var item = node?.AsObject();
            return new ConfiguredRule(
                item?["id"]?.GetValue<string?>() ?? $"UNKNOWN_{index}",
                item?["enabled"]?.GetValue<bool?>() ?? true,
                ParseSeverity(item?["severity"]?.GetValue<string?>()),
                $"$.validation.checks[{index}]");
        }).ToArray();
    }

    private void ValidateTemplatesExist(DrawingPlan plan, ValidationReport report)
    {
        var templates = JsonNavigator.GetArray(context.Profile.Root, "$.templateCatalog.templates") ?? [];
        var byId = templates.Select(x => x?.AsObject()).Where(x => x is not null)
            .ToDictionary(x => x!["id"]?.GetValue<string?>() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        if (context.WorkPart is null) return;
        var expander = new VariableExpander(VariableExpander.BuildDefault(context.Profile, context.WorkPart.FullPath, context.RootDirectory));
        var envRoot = Environment.GetEnvironmentVariable("NX_ESKD_ROOT");
        foreach (var sheet in plan.Sheets)
        {
            if (!byId.TryGetValue(sheet.TemplateId, out var template)) continue;
            var raw = template?["file"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var expanded = expander.Expand(raw, false);
            var path = Path.GetFullPath(expanded, context.Profile.BaseDirectory);
            if (!File.Exists(path))
            {
                var candidates = new[] { context.RootDirectory, envRoot, AppDomain.CurrentDomain.BaseDirectory }
                    .Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
                foreach (var root in candidates)
                {
                    var candidate = Path.GetFullPath(expanded, root!);
                    if (File.Exists(candidate)) { path = candidate; break; }
                    var byName = Path.Combine(root!, "templates", Path.GetFileName(expanded));
                    if (File.Exists(byName)) { path = byName; break; }
                }
            }
            if (!File.Exists(path))
                report.Add(new("VAL_TEMPLATE_MISSING", IssueSeverity.Error,
                    $"Файл шаблона для листа {sheet.Id} не найден: {path}", SheetId: sheet.Id));
        }
    }

    private void ValidateRequiredTitleBlockLabels(DrawingPlan plan, ValidationReport report)
    {
        var active = JsonNavigator.GetString(context.Profile.Root, "$.titleBlocks.activeDefinition");
        var definitions = JsonNavigator.GetArray(context.Profile.Root, "$.titleBlocks.definitions") ?? [];
        var definition = definitions.Select(x => x?.AsObject()).FirstOrDefault(x =>
            string.Equals(x?["id"]?.GetValue<string?>(), active, StringComparison.OrdinalIgnoreCase));
        var required = definition?["requiredLabels"]?.AsArray()
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray() ?? [];
        var planned = plan.Sheets.SelectMany(x => x.TitleBlockValues).Select(x => x.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var label in required.Where(x => !planned.Contains(x)))
            report.Add(new("VAL_TITLE_BLOCK_LABEL_MISSING", IssueSeverity.Error,
                $"Обязательная метка основной надписи '{label}' отсутствует в плане.", ObjectId: label));
    }

    private void ValidateRequiredAttributes(DrawingPlan plan, ValidationReport report)
    {
        foreach (var name in new[] { "DRAWING_NUMBER", "DRAWING_NAME" })
        {
            var value = NxObjectTools.GetStringAttribute(context.WorkPart, name);
            if (string.IsNullOrWhiteSpace(value))
                report.Add(new("VAL_REQUIRED_ATTRIBUTE", IssueSeverity.Warning, $"Не заполнен атрибут {name}. Рекомендуется заполнить свойства детали.", ObjectId: name));
        }
        if (string.IsNullOrWhiteSpace(plan.Designation))
            report.Add(new("VAL_DESIGNATION", IssueSeverity.Warning, "Обозначение документа не определено в профиле или детали."));
    }

    private void ValidateFormats(DrawingPlan plan, ValidationReport report)
    {
        var allowed = JsonNavigator.GetArray(context.Profile.Root, "$.templateCatalog.selectionRules.allowedFormats")?
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var sheet in plan.Sheets.Where(x => allowed.Count > 0 && !allowed.Contains(x.Format)))
            report.Add(new("VAL_FORMAT_NOT_ALLOWED", IssueSeverity.Error,
                $"Формат {sheet.Format} листа {sheet.Id} отсутствует в allowedFormats.", SheetId: sheet.Id));
    }

    private void ValidateScales(DrawingPlan plan, ValidationReport report)
    {
        var allowed = JsonNavigator.GetArray(context.Profile.Root, "$.scalePolicy.allowed")?
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var sheet in plan.Sheets.Where(x => allowed.Count > 0 && !allowed.Contains(x.Scale)))
            report.Add(new("VAL_SCALE_NOT_ALLOWED", IssueSeverity.Error,
                $"Масштаб {sheet.Scale} листа {sheet.Id} отсутствует в scalePolicy.allowed.", SheetId: sheet.Id));
    }

    private void ValidateObjectsInsideBorder(DrawingPlan plan, ValidationReport report)
    {
        var views = OwnedViews().ToArray();
        foreach (var item in views)
        {
            if (!TryRect(item.Object, out var rect))
            {
                report.Add(new("VAL_BOUNDS_UNAVAILABLE", IssueSeverity.ManualReview,
                    $"NX API не предоставил bounds вида {item.Id}; проверка выхода за рамку требует ручного контроля.", ObjectId: item.Id));
                continue;
            }
            var sheet = plan.Sheets.FirstOrDefault(x => x.Views.Any(v => v.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)));
            if (sheet is null) continue;
            var (width, height) = SheetSize(sheet.Format, sheet.Orientation);
            if (rect is null) continue;
            if (rect.Left < 5 || rect.Bottom < 5 || rect.Right > width - 5 || rect.Top > height - 5)
                report.Add(new("VAL_OBJECT_OUTSIDE_BORDER", IssueSeverity.Error,
                    $"Вид {item.Id} выходит за границы листа {sheet.Id}.", SheetId: sheet.Id, ObjectId: item.Id));
        }
    }

    private void ValidateViewOverlap(DrawingPlan plan, ValidationReport report)
    {
        var items = OwnedViews().Select(x => (x.Id, Rect: TryRect(x.Object, out var rect) ? rect : null)).ToArray();
        for (var i = 0; i < items.Length; i++)
        for (var j = i + 1; j < items.Length; j++)
        {
            if (items[i].Rect is null || items[j].Rect is null) continue;
            if (items[i].Rect!.Intersects(items[j].Rect!))
                report.Add(new("VAL_VIEW_OVERLAP", IssueSeverity.Error,
                    $"Виды {items[i].Id} и {items[j].Id} пересекаются.", ObjectId: items[i].Id));
        }
    }

    private void ValidateEmptyViews(DrawingPlan plan, ValidationReport report)
    {
        foreach (var item in OwnedViews())
        {
            var isEmpty = NxReflection.Get(item.Object, "IsEmpty", "Empty") as bool?;
            var objects = NxReflection.Get(item.Object, "Objects", "DisplayObjects", "VisibleObjects");
            var count = objects is null ? (int?)null : NxReflection.Enumerate(objects).Take(1).Count();
            if (isEmpty == true || count == 0)
                report.Add(new("VAL_EMPTY_VIEW", IssueSeverity.Error,
                    $"Вид {item.Id} не содержит отображаемой геометрии.", ObjectId: item.Id));
        }
    }

    private void ValidateOutOfDateViews(DrawingPlan plan, ValidationReport report)
    {
        foreach (var item in OwnedViews())
        {
            var outOfDate = NxReflection.Get(item.Object, "IsOutOfDate", "OutOfDate") as bool?;
            if (outOfDate == true)
                report.Add(new("VAL_VIEW_OUT_OF_DATE", IssueSeverity.Error,
                    $"Вид {item.Id} не обновлён.", ObjectId: item.Id));
        }
    }

    private void ValidateAssociativity(DrawingPlan plan, ValidationReport report)
    {
        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var dimensions = NxReflection.Get(annotations, "Dimensions", "DimensionCollection");
        foreach (var dimension in NxReflection.Enumerate(dimensions))
        {
            if (!NxObjectTools.IsManaged(dimension, profileId: context.Profile.ProfileId,
                    objectKind: "dimension", scopeId: context.ScopeId)) continue;
            var associative = NxReflection.Get(dimension, "IsAssociative", "Associative") as bool?;
            if (associative == false)
                report.Add(new("VAL_DIM_NOT_ASSOCIATIVE", IssueSeverity.Error,
                    $"Управляемый размер {NxReflection.GetName(dimension) ?? dimension.GetType().Name} не ассоциативен."));
        }
    }

    private void ValidateUnresolvedPmiMappings(DrawingPlan plan, ValidationReport report)
    {
        foreach (var issue in context.Report.Issues.Where(x =>
                     x.Code.Contains("PMI", StringComparison.OrdinalIgnoreCase)
                     && x.Code.Contains("UNRESOLVED", StringComparison.OrdinalIgnoreCase)))
            report.Add(new("VAL_UNRESOLVED_PMI_MAPPING", IssueSeverity.Warning, issue.Message, ObjectId: issue.ObjectId));
    }

    private void ValidateFlatPattern(DrawingPlan plan, ValidationReport report)
    {
        if (!plan.DocumentKind.Equals("sheet_metal_drawing", StringComparison.OrdinalIgnoreCase)
            && !plan.Sheets.SelectMany(x => x.Views).Any(x => x.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase))) return;
        var features = NxReflection.Get(context.WorkPart, "Features");
        var flat = NxReflection.Enumerate(features).FirstOrDefault(x =>
            NxObjectTools.IsManaged(x, "FLAT_PATTERN", context.Profile.ProfileId, "feature", context.ScopeId));
        if (flat is null)
            report.Add(new("VAL_FLAT_PATTERN_MISSING", IssueSeverity.Error,
                "Управляемый Flat Pattern текущего задания отсутствует."));
    }

    private void ValidateTechnicalRequirementTokens(DrawingPlan plan, ValidationReport report)
    {
        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var notes = NxReflection.Get(annotations, "Notes", "DraftingNotes");
        var note = NxReflection.Enumerate(notes).FirstOrDefault(x =>
            NxObjectTools.IsManaged(x, "TECHNICAL_REQUIREMENTS", context.Profile.ProfileId, "note", context.ScopeId));
        if (note is null) return;
        var raw = NxReflection.Get(note, "Text", "TextBlock")?.ToString() ?? string.Empty;
        if (Regex.IsMatch(raw, @"\$\{[^}]+\}|\{\{[^}]+\}\}", RegexOptions.CultureInvariant))
            report.Add(new("VAL_TECH_REQ_UNRESOLVED_TOKEN", IssueSeverity.Error,
                "В технических требованиях остались неразрешённые токены.", ObjectId: "TECHNICAL_REQUIREMENTS"));
    }

    private void ValidateTextStyles(DrawingPlan plan, ValidationReport report)
    {
        var styles = JsonNavigator.GetArray(context.Profile.Root, "$.draftingStyles.textStyles") ?? [];
        foreach (var (node, index) in styles.Select((x, i) => (x, i)))
        {
            var height = node?["heightMm"]?.GetValue<double?>();
            if (height is > 0 and < 2.5)
                report.Add(new("VAL_TEXT_TOO_SMALL", IssueSeverity.Error,
                    $"Стиль {node?["id"]?.GetValue<string?>() ?? index.ToString(CultureInfo.InvariantCulture)} имеет высоту {height} мм меньше 2,5 мм.",
                    JsonPath: $"$.draftingStyles.textStyles[{index}].heightMm"));
        }
    }

    private void ValidateLineStyles(DrawingPlan plan, ValidationReport report)
    {
        var styles = JsonNavigator.GetArray(context.Profile.Root, "$.draftingStyles.lineStyles") ?? [];
        foreach (var (node, index) in styles.Select((x, i) => (x, i)))
        {
            if (string.IsNullOrWhiteSpace(node?["nxLineFont"]?.GetValue<string?>()))
                report.Add(new("VAL_LINE_STYLE_MAPPING", IssueSeverity.Error,
                    $"Стиль линии {node?["id"]?.GetValue<string?>() ?? index.ToString(CultureInfo.InvariantCulture)} не имеет nxLineFont.",
                    JsonPath: $"$.draftingStyles.lineStyles[{index}].nxLineFont"));
        }
    }

    private IEnumerable<(string Id, object Object)> OwnedViews()
    {
        var views = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews"));
        foreach (var view in NxReflection.Enumerate(views))
        {
            if (!NxObjectTools.IsManaged(view, profileId: context.Profile.ProfileId,
                    objectKind: "view", scopeId: context.ScopeId)) continue;
            var id = NxObjectTools.GetStringAttribute(view, "AUTO_DWG_ID") ?? NxReflection.GetName(view) ?? "UNKNOWN_VIEW";
            yield return (id, view);
        }
    }

    private static bool TryRect(object value, out Rect? rect)
    {
        rect = null;
        if (value is null) return false;

        try
        {
            var method = value.GetType().GetMethod("GetBorders", BindingFlags.Instance | BindingFlags.Public);
            if (method is not null)
            {
                var args = new object?[4];
                method.Invoke(value, args);
                var l = TryNumber(args[0]);
                var b = TryNumber(args[1]);
                var r = TryNumber(args[2]);
                var t = TryNumber(args[3]);
                if (l is not null && b is not null && r is not null && t is not null)
                {
                    rect = new Rect(Math.Min(l.Value, r.Value), Math.Min(b.Value, t.Value),
                        Math.Max(l.Value, r.Value), Math.Max(b.Value, t.Value));
                    return true;
                }
            }
        }
        catch { }

        try
        {
            var session = NxReflection.Get(value, "Session");
            var ufSession = NxReflection.Get(session, "UFSession");
            var draw = NxReflection.Get(ufSession, "Draw", "UFDraw");
            var tag = NxReflection.Get(value, "Tag");
            if (draw is not null && tag is not null)
            {
                var borders = new double[4];
                if (NxReflection.InvokeCommand(draw, ["AskViewBorders", "AskBorders"], tag, borders))
                {
                    rect = new Rect(Math.Min(borders[0], borders[2]), Math.Min(borders[1], borders[3]),
                        Math.Max(borders[0], borders[2]), Math.Max(borders[1], borders[3]));
                    return true;
                }
            }
        }
        catch { }

        var raw = NxReflection.Get(value, "Bounds", "BoundingBox", "Box", "ViewBounds")
                  ?? NxReflection.InvokeFactory(value, ["GetBounds", "GetBoundingBox"]);
        if (raw is null) return false;

        if (raw is IEnumerable enumerable and not string)
        {
            var numbers = enumerable.Cast<object?>().Select(TryNumber).Where(x => x is not null).Cast<double>().ToArray();
            if (numbers.Length >= 4)
            {
                rect = new Rect(Math.Min(numbers[0], numbers[2]), Math.Min(numbers[1], numbers[3]),
                    Math.Max(numbers[0], numbers[2]), Math.Max(numbers[1], numbers[3]));
                return true;
            }
        }

        var left = TryNumber(NxReflection.Get(raw, "Left", "MinX", "XMin"));
        var bottom = TryNumber(NxReflection.Get(raw, "Bottom", "MinY", "YMin"));
        var right = TryNumber(NxReflection.Get(raw, "Right", "MaxX", "XMax"));
        var top = TryNumber(NxReflection.Get(raw, "Top", "MaxY", "YMax"));
        if (left is null || bottom is null || right is null || top is null) return false;
        rect = new Rect(Math.Min(left.Value, right.Value), Math.Min(bottom.Value, top.Value),
            Math.Max(left.Value, right.Value), Math.Max(bottom.Value, top.Value));
        return true;
    }

    private static double? TryNumber(object? value)
    {
        if (value is not IConvertible convertible) return null;
        try
        {
            var number = convertible.ToDouble(CultureInfo.InvariantCulture);
            return double.IsFinite(number) ? number : null;
        }
        catch { return null; }
    }

    private static (double Width, double Height) SheetSize(string format, string orientation)
    {
        var result = format.ToUpperInvariant() switch
        {
            "A0" => (1189.0, 841.0), "A1" => (841.0, 594.0), "A2" => (594.0, 420.0),
            "A3" => (420.0, 297.0), _ => (210.0, 297.0)
        };
        if (orientation.Equals("portrait", StringComparison.OrdinalIgnoreCase) && result.Item1 > result.Item2)
            return (result.Item2, result.Item1);
        return result;
    }

    private static IssueSeverity ParseSeverity(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "INFO" => IssueSeverity.Info,
            "AUTOFIX" => IssueSeverity.AutoFix,
            "WARNING" => IssueSeverity.Warning,
            "MANUAL_REVIEW" => IssueSeverity.ManualReview,
            _ => IssueSeverity.Error
        };

    private static IssueSeverity MaxSeverity(IssueSeverity actual, IssueSeverity configured)
        => (IssueSeverity)Math.Max((int)actual, (int)configured);

    private sealed record ConfiguredRule(string Id, bool Enabled, IssueSeverity Severity, string JsonPath);

    private sealed record Rect(double Left, double Bottom, double Right, double Top)
    {
        public bool Intersects(Rect other)
            => Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;
    }
}
