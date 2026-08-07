using System.Text.Json;

namespace NxEskd.Core.Diagnostics;

public sealed class ValidationReport
{
    private readonly List<ValidationIssue> _issues = [];

    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public bool HasErrors => _issues.Any(x => x.Severity == IssueSeverity.Error);
    public bool HasWarnings => _issues.Any(x => x.Severity is IssueSeverity.Warning or IssueSeverity.ManualReview);
    public int ErrorCount => _issues.Count(x => x.Severity == IssueSeverity.Error);
    public int WarningCount => _issues.Count(x => x.Severity is IssueSeverity.Warning or IssueSeverity.ManualReview);

    public void Add(ValidationIssue issue) => _issues.Add(issue);
    public void AddRange(IEnumerable<ValidationIssue> issues) => _issues.AddRange(issues);

    public string ToJson(bool indented = true) => JsonSerializer.Serialize(this, JsonOptions(indented));

    public static JsonSerializerOptions JsonOptions(bool indented = true) => new()
    {
        WriteIndented = indented,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
