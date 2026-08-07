using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Planning;

public sealed class DrawingOperationScheduler
{
    public (IReadOnlyList<DrawingOperation> Operations, ValidationReport Report) Build(
        IReadOnlyList<DrawingOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var report = new ValidationReport();
        var byId = new Dictionary<string, DrawingOperation>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in operations)
        {
            if (!byId.TryAdd(operation.OperationId, operation))
                report.Add(new(
                    "PLAN_DUPLICATE_OPERATION_ID",
                    IssueSeverity.Error,
                    $"Повторяется operationId '{operation.OperationId}'.",
                    ObjectId: operation.OperationId));
        }

        foreach (var operation in byId.Values)
        {
            foreach (var dependency in operation.Dependencies)
                if (!byId.ContainsKey(dependency))
                    report.Add(new(
                        "PLAN_OPERATION_DEPENDENCY_MISSING",
                        IssueSeverity.Error,
                        $"Операция '{operation.OperationId}' зависит от отсутствующей операции '{dependency}'.",
                        ObjectId: operation.OperationId));

            foreach (var precondition in operation.Preconditions)
                if (!DrawingOperationPreconditions.IsKnown(precondition))
                    report.Add(new(
                        "PLAN_OPERATION_PRECONDITION_UNKNOWN",
                        IssueSeverity.Error,
                        $"Операция '{operation.OperationId}' содержит неизвестное precondition '{precondition}'.",
                        ObjectId: operation.OperationId,
                        SuggestedFix: "Зарегистрируйте precondition в DrawingOperationPreconditions и реализуйте его в NX evaluator."));
        }

        if (report.HasErrors) return (Array.Empty<DrawingOperation>(), report);

        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<DrawingOperation>();
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string id, Stack<string> path)
        {
            if (!byId.TryGetValue(id, out var operation)) return;
            if (state.TryGetValue(id, out var current))
            {
                if (current == 2) return;
                if (current == 1)
                {
                    var cycle = string.Join(" -> ", path.Reverse().Append(id));
                    if (reportedCycles.Add(cycle))
                        report.Add(new(
                            "PLAN_OPERATION_CYCLE",
                            IssueSeverity.Error,
                            "Обнаружен цикл operation DAG: " + cycle,
                            ObjectId: id));
                    return;
                }
            }

            state[id] = 1;
            path.Push(id);
            foreach (var dependency in operation.Dependencies)
                Visit(dependency, path);
            path.Pop();
            state[id] = 2;
            ordered.Add(operation);
        }

        foreach (var operation in operations)
            Visit(operation.OperationId, new Stack<string>());
        return report.HasErrors
            ? (Array.Empty<DrawingOperation>(), report)
            : (ordered, report);
    }
}
