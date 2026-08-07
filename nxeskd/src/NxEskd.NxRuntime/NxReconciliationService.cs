using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxReconciliationService(NxServiceContext context)
{
    public void Reconcile(DrawingPlan plan)
    {
        var current = EnumerateOwnedObjects().ToArray();
        var desired = BuildDesiredKeys(plan);
        var hasActiveBalloons = plan.Operations.Any(operation =>
                                    operation.ObjectKind.Equals("parts_list", StringComparison.OrdinalIgnoreCase))
                                && JsonNavigator.GetBool(context.Profile.Root,
                                    "$.partsListAndBalloons.balloons.enabled", true);
        var stale = current.Where(item =>
        {
            if (item.Key.ObjectKind.Equals("balloon", StringComparison.OrdinalIgnoreCase))
                return !hasActiveBalloons;
            return !desired.Contains((item.Key.ObjectKind, item.Key.ManagedId));
        }).ToArray();
        if (stale.Length == 0) return;

        foreach (var legacy in stale.Where(item => item.Key.ScopeId.Equals("legacy", StringComparison.OrdinalIgnoreCase)))
            context.Report.Issues.Add(new("NX_STALE_LEGACY_OBJECT", IssueSeverity.ManualReview,
                $"Legacy-объект '{legacy.Key.ManagedId}' не содержит scope и не может быть автоматически удалён.",
                ObjectId: legacy.Key.ManagedId));
        stale = stale.Where(item => !item.Key.ScopeId.Equals("legacy", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (stale.Length == 0) return;

        if (!JsonNavigator.GetBool(context.Profile.Root, "$.execution.idempotency.deleteManagedObjectsMissingFromConfig", false))
        {
            foreach (var item in stale)
                context.Report.Issues.Add(new("NX_STALE_MANAGED_OBJECT_PRESERVED", IssueSeverity.Warning,
                    $"Устаревший управляемый объект '{item.Key.ObjectKind}/{item.Key.ManagedId}' сохранён согласно профилю.",
                    ObjectId: item.Key.ManagedId));
            return;
        }

        if (!JsonNavigator.GetBool(context.Profile.Root, "$.execution.idempotency.confirmManagedDeletion", false))
        {
            context.Report.Issues.Add(new("NX_MANAGED_DELETION_CONFIRMATION_REQUIRED", IssueSeverity.Error,
                $"Найдено {stale.Length} устаревших управляемых объектов. Удаление заблокировано, пока " +
                "$.execution.idempotency.confirmManagedDeletion не установлено в true для этого запуска.",
                SuggestedFix: "Сначала выполнить Preview, проверить список stale-объектов и только затем явно разрешить удаление."));
            foreach (var item in stale)
                context.Report.Messages.Add($"STALE: {item.Key.ObjectKind}/{item.Key.ManagedId}");
            return;
        }

        foreach (var item in stale.OrderBy(item => DeletePriority(item.Key.ObjectKind)))
        {
            if (!NxManagedDeletionService.TrySchedule(context, item.Object, out var error))
            {
                context.Report.Issues.Add(new("NX_MANAGED_DELETE_UNSUPPORTED", IssueSeverity.Error,
                    $"Не удалось поставить на удаление '{item.Key.ObjectKind}/{item.Key.ManagedId}': {error}",
                    ObjectId: item.Key.ManagedId));
                continue;
            }
            context.Report.UpdatedObjects.Add($"deleted:{item.Key.ObjectKind}:{item.Key.ManagedId}");
            context.Log.Info($"Управляемый объект поставлен на удаление: {item.Key.ObjectKind}/{item.Key.ManagedId}.");
        }
    }

    private static HashSet<(string Kind, string Id)> BuildDesiredKeys(DrawingPlan plan)
    {
        var desired = new HashSet<(string Kind, string Id)>(KindIdComparer.Instance);
        foreach (var sheet in plan.Sheets)
        {
            desired.Add(("sheet", sheet.Id));
            foreach (var view in sheet.Views) desired.Add(("view", view.Id));
        }

        foreach (var operation in plan.Operations)
        {
            switch (operation.ObjectKind.ToLowerInvariant())
            {
                case "feature" when operation.TargetId.Equals("FLAT_PATTERN", StringComparison.OrdinalIgnoreCase):
                    desired.Add(("feature", "FLAT_PATTERN"));
                    break;
                case "parts_list":
                    desired.Add(("parts_list", operation.TargetId));
                    break;
                case "note":
                    desired.Add(("note", operation.TargetId));
                    break;
                case "table":
                    desired.Add(("table", operation.TargetId));
                    break;
            }
        }
        return desired;
    }

    private IEnumerable<OwnedObject> EnumerateOwnedObjects()
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var item in Collections())
        foreach (var value in NxReflection.Enumerate(item.Collection))
        {
            if (!seen.Add(value)) continue;
            if (!NxObjectTools.TryGetManagedKey(value, item.FallbackKind, out var key)) continue;
            if (!key.ProfileId.Equals(context.Profile.ProfileId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!key.ScopeId.Equals(context.ScopeId, StringComparison.OrdinalIgnoreCase)
                && !key.ScopeId.Equals("legacy", StringComparison.OrdinalIgnoreCase)) continue;
            yield return new OwnedObject(value, key);
        }
    }

    private IEnumerable<(string FallbackKind, object? Collection)> Collections()
    {
        yield return ("sheet", NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("sheet.collection", "DrawingSheets")));
        yield return ("view", NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews")));
        yield return ("feature", NxReflection.Get(context.WorkPart, "Features"));
        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        yield return ("note", NxReflection.Get(annotations, "Notes", "DraftingNotes"));
        yield return ("table", NxReflection.Get(annotations, "TableSections", "TabularNotes", "Tables"));
        yield return ("parts_list", NxReflection.Get(annotations,
            context.ApiMap.Aliases("partsList.collection", "PartsLists", "PartsListCollection")));
        yield return ("balloon", NxReflection.Get(annotations,
            context.ApiMap.Aliases("balloon.collection", "Balloons", "IdSymbols", "BalloonNotes")));
    }

    private static int DeletePriority(string kind) => kind.ToLowerInvariant() switch
    {
        "pmi" => 0,
        "balloon" => 5,
        "note" or "table" or "parts_list" => 10,
        "view" => 20,
        "sheet" => 30,
        "feature" => 40,
        _ => 50
    };

    private sealed record OwnedObject(object Object, ManagedObjectKey Key);

    private sealed class KindIdComparer : IEqualityComparer<(string Kind, string Id)>
    {
        public static KindIdComparer Instance { get; } = new();

        public bool Equals((string Kind, string Id) x, (string Kind, string Id) y)
            => x.Kind.Equals(y.Kind, StringComparison.OrdinalIgnoreCase)
               && x.Id.Equals(y.Id, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Kind, string Id) obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Kind),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id));
    }
}
