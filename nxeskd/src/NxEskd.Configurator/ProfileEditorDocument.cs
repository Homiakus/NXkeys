using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.Configurator;

internal sealed class ProfileEditorDocument
{
    private string _persistedJson = string.Empty;

    public string ProfilePath { get; private set; } = string.Empty;
    public JsonObject Root { get; private set; } = new();
    public string CurrentJson => Serialize(Root);

    public ProfileDocument Load(string path)
    {
        var document = ProfileLoader.Load(path);
        ProfilePath = document.SourcePath;
        Root = document.Root;
        _persistedJson = Normalize(document.ToJson());
        return document;
    }

    public ValidationReport ApplyRaw(string rawJson)
    {
        Root = ParseRoot(rawJson);
        return Validate();
    }

    public void NotifyRootChanged()
    {
        _ = CurrentJson;
    }

    public ValidationReport Validate()
    {
        if (string.IsNullOrWhiteSpace(ProfilePath))
            throw new InvalidOperationException("Путь профиля не задан.");
        return ValidateCandidate(new ProfileDocument(ProfilePath, Root));
    }

    public ValidationReport Save(string rawJson, string? targetPath = null)
    {
        var parsed = ParseRoot(rawJson);
        var destination = Path.GetFullPath(targetPath ?? ProfilePath);
        var candidate = new ProfileDocument(destination, parsed);
        var report = ValidateCandidate(candidate);
        if (report.HasErrors) return report;

        ProfileLoader.SaveAtomic(candidate);
        ProfilePath = destination;
        Root = parsed;
        _persistedJson = Normalize(candidate.ToJson());
        return report;
    }

    public bool IsDirty(string rawJson)
        => !string.Equals(Normalize(rawJson), _persistedJson, StringComparison.Ordinal);

    private static ValidationReport ValidateCandidate(ProfileDocument candidate)
    {
        var report = new ProfileValidator().Validate(candidate);
        if (report.HasErrors) return report;

        var safety = new DrawingSafetyPolicyAnalyzer().Analyze(candidate);
        foreach (var issue in safety.Issues) report.Add(issue);
        if (report.HasErrors) return report;

        var (plan, planReport) = new DrawingPlanner().Build(
            candidate,
            ModelSnapshot.Empty,
            prevalidated: report);
        if (plan is null || planReport.HasErrors) return planReport;

        plan = DrawingOperationPlanNormalizer.Normalize(plan);
        var (_, scheduleReport) = new DrawingOperationScheduler().Build(plan.Operations);
        foreach (var issue in scheduleReport.Issues) planReport.Add(issue);

        var completeness = new DrawingCompletenessAnalyzer().Analyze(plan);
        foreach (var issue in completeness.Issues) planReport.Add(issue);
        return planReport;
    }

    private static JsonObject ParseRoot(string rawJson)
        => JsonNode.Parse(rawJson, documentOptions: new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        })?.AsObject() ?? throw new InvalidDataException("Корневой JSON должен быть объектом.");

    private static string Serialize(JsonObject root)
        => root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    private static string Normalize(string value)
        => value.Replace("\r\n", "\n").Trim();
}
