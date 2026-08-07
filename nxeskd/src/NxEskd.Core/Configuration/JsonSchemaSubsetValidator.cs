using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Configuration;

/// <summary>
/// Deterministic validator for the JSON Schema keywords used by the bundled profile schema.
/// It intentionally supports a strict subset of Draft 2020-12 rather than pretending to
/// implement unsupported keywords. Unsupported keywords are ignored and can be added explicitly.
/// </summary>
internal sealed class JsonSchemaSubsetValidator
{
    public ValidationReport Validate(ProfileDocument profile)
    {
        var report = new ValidationReport();
        var reference = profile.Root["$schema"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(reference))
        {
            report.Add(new("CFG_SCHEMA_REFERENCE_MISSING", IssueSeverity.Error,
                "В профиле отсутствует обязательная ссылка $schema.", "$.$schema"));
            return report;
        }

        var schemaPath = ResolveSchemaPath(profile, reference);
        if (schemaPath is null)
        {
            report.Add(new("CFG_SCHEMA_FILE_MISSING", IssueSeverity.Error,
                $"Файл JSON Schema '{reference}' не найден рядом с профилем, в NX_ESKD_ROOT/config или в каталогах приложения.", "$.$schema"));
            return report;
        }

        JsonObject schema;
        try
        {
            schema = JsonNode.Parse(File.ReadAllText(schemaPath), documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            })?.AsObject() ?? throw new InvalidDataException("Корень schema должен быть объектом.");
        }
        catch (Exception ex)
        {
            report.Add(new("CFG_SCHEMA_INVALID", IssueSeverity.Error,
                "JSON Schema не удалось загрузить: " + ex.Message, "$.$schema"));
            return report;
        }

