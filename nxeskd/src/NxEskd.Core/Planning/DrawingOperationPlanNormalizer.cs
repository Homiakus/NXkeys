namespace NxEskd.Core.Planning;

public static class DrawingOperationPlanNormalizer
{
    public static DrawingPlan Normalize(DrawingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var original = plan.Operations
            .Where(operation => !IsInjected(operation.OperationId))
            .ToArray();
        var result = new List<DrawingOperation>
        {
            Operation("setup:layers", "layers", "setup", "ensure", []),
            Operation("setup:styles", "drafting_styles", "setup", "apply", ["setup:layers"]),
            Operation("attributes:canonical", "work_part", "attributes", "update", ["setup:styles"])
        };

        foreach (var operation in original.Where(operation =>
                     operation.ObjectKind is not "validation" and not "output"))
        {
            var dependencies = operation.Dependencies.ToList();
            if (operation.ObjectKind is "sheet" or "feature"
                && !dependencies.Contains("attributes:canonical", StringComparer.OrdinalIgnoreCase))
                dependencies.Insert(0, "attributes:canonical");
            result.Add(operation with { Dependencies = dependencies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() });
        }

        var mutationIds = result
            .Where(operation => operation.ObjectKind is not "validation" and not "output")
            .Select(operation => operation.OperationId)
            .ToArray();
        result.Add(Operation(
            "reconciliation:managed_objects",
            "managed_objects",
            "reconciliation",
            "reconcile",
            mutationIds));
        result.Add(Operation(
            "validation:postconditions",
            "drawing",
            "validation",
            "validate",
            ["reconciliation:managed_objects"],
            ["nx_update_completed"]));

        var output = original.FirstOrDefault(operation => operation.ObjectKind.Equals("output", StringComparison.OrdinalIgnoreCase));
        if (output is not null)
            result.Add(output with { Dependencies = ["validation:postconditions"] });

        return plan with { Operations = result };
    }

    private static bool IsInjected(string id)
        => id.Equals("setup:layers", StringComparison.OrdinalIgnoreCase)
           || id.Equals("setup:styles", StringComparison.OrdinalIgnoreCase)
           || id.Equals("attributes:canonical", StringComparison.OrdinalIgnoreCase)
           || id.Equals("reconciliation:managed_objects", StringComparison.OrdinalIgnoreCase);

    private static DrawingOperation Operation(
        string operationId,
        string targetId,
        string objectKind,
        string changeKind,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<string>? preconditions = null)
        => new(
            operationId,
            targetId,
            objectKind,
            changeKind,
            dependencies,
            preconditions ?? Array.Empty<string>(),
            new Dictionary<string, object?>());
}
