using NxEskd.Core.Configuration;
using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class PlanApplicabilityTests
{
    [Fact]
    public void UniversalProfileSkipsUnavailableOperationsForOrdinaryPart()
    {
        using var fixture = ProfileFixture.Create();
        var model = OrdinaryPart();

        var (rawPlan, report) = new DrawingPlanner().Build(fixture.Profile, model);
        Assert.NotNull(rawPlan);
        Assert.False(report.HasErrors);
        var originalSheetCount = rawPlan!.Sheets.Count;

        var filtered = PlanApplicabilityFilter.Apply(rawPlan, fixture.Profile, report);

        Assert.False(report.HasErrors);
        Assert.Equal("part_drawing", filtered.DocumentKind);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_DOCUMENT_KIND_ADJUSTED");
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_SECTION_DATUM_SKIPPED");
        Assert.True(filtered.Sheets.Count < originalSheetCount);
        Assert.DoesNotContain(filtered.Sheets,
            sheet => sheet.Role.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(filtered.Sheets.SelectMany(sheet => sheet.Views), view =>
            view.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase)
            || view.Id.Equals("VIEW_SECTION_A", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(filtered.Operations, operation =>
            operation.OperationId.Equals("feature:flat_pattern", StringComparison.OrdinalIgnoreCase)
            || operation.OperationId.Equals("table:BEND_TABLE", StringComparison.OrdinalIgnoreCase)
            || operation.OperationId.Equals("sheet:SHEET_2", StringComparison.OrdinalIgnoreCase)
            || operation.OperationId.Equals("title_block:SHEET_2", StringComparison.OrdinalIgnoreCase)
            || operation.OperationId.Equals("view:VIEW_SECTION_A", StringComparison.OrdinalIgnoreCase)
            || operation.OperationId.Equals("pmi:inherit", StringComparison.OrdinalIgnoreCase));
        Assert.All(filtered.Operations, operation =>
            Assert.DoesNotContain(operation.Dependencies, dependency =>
                dependency.Equals("feature:flat_pattern", StringComparison.OrdinalIgnoreCase)
                || dependency.Equals("table:BEND_TABLE", StringComparison.OrdinalIgnoreCase)
                || dependency.Equals("sheet:SHEET_2", StringComparison.OrdinalIgnoreCase)
                || dependency.Equals("title_block:SHEET_2", StringComparison.OrdinalIgnoreCase)
                || dependency.Equals("view:VIEW_SECTION_A", StringComparison.OrdinalIgnoreCase)
                || dependency.Equals("pmi:inherit", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void UniversalProfileKeepsSectionWhenDatumPlaneExists()
    {
        using var fixture = ProfileFixture.Create();
        var model = OrdinaryPart() with { DatumPlanes = ["DWG_SECTION_A"] };
        var (rawPlan, report) = new DrawingPlanner().Build(fixture.Profile, model);
        Assert.NotNull(rawPlan);

        var filtered = PlanApplicabilityFilter.Apply(rawPlan!, fixture.Profile, report);

        Assert.False(report.HasErrors);
        Assert.Contains(filtered.Sheets.SelectMany(sheet => sheet.Views),
            view => view.Id.Equals("VIEW_SECTION_A", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Issues, issue => issue.Code == "PLAN_SECTION_DATUM_SKIPPED");
    }

    [Fact]
    public void ProductionSheetMetalProfileRejectsOrdinaryPart()
    {
        using var fixture = ProfileFixture.Create();
        fixture.Profile.Root["profileStatus"] = "active";
        fixture.Profile.Root["job"]!["documentKind"] = "sheet_metal_drawing";
        var model = ModelSnapshot.Empty with { IsSheetMetal = false };
        var (rawPlan, report) = new DrawingPlanner().Build(fixture.Profile, model);
        Assert.NotNull(rawPlan);

        _ = PlanApplicabilityFilter.Apply(rawPlan!, fixture.Profile, report);

        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_DOCUMENT_KIND_MODEL_MISMATCH");
        Assert.True(report.HasErrors);
    }

    [Fact]
    public void ProductionProfileRejectsMissingSectionDatumPlane()
    {
        using var fixture = ProfileFixture.Create();
        fixture.Profile.Root["profileStatus"] = "active";
        fixture.Profile.Root["job"]!["documentKind"] = "part_drawing";
        var model = OrdinaryPart();
        var (rawPlan, report) = new DrawingPlanner().Build(fixture.Profile, model);
        Assert.NotNull(rawPlan);

        _ = PlanApplicabilityFilter.Apply(rawPlan!, fixture.Profile, report);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "PLAN_SECTION_DATUM_MISSING"
            && issue.ObjectId == "VIEW_SECTION_A");
        Assert.True(report.HasErrors);
    }

    private static ModelSnapshot OrdinaryPart() => new(
        "ORDINARY_PART",
        "C:\\test\\ordinary.prt",
        "ORDINARY_PART",
        true,
        "Millimeters",
        1,
        0,
        ["Front", "Top", "Right", "Isometric"],
        0,
        new BoundingBoxSnapshot(0, 0, 0, 100, 80, 40),
        false,
        false,
        "NXOpen-test",
        Array.Empty<string>());

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
            var directory = Path.Combine(Path.GetTempPath(), "nx-eskd-applicability-tests-" + Guid.NewGuid().ToString("N"));
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
