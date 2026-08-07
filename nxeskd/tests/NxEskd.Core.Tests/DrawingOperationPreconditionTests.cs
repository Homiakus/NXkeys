using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class DrawingOperationPreconditionTests
{
    [Fact]
    public void UnknownPreconditionBlocksScheduling()
    {
        var operation = new DrawingOperation(
            "view:FRONT",
            "FRONT",
            "view",
            "ensure",
            [],
            ["invented_precondition"],
            new Dictionary<string, object?>());

        var (ordered, report) = new DrawingOperationScheduler().Build([operation]);

        Assert.Empty(ordered);
        Assert.Contains(report.Issues,
            issue => issue.Code == "PLAN_OPERATION_PRECONDITION_UNKNOWN");
    }

    [Fact]
    public void AllPlannerPreconditionsAreRegistered()
    {
        var known = DrawingOperationPreconditions.Known;

        Assert.Contains(DrawingOperationPreconditions.TemplateAvailable, known);
        Assert.Contains(DrawingOperationPreconditions.SourceModelViewResolved, known);
        Assert.Contains(DrawingOperationPreconditions.NxUpdateCompleted, known);
        Assert.Contains(DrawingOperationPreconditions.NoBlockingDiagnostics, known);
    }
}
