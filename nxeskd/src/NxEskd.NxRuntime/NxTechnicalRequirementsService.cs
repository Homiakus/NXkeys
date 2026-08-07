using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxTechnicalRequirementsService(NxServiceContext context)
{
    private const string ManagedId = "TECHNICAL_REQUIREMENTS";
    private const string ObjectKind = "note";

    public void CreateOrUpdate(DrawingPlan plan)
    {
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.technicalRequirements.enabled", true)) return;
        var lines = BuildLines();
        if (lines.Count == 0) return;

        var targetSheet = new NxSheetTargetService(context).Activate(plan, "main", "notes");
        if (targetSheet is null) return;
        var targetSheetId = NxObjectTools.GetStringAttribute(targetSheet, "AUTO_DWG_ID")
                            ?? plan.Sheets.FirstOrDefault()?.Id
                            ?? string.Empty;

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var notes = NxReflection.Get(annotations, "Notes", "DraftingNotes");
        var all = NxReflection.Enumerate(notes).ToArray();
        var existing = all.FirstOrDefault(IsOwned);
        if (existing is not null)
        {
            NxObjectTools.EnsureOwnershipMetadata(existing, ManagedId, context.Profile.ProfileId,
                context.ConfigHash, ObjectKind, context.ScopeId);
            if (SetText(existing, lines))
            {
                NxObjectTools.SetStringAttribute(existing, "AUTO_DWG_TARGET_SHEET_ID", targetSheetId);
                context.Report.UpdatedObjects.Add("note:" + ManagedId);
                return;
            }

            context.Report.Issues.Add(new("NX_NOTE_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                "Существующие технические требования найдены, но NX API не подтвердил их обновление. Создание второй заметки запрещено во избежание дубликатов."));
            return;
        }

        var nameCollision = all.FirstOrDefault(x =>
            string.Equals(NxReflection.GetName(x), ManagedId, StringComparison.OrdinalIgnoreCase)
            && !IsOwned(x));
        if (nameCollision is not null)
        {
            context.Report.Issues.Add(new("NX_NOTE_UNMANAGED_COLLISION", IssueSeverity.Error,
                "Найдена ручная или принадлежащая другой области заметка TECHNICAL_REQUIREMENTS. Автоматическое присвоение или изменение запрещено."));
            return;
        }

        var builder = NxReflection.InvokeFactory(annotations ?? notes,
            context.ApiMap.Aliases("note.builder", "CreateDraftingNoteBuilder", "CreateNoteBuilder", "DraftingNoteBuilder"), (object?)null);
        if (builder is null)
        {
            context.Report.Issues.Add(new("NX_NOTE_BUILDER_UNSUPPORTED", IssueSeverity.Warning,
                "Не найден DraftingNoteBuilder; технические требования не созданы автоматически."));
            return;
        }

        var commitOwnsBuilder = false;
        try
        {
            if (!SetText(builder, lines))
                throw new InvalidOperationException("NX DraftingNoteBuilder не принял текст технических требований.");
            var point = new Point3d(25, 55, 0);
            NxReflection.Set(builder, point, "Origin", "Placement", "Position", "Text.Origin");
            commitOwnsBuilder = true;
            var note = NxReflection.CommitObjectAndDestroy(builder);
            NxObjectTools.TagManaged(note, ManagedId, context.Profile.ProfileId, context.ConfigHash, ObjectKind, context.ScopeId);
            NxObjectTools.SetStringAttribute(note, "AUTO_DWG_TARGET_SHEET_ID", targetSheetId);
            context.Report.CreatedObjects.Add("note:" + ManagedId);
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
        }
    }

    private bool IsOwned(object item)
        => NxObjectTools.IsManaged(item, ManagedId, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private List<string> BuildLines()
    {
        var result = new List<(string Group, string Text)>();
        var items = JsonNavigator.GetArray(context.Profile.Root, "$.job.technicalRequirements.items") ?? [];
        foreach (var itemNode in items)
        {
            var item = itemNode!.AsObject();
            var text = item["text"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(text)) result.Add((item["group"]?.GetValue<string?>() ?? "references", text));
        }
        var unspecified = JsonNavigator.GetString(context.Profile.Root, "$.job.technicalRequirements.unspecifiedTolerances");
        if (!string.IsNullOrWhiteSpace(unspecified) && result.All(x => !x.Text.Equals(unspecified, StringComparison.OrdinalIgnoreCase)))
            result.Add(("dimensions_and_tolerances", unspecified));

        var order = JsonNavigator.GetArray(context.Profile.Root, "$.technicalRequirements.groupOrder")?
            .Select((x, i) => (Name: x?.GetValue<string?>() ?? string.Empty, Index: i))
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase) ?? [];
        return result.OrderBy(x => order.TryGetValue(x.Group, out var index) ? index : int.MaxValue)
            .Select((x, i) => $"{i + 1}. {x.Text.Trim()}").ToList();
    }

    private static bool SetText(object target, IReadOnlyList<string> lines)
    {
        var array = lines.ToArray();
        if (NxReflection.Set(target, array, "Text", "TextBlock", "Text.TextBlock", "Text.Text")) return true;
        foreach (var args in new object?[][] { [array], [string.Join(Environment.NewLine, lines)] })
        {
            try
            {
                if (NxReflection.InvokeCommand(target, ["SetText", "SetTextBlock", "SetLines"], args)) return true;
            }
            catch
            {
                break;
            }
        }
        var textObject = NxReflection.Get(target, "Text", "TextBlock");
        if (textObject is not null)
        {
            try { return NxReflection.InvokeCommand(textObject, ["SetText", "SetTextBlock", "SetLines"], array); }
            catch { }
        }
        return false;
    }
}
