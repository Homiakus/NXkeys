using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;

namespace NxEskd.NxRuntime;

internal sealed class NxManagedObjectRegistry
{
    private readonly IReadOnlyList<ManagedObjectRecord> _records;

    private NxManagedObjectRegistry(IReadOnlyList<ManagedObjectRecord> records)
    {
        _records = records;
    }

    public static NxManagedObjectRegistry Build(NxServiceContext context)
    {
        var records = new List<ManagedObjectRecord>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

        AddCollection(records, seen, "sheet",
            NxReflection.Get(context.WorkPart, context.ApiMap.Aliases("sheet.collection", "DrawingSheets")));
        AddCollection(records, seen, "view",
            NxReflection.Get(context.WorkPart, context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews")));
        AddCollection(records, seen, "feature", NxReflection.Get(context.WorkPart, "Features"));

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        AddCollection(records, seen, "note", NxReflection.Get(annotations, "Notes", "DraftingNotes"));
        AddCollection(records, seen, "table", NxReflection.Get(annotations, "TableSections", "TabularNotes", "Tables"));
        AddCollection(records, seen, "parts_list",
            NxReflection.Get(annotations, context.ApiMap.Aliases("partsList.collection", "PartsLists", "PartsListCollection")));
        AddCollection(records, seen, "balloon",
            NxReflection.Get(annotations, context.ApiMap.Aliases("balloon.collection", "Balloons", "IdSymbols", "BalloonNotes")));
        AddCollection(records, seen, "title_block",
            NxReflection.Get(annotations, context.ApiMap.Aliases("titleBlock.collection", "TitleBlocks", "TitleBlockCollection")));

        return new NxManagedObjectRegistry(records);
    }

    public bool ValidateForExecution(
        NxServiceContext context,
        DrawingCommand command,
        DrawingPlan plan)
    {
        var valid = new NxCapabilityPreflight(context).Validate(command, plan);

        foreach (var group in _records.GroupBy(x => x.Key).Where(x => x.Count() > 1))
        {
            valid = false;
            context.Report.Issues.Add(new(
                "NX_MANAGED_ID_DUPLICATE",
                IssueSeverity.Error,
                $"В детали найдено {group.Count()} объектов с одним ownership key '{group.Key}'. " +
                "Автоматическое обновление заблокировано до устранения дубликатов.",
                ObjectId: group.Key.ManagedId));
        }

        foreach (var legacy in _records.Where(x => x.Key.ScopeId == "legacy"))
        {
            var conflicts = _records.Where(x =>
                !IsSameNxObject(x.Object, legacy.Object)
                && string.Equals(x.Key.ProfileId, legacy.Key.ProfileId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Key.ObjectKind, legacy.Key.ObjectKind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Key.ManagedId, legacy.Key.ManagedId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (conflicts.Length == 0) continue;

            valid = false;
            context.Report.Issues.Add(new(
                "NX_LEGACY_OWNERSHIP_AMBIGUOUS",
                IssueSeverity.Error,
                $"Legacy managed-объект '{legacy.Key.ManagedId}' не содержит AUTO_DWG_SCOPE_ID и конфликтует с другим объектом. " +
                "Автоматическая миграция ownership запрещена.",
                ObjectId: legacy.Key.ManagedId));
        }

        var inCurrentScope = _records.Where(x =>
                string.Equals(x.Key.ProfileId, context.Profile.ProfileId, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(x.Key.ScopeId, context.ScopeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Key.ScopeId, "legacy", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (command == DrawingCommand.Generate && inCurrentScope.Length > 0)
        {
            valid = false;
            context.Report.Issues.Add(new(
                "NX_GENERATE_ALREADY_MANAGED",
                IssueSeverity.Error,
                $"Generate запрещён: в детали уже найдено {inCurrentScope.Length} управляемых объектов текущего профиля/задания. Используйте Update."));
        }
        else if (command == DrawingCommand.Update && inCurrentScope.Length == 0)
        {
            valid = false;
            context.Report.Issues.Add(new(
                "NX_UPDATE_NOT_MANAGED",
                IssueSeverity.Error,
                "Update запрещён: в детали не найдено ни одного управляемого объекта текущего профиля/задания. Используйте Generate."));
        }

        var foreignSameIds = _records.Where(x =>
                string.Equals(x.Key.ProfileId, context.Profile.ProfileId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Key.ScopeId, "legacy", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Key.ScopeId, context.ScopeId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => (x.Key.ObjectKind, x.Key.ManagedId), StringTupleComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var group in foreignSameIds)
        {
            context.Report.Issues.Add(new(
                "NX_MANAGED_FOREIGN_SCOPE",
                IssueSeverity.Warning,
                $"Объект '{group.Key.ManagedId}' типа '{group.Key.ObjectKind}' принадлежит другой области задания. Он не будет изменён.",
                ObjectId: group.Key.ManagedId));
        }

        return valid && !context.Report.Issues.Any(x => x.Severity == IssueSeverity.Error);
    }

    private static void AddCollection(
        ICollection<ManagedObjectRecord> records,
        ISet<object> seen,
        string fallbackKind,
        object? collection)
    {
        foreach (var item in NxReflection.Enumerate(collection))
        {
            if (!seen.Add(item)) continue;
            if (NxObjectTools.TryGetManagedKey(item, fallbackKind, out var key))
                records.Add(new ManagedObjectRecord(item, key));
        }
    }

    private sealed record ManagedObjectRecord(object Object, ManagedObjectKey Key);

    private sealed class StringTupleComparer : IEqualityComparer<(string ObjectKind, string ManagedId)>
    {
        public static StringTupleComparer OrdinalIgnoreCase { get; } = new();

        public bool Equals((string ObjectKind, string ManagedId) x, (string ObjectKind, string ManagedId) y)
            => string.Equals(x.ObjectKind, y.ObjectKind, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.ManagedId, y.ManagedId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ObjectKind, string ManagedId) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ObjectKind),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ManagedId));
    }

    private static bool IsSameNxObject(object? a, object? b)
    {
        if (a is null || b is null) return false;
        if (ReferenceEquals(a, b)) return true;
        var tagA = NxReflection.Get(a, "Tag");
        var tagB = NxReflection.Get(b, "Tag");
        return tagA is not null && tagB is not null && Equals(tagA, tagB);
    }
}
