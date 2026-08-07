using NxEskd.Core.Layout;

namespace NxEskd.Core.Tests;

public sealed class LayoutSolverTests
{
    [Theory]
    [InlineData("right")]
    [InlineData("left")]
    public void HorizontalProjectedViewRemainsOnParentAxis(string relation)
    {
        var area = new Rect2(0, 0, 400, 300);
        var parent = new LayoutItem("BASE", "view", new Rect2(0, 0, 100, 80), 100);
        var child = new LayoutItem("PROJECTED", "projected_view", new Rect2(0, 0, 80, 60), 75,
            "BASE", null, relation);

        var result = new LayoutSolver().Solve(area, [], [parent, child], 10, 500);

        Assert.Empty(result.Unresolved);
        Assert.True(result.Placements.TryGetValue("BASE", out var parentRect));
        Assert.True(result.Placements.TryGetValue("PROJECTED", out var childRect));
        Assert.Equal(parentRect.Center.Y, childRect.Center.Y, 6);
        if (relation == "right") Assert.True(childRect.Left >= parentRect.Right + 10);
        else Assert.True(childRect.Right <= parentRect.Left - 10);
    }

    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public void VerticalProjectedViewRemainsOnParentAxis(string relation)
    {
        var area = new Rect2(0, 0, 400, 300);
        var fixedParent = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE"] = new Rect2(150, 100, 100, 80)
        };
        var child = new LayoutItem("PROJECTED", "projected_view", new Rect2(0, 0, 80, 60), 75,
            "BASE", null, relation);

        var result = new LayoutSolver().Solve(area, [], [child], 10, 500, fixedParent);

        Assert.Empty(result.Unresolved);
        var childRect = Assert.Single(result.Placements).Value;
        Assert.Equal(fixedParent["BASE"].Center.X, childRect.Center.X, 6);
        if (relation == "top") Assert.True(childRect.Bottom >= fixedParent["BASE"].Top + 10);
        else Assert.True(childRect.Top <= fixedParent["BASE"].Bottom - 10);
    }

    [Fact]
    public void ProjectedViewWithoutResolvedParentIsNotFreelyPlaced()
    {
        var area = new Rect2(0, 0, 400, 300);
        var child = new LayoutItem("PROJECTED", "projected_view", new Rect2(0, 0, 80, 60), 75,
            "MISSING", "top_right", "right");

        var result = new LayoutSolver().Solve(area, [], [child], 10, 500);

        Assert.Contains("PROJECTED", result.Unresolved);
        Assert.DoesNotContain("PROJECTED", result.Placements.Keys);
    }

    [Fact]
    public void ProjectionConstraintWinsOverGenericPreferredAnchor()
    {
        var area = new Rect2(0, 0, 400, 300);
        var fixedParent = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE"] = new Rect2(100, 100, 100, 80)
        };
        var child = new LayoutItem("PROJECTED", "projected_view", new Rect2(0, 0, 80, 60), 75,
            "BASE", "top_left", "right");

        var result = new LayoutSolver().Solve(area, [], [child], 10, 500, fixedParent);

        Assert.Empty(result.Unresolved);
        var childRect = result.Placements["PROJECTED"];
        Assert.Equal(fixedParent["BASE"].Center.Y, childRect.Center.Y, 6);
        Assert.True(childRect.Left >= fixedParent["BASE"].Right + 10);
    }

    [Fact]
    public void ChainedProjectionPlacesParentBeforeLargerChild()
    {
        var area = new Rect2(0, 0, 700, 400);
        var baseView = new LayoutItem("BASE", "view", new Rect2(0, 0, 80, 70), 100);
        var first = new LayoutItem("FIRST", "projected_view", new Rect2(0, 0, 60, 50), 75,
            "BASE", null, "right");
        var second = new LayoutItem("SECOND", "projected_view", new Rect2(0, 0, 140, 90), 75,
            "FIRST", null, "right");

        var result = new LayoutSolver().Solve(area, [], [second, first, baseView], 10, 1000);

        Assert.Empty(result.Unresolved);
        Assert.True(result.Placements["FIRST"].Left >= result.Placements["BASE"].Right + 10);
        Assert.True(result.Placements["SECOND"].Left >= result.Placements["FIRST"].Right + 10);
        Assert.Equal(result.Placements["FIRST"].Center.Y, result.Placements["SECOND"].Center.Y, 6);
    }
}
