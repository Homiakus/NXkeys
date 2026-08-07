using NxEskd.Core.Planning;

namespace NxEskd.Core.Tests;

public sealed class DrawingOperationSchedulerTests
{
    [Fact]
    public void DependenciesAreOrderedBeforeDependents()
    {
        var operations = new[]
        {
            Operation("view:TOP", ["view:FRONT"]),
            Operation("view:FRONT", ["sheet:S1"]),
            Operation("sheet:S1", [])
        };

        var (ordered, report) = new DrawingOperationScheduler().Build(operations);

        Assert.False(report.HasErrors);
        Assert.True(IndexOf(ordered, "sheet:S1") < IndexOf(ordered, "view:FRONT"));
        Assert.True(IndexOf(ordered, "view:FRONT") < IndexOf(ordered, "view:TOP"));
    }

    [Fact]
    public void MissingDependencyIsBlocking()
    {
        var (ordered, report) = new DrawingOperationScheduler().Build(
            [Operation("view:TOP", ["view:MISSING"])]);

        Assert.Empty(ordered);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_OPERATION_DEPENDENCY_MISSING");
    }

    [Fact]
    public void CycleIsBlocking()
    {
        var operations = new[]
        {
            Operation("A", ["B"]),
            Operation("B", ["A"])
        };

        var (ordered, report) = new DrawingOperationScheduler().Build(operations);

        Assert.Empty(ordered);
        Assert.Contains(report.Issues, issue => issue.Code == "PLAN_OPERATION_CYCLE");
    }

    [Fact]
    public void NormalizerAddsRuntimeStagesAndPublicationGate()
    {
        var plan = new DrawingPlan(
            "P",
            "part_drawing",
            "A.001",
            "Деталь",
            [],
            new Dictionary<string, string>(),
            false)
        {
            Operations =
            [
                new DrawingOperation("sheet:S1", "S1", "sheet", "ensure", [], [],
                    new Dictionary<string, object?>()),
                new DrawingOperation("validation:postconditions", "drawing", "validation", "validate",
                    ["sheet:S1"], [], new Dictionary<string, object?>()),
                new DrawingOperation("output:publish", "drawing", "output", "publish",
                    ["validation:postconditions"], [], new Dictionary<string, object?>())
            ]
        };

        var normalized = DrawingOperationPlanNormalizer.Normalize(plan);
        var (ordered, report) = new DrawingOperationScheduler().Build(normalized.Operations);

        Assert.False(report.HasErrors);
        Assert.Contains(normalized.Operations, operation => operation.OperationId == "setup:layers");
        Assert.Contains(normalized.Operations, operation => operation.OperationId == "attributes:canonical");
        Assert.Contains(normalized.Operations, operation => operation.OperationId == "reconciliation:managed_objects");
        Assert.True(IndexOf(ordered, "attributes:canonical") < IndexOf(ordered, "sheet:S1"));
        Assert.True(IndexOf(ordered, "reconciliation:managed_objects") < IndexOf(ordered, "validation:postconditions"));
        Assert.True(IndexOf(ordered, "validation:postconditions") < IndexOf(ordered, "output:publish"));
    }

    private static DrawingOperation Operation(string id, IReadOnlyList<string> dependencies)
        => new(id, id, "view", "ensure", dependencies, [], new Dictionary<string, object?>());

    private static int IndexOf(IReadOnlyList<DrawingOperation> operations, string id)
        => operations.Select((operation, index) => (operation, index))
            .Single(pair => pair.operation.OperationId == id).index;
}
