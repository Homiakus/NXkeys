using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

namespace NxEskd.Core.Tests;

public sealed class CoreContractsTests
{
    [Fact]
    public void StrictSchemaRejectsUnknownTopLevelProperty()
    {
        using var fixture = ProfileFixture.Create();
        fixture.Profile.Root["unknownTopLevelTypo"] = true;
        ProfileLoader.SaveAtomic(fixture.Profile);

        var loaded = ProfileLoader.Load(fixture.Profile.SourcePath);
        var report = new ProfileValidator().Validate(loaded);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "CFG_SCHEMA_UNKNOWN_PROPERTY"
            && issue.JsonPath == "$.unknownTopLevelTypo"
            && issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void FutureSchemaVersionIsBlocked()
    {
        using var fixture = ProfileFixture.Create();
        fixture.Profile.Root["schemaVersion"] = "1.1.0";
        ProfileLoader.SaveAtomic(fixture.Profile);

        var report = new ProfileValidator().Validate(ProfileLoader.Load(fixture.Profile.SourcePath));

        Assert.Contains(report.Issues, issue =>
            issue.Code == "CFG_SCHEMA_VERSION_FUTURE"
            && issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void KnownLegacyProfileIsMigratedInMemory()
    {
        using var fixture = ProfileFixture.Create();
        fixture.Profile.Root["schemaVersion"] = "0.9.0";
        var job = fixture.Profile.Root["job"]!.AsObject();
        job["id"] = job["jobId"]!.DeepClone();
        job.Remove("jobId");
        ProfileLoader.SaveAtomic(fixture.Profile);

        var migrated = ProfileLoader.Load(fixture.Profile.SourcePath);

        Assert.True(migrated.WasMigrated);
        Assert.Equal("0.9.0", migrated.OriginalSchemaVersion);
        Assert.Equal(ProfileValidator.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(JsonNavigator.GetString(migrated.Root, "$.job.jobId")));
        Assert.Null(JsonNavigator.Get(migrated.Root, "$.job.id"));
    }

    [Fact]
    public void PlannerRejectsViewDependencyCycle()
    {
        using var fixture = ProfileFixture.Create();
        var views = fixture.Profile.Root["job"]?["sheetPlan"]?[0]?["views"]?.AsArray()
                    ?? throw new InvalidOperationException("Fixture has no views.");
        Assert.True(views.Count >= 2);
        var first = views[0]!.AsObject();
        var second = views[1]!.AsObject();
        var firstId = first["id"]!.GetValue<string>();
        var secondId = second["id"]!.GetValue<string>();
        first["parentViewId"] = secondId;
        second["parentViewId"] = firstId;

        var (_, report) = new DrawingPlanner().Build(fixture.Profile, ModelSnapshot.Empty);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "CFG_DEPENDENCY_CYCLE"
            && issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void AutoScaleUsesModelBoundingBox()
    {
        using var fixture = ProfileFixture.Create();
        foreach (var sheet in fixture.Profile.Root["job"]?["sheetPlan"]?.AsArray() ?? [])
            sheet!["scale"] = "auto";
        var model = new ModelSnapshot(
            "PART", fixture.Profile.SourcePath, "PART", true, "Millimeters",
            1, 0, ["Front"], 0,
            new BoundingBoxSnapshot(0, 0, 0, 1000, 500, 100),
            false, false, "test");

        var (plan, report) = new DrawingPlanner().Build(fixture.Profile, model);

        Assert.False(report.HasErrors);
        Assert.NotNull(plan);
        Assert.All(plan!.Sheets, sheet =>
            Assert.False(string.Equals("auto", sheet.Scale, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void DryRunNeverCallsMutationAdapter()
    {
        using var fixture = ProfileFixture.Create();
        fixture.Profile.Root["execution"]!["dryRun"] = true;
        ProfileLoader.SaveAtomic(fixture.Profile);
        var adapter = new FakeAdapter();

        var report = new DrawingEngine().Run(DrawingCommand.Generate, fixture.Profile.SourcePath, adapter);

        Assert.True(report.Success, string.Join(Environment.NewLine, report.Issues.Select(x => x.Message)));
        Assert.Equal(1, adapter.AnalyzeCount);
        Assert.Equal(1, adapter.PreviewCount);
        Assert.Equal(0, adapter.ExecuteCount);
        Assert.Equal(0, adapter.ExportCount);
    }

    private sealed class FakeAdapter : IExecutionAdapter
    {
        public int AnalyzeCount { get; private set; }
        public int PreviewCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public int ExportCount { get; private set; }
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

        public void Export(ProfileDocument profile, DrawingPlan plan, ExecutionReport report) => ExportCount++;

        public void Inventory(ProfileDocument profile, ExecutionReport report) { }
    }

    private sealed class ProfileFixture : IDisposable
    {
        private ProfileFixture(string directory, ProfileDocument profile)
        {
            Directory = directory;
            Profile = profile;
        }

        public string Directory { get; }
        public ProfileDocument Profile { get; }

        public static ProfileFixture Create()
        {
            var root = LocateRepositoryRoot();
            var config = Path.Combine(root, "config");
            var directory = Path.Combine(Path.GetTempPath(), "nx-eskd-core-tests-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var profilePath = Path.Combine(directory, "active-profile.json");
            File.Copy(Path.Combine(config, "active-profile.example.json"), profilePath);
            File.Copy(Path.Combine(config, "nx-eskd-profile.schema.json"),
                Path.Combine(directory, "nx-eskd-profile.schema.json"));
            return new ProfileFixture(directory, ProfileLoader.Load(profilePath));
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch { }
        }

        private static string LocateRepositoryRoot()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                var current = new DirectoryInfo(Path.GetFullPath(start));
                while (current is not null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "config", "active-profile.example.json")))
                        return current.FullName;
                    current = current.Parent;
                }
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
