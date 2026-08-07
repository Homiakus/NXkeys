using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class DrawingViewKindsTests
{
    [Fact]
    public void CompletenessAnalyzerRejectsUnsupportedViewInsteadOfTreatingItAsBase()
    {
        var baseView = View("BASE", DrawingViewKinds.Base);
        var unsupported = View("EXPLODED", "exploded");
        var plan = new DrawingPlan(
            "profile",
            "part_drawing",
            "A.001",
            "Part",
            [new SheetPlan("S1", "main", "template", "A3", "landscape", "1:1",
                [baseView, unsupported], [])],
            new Dictionary<string, string>(),
            false);

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "PLAN_VIEW_TYPE_UNSUPPORTED"
            && issue.ObjectId == "EXPLODED");
    }

    [Fact]
    public void RuntimeSupportedKindsContainOnlyExplicitlyImplementedFamilies()
    {
        Assert.Contains(DrawingViewKinds.Base, DrawingViewKinds.RuntimeSupported);
        Assert.Contains(DrawingViewKinds.Projected, DrawingViewKinds.RuntimeSupported);
        Assert.Contains(DrawingViewKinds.Section, DrawingViewKinds.RuntimeSupported);
        Assert.Contains(DrawingViewKinds.Detail, DrawingViewKinds.RuntimeSupported);
        Assert.Contains(DrawingViewKinds.FlatPattern, DrawingViewKinds.RuntimeSupported);
        Assert.DoesNotContain("exploded", DrawingViewKinds.RuntimeSupported);
        Assert.DoesNotContain("auxiliary", DrawingViewKinds.RuntimeSupported);
        Assert.DoesNotContain("broken", DrawingViewKinds.RuntimeSupported);
    }

    private static ViewPlan View(string id, string type)
        => new(
            id,
            type,
            id,
            null,
            null,
            "Front",
            null,
            new PlacementPlan("auto", null, 20, null, null),
            new ScalePlan(true, null, "1:1"),
            "removed",
            false,
            null,
            null);
}
