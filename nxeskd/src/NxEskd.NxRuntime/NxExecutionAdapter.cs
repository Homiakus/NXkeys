using System.Collections;
using System.Globalization;
using System.Reflection;
using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Runtime;
using NxEskd.Core.Utilities;

namespace NxEskd.NxRuntime;

public sealed class NxExecutionAdapter : IExecutionAdapter
{
    private readonly Session _session;
    private readonly UI _ui;
    private readonly Part _workPart;
    private readonly string _rootDirectory;
    private NxServiceContext? _context;

    public NxExecutionAdapter(string rootDirectory)
    {
        _session = Session.GetSession();
        _ui = UI.GetUI();
        _workPart = _session.Parts.Work ?? throw new InvalidOperationException("В NX не открыта рабочая деталь.");
        _rootDirectory = rootDirectory;
    }

    public string? CurrentPartPath => string.IsNullOrWhiteSpace(_workPart.FullPath) ? null : _workPart.FullPath;

    public ModelSnapshot AnalyzeModel(ProfileDocument profile, ExecutionReport report)
    {
        EnsureCurrentWorkPart();
        var context = Context(profile, report);
        var bodies = NxReflection.Enumerate(NxReflection.Get(_workPart, "Bodies")).ToArray();
        var componentAssembly = NxReflection.Get(_workPart, "ComponentAssembly");
        var rootComponent = NxReflection.Get(componentAssembly, "RootComponent");
        var components = EnumerateComponents(rootComponent).ToArray();
        var modelViews = NxReflection.Enumerate(NxReflection.Get(_workPart, "ModelingViews"))
            .Select(NxReflection.GetName).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray();
        var pmiManager = NxReflection.Get(_workPart, "PmiManager", "PMIManager");
        var pmiObjects = NxReflection.Enumerate(NxReflection.Get(pmiManager, "Objects", "PmiObjects", "Annotations")).Count();
        var displayPart = _session.Parts.Display;
        var units = NxReflection.Get(_workPart, "PartUnits", "Units")?.ToString() ?? "unknown";
        var boundingBox = TryReadBoundingBox(_session, bodies);
        var features = NxReflection.Get(_workPart, "Features");
        var sheetMetalManager = NxReflection.Get(features,
                                    context.ApiMap.Aliases("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager"))
                                ?? NxReflection.Get(_workPart,
                                    context.ApiMap.Aliases("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager"));
        var isSheetMetal = NxModelClassifier.IsSheetMetal(sheetMetalManager, bodies, features, context.Log);
        var datumPlanes = NxModelClassifier.DatumPlaneNames(_workPart, features);

        context.Log.Info($"Анализ модели: тел {bodies.Length}, компонентов {components.Length}, PMI {pmiObjects}, " +
                         $"datum planes {datumPlanes.Length}, sheet metal {(isSheetMetal ? "да" : "нет")}, " +
                         $"габарит {(boundingBox is null ? "не определён" : $"{boundingBox.SizeX:0.###}×{boundingBox.SizeY:0.###}×{boundingBox.SizeZ:0.###}")}.");
        return new ModelSnapshot(
            _workPart.Name,
            CurrentPartPath,
            displayPart?.Name,
            IsSamePart(displayPart, _workPart),
            units,
            bodies.Length,
            components.Length,
            modelViews,
            pmiObjects,
            boundingBox,
            rootComponent is not null || components.Length > 0,
            isSheetMetal,
            typeof(Session).Assembly.FullName ?? string.Empty,
            datumPlanes);
    }

    public void Preview(ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
    {
        EnsureCurrentWorkPart();
        var context = Context(profile, report);
        report.Messages.Add($"План: {plan.Sheets.Count} листов, {plan.Sheets.Sum(s => s.Views.Count)} видов, {plan.Operations.Count} операций.");
        foreach (var sheet in plan.Sheets)
            report.Messages.Add($"{sheet.Id}: {sheet.Format} {sheet.Orientation}, масштаб {sheet.Scale}, шаблон {sheet.TemplateId}, виды: {string.Join(", ", sheet.Views.Select(v => v.Id))}");
        var (operations, scheduleReport) = new DrawingOperationScheduler().Build(plan.Operations);
        foreach (var issue in scheduleReport.Issues) report.Issues.Add(issue);
        foreach (var operation in operations)
            report.Messages.Add($"{operation.OperationId}: {operation.ChangeKind} {operation.ObjectKind}/{operation.TargetId}; depends=[{string.Join(",", operation.Dependencies)}]");
        context.Log.Info("Предпросмотр исполняемого operation DAG сформирован без изменения детали.");
    }

    public ValidationReport Execute(DrawingCommand command, ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
    {
        var context = Context(profile, report);
        object? undoMark = null;
        try
        {
            EnsureCurrentWorkPart();
            if (!NxManagedObjectRegistry.Build(context).ValidateForExecution(context, command, plan))
                return new ValidationReport();

            undoMark = NxReflection.InvokeFactory(_session, ["SetUndoMark"], Session.MarkVisibility.Visible, "NX ESKD Drawing Generator");
            if (undoMark is null) throw new InvalidOperationException("NX не создал Undo mark; безопасное выполнение невозможно.");

            var validation = new NxDrawingOperationExecutor(
                context,
                plan,
                EnsureCurrentWorkPart,
                () => TryUpdate(undoMark)).Execute();
            if (report.Issues.Any(issue => issue.Severity == IssueSeverity.Error) || validation.HasErrors)
            {
                Rollback(undoMark, report);
                return validation;
            }
            return validation;
        }
        catch
        {
            if (undoMark is not null) Rollback(undoMark, report);
            throw;
        }
    }

    public ValidationReport ValidateResult(ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
    {
        EnsureCurrentWorkPart();
        return new NxValidationService(Context(profile, report)).Validate(plan);
    }

    public void Export(ProfileDocument profile, DrawingPlan plan, ExecutionReport report)
    {
        EnsureCurrentWorkPart();
        var context = Context(profile, report);
        if (JsonNavigator.GetBool(profile.Root, "$.output.updateBeforeSave", true))
            TryUpdate();
        if (!new NxNativePartPublisher(context).Save(plan)) return;
        if (!context.Report.Issues.Any(i => i.Severity == IssueSeverity.Error))
            new NxExportService(context).Export(plan);
    }

    public void Inventory(ProfileDocument profile, ExecutionReport report)
        => new NxCapabilityScanner(Context(profile, report)).WriteInventory(report);

    private NxServiceContext Context(ProfileDocument profile, ExecutionReport report)
    {
        if (_context is not null && ReferenceEquals(_context.Profile, profile) && ReferenceEquals(_context.Report, report)) return _context;
        var scopeId = JsonNavigator.GetString(profile.Root, "$.job.jobId")
                      ?? JsonNavigator.GetString(profile.Root, "$.job.id")
                      ?? JsonNavigator.GetString(profile.Root, "$.job.document.designation")
                      ?? _workPart.Name;
        _context = new NxServiceContext
        {
            Session = _session,
            Ui = _ui,
            WorkPart = _workPart,
            Profile = profile,
            Report = report,
            Log = new NxLog(_session),
            RootDirectory = _rootDirectory,
            ApiMap = NxApiMap.Load(_rootDirectory),
            ConfigHash = Hashing.Sha256(profile.ToJson()),
            ScopeId = scopeId
        };
        return _context;
    }

    private void TryUpdate(object? undoMark = null)
    {
        EnsureCurrentWorkPart();
        var updateManager = NxReflection.Get(_session, "UpdateManager")
                            ?? throw new InvalidOperationException("NX UpdateManager недоступен.");
        var ownsMark = undoMark is null;
        var mark = undoMark ?? NxReflection.InvokeFactory(_session, ["SetUndoMark"], Session.MarkVisibility.Invisible, "NX ESKD update")
                   ?? throw new InvalidOperationException("NX не создал Undo mark для обновления.");
        try
        {
            if (!NxReflection.InvokeCommand(updateManager, ["DoUpdate", "UpdateAll"], mark))
                throw new InvalidOperationException("NX update не был подтвержден выбранным API.");
        }
        finally
        {
            if (ownsMark) TryDeleteUndoMark(mark);
        }
    }

    private void EnsureCurrentWorkPart()
    {
        var current = _session.Parts.Work;
        if (current is null || !IsSamePart(current, _workPart))
            throw new InvalidOperationException("Рабочая деталь NX была закрыта или изменена после запуска команды. Операция отменена.");
    }

    private static bool IsSamePart(Part? a, Part? b)
    {
        if (a is null || b is null) return false;
        if (ReferenceEquals(a, b)) return true;
        if (a.Tag != Tag.Null && a.Tag == b.Tag) return true;
        if (!string.IsNullOrWhiteSpace(a.FullPath) && !string.IsNullOrWhiteSpace(b.FullPath))
            return string.Equals(Path.GetFullPath(a.FullPath), Path.GetFullPath(b.FullPath), StringComparison.OrdinalIgnoreCase);
        return string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void Rollback(object undoMark, ExecutionReport report)
    {
        try
        {
            if (!NxReflection.InvokeCommand(_session, ["UndoToMark"], undoMark, "NX ESKD rollback"))
                throw new InvalidOperationException("NX не подтвердил UndoToMark.");
            report.RolledBack = true;
        }
        catch (Exception rollbackException)
        {
            report.RolledBack = false;
            report.Issues.Add(new("NX_ROLLBACK_FAILED", IssueSeverity.Error,
                "Не удалось подтвердить откат NX: " + rollbackException.Message));
        }
        finally
        {
            TryDeleteUndoMark(undoMark);
        }
    }

    private void TryDeleteUndoMark(object mark)
    {
        try { _ = NxReflection.InvokeCommand(_session, ["DeleteUndoMark"], mark, "NX ESKD cleanup"); }
        catch (Exception ex) { _context?.Log.Warn("Не удалось удалить служебный Undo mark: " + ex.Message); }
    }

    private static IEnumerable<object> EnumerateComponents(object? rootComponent)
    {
        if (rootComponent is null) yield break;
        var stack = new Stack<object>();
        foreach (var child in NxReflection.Enumerate(NxReflection.GetOrInvoke(rootComponent, "Children", "GetChildren")))
            stack.Push(child);

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (stack.Count > 0)
        {
            var component = stack.Pop();
            if (!visited.Add(component)) continue;
            yield return component;
            foreach (var child in NxReflection.Enumerate(NxReflection.GetOrInvoke(component, "Children", "GetChildren")))
                stack.Push(child);
        }
    }

    private static BoundingBoxSnapshot? TryReadBoundingBox(Session session, IEnumerable<object> bodies)
    {
        BoundingBoxSnapshot? combined = null;
        foreach (var body in bodies)
        {
            if (!TryGetBodyBoundingBox(session, body, out var current)) continue;
            combined = combined is null
                ? current
                : new BoundingBoxSnapshot(
                    Math.Min(combined.MinX, current.MinX),
                    Math.Min(combined.MinY, current.MinY),
                    Math.Min(combined.MinZ, current.MinZ),
                    Math.Max(combined.MaxX, current.MaxX),
                    Math.Max(combined.MaxY, current.MaxY),
                    Math.Max(combined.MaxZ, current.MaxZ));
        }
        return combined;
    }

    private static bool TryGetBodyBoundingBox(Session session, object body, out BoundingBoxSnapshot box)
    {
        box = default!;
        try
        {
            var ufSession = NxReflection.Get(session, "UFSession");
            var modl = NxReflection.Get(ufSession, "Modl", "UFModl");
            var tag = NxReflection.Get(body, "Tag");
            if (modl is not null && tag is not null)
            {
                var bboxArray = new double[6];
                if (NxReflection.InvokeCommand(modl, ["AskBoundingBox", "AskBoundingBoxExact"], tag, bboxArray))
                {
                    box = new BoundingBoxSnapshot(bboxArray[0], bboxArray[1], bboxArray[2], bboxArray[3], bboxArray[4], bboxArray[5]);
                    if (IsValid(box)) return true;
                }
            }
        }
        catch { }

        try
        {
            var method = body.GetType().GetMethod("GetBoundingBox", BindingFlags.Instance | BindingFlags.Public);
            if (method is not null)
            {
                var args = new object?[3];
                method.Invoke(body, args);
                var minCorner = args[0];
                var boxSize = args[2];
                if (TryPoint(minCorner, out var min) && TryPoint(boxSize, out var size))
                {
                    box = new BoundingBoxSnapshot(min.X, min.Y, min.Z, min.X + size.X, min.Y + size.Y, min.Z + size.Z);
                    if (IsValid(box)) return true;
                }
            }
        }
        catch { }

        var raw = NxReflection.Get(body, "BoundingBox", "Bounds", "Box")
                  ?? NxReflection.InvokeFactory(body, ["GetBoundingBox", "AskBoundingBox"]);
        return TryParseBoundingBox(raw, out box);
    }

    private static bool TryParseBoundingBox(object? raw, out BoundingBoxSnapshot box)
    {
        box = default!;
        if (raw is null) return false;
        var values = new List<double>();
        if (raw is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
                if (TryDouble(item, out var number)) values.Add(number);
            if (values.Count >= 6)
            {
                box = new BoundingBoxSnapshot(values[0], values[1], values[2], values[3], values[4], values[5]);
                return IsValid(box);
            }
        }

        var min = NxReflection.Get(raw, "Min", "Minimum", "MinPoint", "Lower");
        var max = NxReflection.Get(raw, "Max", "Maximum", "MaxPoint", "Upper");
        if (TryPoint(min, out var minPoint) && TryPoint(max, out var maxPoint))
        {
            box = new BoundingBoxSnapshot(minPoint.X, minPoint.Y, minPoint.Z, maxPoint.X, maxPoint.Y, maxPoint.Z);
            return IsValid(box);
        }

        var names = new[] { "MinX", "MinY", "MinZ", "MaxX", "MaxY", "MaxZ" };
        values.Clear();
        foreach (var name in names)
        {
            if (!TryDouble(NxReflection.Get(raw, name), out var number)) return false;
            values.Add(number);
        }
        box = new BoundingBoxSnapshot(values[0], values[1], values[2], values[3], values[4], values[5]);
        return IsValid(box);
    }

    private static bool TryPoint(object? raw, out (double X, double Y, double Z) point)
    {
        point = default;
        if (raw is null) return false;
        if (!TryDouble(NxReflection.Get(raw, "X"), out var x)
            || !TryDouble(NxReflection.Get(raw, "Y"), out var y)
            || !TryDouble(NxReflection.Get(raw, "Z"), out var z)) return false;
        point = (x, y, z);
        return true;
    }

    private static bool TryDouble(object? value, out double number)
    {
        if (value is IConvertible convertible)
        {
            try
            {
                number = convertible.ToDouble(CultureInfo.InvariantCulture);
                return double.IsFinite(number);
            }
            catch { }
        }
        number = 0;
        return false;
    }

    private static bool IsValid(BoundingBoxSnapshot box)
        => double.IsFinite(box.MinX) && double.IsFinite(box.MinY) && double.IsFinite(box.MinZ)
           && double.IsFinite(box.MaxX) && double.IsFinite(box.MaxY) && double.IsFinite(box.MaxZ)
           && box.MaxX >= box.MinX && box.MaxY >= box.MinY && box.MaxZ >= box.MinZ;
}