        var allowUnknown = JsonNavigator.GetBool(profile.Root, "$.execution.allowUnknownJsonProperties", false);
        ValidateNode(profile.Root, schema, "$", report, allowUnknown);
        return report;
    }

    private static string? ResolveSchemaPath(ProfileDocument profile, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;

        if (Path.IsPathRooted(reference))
        {
            try
            {
                var absolute = Path.GetFullPath(reference);
                return File.Exists(absolute) ? absolute : null;
            }
            catch
            {
                return null;
            }
        }

        var fileName = Path.GetFileName(reference);
        var baseDir = string.IsNullOrWhiteSpace(profile.BaseDirectory) ? Environment.CurrentDirectory : profile.BaseDirectory;
        var candidates = new List<string>();

        try
        {
            candidates.Add(Path.GetFullPath(reference, baseDir));
        }
        catch
        {
            // Ignore invalid relative reference
        }

        var root = Environment.GetEnvironmentVariable("NX_ESKD_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
        {
            candidates.Add(Path.Combine(root, "config", fileName));
            candidates.Add(Path.Combine(root, fileName));
        }

        AddAncestorCandidates(candidates, AppContext.BaseDirectory, fileName);
        AddAncestorCandidates(candidates, Environment.CurrentDirectory, fileName);
        AddAncestorCandidates(candidates, baseDir, fileName);

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c =>
            {
                try { return Path.GetFullPath(c); } catch { return null; }
            })
            .Where(c => c is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static void AddAncestorCandidates(ICollection<string> candidates, string? start, string fileName)
    {
        if (string.IsNullOrWhiteSpace(start)) return;
        try
        {
            var fullStart = Path.GetFullPath(start);
            if (string.IsNullOrWhiteSpace(fullStart)) return;
            var directory = new DirectoryInfo(fullStart);
            while (directory is not null)
            {
                candidates.Add(Path.Combine(directory.FullName, "config", fileName));
                candidates.Add(Path.Combine(directory.FullName, fileName));
                directory = directory.Parent;
            }
        }
        catch
        {
            // Ignore invalid start path
        }
    }

    private static void ValidateNode(
        JsonNode? node,
        JsonObject schema,
        string path,
        ValidationReport report,
        bool allowUnknown)
    {
        if (schema["type"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var expectedType)
            && !MatchesType(node, expectedType))
        {
            report.Add(new("CFG_SCHEMA_TYPE", IssueSeverity.Error,
                $"{path}: ожидался тип '{expectedType}', получен '{DescribeType(node)}'.", path));
            return;
        }

        if (schema["enum"] is JsonArray enumValues
            && !enumValues.Any(candidate => JsonNode.DeepEquals(candidate, node)))
            report.Add(new("CFG_SCHEMA_ENUM", IssueSeverity.Error,
                $"{path}: значение не входит в допустимый enum.", path));

        if (node is JsonObject obj)
        {
            if (schema["required"] is JsonArray required)
            {
                foreach (var requiredNode in required)
                {
                    var name = requiredNode?.GetValue<string?>();
                    if (!string.IsNullOrWhiteSpace(name) && !obj.ContainsKey(name))
                        report.Add(new("CFG_SCHEMA_REQUIRED", IssueSeverity.Error,
                            $"{path}: отсутствует обязательное свойство '{name}'.", path + "." + name));
                }
            }

            var properties = schema["properties"] as JsonObject;
            if (properties is not null)
            {
                foreach (var property in properties)
                {
                    if (!obj.TryGetPropertyValue(property.Key, out var value) || property.Value is not JsonObject childSchema) continue;
                    ValidateNode(value, childSchema, path + "." + property.Key, report, allowUnknown);
                }
            }

            var additionalAllowed = schema["additionalProperties"]?.GetValue<bool?>() ?? true;
            if (!allowUnknown && !additionalAllowed && properties is not null)
            {
                foreach (var property in obj)
                    if (!properties.ContainsKey(property.Key))
                        report.Add(new("CFG_SCHEMA_UNKNOWN_PROPERTY", IssueSeverity.Error,
                            $"{path}: неизвестное свойство '{property.Key}'.", path + "." + property.Key));
            }
        }
        else if (node is JsonArray array)
        {
            var minItems = schema["minItems"]?.GetValue<int?>();
            if (minItems is not null && array.Count < minItems)
                report.Add(new("CFG_SCHEMA_MIN_ITEMS", IssueSeverity.Error,
                    $"{path}: требуется не менее {minItems} элементов.", path));
            var maxItems = schema["maxItems"]?.GetValue<int?>();
            if (maxItems is not null && array.Count > maxItems)
                report.Add(new("CFG_SCHEMA_MAX_ITEMS", IssueSeverity.Error,
                    $"{path}: допускается не более {maxItems} элементов.", path));
            if (schema["items"] is JsonObject itemSchema)
                for (var i = 0; i < array.Count; i++)
                    ValidateNode(array[i], itemSchema, $"{path}[{i}]", report, allowUnknown);
        }
        else if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                var minLength = schema["minLength"]?.GetValue<int?>();
                if (minLength is not null && text.Length < minLength)
                    report.Add(new("CFG_SCHEMA_MIN_LENGTH", IssueSeverity.Error,
                        $"{path}: длина строки должна быть не менее {minLength}.", path));
                var maxLength = schema["maxLength"]?.GetValue<int?>();
                if (maxLength is not null && text.Length > maxLength)
                    report.Add(new("CFG_SCHEMA_MAX_LENGTH", IssueSeverity.Error,
                        $"{path}: длина строки должна быть не более {maxLength}.", path));
                var pattern = schema["pattern"]?.GetValue<string?>();
                if (!string.IsNullOrWhiteSpace(pattern) && !Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant))
                    report.Add(new("CFG_SCHEMA_PATTERN", IssueSeverity.Error,
                        $"{path}: строка не соответствует шаблону '{pattern}'.", path));
            }

            if (TryNumber(value, out var number))
            {
                var minimum = schema["minimum"]?.GetValue<double?>();
                if (minimum is not null && number < minimum)
                    report.Add(new("CFG_SCHEMA_MINIMUM", IssueSeverity.Error,
                        $"{path}: значение должно быть не меньше {minimum.Value.ToString(CultureInfo.InvariantCulture)}.", path));
                var maximum = schema["maximum"]?.GetValue<double?>();
                if (maximum is not null && number > maximum)
                    report.Add(new("CFG_SCHEMA_MAXIMUM", IssueSeverity.Error,
                        $"{path}: значение должно быть не больше {maximum.Value.ToString(CultureInfo.InvariantCulture)}.", path));
            }
        }
    }

    private static bool MatchesType(JsonNode? node, string expected) => expected switch
    {
        "object" => node is JsonObject,
        "array" => node is JsonArray,
        "string" => node is JsonValue value && value.TryGetValue<string>(out _),
        "boolean" => node is JsonValue value && value.TryGetValue<bool>(out _),
        "integer" => node is JsonValue value && IsInteger(value),
        "number" => node is JsonValue value && TryNumber(value, out _),
        "null" => node is null,
        _ => true
    };

    private static string DescribeType(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonObject) return "object";
        if (node is JsonArray) return "array";
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out _)) return "string";
            if (value.TryGetValue<bool>(out _)) return "boolean";
            if (IsInteger(value)) return "integer";
            if (TryNumber(value, out _)) return "number";
        }
        return "unknown";
    }

    private static bool IsInteger(JsonValue value)
        => value.TryGetValue<int>(out _)
           || value.TryGetValue<long>(out _)
           || value.TryGetValue<uint>(out _)
           || value.TryGetValue<ulong>(out _);

    private static bool TryNumber(JsonValue value, out double number)
    {
        if (value.TryGetValue<double>(out number)) return double.IsFinite(number);
        if (value.TryGetValue<decimal>(out var decimalValue))
        {
            number = (double)decimalValue;
            return double.IsFinite(number);
        }
        number = 0;
        return false;
    }
}
