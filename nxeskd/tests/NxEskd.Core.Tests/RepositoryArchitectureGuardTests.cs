using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Layout;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

namespace NxEskd.Core.Tests;

public sealed class RepositoryArchitectureGuardTests
{
    [Fact]
    public void CoreHasNoUiOrNxOpenReferences()
    {
        var root = LocateNxKeysRoot();
        var coreProjPath = Path.Combine(root, "nxeskd", "src", "NxEskd.Core", "NxEskd.Core.csproj");
        var text = File.ReadAllText(coreProjPath);

        Assert.DoesNotContain("NXOpen", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UseWPF", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UseWindowsForms", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PresentationCore", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PresentationFramework", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguratorHasNoNxOpenReferences()
    {
        var root = LocateNxKeysRoot();
        var cfgProjPath = Path.Combine(root, "nxeskd", "src", "NxEskd.Configurator", "NxEskd.Configurator.csproj");
        var text = File.ReadAllText(cfgProjPath);

        Assert.DoesNotContain("NXOpen", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RequiresNxOpen", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguratorDoesNotUseModuleInitializerOrVisualTreePatching()
    {
        var root = LocateNxKeysRoot();
        var directory = Path.Combine(root, "nxeskd", "src", "NxEskd.Configurator");
        var sources = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToArray();

        Assert.DoesNotContain(sources, item =>
            item.Text.Contains("[ModuleInitializer]", StringComparison.Ordinal)
            || item.Text.Contains("FindVisualChildren<", StringComparison.Ordinal)
            || item.Text.Contains("DispatcherPriority.ContextIdle", StringComparison.Ordinal));
    }

    [Fact]
    public void CommandBridgeHasNoWpfReferencesOrModalWaits()
    {
        var root = LocateNxKeysRoot();
        var bridgeProjPath = Path.Combine(root, "NX2512_CommandBridge", "NX2512_CommandBridge.csproj");
        var text = File.ReadAllText(bridgeProjPath);

        Assert.DoesNotContain("UseWPF", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PresentationCore", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PresentationFramework", text, StringComparison.OrdinalIgnoreCase);

        var bridgeSource = File.ReadAllText(Path.Combine(root, "NX2512_CommandBridge", "Program.cs"));
        Assert.DoesNotContain("WaitForExit", bridgeSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NxDependentProjectsAreMarkedExplicitly()
    {
        var root = LocateNxKeysRoot();
        var targets = File.ReadAllText(Path.Combine(root, "nxeskd", "Directory.Build.targets"));
        var runtimeProject = File.ReadAllText(Path.Combine(root,
            "nxeskd", "src", "NxEskd.NxRuntime", "NxEskd.NxRuntime.csproj"));
        var configuratorProject = File.ReadAllText(Path.Combine(root,
            "nxeskd", "src", "NxEskd.Configurator", "NxEskd.Configurator.csproj"));

        Assert.Contains("'$(RequiresNxOpen)' == 'true'", targets, StringComparison.Ordinal);
        Assert.Contains("<RequiresNxOpen>true</RequiresNxOpen>", runtimeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiresNxOpen", configuratorProject, StringComparison.Ordinal);
    }

    [Fact]
    public void NativePublisherUsesResolvedPublicationPlanForDestructiveFlags()
    {
        var root = LocateNxKeysRoot();
        var path = Path.Combine(root, "nxeskd", "src", "NxEskd.NxRuntime", "NxNativePartPublisher.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("plan.Publication", source, StringComparison.Ordinal);
        Assert.Contains("publication.AllowOverwriteExisting", source, StringComparison.Ordinal);
        Assert.Contains("publication.AllowOverwriteReleasedDocument", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$.output.allowOverwriteExisting", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$.output.allowOverwriteReleasedDocument", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokePlanningAndExecutionInvariantsPass()
    {
        var root = LocateNxKeysRoot();
        var profilePath = Path.Combine(root, "nxeskd", "config", "active-profile.example.json");
        Assert.True(File.Exists(profilePath), $"Profile example file not found at {profilePath}");

        var profile = ProfileLoader.Load(profilePath);
        var validation = new ProfileValidator().Validate(profile);
        Assert.False(validation.HasErrors, string.Join("; ", validation.Issues.Select(x => x.Message)));

        var planner = new DrawingPlanner();
        var (plan, planReport) = planner.Build(profile, ModelSnapshot.Empty);
        Assert.NotNull(plan);
        Assert.False(planReport.HasErrors);
        Assert.NotEmpty(plan.Sheets);
        Assert.True(plan.Sheets.Sum(x => x.Views.Count) > 0);
        Assert.Contains(plan.Operations, x => x.OperationId == "validation:postconditions");

        var solver = new LayoutSolver();
        var layout = solver.Solve(
            new Rect2(0, 0, 390, 270),
            [new Rect2(220, 0, 170, 60)],
            [
                new LayoutItem("main", "view", new Rect2(0, 0, 120, 80), 100, PreferredAnchor: "center_left"),
                new LayoutItem("iso", "view", new Rect2(0, 0, 80, 60), 50, PreferredAnchor: "top_right")
            ], 10);
        Assert.Empty(layout.Unresolved);

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
        Assert.NotNull(scaledPlan);
        Assert.False(scaledReport.HasErrors);
        Assert.DoesNotContain(scaledPlan.Sheets, x => x.Scale.Equals("auto", StringComparison.OrdinalIgnoreCase));

        // Cycle detection check
        var tempCyclePath = Path.Combine(Path.GetTempPath(), "nx-eskd-cycle-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var cycleProfile = profile.DeepClone(tempCyclePath);
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
                Assert.Contains(cycleReport.Issues, x => x.Code == "CFG_DEPENDENCY_CYCLE" && x.Severity == IssueSeverity.Error);
            }
        }
        finally
        {
            if (File.Exists(tempCyclePath)) try { File.Delete(tempCyclePath); } catch { }
        }

        // Dry-run fake execution check
        var tempDryPath = Path.Combine(Path.GetTempPath(), "nx-eskd-dry-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var dryRunProfile = profile.DeepClone(tempDryPath);
            ((JsonObject)dryRunProfile.Root["execution"]!)["dryRun"] = true;
            ProfileLoader.SaveAtomic(dryRunProfile);
            var fake = new TestFakeAdapter();
            var report = new DrawingEngine().Run(DrawingCommand.Generate, tempDryPath, fake);
            Assert.True(report.Success);
            Assert.Equal(0, fake.ExecuteCount);
            Assert.Equal(1, fake.PreviewCount);
            Assert.Equal(1, fake.AnalyzeCount);
        }
        finally
        {
            if (File.Exists(tempDryPath)) try { File.Delete(tempDryPath); } catch { }
        }
    }

    private static string LocateNxKeysRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "nxeskd")) &&
                    File.Exists(Path.Combine(current.FullName, "install-nxkeys.ps1")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("NXKeys repository root not found.");
    }
}

file sealed class TestFakeAdapter : IExecutionAdapter
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
