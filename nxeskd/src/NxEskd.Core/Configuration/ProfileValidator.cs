using System.Text.Json.Nodes;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Configuration;

public sealed class ProfileValidator
{
    public const string CurrentSchemaVersion = "1.0.0";

    private static readonly string[] RequiredTopLevel =
    [
        "$schema", "schemaVersion", "documentType", "profileId", "targetEnvironment", "execution", "templateCatalog",
        "titleBlocks", "draftingStyles", "viewGeneration", "pmiInheritance", "validation", "job"
    ];

    public ValidationReport Validate(ProfileDocument profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var report = new ValidationReport();

        // Execute the bundled JSON Schema before semantic validation. The subset validator supports
        // every keyword currently used by nx-eskd-profile.schema.json and preserves JSON paths.
        report.AddRange(new JsonSchemaSubsetValidator().Validate(profile).Issues);

        foreach (var property in RequiredTopLevel)
        {
            if (!profile.Root.ContainsKey(property))
                report.Add(new("CFG_REQUIRED_SECTION", IssueSeverity.Error,
                    $"Отсутствует обязательный раздел '{property}'.", "$." + property));
        }

        RequireString(profile, report, "$.profileId", "CFG_PROFILE_ID");
        RequireString(profile, report, "$.schemaVersion", "CFG_SCHEMA_VERSION");
        RequireString(profile, report, "$.job.jobId", "CFG_JOB_ID");
        RequireString(profile, report, "$.job.documentKind", "CFG_DOCUMENT_KIND");
        RequireString(profile, report, "$.job.document.designation", "CFG_DESIGNATION");
        RequireString(profile, report, "$.job.document.name", "CFG_DOCUMENT_NAME");

        ValidateVersion(profile, report);
        ValidateEnvironment(profile, report);
        ValidateTemplates(profile, report);
        ValidateTitleBlockDefinition(profile, report);
        ValidateStyles(profile, report);
        ValidateSheetPlan(profile, report);
        ValidatePmiMappings(profile, report);
        ValidateManagedLayers(profile, report);
        ValidateExecutionSafety(profile, report);
        return report;
    }

    private static void ValidateVersion(ProfileDocument profile, ValidationReport report)
    {
        if (!Version.TryParse(profile.SchemaVersion, out var actual))
        {
            report.Add(new("CFG_SCHEMA_VERSION_INVALID", IssueSeverity.Error,
                $"Некорректный schemaVersion '{profile.SchemaVersion}'. Ожидается semver-подобная версия {CurrentSchemaVersion}.", "$.schemaVersion"));
            return;
        }

        var current = Version.Parse(CurrentSchemaVersion);
        if (actual.Major != current.Major)
        {
            report.Add(new("CFG_SCHEMA_VERSION_UNSUPPORTED", IssueSeverity.Error,
                $"Версия профиля {actual} несовместима с поддерживаемой {current}. Выполните явную миграцию профиля.",
                "$.schemaVersion", SuggestedFix: $"Мигрировать профиль до {CurrentSchemaVersion}."));
            return;
        }

        if (actual > current)
            report.Add(new("CFG_SCHEMA_VERSION_FUTURE", IssueSeverity.Error,
                $"Профиль версии {actual} новее поддерживаемой {current}. Запуск с неизвестной семантикой запрещён.", "$.schemaVersion"));
        else if (actual < current)
            report.Add(new("CFG_SCHEMA_VERSION_OLDER", IssueSeverity.Warning,
                $"Профиль версии {actual} старше текущей {current}. Рекомендуется выполнить миграцию и сохранить резервную копию.",
                "$.schemaVersion", SuggestedFix: $"Мигрировать профиль до {CurrentSchemaVersion}."));
    }

    private static void ValidateEnvironment(ProfileDocument profile, ValidationReport report)
    {
        var release = JsonNavigator.GetString(profile.Root, "$.targetEnvironment.nxRelease");
        if (!string.Equals(release, "2512", StringComparison.OrdinalIgnoreCase)
            && !(release?.StartsWith("2512:", StringComparison.OrdinalIgnoreCase) ?? false))
            report.Add(new("CFG_NX_RELEASE_UNSUPPORTED", IssueSeverity.Error,
                $"Профиль предназначен для NX '{release}', тогда как этот пакет поддерживает NX 2512 и конкретные MR должны подтверждаться inventory.",
                "$.targetEnvironment.nxRelease"));

        var units = JsonNavigator.GetString(profile.Root, "$.targetEnvironment.units");
        if (units is not ("millimeter" or "inch"))
            report.Add(new("CFG_UNITS_UNSUPPORTED", IssueSeverity.Error,
                $"Неподдерживаемые единицы профиля '{units}'.", "$.targetEnvironment.units"));

        var standard = JsonNavigator.GetString(profile.Root, "$.targetEnvironment.drawingStandard");
        if (!string.Equals(standard, "ESKD", StringComparison.OrdinalIgnoreCase))
            report.Add(new("CFG_DRAWING_STANDARD_UNSUPPORTED", IssueSeverity.Error,
                $"Профиль стандарта '{standard}' не поддерживается данным ESKD runtime.", "$.targetEnvironment.drawingStandard"));
    }

