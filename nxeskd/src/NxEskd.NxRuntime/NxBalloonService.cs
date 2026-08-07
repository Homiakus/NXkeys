using System.Globalization;
using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.NxRuntime;

internal sealed class NxBalloonService(NxServiceContext context)
{
    private const string ObjectKind = "balloon";

    public void CreateOrUpdate(IReadOnlyList<NxBomRow> rows)
    {
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.partsListAndBalloons.balloons.enabled", true)) return;
        if (rows.Count == 0) return;

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var collection = NxReflection.Get(annotations, context.ApiMap.Aliases(
            "balloon.collection", "Balloons", "IdSymbols", "BalloonNotes"));
        if (collection is null)
        {
            context.Report.Issues.Add(new("NX_BALLOON_COLLECTION_MISSING", IssueSeverity.Error,
                "Не найдена коллекция balloons/ID symbols. Позиционные обозначения не созданы."));
            return;
        }

        var targetView = ResolveTargetView();
        if (targetView is null)
        {
            context.Report.Issues.Add(new("NX_BALLOON_TARGET_VIEW_MISSING", IssueSeverity.Error,
                "Не найден управляемый чертёжный вид для размещения позиций."));
            return;
        }

        var existing = NxReflection.Enumerate(collection).ToArray();
        var desiredIds = rows.Select(row => ManagedId(row.Position))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placements = BuildPlacements(rows, targetView);
        foreach (var row in rows)
        {
            var managedId = ManagedId(row.Position);
            var balloon = existing.FirstOrDefault(item => IsOwned(item, managedId));
            if (balloon is not null)
            {
                NxObjectTools.EnsureOwnershipMetadata(balloon, managedId, context.Profile.ProfileId,
                    context.ConfigHash, ObjectKind, context.ScopeId);
                if (!SynchronizeExisting(collection, balloon, row, targetView, placements[row.Position]))
                    context.Report.Issues.Add(new("NX_BALLOON_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                        $"Позиция {row.Position} найдена, но NX API не подтвердил её ассоциацию/текст/размещение.",
                        ObjectId: managedId));
                else
                    context.Report.UpdatedObjects.Add("balloon:" + managedId);
                continue;
            }

            var created = CreateBalloon(collection, row, targetView, placements[row.Position]);
            if (created is null)
            {
                context.Report.Issues.Add(new("NX_BALLOON_CREATE_UNSUPPORTED", IssueSeverity.Error,
                    $"Не удалось создать ассоциативную позицию {row.Position}.", ObjectId: managedId));
                continue;
            }
            NxObjectTools.TagManaged(created, managedId, context.Profile.ProfileId, context.ConfigHash,
                ObjectKind, context.ScopeId);
            WriteState(created, row, targetView, placements[row.Position]);
            context.Report.CreatedObjects.Add("balloon:" + managedId);
        }

        ReconcileStale(existing, desiredIds);
    }

    private object? ResolveTargetView()
    {
        var views = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews"));
        var all = NxReflection.Enumerate(views)
            .Where(view => NxObjectTools.IsManaged(view, profileId: context.Profile.ProfileId,
                objectKind: "view", scopeId: context.ScopeId))
            .ToArray();
        if (all.Length == 0) return null;

