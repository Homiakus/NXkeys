using System.Globalization;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxFlatPatternService(NxServiceContext context)
{
    private const string ManagedId = "FLAT_PATTERN";
    private const string ObjectKind = "feature";

    public void EnsureFlatPattern(DrawingPlan plan)
    {
        var required = plan.DocumentKind.Equals("sheet_metal_drawing", StringComparison.OrdinalIgnoreCase)
                       || plan.Sheets.SelectMany(x => x.Views)
                           .Any(v => v.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase));
        if (!required) return;
        if (!plan.Model.IsSheetMetal)
        {
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_SKIPPED_NON_SHEET_METAL", IssueSeverity.Warning,
                "Flat Pattern пропущен: анализ модели не подтвердил листовой металл."));
            return;
        }

        var candidates = FindFlatPatternCandidates().ToArray();
        var existing = candidates.FirstOrDefault(IsOwned);
        if (existing is not null)
        {
            NxObjectTools.EnsureOwnershipMetadata(existing, ManagedId, context.Profile.ProfileId,
                context.ConfigHash, ObjectKind, context.ScopeId);
            if (!UpdateExisting(existing))
                context.Report.Issues.Add(new("NX_FLAT_PATTERN_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                    "Существующий управляемый Flat Pattern найден, но NX API не подтвердил его обновление."));
            else
                context.Report.UpdatedObjects.Add("feature:flat_pattern");
            return;
        }

        var unmanaged = candidates.FirstOrDefault();
        if (unmanaged is not null)
        {
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_UNMANAGED_COLLISION", IssueSeverity.Error,
                $"Найден существующий ручной или принадлежащий другой области Flat Pattern '{NxReflection.GetName(unmanaged)}'. " +
                "Автоматическое присвоение или изменение запрещено."));
            return;
        }

        if (!JsonNavigator.GetBool(context.Profile.Root, "$.sheetMetalFlatPattern.flatPatternSource.createIfMissing", true))
        {
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_MISSING", IssueSeverity.Error,
                "Flat Pattern отсутствует, а автоматическое создание запрещено."));
            return;
        }

        var stationary = ResolveStationaryFace();
        var xDirection = ResolveXDirection(stationary.Object);
        if (stationary.Object is null || xDirection.Object is null)
        {
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_REFERENCE_MISSING", IssueSeverity.Error,
                "Не удалось разрешить стационарную грань и/или направление X для Flat Pattern. Commit запрещён."));
            return;
        }

        AddAutomaticSelectionReviews(stationary, xDirection);
        var owner = ResolveSheetMetalManager();
        var builder = NxReflection.InvokeFactory(owner,
            context.ApiMap.Aliases("flatPattern.builder", "CreateFlatPatternBuilder", "CreateFlatSolidBuilder"),
            (object?)null);
        if (builder is null)
        {
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_BUILDER_UNSUPPORTED", IssueSeverity.Error,
                "Не найден Features.SheetmetalManager.CreateFlatPatternBuilder для локальной NX."));
            return;
        }

        var commitOwnsBuilder = false;
        try
        {
            if (!ConfigureBuilder(builder, stationary.Object, xDirection.Object))
                throw new InvalidOperationException("FlatPatternBuilder не принял UpwardFace и XAxisEdge.");
            commitOwnsBuilder = true;
            var result = NxReflection.CommitObjectAndDestroy(builder);
            NxObjectTools.TagManaged(result, ManagedId, context.Profile.ProfileId, context.ConfigHash,
                ObjectKind, context.ScopeId);
            WriteReferenceState(result, stationary.Identity, xDirection.Identity);
            context.Report.CreatedObjects.Add("feature:flat_pattern");
        }
        catch (Exception ex)
        {
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_CREATE_FAILED", IssueSeverity.Error, ex.Message));
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
        }
    }

    private object? ResolveSheetMetalManager()
    {
        var features = NxReflection.Get(context.WorkPart, "Features");
        return NxReflection.Get(features,
                   context.ApiMap.Aliases("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager"))
               ?? NxReflection.Get(context.WorkPart,
                   context.ApiMap.Aliases("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager"));
    }

    private bool UpdateExisting(object feature)
    {
        var stationary = ResolveStationaryFace();
        var xDirection = ResolveXDirection(stationary.Object);
        if (stationary.Object is null || xDirection.Object is null) return false;

        var oldStationary = NxObjectTools.GetStringAttribute(feature, "AUTO_DWG_STATIONARY_FACE");
        var oldDirection = NxObjectTools.GetStringAttribute(feature, "AUTO_DWG_X_DIRECTION");
        var stateChanged = !string.Equals(oldStationary, stationary.Identity, StringComparison.Ordinal)
                           || !string.Equals(oldDirection, xDirection.Identity, StringComparison.Ordinal);
        if (stateChanged)
        {
            var builder = NxReflection.InvokeFactory(ResolveSheetMetalManager(),
                context.ApiMap.Aliases("flatPattern.builder", "CreateFlatPatternBuilder", "CreateFlatSolidBuilder"),
                feature);
            if (builder is null)
            {
                context.Report.Issues.Add(new("NX_FLAT_PATTERN_REFERENCE_CHANGE_REQUIRES_RECREATE", IssueSeverity.Error,
                    "Изменилась стационарная грань или X-направление, но edit builder Flat Pattern недоступен."));
                return false;
            }
            var commitOwnsBuilder = false;
            try
            {
                if (!ConfigureBuilder(builder, stationary.Object, xDirection.Object)) return false;
                commitOwnsBuilder = true;
                NxReflection.CommitCommandAndDestroy(builder);
            }
            finally
            {
                if (!commitOwnsBuilder) NxReflection.Destroy(builder);
            }
        }

        _ = NxReflection.TryInvokeCommand(feature, ["Update", "Regenerate"]);
        WriteReferenceState(feature, stationary.Identity, xDirection.Identity);
        return true;
    }

    private bool ConfigureBuilder(object builder, object stationaryFace, object xDirectionEdge)
    {
        _ = SetAny(builder, ManagedId, "Name", "FeatureName");

        var upwardSelector = NxReflection.Get(builder,
            "UpwardFace", "StationaryFace", "FixedFace", "ReferenceFace");
        var xAxisSelector = NxReflection.Get(builder,
            "XAxisEdge", "XDirectionEdge", "ReferenceEdge", "DirectionEdge");

        var face = SetSelector(upwardSelector, stationaryFace)
                   || SetAny(builder, stationaryFace,
                       "UpwardFace", "StationaryFace", "FixedFace", "ReferenceFace");
        var direction = SetSelector(xAxisSelector, xDirectionEdge)
                        || SetAny(builder, xDirectionEdge,
                            "XAxisEdge", "XDirectionEdge", "ReferenceEdge", "DirectionEdge");

        if (!face)
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_STATIONARY_SET_UNSUPPORTED", IssueSeverity.Error,
                "NX API не подтвердил установку UpwardFace Flat Pattern."));
        if (!direction)
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_X_DIRECTION_SET_UNSUPPORTED", IssueSeverity.Error,
                "NX API не подтвердил установку XAxisEdge Flat Pattern."));
        return face && direction;
    }

    private static bool SetSelector(object? selector, object value)
    {
        if (selector is null) return false;
        if (NxReflection.TryInvokeCommand(selector,
                ["SetValue", "SetObject", "SetSelectedObject", "SetSelectedObjects"], value))
            return true;
        return NxReflection.Set(selector, value, "Value", "Object", "SelectedObject");
    }

    private void AddAutomaticSelectionReviews(SelectionResult stationary, SelectionResult xDirection)
    {
        if (stationary.Automatic && JsonNavigator.GetBool(context.Profile.Root,
                "$.sheetMetalFlatPattern.flatPatternSource.stationaryFace.manualReviewWhenAutomatic", true))
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_STATIONARY_AUTOMATIC", IssueSeverity.ManualReview,
                $"Стационарная грань выбрана автоматически: {stationary.Identity}. Проверьте ориентацию развертки."));
        if (xDirection.Automatic && JsonNavigator.GetBool(context.Profile.Root,
                "$.sheetMetalFlatPattern.flatPatternSource.xDirection.manualReviewWhenAutomatic", true))
            context.Report.Issues.Add(new("NX_FLAT_PATTERN_X_DIRECTION_AUTOMATIC", IssueSeverity.ManualReview,
                $"Направление X выбрано автоматически: {xDirection.Identity}. Проверьте ориентацию развертки."));
    }

    private SelectionResult ResolveStationaryFace()
    {
        var settings = JsonNavigator.GetObject(context.Profile.Root,
            "$.sheetMetalFlatPattern.flatPatternSource.stationaryFace");
        var attributeName = settings?["attributeName"]?.GetValue<string?>() ?? "FLAT_PATTERN_STATIONARY_FACE";
        var faces = EnumerateFaces().ToArray();
        var marked = faces.FirstOrDefault(face => IsTruthy(NxObjectTools.GetStringAttribute(face, attributeName))
                                                  || string.Equals(NxReflection.GetName(face), attributeName,
                                                      StringComparison.OrdinalIgnoreCase));
        if (marked is not null) return new SelectionResult(marked, StableIdentity(marked), false);

        var planar = faces.Where(IsPlanarFace)
            .Select(face => (Face: face, Area: ReadDouble(face, "Area", "SurfaceArea")
                                           ?? InvokeDouble(face, "GetArea", "AskArea") ?? 0))
            .OrderByDescending(x => x.Area)
            .ThenBy(x => StableIdentity(x.Face), StringComparer.Ordinal)
            .FirstOrDefault();
        return planar.Face is null
            ? SelectionResult.Missing
            : new SelectionResult(planar.Face, StableIdentity(planar.Face), true);
    }

    private SelectionResult ResolveXDirection(object? stationaryFace)
    {
        var settings = JsonNavigator.GetObject(context.Profile.Root,
            "$.sheetMetalFlatPattern.flatPatternSource.xDirection");
        var namedObject = settings?["namedObject"]?.GetValue<string?>() ?? "FLAT_PATTERN_X_EDGE";
        var edges = EnumerateEdges(stationaryFace).ToArray();
        var named = edges.FirstOrDefault(edge => string.Equals(NxReflection.GetName(edge), namedObject,
            StringComparison.OrdinalIgnoreCase));
        if (named is not null) return new SelectionResult(named, StableIdentity(named), false);

        var longest = edges.Select(edge => (Edge: edge, Length: ReadDouble(edge, "Length")
                                                       ?? InvokeDouble(edge, "GetLength", "AskLength") ?? 0))
            .OrderByDescending(x => x.Length)
            .ThenBy(x => StableIdentity(x.Edge), StringComparer.Ordinal)
            .FirstOrDefault();
        return longest.Edge is null
            ? SelectionResult.Missing
            : new SelectionResult(longest.Edge, StableIdentity(longest.Edge), true);
    }

    private IEnumerable<object> EnumerateFaces()
    {
        foreach (var body in NxReflection.Enumerate(NxReflection.Get(context.WorkPart, "Bodies")))
        foreach (var face in NxReflection.Enumerate(NxReflection.GetOrInvoke(body, "Faces", "GetFaces")))
            yield return face;
    }

    private IEnumerable<object> EnumerateEdges(object? stationaryFace)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (stationaryFace is not null)
        {
            foreach (var edge in NxReflection.Enumerate(NxReflection.GetOrInvoke(stationaryFace, "Edges", "GetEdges")))
                if (seen.Add(edge)) yield return edge;
        }
        foreach (var body in NxReflection.Enumerate(NxReflection.Get(context.WorkPart, "Bodies")))
        foreach (var edge in NxReflection.Enumerate(NxReflection.GetOrInvoke(body, "Edges", "GetEdges")))
            if (seen.Add(edge)) yield return edge;
    }

    private static bool IsPlanarFace(object face)
    {
        var type = NxReflection.Get(face, "SolidFaceType", "FaceType", "GeometryType", "Type")?.ToString()
                   ?? face.GetType().Name;
        return type.Contains("Planar", StringComparison.OrdinalIgnoreCase)
               || type.Equals("Plane", StringComparison.OrdinalIgnoreCase);
    }

    private bool SetAny(object target, object value, params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (NxReflection.Set(target, value, path)) return true;
            }
            catch (Exception ex)
            {
                context.Log.Warn($"Flat Pattern property {path}: {ex.Message}");
            }
        }
        return false;
    }

    private void WriteReferenceState(object feature, string stationaryIdentity, string directionIdentity)
    {
        NxObjectTools.SetStringAttribute(feature, "AUTO_DWG_STATIONARY_FACE", stationaryIdentity);
        NxObjectTools.SetStringAttribute(feature, "AUTO_DWG_X_DIRECTION", directionIdentity);
    }

    private bool IsOwned(object item)
        => NxObjectTools.IsManaged(item, ManagedId, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private IEnumerable<object> FindFlatPatternCandidates()
    {
        var preferred = JsonNavigator.GetArray(context.Profile.Root,
                "$.sheetMetalFlatPattern.flatPatternSource.preferredFeatureNames")?
            .Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray()
            ?? [ManagedId, "РАЗВЕРТКА"];
        var features = NxReflection.Get(context.WorkPart, "Features");
        return NxReflection.Enumerate(features).Where(f =>
            preferred.Any(n => string.Equals(NxReflection.GetName(f), n, StringComparison.OrdinalIgnoreCase))
            || f.GetType().Name.Contains("FlatPattern", StringComparison.OrdinalIgnoreCase));
    }

    private static string StableIdentity(object value)
        => NxReflection.Get(value, "JournalIdentifier", "Name", "Tag")?.ToString()
           ?? value.GetType().FullName
           ?? value.GetType().Name;

    private static bool IsTruthy(string? value)
        => value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
                                 || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                                 || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static double? ReadDouble(object target, params string[] names)
    {
        var raw = NxReflection.Get(target, names);
        if (raw is not IConvertible convertible) return null;
        try
        {
            var result = convertible.ToDouble(CultureInfo.InvariantCulture);
            return double.IsFinite(result) ? result : null;
        }
        catch { return null; }
    }

    private static double? InvokeDouble(object target, params string[] names)
    {
        try
        {
            var raw = NxReflection.InvokeFactory(target, names);
            if (raw is not IConvertible convertible) return null;
            var result = convertible.ToDouble(CultureInfo.InvariantCulture);
            return double.IsFinite(result) ? result : null;
        }
        catch { return null; }
    }

    private sealed record SelectionResult(object? Object, string Identity, bool Automatic)
    {
        public static SelectionResult Missing { get; } = new(null, string.Empty, false);
    }
}