    private static void ValidateExecutionSafety(ProfileDocument profile, ValidationReport report)
    {
        if (!JsonNavigator.GetBool(profile.Root, "$.execution.rollbackOnError", true))
            report.Add(new("CFG_ROLLBACK_REQUIRED", IssueSeverity.Error,
                "execution.rollbackOnError=false запрещён для операций Generate/Update.", "$.execution.rollbackOnError"));
        if (!JsonNavigator.GetBool(profile.Root, "$.execution.preserveManualObjects", true))
            report.Add(new("CFG_MANUAL_OBJECT_PROTECTION_REQUIRED", IssueSeverity.Error,
                "execution.preserveManualObjects=false запрещён: runtime никогда не должен удалять или присваивать ручные объекты.",
                "$.execution.preserveManualObjects"));
        if (!JsonNavigator.GetBool(profile.Root, "$.execution.singleUndoTransaction", true))
            report.Add(new("CFG_SINGLE_UNDO_REQUIRED", IssueSeverity.Error,
                "execution.singleUndoTransaction=false не поддерживается безопасным runtime.", "$.execution.singleUndoTransaction"));
    }

    private static void ValidateTemplates(ProfileDocument profile, ValidationReport report)
    {
        var templates = JsonNavigator.GetArray(profile.Root, "$.templateCatalog.templates");
        if (templates is null || templates.Count == 0)
        {
            report.Add(new("CFG_NO_TEMPLATES", IssueSeverity.Error, "Каталог шаблонов пуст.", "$.templateCatalog.templates"));
            return;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (node, index) in templates.Select((n, i) => (n, i)))
        {
            var obj = node?.AsObject();
            var id = obj?["id"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(id))
                report.Add(new("CFG_TEMPLATE_ID", IssueSeverity.Error, "Шаблон не имеет id.", $"$.templateCatalog.templates[{index}].id"));
            else if (!ids.Add(id))
                report.Add(new("CFG_TEMPLATE_DUPLICATE", IssueSeverity.Error, $"Повторяющийся template id: {id}.", $"$.templateCatalog.templates[{index}].id"));
            if (string.IsNullOrWhiteSpace(obj?["file"]?.GetValue<string?>()))
                report.Add(new("CFG_TEMPLATE_FILE", IssueSeverity.Error, $"Для шаблона {id ?? index.ToString()} не задан файл.", $"$.templateCatalog.templates[{index}].file"));
        }
    }

    private static void ValidateTitleBlockDefinition(ProfileDocument profile, ValidationReport report)
    {
        var active = JsonNavigator.GetString(profile.Root, "$.titleBlocks.activeDefinition");
        var definitions = JsonNavigator.GetArray(profile.Root, "$.titleBlocks.definitions") ?? [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (node, index) in definitions.Select((n, i) => (n, i)))
        {
            var id = node?["id"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(id))
                report.Add(new("CFG_TITLE_BLOCK_ID", IssueSeverity.Error,
                    "Определение основной надписи не имеет id.", $"$.titleBlocks.definitions[{index}].id"));
            else if (!ids.Add(id))
                report.Add(new("CFG_TITLE_BLOCK_DUPLICATE", IssueSeverity.Error,
                    $"Повторяющийся id основной надписи: {id}.", $"$.titleBlocks.definitions[{index}].id"));
        }
        if (string.IsNullOrWhiteSpace(active) || !ids.Contains(active))
            report.Add(new("CFG_TITLE_BLOCK_ACTIVE_MISSING", IssueSeverity.Error,
                $"Активное определение основной надписи '{active}' не найдено.", "$.titleBlocks.activeDefinition"));
    }

    private static void ValidateStyles(ProfileDocument profile, ValidationReport report)
    {
        ValidateUniqueIds(JsonNavigator.GetArray(profile.Root, "$.draftingStyles.textStyles"), "CFG_TEXT_STYLE", "$.draftingStyles.textStyles", report);
        ValidateUniqueIds(JsonNavigator.GetArray(profile.Root, "$.draftingStyles.lineStyles"), "CFG_LINE_STYLE", "$.draftingStyles.lineStyles", report);
    }