        var preferred = JsonNavigator.GetArray(context.Profile.Root,
            "$.partsListAndBalloons.balloons.preferredViewIds")?
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray() ?? [];
        foreach (var id in preferred)
        {
            var match = all.FirstOrDefault(view => string.Equals(
                NxObjectTools.GetStringAttribute(view, "AUTO_DWG_ID"), id, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        var baseView = all.FirstOrDefault(view => string.Equals(
            NxObjectTools.GetStringAttribute(view, "AUTO_DWG_VIEW_TYPE"), "base", StringComparison.OrdinalIgnoreCase));
        var selected = baseView ?? all[0];
        context.Report.Issues.Add(new("NX_BALLOON_VIEW_AUTO_SELECTED", IssueSeverity.ManualReview,
            $"Вид для позиций выбран автоматически: {NxObjectTools.GetStringAttribute(selected, "AUTO_DWG_ID") ?? NxReflection.GetName(selected)}."));
        return selected;
    }

    private Dictionary<int, Point3d> BuildPlacements(IReadOnlyList<NxBomRow> rows, object targetView)
    {
        var minimumGap = Math.Max(3, JsonNavigator.GetDouble(context.Profile.Root,
            "$.partsListAndBalloons.balloons.minimumGapMm", 5));
        var center = ReadCenter(targetView) ?? new Point3d(150, 120, 0);
        var bounds = ReadBounds(targetView);
        var radiusX = Math.Max(45, (bounds?.Width ?? 100) / 2 + 20);
        var radiusY = Math.Max(35, (bounds?.Height ?? 70) / 2 + 20);
        var result = new Dictionary<int, Point3d>();
        for (var i = 0; i < rows.Count; i++)
        {
            var angle = 2 * Math.PI * i / Math.Max(rows.Count, 1);
            var x = center.X + radiusX * Math.Cos(angle);
            var y = center.Y + radiusY * Math.Sin(angle);
            if (i > 0)
            {
                var previous = result[rows[i - 1].Position];
                if (Distance(previous, new Point3d(x, y, 0)) < minimumGap)
                    y += minimumGap;
            }
            result[rows[i].Position] = new Point3d(x, y, 0);
        }
        return result;
    }

    private object? CreateBalloon(object collection, NxBomRow row, object targetView, Point3d point)
    {
        var builder = NxReflection.InvokeFactory(collection, context.ApiMap.Aliases(
            "balloon.builder", "CreateBalloonBuilder", "CreateIdSymbolBuilder", "CreateBalloonNoteBuilder"),
            (object?)null);
        if (builder is null) return null;
        var commitOwnsBuilder = false;
        try
        {
            if (!Configure(builder, row, targetView, point)) return null;
            commitOwnsBuilder = true;
            return NxReflection.CommitObjectAndDestroy(builder);
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
        }
    }

    private bool SynchronizeExisting(
        object collection,
        object balloon,
        NxBomRow row,
        object targetView,
        Point3d point)
    {
        var builder = NxReflection.InvokeFactory(collection, context.ApiMap.Aliases(
            "balloon.builder", "CreateBalloonBuilder", "CreateIdSymbolBuilder", "CreateBalloonNoteBuilder"),
            balloon);
        if (builder is not null)
        {
            var commitOwnsBuilder = false;
            try
            {
                if (!Configure(builder, row, targetView, point)) return false;
                commitOwnsBuilder = true;
                NxReflection.CommitCommandAndDestroy(builder);
            }
            finally
            {
                if (!commitOwnsBuilder) NxReflection.Destroy(builder);
            }
        }
        else
        {
            if (!Configure(balloon, row, targetView, point)) return false;
            try
            {
                if (!NxReflection.InvokeCommand(balloon, ["Update", "Regenerate", "Refresh"])) return false;
            }
            catch (Exception ex)
            {
                context.Log.Warn($"Balloon {row.Position} update: {ex.Message}");
                return false;
            }
        }
        WriteState(balloon, row, targetView, point);
        return true;
    }

    private bool Configure(object target, NxBomRow row, object targetView, Point3d point)
    {
        var text = row.Position.ToString(CultureInfo.InvariantCulture);
        var association = SetAny(target, row.RepresentativeComponent,
            "AssociatedObject", "Object", "Component", "Leader.AssociatedObject", "AssociativeObject");
        var view = SetAny(target, targetView, "View", "DrawingView", "TargetView", "Leader.View");
        var textApplied = SetAny(target, text, "Text", "Value", "Label", "SymbolText", "UpperText");
        var placement = SetAny(target, point, "Origin", "Placement", "Position", "AnnotationOrigin");
        if (!association)
            context.Report.Issues.Add(new("NX_BALLOON_ASSOCIATION_UNSUPPORTED", IssueSeverity.Error,
                $"NX API не подтвердил ассоциацию позиции {row.Position} с компонентом {row.PartNumber}."));
        if (!view)
            context.Report.Issues.Add(new("NX_BALLOON_VIEW_SET_UNSUPPORTED", IssueSeverity.Error,
                $"NX API не подтвердил вид для позиции {row.Position}."));
        if (!textApplied || !placement)
            context.Report.Issues.Add(new("NX_BALLOON_CONTENT_UNSUPPORTED", IssueSeverity.Error,
                $"NX API не подтвердил текст/размещение позиции {row.Position}."));
        return association && view && textApplied && placement;
    }

    private void WriteState(object balloon, NxBomRow row, object targetView, Point3d point)
    {
        NxObjectTools.SetStringAttribute(balloon, "AUTO_DWG_POSITION",
            row.Position.ToString(CultureInfo.InvariantCulture));
        NxObjectTools.SetStringAttribute(balloon, "AUTO_DWG_COMPONENT_IDENTITY", row.Identity);
        NxObjectTools.SetStringAttribute(balloon, "AUTO_DWG_TARGET_VIEW_ID",
            NxObjectTools.GetStringAttribute(targetView, "AUTO_DWG_ID") ?? string.Empty);
        NxObjectTools.SetStringAttribute(balloon, "AUTO_DWG_APPLIED_X", point.X.ToString("R", CultureInfo.InvariantCulture));
        NxObjectTools.SetStringAttribute(balloon, "AUTO_DWG_APPLIED_Y", point.Y.ToString("R", CultureInfo.InvariantCulture));
    }

    private void ReconcileStale(IEnumerable<object> existing, IReadOnlySet<string> desiredIds)
    {
        var stale = existing.Where(item =>
        {
            if (!NxObjectTools.IsManaged(item, profileId: context.Profile.ProfileId,
                    objectKind: ObjectKind, scopeId: context.ScopeId)) return false;
            var id = NxObjectTools.GetStringAttribute(item, "AUTO_DWG_ID");
            return !string.IsNullOrWhiteSpace(id) && !desiredIds.Contains(id);
        }).ToArray();
        if (stale.Length == 0) return;

        if (!JsonNavigator.GetBool(context.Profile.Root,
                "$.execution.idempotency.deleteManagedObjectsMissingFromConfig", false))
        {
            foreach (var item in stale)
            {
                var id = NxObjectTools.GetStringAttribute(item, "AUTO_DWG_ID") ?? "UNKNOWN";
                context.Report.Issues.Add(new("NX_STALE_BALLOON_PRESERVED", IssueSeverity.Warning,
                    $"Устаревшая управляемая позиция '{id}' сохранена политикой удаления.", ObjectId: id));
            }
            return;
        }

        if (!JsonNavigator.GetBool(context.Profile.Root,
                "$.execution.idempotency.confirmManagedDeletion", false))
        {
            context.Report.Issues.Add(new("NX_BALLOON_DELETION_CONFIRMATION_REQUIRED", IssueSeverity.Error,
                $"Найдено {stale.Length} устаревших позиций. Сначала выполните Preview, затем явно подтвердите удаление managed-объектов."));
            foreach (var item in stale)
                context.Report.Messages.Add("STALE: balloon/" +
                    (NxObjectTools.GetStringAttribute(item, "AUTO_DWG_ID") ?? "UNKNOWN"));
            return;
        }

        foreach (var item in stale)
        {
            var id = NxObjectTools.GetStringAttribute(item, "AUTO_DWG_ID") ?? "UNKNOWN";
            if (!NxManagedDeletionService.TrySchedule(context, item, out var error))
            {
                context.Report.Issues.Add(new("NX_STALE_BALLOON_DELETE_FAILED", IssueSeverity.Error,
                    $"Не удалось удалить устаревшую позицию '{id}': {error}", ObjectId: id));
                continue;
            }
            context.Report.UpdatedObjects.Add("deleted:balloon:" + id);
        }
    }

    private bool IsOwned(object item, string id)
        => NxObjectTools.IsManaged(item, id, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private bool SetAny(object target, object? value, params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (NxReflection.Set(target, value, path)) return true;
            }
            catch (Exception ex)
            {
                context.Log.Warn($"Balloon property {path}: {ex.Message}");
            }
        }
        return false;
    }

    private static Point3d? ReadCenter(object view)
    {
        var raw = NxReflection.Get(view, "Origin", "Center", "Position", "Placement");
        var x = ReadDouble(NxReflection.Get(raw, "X"));
        var y = ReadDouble(NxReflection.Get(raw, "Y"));
        return x is null || y is null ? null : new Point3d(x.Value, y.Value, 0);
    }

    private static (double Width, double Height)? ReadBounds(object view)
    {
        var raw = NxReflection.Get(view, "Bounds", "BoundingBox", "ViewBounds");
        var width = ReadDouble(NxReflection.Get(raw, "Width"));
        var height = ReadDouble(NxReflection.Get(raw, "Height"));
        return width is null || height is null ? null : (Math.Abs(width.Value), Math.Abs(height.Value));
    }

    private static double? ReadDouble(object? value)
    {
        if (value is not IConvertible convertible) return null;
        try
        {
            var number = convertible.ToDouble(CultureInfo.InvariantCulture);
            return double.IsFinite(number) ? number : null;
        }
        catch { return null; }
    }

    private static double Distance(Point3d a, Point3d b)
        => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static string ManagedId(int position) => "BALLOON_" + position.ToString(CultureInfo.InvariantCulture);
}
