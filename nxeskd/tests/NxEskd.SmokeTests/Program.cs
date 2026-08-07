using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Layout;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

var profilePath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "active-profile.example.json"));

Console.WriteLine("Profile: " + profilePath);
var profile = ProfileLoader.Load(profilePath);
var validation = new ProfileValidator().Validate(profile);
foreach (var issue in validation.Issues) Console.WriteLine($"{issue.Severity}: {issue.Code}: {issue.Message}");
if (validation.HasErrors) return 10;

var planner = new DrawingPlanner();
var (plan, planReport) = planner.Build(profile, ModelSnapshot.Empty);
if (plan is null || planReport.HasErrors) return 11;
if (plan.Sheets.Count == 0 || plan.Sheets.Sum(x => x.Views.Count) == 0) return 12;
if (plan.Operations.Count == 0 || plan.Operations.All(x => x.OperationId != "validation:postconditions")) return 13;
Console.WriteLine($"Plan: sheets={plan.Sheets.Count}, views={plan.Sheets.Sum(x => x.Views.Count)}, operations={plan.Operations.Count}");

var solver = new LayoutSolver();
var layout = solver.Solve(
    new Rect2(0, 0, 390, 270),
    [new Rect2(220, 0, 170, 60)],
    [
        new LayoutItem("main", "view", new Rect2(0,0,120,80), 100, PreferredAnchor: "center_left"),
        new LayoutItem("iso", "view", new Rect2(0,0,80,60), 50, PreferredAnchor: "top_right")
    ], 10);
if (layout.Unresolved.Count > 0) return 14;
Console.WriteLine("Layout OK: " + string.Join(", ", layout.Placements.Select(x => x.Key + "=" + x.Value)));

var model = new ModelSnapshot(
    "TEST",
    "C:\\test\\part.prt",
    "TEST",
    true,
    "Millimeters",
    1,
    0,
    ["Front"],
    0,
    new BoundingBoxSnapshot(0, 0, 0, 1000, 500, 100),
    false,
    false,
    "NXOpen-test");
var (scaledPlan, scaledReport) = planner.Build(profile, model);
if (scaledPlan is null || scaledReport.HasErrors || scaledPlan.Sheets.Any(x => x.Scale.Equals("auto", StringComparison.OrdinalIgnoreCase))) return 15;

var cycleProfile = profile.DeepClone(Path.Combine(Path.GetTempPath(), "nx-eskd-cycle.json"));
var cycleSheets = cycleProfile.Root["job"]?["sheetPlan"]?.AsArray();
var cycleViews = cycleSheets?.FirstOrDefault()?["views"]?.AsArray();
if (cycleViews is { Count: >= 2 })
{
    var first = cycleViews[0]!.AsObject();
    var second = cycleViews[1]!.AsObject();
    var firstId = first["id"]!.GetValue<string>();
    var secondId = second["id"]!.GetValue<string>();
    first["parentViewId"] = secondId;
    second["parentViewId"] = firstId;
    var (_, cycleReport) = planner.Build(cycleProfile, ModelSnapshot.Empty);
    if (!cycleReport.Issues.Any(x => x.Code == "CFG_DEPENDENCY_CYCLE" && x.Severity == IssueSeverity.Error)) return 16;
}

var tempRoot = Path.Combine(Path.GetTempPath(), "nx-eskd-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
try
{
    var requestPath = Path.Combine(tempRoot, "request.json");
    CommandRequest.Create(DrawingCommand.Preview, profilePath, "C:\\test\\part.prt", dryRun: true).SaveAtomic(requestPath);
    var request = CommandRequest.Load(requestPath);
    if (request.Command != DrawingCommand.Preview || !request.DryRun || string.IsNullOrWhiteSpace(request.ProfileSha256)) return 17;

    var clonePath = Path.Combine(tempRoot, "profile.json");
    var clone = profile.DeepClone(clonePath);
    ProfileLoader.SaveAtomic(clone);
    var originalText = File.ReadAllText(clonePath);
    clone.Root["profileName"] = "Smoke test changed";
    ProfileLoader.SaveAtomic(clone);
    if (!File.Exists(clonePath + ".bak") || File.ReadAllText(clonePath + ".bak") != originalText) return 18;

    var dryRunProfilePath = Path.Combine(tempRoot, "dry-run-profile.json");
    var dryRunProfile = profile.DeepClone(dryRunProfilePath);
    ((JsonObject)dryRunProfile.Root["execution"]!)["dryRun"] = true;
    ProfileLoader.SaveAtomic(dryRunProfile);
    var fake = new FakeAdapter();
    var report = new DrawingEngine().Run(DrawingCommand.Generate, dryRunProfilePath, fake);
    if (!report.Success || fake.ExecuteCount != 0 || fake.PreviewCount != 1 || fake.AnalyzeCount != 1) return 19;

    var repositoryRoot = FindRepositoryRoot(profilePath);
    if (repositoryRoot is not null)
    {
        var runtimeFiles = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src", "NxEskd.NxRuntime"), "*.cs", SearchOption.AllDirectories).ToArray();
        if (runtimeFiles.Any(path => File.ReadAllText(path).Contains("NxReflection.Commit(", StringComparison.Ordinal))) return 20;
        if (runtimeFiles.Any(path => File.ReadAllText(path).Contains("catch { NxReflection.Destroy(builder); throw; }", StringComparison.Ordinal))) return 21;
        var reflectionSource = File.ReadAllText(Path.Combine(repositoryRoot, "src", "NxEskd.NxRuntime", "NxReflection.cs"));
        if (!reflectionSource.Contains("CommitObjectAndDestroy", StringComparison.Ordinal)
            || !reflectionSource.Contains("CommitCommandAndDestroy", StringComparison.Ordinal)) return 22;
    }
}
finally
{
    try { Directory.Delete(tempRoot, recursive: true); } catch { }
    try { if (File.Exists(cycleProfile.SourcePath)) File.Delete(cycleProfile.SourcePath); } catch { }
}

Console.WriteLine("All smoke tests passed.");
return 0;

static string? FindRepositoryRoot(string profilePath)
{
    var directory = new DirectoryInfo(Path.GetDirectoryName(profilePath)!);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src", "NxEskd.NxRuntime"))) return directory.FullName;
        directory = directory.Parent;
    }
    return null;
}

file sealed class FakeAdapter : IExecutionAdapter
{
    public int ExecuteCount { get; private set; }
    public int PreviewCount { get; private set; }
    public int AnalyzeCount { get; private set; }
    public string? CurrentPartPath => null;

    public ModelSnapshot AnalyzeModel(ProfileDocument profile, ExecutionReport report)
    {
        AnalyzeCount++;
        return ModelSnapshot.Empty;
    }

    public void Preview(ProfileDocument profile, DrawingPlan plan, ExecutionReport report) => PreviewCount++;

    public ValidationReport Execute(DrawingCommand command, ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
    {
        ExecuteCount++;
        return new ValidationReport();
    }

    public ValidationReport ValidateResult(ProfileDocument profile, DrawingPlan plan, ExecutionReport report) => new();
    public void Export(ProfileDocument profile, DrawingPlan plan, ExecutionReport report) { }
    public void Inventory(ProfileDocument profile, ExecutionReport report) { }
}