    private static void ValidateSheetPlan(ProfileDocument profile, ValidationReport report)
    {
        var sheets = JsonNavigator.GetArray(profile.Root, "$.job.sheetPlan");
        if (sheets is null || sheets.Count == 0)
        {
            report.Add(new("CFG_NO_SHEETS", IssueSeverity.Error, "В задании не определен ни один лист.", "$.job.sheetPlan"));
            return;
        }

        var templateIds = JsonNavigator.GetArray(profile.Root, "$.templateCatalog.templates")?
            .Select(x => x?["id"]?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var allowedScales = JsonNavigator.GetArray(profile.Root, "$.scalePolicy.allowed")?
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var sheetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var viewIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sheetNode, sheetIndex) in sheets.Select((n, i) => (n, i)))
        {
            var sheet = sheetNode?.AsObject();
            var sheetId = sheet?["id"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(sheetId) || !sheetIds.Add(sheetId))
                report.Add(new("CFG_SHEET_ID", IssueSeverity.Error, "ID листа отсутствует или повторяется.", $"$.job.sheetPlan[{sheetIndex}].id"));

            var templateId = sheet?["templateId"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(templateId) && !templateIds.Contains(templateId))
                report.Add(new("CFG_UNKNOWN_TEMPLATE", IssueSeverity.Error, $"Лист ссылается на неизвестный шаблон {templateId}.", $"$.job.sheetPlan[{sheetIndex}].templateId", sheetId));

            var scale = sheet?["scale"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(scale) && !scale.Equals("auto", StringComparison.OrdinalIgnoreCase)
                && allowedScales.Count > 0 && !allowedScales.Contains(scale))
                report.Add(new("CFG_SCALE_NOT_ALLOWED", IssueSeverity.Error,
                    $"Масштаб '{scale}' отсутствует в scalePolicy.allowed.", $"$.job.sheetPlan[{sheetIndex}].scale", sheetId));

            var views = sheet?["views"]?.AsArray();
            if (views is null) continue;
            foreach (var (viewNode, viewIndex) in views.Select((n, i) => (n, i)))
            {
                var view = viewNode?.AsObject();
                var id = view?["id"]?.GetValue<string?>();
                if (string.IsNullOrWhiteSpace(id) || !viewIds.Add(id))
                    report.Add(new("CFG_VIEW_ID", IssueSeverity.Error, "ID вида отсутствует или повторяется.", $"$.job.sheetPlan[{sheetIndex}].views[{viewIndex}].id", sheetId));
            }
        }

        foreach (var (sheetNode, sheetIndex) in sheets.Select((n, i) => (n, i)))
        {
            var sheet = sheetNode?.AsObject();
            var sheetId = sheet?["id"]?.GetValue<string?>();
            var views = sheet?["views"]?.AsArray();
            if (views is null) continue;
            foreach (var (viewNode, viewIndex) in views.Select((n, i) => (n, i)))
            {
                var parent = viewNode?["parentViewId"]?.GetValue<string?>();
                if (!string.IsNullOrWhiteSpace(parent) && !viewIds.Contains(parent))
                    report.Add(new("CFG_UNKNOWN_PARENT_VIEW", IssueSeverity.Error, $"Не найден родительский вид {parent}.", $"$.job.sheetPlan[{sheetIndex}].views[{viewIndex}].parentViewId", sheetId));
            }
        }
    }

    private static void ValidatePmiMappings(ProfileDocument profile, ValidationReport report)
    {
        var mappings = JsonNavigator.GetArray(profile.Root, "$.pmiInheritance.viewMapping");
        if (mappings is null) return;
        var targetIds = JsonNavigator.GetArray(profile.Root, "$.job.sheetPlan")?
            .SelectMany(s => s?["views"]?.AsArray() ?? [])
            .Select(v => v?["id"]?.GetValue<string?>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var (node, i) in mappings.Select((n, idx) => (n, idx)))
        {
            var target = node?["targetDrawingViewId"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(target) && !targetIds.Contains(target))
                report.Add(new("CFG_PMI_TARGET_VIEW", IssueSeverity.Warning,
                    $"PMI mapping ссылается на отсутствующий вид {target}.", $"$.pmiInheritance.viewMapping[{i}].targetDrawingViewId"));
        }
    }

    private static void ValidateManagedLayers(ProfileDocument profile, ValidationReport report)
    {
        var layers = JsonNavigator.GetArray(profile.Root, "$.layers.managedLayers");
        if (layers is null) return;
        var numbers = new HashSet<int>();
        foreach (var (node, i) in layers.Select((n, idx) => (n, idx)))
        {
            var number = node?["number"]?.GetValue<int?>();
            if (number is null or < 1 or > 256)
                report.Add(new("CFG_LAYER_NUMBER", IssueSeverity.Error, "Номер слоя должен находиться в диапазоне 1–256.", $"$.layers.managedLayers[{i}].number"));
            else if (!numbers.Add(number.Value))
                report.Add(new("CFG_LAYER_DUPLICATE", IssueSeverity.Error, $"Слой {number} определен повторно.", $"$.layers.managedLayers[{i}].number"));
        }
    }

    private static void RequireString(ProfileDocument profile, ValidationReport report, string path, string code)
    {
        if (string.IsNullOrWhiteSpace(JsonNavigator.GetString(profile.Root, path)))
            report.Add(new(code, IssueSeverity.Error, $"Обязательное строковое значение не задано: {path}.", path));
    }

    private static void ValidateUniqueIds(JsonArray? array, string code, string path, ValidationReport report)
    {
        if (array is null) return;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (node, i) in array.Select((n, idx) => (n, idx)))
        {
            var id = node?["id"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(id))
                report.Add(new(code + "_ID", IssueSeverity.Error, "Элемент стиля не имеет id.", $"{path}[{i}].id"));
            else if (!ids.Add(id))
                report.Add(new(code + "_DUPLICATE", IssueSeverity.Error, $"Повторяющийся id стиля: {id}.", $"{path}[{i}].id"));
        }
    }
}
