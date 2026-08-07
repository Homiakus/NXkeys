using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class DrawingCompletenessAnalyzerTests
{
    [Fact]
    public void EmptyDrawingIsBlocking()
    {
        var plan = CreatePlan([], ModelSnapshot.Empty);

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_NO_DRAWING_VIEWS");
    }

    [Fact]
    public void SpatialModelWithOneViewRequiresEngineeringReview()
    {
        var model = ModelSnapshot.Empty with
        {
            BodyCount = 1,
            BoundingBox = new BoundingBoxSnapshot(0, 0, 0, 100, 80, 40),
            PmiCount = 0
        };
        var plan = CreatePlan([BaseView("FRONT")], model);

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.False(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_VIEW_SET_POSSIBLY_INSUFFICIENT");
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_DIMENSION_SOURCE_MISSING");
    }

    [Fact]
    public void ProjectedViewRequiresParentAndDirection()
    {
        var projected = new ViewPlan(
            "TOP",
            "projected",
            "Сверху",
            null,
            null,
            null,
            new PlacementPlan("aligned_auto", null, 20, null, null),
            new ScalePlan(true, null, "1:1"),
            "removed",
            false,
            null,
            null);
        var plan = CreatePlan([BaseView("FRONT"), projected], ModelSnapshot.Empty with { PmiCount = 3 });

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_DEPENDENT_VIEW_PARENT_MISSING");
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_PROJECTED_DIRECTION_MISSING");
    }

    [Fact]
    public void DependentViewCannotReferenceParentOnAnotherSheet()
    {
        var top = ProjectedView("TOP", "FRONT", "top");
        var first = Sheet("SHEET_1", [BaseView("FRONT")]);
        var second = Sheet("SHEET_2", [top]);
        var plan = new DrawingPlan(
            "TEST",
            "part_drawing",
            "A.001",
            "Тест",
            [first, second],
            new Dictionary<string, string>(),
            false)
        {
            Model = ModelSnapshot.Empty with { PmiCount = 2 },
            Operations = Array.Empty<DrawingOperation>()
        };

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_DEPENDENT_VIEW_CROSS_SHEET");
    }

    [Fact]
    public void SheetMetalDrawingRequiresFlatPatternView()
    {
        var plan = CreatePlan(
            [BaseView("FRONT")],
            ModelSnapshot.Empty with { BodyCount = 1, IsSheetMetal = true, PmiCount = 2 },
            "sheet_metal_drawing");

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_FLAT_PATTERN_VIEW_MISSING");
    }

    [Fact]
    public void CompleteOrdinaryPartPlanPassesBlockingChecks()
    {
        var model = ModelSnapshot.Empty with
        {
            BodyCount = 1,
            PmiCount = 6,
            BoundingBox = new BoundingBoxSnapshot(0, 0, 0, 100, 80, 40)
        };
        var plan = CreatePlan([BaseView("FRONT"), ProjectedView("TOP", "FRONT", "top")], model);

        var report = new DrawingCompletenessAnalyzer().Analyze(plan);

        Assert.False(report.HasErrors);
        Assert.DoesNotContain(report.Issues, issue => issue.Code == "PLAN_DIMENSION_SOURCE_MISSING");
    }

    private static DrawingPlan CreatePlan(
        IReadOnlyList<ViewPlan> views,
        ModelSnapshot model,
        string documentKind = "part_drawing")
    {
        return new DrawingPlan(
            "TEST",
            documentKind,
            "A.001",
            "Тест",
            [Sheet("SHEET_1", views)],
            new Dictionary<string, string>(),
            false)
        {
            Model = model,
            Operations = Array.Empty<DrawingOperation>()
        };
    }

    private static SheetPlan Sheet(string id, IReadOnlyList<ViewPlan> views) => new(
        id,
        "main",
        "TEMPLATE",
        "A3",
        "landscape",
        "1:1",
        views,
        Array.Empty<TitleBlockValuePlan>());

    private static ViewPlan ProjectedView(string id, string parentId, string direction) => new(
        id,
        "projected",
        id,
        parentId,
        direction,
        null,
        null,
        new PlacementPlan("aligned_auto", null, 20, null, null),
        new ScalePlan(true, null, "1:1"),
        "removed",
        false,
        null,
        null);

    private static ViewPlan BaseView(string id) => new(
        id,
        "base",
        "Главный вид",
        null,
        null,
        "Front",
        null,
        new PlacementPlan("auto", "center_left", 20, null, null),
        new ScalePlan(true, null, "1:1"),
        "removed",
        false,
        null,
        null);
}
