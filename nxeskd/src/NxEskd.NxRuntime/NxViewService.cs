using System.Collections;
using System.Globalization;
using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Layout;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxViewService(NxServiceContext context)
{
    private const string ObjectKind = "view";
    private readonly Dictionary<string, object> _views = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object> Views => _views;

    public void CreateOrUpdateViews(SheetPlan sheetPlan)
    {
        var collection = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews"));
        if (collection is null) throw new InvalidOperationException("Коллекция чертежных видов не найдена.");

        var all = NxReflection.Enumerate(collection).ToList();
        var placements = BuildPlacements(sheetPlan);
        foreach (var plan in sheetPlan.Views)
        {
            var existing = all.FirstOrDefault(x => IsOwned(x, plan.Id));
            if (existing is not null)
            {
                NxObjectTools.EnsureOwnershipMetadata(existing, plan.Id, context.Profile.ProfileId,
                    context.ConfigHash, ObjectKind, context.ScopeId);
                var point = PreserveManualPositionWhenRequired(existing, plan, placements[plan.Id]);
                if (!SynchronizeExisting(collection, existing, plan, sheetPlan.Id, point))
                    context.Report.Issues.Add(new("NX_VIEW_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                        $"Вид {plan.Id} найден, но NX API не подтвердил синхронизацию полного состояния.",
                        SheetId: sheetPlan.Id, ObjectId: plan.Id));
                _views[plan.Id] = existing;
                context.Report.UpdatedObjects.Add("view:" + plan.Id);
                continue;
            }

            var nameCollision = all.FirstOrDefault(x =>
                string.Equals(NxReflection.GetName(x), plan.Name, StringComparison.OrdinalIgnoreCase)
                && !IsOwned(x, plan.Id));
            if (nameCollision is not null)
            {
                context.Report.Issues.Add(new("NX_VIEW_UNMANAGED_COLLISION", IssueSeverity.Error,
                    $"Вид с именем '{plan.Name}' уже существует, но не принадлежит области '{context.Profile.ProfileId}/{context.ScopeId}'.",
                    SheetId: sheetPlan.Id, ObjectId: plan.Id));
                continue;
            }

            var created = CreateView(collection, plan, placements[plan.Id]);
            if (created is null)
            {
                context.Report.Issues.Add(new("NX_VIEW_CREATE_UNSUPPORTED", IssueSeverity.Error,
                    $"Не удалось создать вид {plan.Id} типа {plan.Type}. Запустите диагностику API.",
                    SheetId: sheetPlan.Id, ObjectId: plan.Id));
                continue;
            }
            NxObjectTools.TagManaged(created, plan.Id, context.Profile.ProfileId, context.ConfigHash,
                ObjectKind, context.ScopeId);
            WriteDesiredState(created, plan, sheetPlan.Id, placements[plan.Id]);
            _views[plan.Id] = created;
            all.Add(created);
            context.Report.CreatedObjects.Add("view:" + plan.Id);
            context.Log.Info($"Создан вид {plan.Id} ({plan.Type}).");
        }

        RefineLayoutWithActualBounds(sheetPlan);
        UpdateAllViews(collection);
        ValidateProjectionAlignment(sheetPlan);
    }

    private bool IsOwned(object item, string id)
        => NxObjectTools.IsManaged(item, id, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private bool SynchronizeExisting(
        object collection,
        object view,
        ViewPlan plan,
        string sheetId,
        Point2 point)
    {
        var storedType = NxObjectTools.GetStringAttribute(view, "AUTO_DWG_VIEW_TYPE");
        if (!string.IsNullOrWhiteSpace(storedType)
            && !storedType.Equals(plan.Type, StringComparison.OrdinalIgnoreCase))
        {
            context.Report.Issues.Add(new("NX_VIEW_KIND_CHANGE_REQUIRES_RECREATE", IssueSeverity.Error,
                $"Вид {plan.Id} имеет тип '{storedType}', профиль требует '{plan.Type}'. " +
                "Смена типа должна выполняться контролируемым delete/recreate после Preview.",
                SheetId: sheetId, ObjectId: plan.Id));
            return false;
        }

        var aliases = BuilderAliases(plan.Type);
        var builder = NxReflection.InvokeFactory(collection, aliases, view);
        if (builder is not null)
        {
            var commitOwnsBuilder = false;
            try
            {
                if (!ConfigureView(builder, plan, point)) return false;
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
            if (!ConfigureView(view, plan, point)) return false;
            try { _ = NxReflection.InvokeCommand(view, ["Update", "UpdateView", "Regenerate"]); }
            catch (Exception ex)
            {
                context.Report.Issues.Add(new("NX_VIEW_UPDATE_FAILED", IssueSeverity.Error,
                    $"Обновление вида {plan.Id} завершилось ошибкой: {ex.Message}",
                    SheetId: sheetId, ObjectId: plan.Id));
                return false;
            }
        }

        WriteDesiredState(view, plan, sheetId, point);
        return VerifyPositionWhenReadable(view, plan, point);
    }

    private object? CreateView(object collection, ViewPlan plan, Point2 point)
    {
        var builder = NxReflection.InvokeFactory(collection, BuilderAliases(plan.Type), (object?)null);
        if (builder is null) return null;

        var commitOwnsBuilder = false;
        try
        {
            if (!ConfigureView(builder, plan, point))
                throw new InvalidOperationException($"Builder вида {plan.Id} не принял обязательное состояние.");
            commitOwnsBuilder = true;
            return NxReflection.CommitObjectAndDestroy(builder);
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
        }
    }

    private bool ConfigureView(object target, ViewPlan plan, Point2 point)
    {
        _ = SetAny(target, plan.Name, "Name", "ViewName");
        var placementApplied = SetPlacement(target, point);
        var scaleApplied = SetScale(target, plan);
        _ = SetHiddenLines(target, plan.HiddenLines);

        var relationApplied = plan.Type.ToLowerInvariant() switch
        {
            "projected" => ConfigureProjected(target, plan),
            "section" or "half_section" or "stepped_section" => ConfigureSection(target, plan),
            "detail" => ConfigureDetail(target, plan),
            _ => ConfigureBase(target, plan)
        };
        return placementApplied && scaleApplied && relationApplied;
    }

    private bool ConfigureBase(object target, ViewPlan plan)
    {
        var modelViewName = plan.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase)
            ? plan.ModelView ?? "Flat Pattern"
            : plan.ModelView ?? plan.FallbackModelView ?? "Front";
        var modelView = FindModelView(modelViewName) ?? FindModelView(plan.FallbackModelView ?? string.Empty);
        if (modelView is null)
        {
            context.Report.Issues.Add(new("NX_MODEL_VIEW_MISSING", IssueSeverity.Error,
                $"Модельный вид '{modelViewName}' не найден для {plan.Id}.", ObjectId: plan.Id));
            return false;
        }
        if (!SetAny(target, modelView, "ModelView", "ModelingView", "ViewSource.ModelView", "Orientation.ModelView"))
        {
            context.Report.Issues.Add(new("NX_MODEL_VIEW_SET_UNSUPPORTED", IssueSeverity.Error,
                $"NX API не подтвердил источник model view для {plan.Id}.", ObjectId: plan.Id));
            return false;
        }

        if (plan.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase))
        {
            var flat = SetAny(target, true, "IsFlatPattern", "FlatPattern", "ViewStyle.FlatPattern");
            if (plan.InheritFlatPatternPmi)
                _ = SetAny(target, true, "InheritFlatPatternPmi", "ViewStyle.InheritFlatPatternPmi");
            if (!flat)
            {
                context.Report.Issues.Add(new("NX_FLAT_VIEW_FLAG_UNSUPPORTED", IssueSeverity.Error,
                    $"NX API не подтвердил режим Flat Pattern для вида {plan.Id}.", ObjectId: plan.Id));
                return false;
            }
        }
        return true;
    }

    private bool ConfigureProjected(object target, ViewPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.ParentViewId) || !_views.TryGetValue(plan.ParentViewId, out var parent))
        {
            context.Report.Issues.Add(new("NX_PARENT_VIEW_MISSING", IssueSeverity.Error,
                $"Родительский вид {plan.ParentViewId} не создан для {plan.Id}.", ObjectId: plan.Id));
            return false;
        }
        if (!SetAny(target, parent, "ParentView", "Parent", "SourceView"))
        {
            context.Report.Issues.Add(new("NX_PARENT_VIEW_SET_UNSUPPORTED", IssueSeverity.Error,
                $"NX API не подтвердил родителя {plan.ParentViewId} для {plan.Id}.", ObjectId: plan.Id));
            return false;
        }
        if (!string.IsNullOrWhiteSpace(plan.ProjectionDirection))
            _ = SetAny(target, plan.ProjectionDirection, "Direction", "ProjectionDirection");
        return true;
    }

    private bool ConfigureSection(object target, ViewPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.ParentViewId) || !_views.TryGetValue(plan.ParentViewId, out var parent))
        {
            context.Report.Issues.Add(new("NX_SECTION_PARENT_MISSING", IssueSeverity.Error,
                $"Родительский вид {plan.ParentViewId} не создан для разреза {plan.Id}.", ObjectId: plan.Id));
            return false;
        }
        if (!SetAny(target, parent, "ParentView", "Parent", "SourceView")) return false;
        if (!string.IsNullOrWhiteSpace(plan.SectionDatumPlane))
        {
            var datum = FindDatumPlane(plan.SectionDatumPlane);
            if (datum is null)
            {
                context.Report.Issues.Add(new("NX_SECTION_DATUM_MISSING", IssueSeverity.Error,
                    $"Плоскость разреза {plan.SectionDatumPlane} не найдена.", ObjectId: plan.Id));
                return false;
            }
            if (!SetAny(target, datum, "SectionPlane", "CuttingPlane", "Plane", "SectionLine.Plane"))
            {
                context.Report.Issues.Add(new("NX_SECTION_DATUM_SET_UNSUPPORTED", IssueSeverity.Error,
                    $"NX API не подтвердил плоскость разреза для {plan.Id}.", ObjectId: plan.Id));
                return false;
            }
        }
        if (!string.IsNullOrWhiteSpace(plan.SectionDirection))
            _ = SetAny(target, plan.SectionDirection, "Direction", "SectionDirection");
        return true;
    }

    private bool ConfigureDetail(object target, ViewPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.ParentViewId) || !_views.TryGetValue(plan.ParentViewId, out var parent))
        {
            context.Report.Issues.Add(new("NX_DETAIL_PARENT_MISSING", IssueSeverity.Error,
                $"Родительский вид не задан для detail view {plan.Id}.", ObjectId: plan.Id));
            return false;
        }
        if (!SetAny(target, parent, "ParentView", "Parent", "SourceView")) return false;
        context.Report.Issues.Add(new("NX_DETAIL_BOUNDARY_REQUIRES_VERIFICATION", IssueSeverity.ManualReview,
            $"Для detail view {plan.Id} родитель задан, но граница detail должна быть подтверждена локальным NX API/шаблоном.",
            ObjectId: plan.Id));
        return true;
    }

    private object? FindModelView(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var collections = new[]
        {
            NxReflection.Get(context.WorkPart, "ModelingViews"),
            NxReflection.Get(context.WorkPart, "Views"),
            NxReflection.Get(context.WorkPart, "ModelViews")
        };
        return collections.Select(c => NxReflection.FindByName(c, name)).FirstOrDefault(x => x is not null);
    }

    private object? FindDatumPlane(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return NxReflection.FindByName(NxReflection.Get(context.WorkPart, "Datums", "DatumPlanes"), name)
               ?? NxReflection.Enumerate(NxReflection.Get(context.WorkPart, "Features"))
                   .FirstOrDefault(x => string.Equals(NxReflection.GetName(x), name, StringComparison.OrdinalIgnoreCase));
    }

    private Point2 PreserveManualPositionWhenRequired(object view, ViewPlan plan, Point2 planned)
    {
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.execution.preserveManualViewPositions", true)) return planned;
        if (plan.Placement.X is not null && plan.Placement.Y is not null) return planned;
        if (!TryPoint(NxReflection.Get(view, "Origin", "Placement", "Center", "Position"), out var current)) return planned;
        if (!double.TryParse(NxObjectTools.GetStringAttribute(view, "AUTO_DWG_APPLIED_X"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var previousX)
            || !double.TryParse(NxObjectTools.GetStringAttribute(view, "AUTO_DWG_APPLIED_Y"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var previousY)) return planned;
        const double tolerance = 0.01;
        if (Math.Abs(current.X - previousX) <= tolerance && Math.Abs(current.Y - previousY) <= tolerance) return planned;
        context.Report.Messages.Add($"Ручная позиция вида {plan.Id} сохранена: ({current.X:0.###}; {current.Y:0.###}).");
        return current;
    }

    private Dictionary<string, Point2> BuildPlacements(SheetPlan sheetPlan)
    {
        var (width, height) = SheetSize(sheetPlan.Format, sheetPlan.Orientation);
        var area = new Rect2(20, 15, Math.Max(10, width - 40), Math.Max(10, height - 35));
        var reserved = new List<Rect2>
        {
            new(Math.Max(area.Left, width - 190), area.Bottom, Math.Min(180, area.Width), Math.Min(60, area.Height))
        };
        var result = new Dictionary<string, Point2>(StringComparer.OrdinalIgnoreCase);
        var fixedPlacements = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase);
        var autoItems = new List<LayoutItem>();
        foreach (var plan in sheetPlan.Views)
        {
            var size = EstimateBounds(plan);
            if (plan.Placement.X is double x && plan.Placement.Y is double y)
            {
                var rect = new Rect2(x - size.Width / 2, y - size.Height / 2, size.Width, size.Height);
                result[plan.Id] = new Point2(x, y);
                fixedPlacements[plan.Id] = rect;
                continue;
            }

            var isProjected = plan.Type.Equals("projected", StringComparison.OrdinalIgnoreCase);
            autoItems.Add(new LayoutItem(
                plan.Id,
                isProjected ? "projected_view" : "view",
                new Rect2(0, 0, size.Width, size.Height),
                Priority(plan.Type),
                plan.ParentViewId,
                PreferredAnchor(plan),
                isProjected ? plan.ProjectionDirection : null));
        }

        var gap = Math.Max(5, JsonNavigator.GetDouble(context.Profile.Root, "$.layoutSolver.minimumGapMm", 10));
        var layout = new LayoutSolver().Solve(
            area,
            reserved,
            autoItems,
            gap,
            JsonNavigator.GetInt(context.Profile.Root, "$.layoutSolver.maxIterations", 500),
            fixedPlacements);
        foreach (var item in autoItems)
        {
            if (layout.Placements.TryGetValue(item.Id, out var rect)) result[item.Id] = rect.Center;
            else
                context.Report.Issues.Add(new("NX_LAYOUT_UNRESOLVED", IssueSeverity.Error,
                    $"Не удалось разместить вид {item.Id} без пересечений и нарушения проекционной связи на листе {sheetPlan.Id}.",
                    SheetId: sheetPlan.Id, ObjectId: item.Id));
        }

        if (layout.Unresolved.Count > 0)
            throw new InvalidOperationException(
                $"Компоновка листа {sheetPlan.Id} неразрешима: {string.Join(", ", layout.Unresolved)}.");
        return result;
    }

    private void RefineLayoutWithActualBounds(SheetPlan sheetPlan)
    {
        var auto = sheetPlan.Views.Where(x => x.Placement.X is null || x.Placement.Y is null).ToArray();
        if (auto.Length == 0) return;
        var (width, height) = SheetSize(sheetPlan.Format, sheetPlan.Orientation);
        var area = new Rect2(20, 15, Math.Max(10, width - 40), Math.Max(10, height - 35));
        var reserved = new List<Rect2>
        {
            new(Math.Max(area.Left, width - 190), area.Bottom, Math.Min(180, area.Width), Math.Min(60, area.Height))
        };
        var fixedPlacements = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase);
        foreach (var fixedPlan in sheetPlan.Views.Where(x => x.Placement.X is not null && x.Placement.Y is not null))
        {
            var size = _views.TryGetValue(fixedPlan.Id, out var fixedView) && TryBounds(fixedView, out var actual)
                ? (actual.Width, actual.Height)
                : EstimateBounds(fixedPlan);
            fixedPlacements[fixedPlan.Id] = new Rect2(
                fixedPlan.Placement.X!.Value - size.Item1 / 2,
                fixedPlan.Placement.Y!.Value - size.Item2 / 2,
                size.Item1,
                size.Item2);
        }

        var items = auto.Select(plan =>
        {
            var size = _views.TryGetValue(plan.Id, out var view) && TryBounds(view, out var actual)
                ? (actual.Width, actual.Height)
                : EstimateBounds(plan);
            var isProjected = plan.Type.Equals("projected", StringComparison.OrdinalIgnoreCase);
            return new LayoutItem(
                plan.Id,
                isProjected ? "projected_view" : "view",
                new Rect2(0, 0, Math.Max(5, size.Item1), Math.Max(5, size.Item2)),
                Priority(plan.Type),
                plan.ParentViewId,
                PreferredAnchor(plan),
                isProjected ? plan.ProjectionDirection : null);
        }).ToArray();
        var layout = new LayoutSolver().Solve(
            area,
            reserved,
            items,
            Math.Max(5, JsonNavigator.GetDouble(context.Profile.Root, "$.layoutSolver.minimumGapMm", 10)),
            JsonNavigator.GetInt(context.Profile.Root, "$.layoutSolver.maxIterations", 500),
            fixedPlacements);

        if (layout.Unresolved.Count > 0)
        {
            foreach (var unresolved in layout.Unresolved)
                context.Report.Issues.Add(new("NX_LAYOUT_ACTUAL_BOUNDS_UNRESOLVED", IssueSeverity.Error,
                    $"Фактические bounds вида {unresolved} не удалось разместить без пересечения и нарушения проекционной связи.",
                    SheetId: sheetPlan.Id, ObjectId: unresolved));
            return;
        }

        foreach (var plan in auto)
        {
            if (!layout.Placements.TryGetValue(plan.Id, out var rect) || !_views.TryGetValue(plan.Id, out var view)) continue;
            var point = PreserveManualPositionWhenRequired(view, plan, rect.Center);
            if (!SetPlacement(view, point))
                context.Report.Issues.Add(new("NX_LAYOUT_APPLY_FAILED", IssueSeverity.Error,
                    $"Не удалось применить уточнённую позицию вида {plan.Id}.",
                    SheetId: sheetPlan.Id, ObjectId: plan.Id));
            else
                WriteDesiredState(view, plan, sheetPlan.Id, point);
        }
    }

    private void ValidateProjectionAlignment(SheetPlan sheetPlan)
    {
        const double tolerance = 0.1;
        foreach (var plan in sheetPlan.Views.Where(view =>
                     view.Type.Equals("projected", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(view.ParentViewId)
                     && !string.IsNullOrWhiteSpace(view.ProjectionDirection)))
        {
            if (!_views.TryGetValue(plan.Id, out var view)
                || !_views.TryGetValue(plan.ParentViewId!, out var parent)
                || !TryPoint(NxReflection.Get(view, "Origin", "Placement", "Center", "Position"), out var childPoint)
                || !TryPoint(NxReflection.Get(parent, "Origin", "Placement", "Center", "Position"), out var parentPoint))
                continue;

            var direction = plan.ProjectionDirection!.ToLowerInvariant();
            var deviation = direction is "top" or "up" or "bottom" or "down"
                ? Math.Abs(childPoint.X - parentPoint.X)
                : Math.Abs(childPoint.Y - parentPoint.Y);
            if (deviation <= tolerance) continue;

            context.Report.Issues.Add(new("NX_PROJECTED_VIEW_ALIGNMENT_FAILED", IssueSeverity.Error,
                $"Проекционный вид {plan.Id} отклонён от оси родительского вида {plan.ParentViewId} на {deviation:0.###} мм.",
                SheetId: sheetPlan.Id,
                ObjectId: plan.Id,
                SuggestedFix: "Проверьте направление проецирования и ручное перемещение вида."));
        }
    }

    private (double Width, double Height) EstimateBounds(ViewPlan plan)
    {
        var scale = plan.Scale.RelativeToMain ?? ParseScale(plan.Scale.ExplicitScale ?? "1:1");
        var baseSize = plan.Type.ToLowerInvariant() switch
        {
            "detail" => (70.0, 55.0),
            "section" or "half_section" or "stepped_section" => (110.0, 75.0),
            "flat_pattern" => (150.0, 100.0),
            "projected" => (110.0, 80.0),
            _ => (130.0, 90.0)
        };
        return (Math.Clamp(baseSize.Item1 * Math.Sqrt(Math.Max(scale, 0.01)), 35, 240),
            Math.Clamp(baseSize.Item2 * Math.Sqrt(Math.Max(scale, 0.01)), 25, 180));
    }

    private static string PreferredAnchor(ViewPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.Placement.PreferredAnchor)) return plan.Placement.PreferredAnchor;
        return plan.ProjectionDirection?.ToLowerInvariant() switch
        {
            "top" or "up" => "top_center",
            "bottom" or "down" => "bottom_center",
            "right" => "center_right",
            "left" => "center_left",
            _ => plan.Type.ToLowerInvariant() switch
            {
                "detail" => "top_right",
                "section" or "half_section" or "stepped_section" => "bottom_center",
                "flat_pattern" => "center",
                _ => "center_left"
            }
        };
    }

    private static int Priority(string type) => type.ToLowerInvariant() switch
    {
        "base" => 100,
        "flat_pattern" => 95,
        "section" or "half_section" or "stepped_section" => 85,
        "projected" => 75,
        "detail" => 65,
        _ => 50
    };

    private bool SetPlacement(object target, Point2 point)
    {
        var nxPoint = new Point3d(point.X, point.Y, 0.0);
        if (SetAny(target, nxPoint, "Placement", "PlacementPoint", "Origin", "Position", "ViewPlacement.Point")) return true;
        var x = SetAny(target, point.X, "X", "Placement.X", "ViewPlacement.X");
        var y = SetAny(target, point.Y, "Y", "Placement.Y", "ViewPlacement.Y");
        return x && y;
    }

    private bool SetScale(object target, ViewPlan plan)
    {
        var value = plan.Scale.RelativeToMain ?? ParseScale(plan.Scale.ExplicitScale ?? "1:1");
        return SetAny(target, value, "Scale", "ScaleFactor", "ViewScale", "Style.Scale");
    }

    private bool SetHiddenLines(object target, string hiddenLines)
    {
        var visible = hiddenLines.Equals("visible", StringComparison.OrdinalIgnoreCase);
        return SetAny(target, visible, "ShowHiddenLines", "Style.ShowHiddenLines", "ViewStyle.ShowHiddenLines")
               || SetAny(target, hiddenLines, "HiddenLines", "Style.HiddenLines", "ViewStyle.HiddenLines");
    }

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
                context.Log.Warn($"Не применено свойство вида {path}: {ex.Message}");
            }
        }
        return false;
    }

    private void WriteDesiredState(object view, ViewPlan plan, string sheetId, Point2 point)
    {
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_VIEW_TYPE", plan.Type);
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_SHEET_ID", sheetId);
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_PARENT_VIEW_ID", plan.ParentViewId ?? string.Empty);
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_MODEL_VIEW", plan.ModelView ?? plan.FallbackModelView ?? string.Empty);
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_VIEW_SCALE",
            (plan.Scale.RelativeToMain ?? ParseScale(plan.Scale.ExplicitScale ?? "1:1")).ToString("R", CultureInfo.InvariantCulture));
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_HIDDEN_LINES", plan.HiddenLines);
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_APPLIED_X", point.X.ToString("R", CultureInfo.InvariantCulture));
        NxObjectTools.SetStringAttribute(view, "AUTO_DWG_APPLIED_Y", point.Y.ToString("R", CultureInfo.InvariantCulture));
    }

    private bool VerifyPositionWhenReadable(object view, ViewPlan plan, Point2 expected)
    {
        if (!TryPoint(NxReflection.Get(view, "Origin", "Placement", "Center", "Position"), out var actual)) return true;
        const double tolerance = 0.05;
        if (Math.Abs(actual.X - expected.X) <= tolerance && Math.Abs(actual.Y - expected.Y) <= tolerance) return true;
        context.Report.Issues.Add(new("NX_VIEW_POSITION_POSTCONDITION_FAILED", IssueSeverity.Error,
            $"Вид {plan.Id}: позиция NX ({actual.X:0.###}; {actual.Y:0.###}) не совпадает с планом ({expected.X:0.###}; {expected.Y:0.###}).",
            ObjectId: plan.Id));
        return false;
    }

    private static bool TryPoint(object? raw, out Point2 point)
    {
        point = default;
        if (raw is null) return false;
        var x = TryDouble(NxReflection.Get(raw, "X"));
        var y = TryDouble(NxReflection.Get(raw, "Y"));
        if (x is null || y is null) return false;
        point = new Point2(x.Value, y.Value);
        return true;
    }

    private static bool TryBounds(object view, out (double Width, double Height) bounds)
    {
        bounds = default;
        var raw = NxReflection.Get(view, "Bounds", "BoundingBox", "Box", "ViewBounds")
                  ?? NxReflection.InvokeFactory(view, ["GetBounds", "GetBoundingBox"]);
        if (raw is null) return false;
        if (raw is IEnumerable enumerable && raw is not string)
        {
            var values = enumerable.Cast<object?>().Select(TryDouble).Where(x => x is not null).Cast<double>().ToArray();
            if (values.Length >= 4)
            {
                bounds = (Math.Abs(values[2] - values[0]), Math.Abs(values[3] - values[1]));
                return bounds.Width > 0 && bounds.Height > 0;
            }
        }
        var width = TryDouble(NxReflection.Get(raw, "Width"));
        var height = TryDouble(NxReflection.Get(raw, "Height"));
        if (width is null || height is null) return false;
        bounds = (Math.Abs(width.Value), Math.Abs(height.Value));
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static (double Width, double Height) SheetSize(string format, string orientation)
    {
        var result = format.ToUpperInvariant() switch
        {
            "A0" => (1189.0, 841.0), "A1" => (841.0, 594.0), "A2" => (594.0, 420.0),
            "A3" => (420.0, 297.0), _ => (210.0, 297.0)
        };
        if (orientation.Equals("portrait", StringComparison.OrdinalIgnoreCase) && result.Item1 > result.Item2)
            return (result.Item2, result.Item1);
        return result;
    }

    private static double ParseScale(string scale)
    {
        var parts = scale.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 2
               && double.TryParse(parts[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
               && double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
               && b > 0 ? a / b : 1.0;
    }

    private string[] BuilderAliases(string type) => type.ToLowerInvariant() switch
    {
        "projected" => context.ApiMap.Aliases("view.projectedBuilder", "CreateProjectedViewBuilder", "ProjectedViewBuilder"),
        "section" or "half_section" or "stepped_section" => context.ApiMap.Aliases("view.sectionBuilder", "CreateSectionViewBuilder", "SectionViewBuilder"),
        "detail" => context.ApiMap.Aliases("view.detailBuilder", "CreateDetailViewBuilder", "DetailViewBuilder"),
        _ => context.ApiMap.Aliases("view.baseBuilder", "CreateBaseViewBuilder", "BaseViewBuilder")
    };

    private static double? TryDouble(object? value)
    {
        if (value is not IConvertible convertible) return null;
        try
        {
            var number = convertible.ToDouble(CultureInfo.InvariantCulture);
            return double.IsFinite(number) ? number : null;
        }
        catch { return null; }
    }

    private void UpdateAllViews(object collection)
    {
        try
        {
            if (!NxReflection.InvokeCommand(collection,
                    context.ApiMap.Aliases("view.updateAll", "UpdateViews", "UpdateAll", "UpdateOutOfDateViews")))
                context.Report.Issues.Add(new("NX_VIEW_UPDATE_ALL_UNSUPPORTED", IssueSeverity.Error,
                    "NX API не подтвердил общее обновление чертёжных видов."));
        }
        catch (Exception ex)
        {
            context.Report.Issues.Add(new("NX_VIEW_UPDATE_ALL_FAILED", IssueSeverity.Error,
                "Обновление видов завершилось ошибкой: " + ex.Message));
        }
    }
}
