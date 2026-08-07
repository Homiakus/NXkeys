using System.Reflection;
using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxSheetService(NxServiceContext context)
{
    private const string ObjectKind = "sheet";

    public object EnsureSheet(SheetPlan plan, int sheetNumber)
    {
        var collection = NxReflection.Get(context.WorkPart, context.ApiMap.Aliases("sheet.collection", "DrawingSheets"));
        if (collection is null) throw new InvalidOperationException("В NX Open не найдена коллекция DrawingSheets.");

        var all = NxReflection.Enumerate(collection).ToArray();
        var existing = all.FirstOrDefault(x => IsOwned(x, plan.Id));
        var nameCollision = all.FirstOrDefault(x =>
            string.Equals(NxReflection.GetName(x), plan.Id, StringComparison.OrdinalIgnoreCase)
            && !IsOwned(x, plan.Id));
        if (existing is null && nameCollision is not null)
            throw new InvalidOperationException($"Лист с именем '{plan.Id}' уже существует, но не принадлежит области '{context.Profile.ProfileId}/{context.ScopeId}'. Автоматическое присвоение ручного или чужого листа запрещено.");
        if (existing is not null)
        {
            NxObjectTools.EnsureOwnershipMetadata(existing, plan.Id, context.Profile.ProfileId,
                context.ConfigHash, ObjectKind, context.ScopeId);
            if (!SynchronizeExisting(collection, existing, plan, sheetNumber))
                context.Report.Issues.Add(new("NX_SHEET_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                    $"Лист {plan.Id} найден, но NX API не подтвердил синхронизацию формата/масштаба.",
                    SheetId: plan.Id));
            ActivateRequired(collection, existing, plan.Id);
            context.Report.UpdatedObjects.Add("sheet:" + plan.Id);
            return existing;
        }

        var templateFile = ResolveTemplateFile(plan.TemplateId);
        if (string.IsNullOrWhiteSpace(templateFile) || !File.Exists(templateFile))
            throw new FileNotFoundException(
                $"Шаблон '{plan.TemplateId}' для листа {plan.Id} не найден. Создание пустого листа вместо шаблона запрещено.",
                templateFile);

        var sheet = TryCreateFromTemplate(collection, plan, templateFile);
        if (sheet is null)
            throw new NotSupportedException(
                $"NX Open 2512 не подтвердил создание листа {plan.Id} из шаблона '{templateFile}'. " +
                "Fallback на пустой DrawingSheetBuilder запрещён, поскольку он теряет рамку, штамп и именованные области.");

        NxObjectTools.TagManaged(sheet, plan.Id, context.Profile.ProfileId, context.ConfigHash, ObjectKind, context.ScopeId);
        WriteDesiredState(sheet, plan, sheetNumber);
        if (!SynchronizeExisting(collection, sheet, plan, sheetNumber, allowTemplateMismatch: true))
            context.Report.Issues.Add(new("NX_SHEET_POSTCONDITION_UNCONFIRMED", IssueSeverity.Error,
                $"Лист {plan.Id} создан, но его требуемое состояние не подтверждено.", SheetId: plan.Id));
        ActivateRequired(collection, sheet, plan.Id);
        context.Report.CreatedObjects.Add("sheet:" + plan.Id);
        context.Log.Info($"Создан лист {plan.Id} из шаблона {plan.TemplateId} ({plan.Format}, {plan.Orientation}, {plan.Scale}).");
        return sheet;
    }

    private bool IsOwned(object item, string id)
        => NxObjectTools.IsManaged(item, id, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private bool SynchronizeExisting(
        object collection,
        object sheet,
        SheetPlan plan,
        int sheetNumber,
        bool allowTemplateMismatch = false)
    {
        var previousTemplate = NxObjectTools.GetStringAttribute(sheet, "AUTO_DWG_TEMPLATE_ID");
        if (!allowTemplateMismatch && !string.IsNullOrWhiteSpace(previousTemplate)
            && !string.Equals(previousTemplate, plan.TemplateId, StringComparison.OrdinalIgnoreCase))
        {
            context.Report.Issues.Add(new("NX_SHEET_TEMPLATE_CHANGE_REQUIRES_RECREATE", IssueSeverity.Error,
                $"Лист {plan.Id} создан из шаблона '{previousTemplate}', а профиль требует '{plan.TemplateId}'. " +
                "Автоматическая замена шаблона без явного recreate запрещена.", SheetId: plan.Id));
            return false;
        }

        var aliases = context.ApiMap.Aliases("sheet.builder", "CreateDrawingSheetBuilder", "DrawingSheetBuilder", "CreateSheetBuilder");
        var builder = NxReflection.InvokeFactory(collection, aliases, sheet);
        if (builder is not null)
        {
            var commitOwnsBuilder = false;
            try
            {
                var applied = ConfigureSheet(builder, plan, sheetNumber);
                if (!applied.RequiredStateApplied) return false;
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
            var applied = ConfigureSheet(sheet, plan, sheetNumber);
            if (!applied.RequiredStateApplied) return false;
            try { _ = NxReflection.InvokeCommand(sheet, ["Update", "Regenerate"]); }
            catch (Exception ex) { context.Log.Warn($"Обновление листа {plan.Id}: {ex.Message}"); }
        }

        WriteDesiredState(sheet, plan, sheetNumber);
        return VerifyDimensionsWhenReadable(sheet, plan);
    }

    private object? TryCreateFromTemplate(object collection, SheetPlan plan, string templateFile)
    {
        var aliases = context.ApiMap.Aliases("sheet.createFromTemplate", "CreateSheetFromTemplate", "CreateDrawingSheetFromTemplate", "AddSheetFromTemplate");
        var attempts = new object?[][]
        {
            [plan.Id, templateFile], [templateFile, plan.Id], [templateFile], [plan.Id, templateFile, 1.0, 1.0]
        };
        var candidates = new List<(MethodInfo Method, object?[] Args, int Score)>();
        foreach (var method in collection.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                     .Where(x => aliases.Contains(x.Name, StringComparer.OrdinalIgnoreCase) && x.ReturnType != typeof(void)))
        foreach (var args in attempts)
        {
            if (!CanAccept(method.GetParameters(), args)) continue;
            candidates.Add((method, args, method.GetParameters().Length - args.Length));
        }
        var candidate = candidates.OrderBy(x => x.Score).ThenBy(x => x.Method.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (candidate.Method is null)
        {
            context.Log.Warn("Создание листа из шаблона невозможно: не найдена фабрика с объектным return type.");
            return null;
        }

        try
        {
            return NxReflection.InvokeFactory(collection, candidate.Method.Name, candidate.Args);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Создание листа {plan.Id} из шаблона '{templateFile}' завершилось ошибкой.", ex);
        }
    }

    private SheetApplyResult ConfigureSheet(object target, SheetPlan plan, int sheetNumber)
    {
        _ = SetAny(target, plan.Id, "Name", "SheetName");
        _ = SetAny(target, sheetNumber, "Number", "SheetNumber");
        var (numerator, denominator) = ParseScale(plan.Scale);
        var scaleNumerator = SetAny(target, numerator, "ScaleNumerator", "Scale.Numerator");
        var scaleDenominator = SetAny(target, denominator, "ScaleDenominator", "Scale.Denominator");
        var size = GetFormat(plan.Format, plan.Orientation);
        var width = SetAny(target, size.Width, "Length", "Width", "CustomSize.Width");
        var height = SetAny(target, size.Height, "Height", "CustomSize.Height");
        var format = SetAny(target, plan.Format, "StandardMetricSize", "Size", "SheetSize");
        return new SheetApplyResult((scaleNumerator && scaleDenominator) && ((width && height) || format));
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
                context.Log.Warn($"Не применено свойство листа {path}: {ex.Message}");
            }
        }
        return false;
    }

    private void WriteDesiredState(object sheet, SheetPlan plan, int sheetNumber)
    {
        NxObjectTools.SetStringAttribute(sheet, "AUTO_DWG_TEMPLATE_ID", plan.TemplateId);
        NxObjectTools.SetStringAttribute(sheet, "AUTO_DWG_SHEET_FORMAT", plan.Format);
        NxObjectTools.SetStringAttribute(sheet, "AUTO_DWG_SHEET_ORIENTATION", plan.Orientation);
        NxObjectTools.SetStringAttribute(sheet, "AUTO_DWG_SHEET_SCALE", plan.Scale);
        NxObjectTools.SetStringAttribute(sheet, "AUTO_DWG_SHEET_NUMBER", sheetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private bool VerifyDimensionsWhenReadable(object sheet, SheetPlan plan)
    {
        var size = GetFormat(plan.Format, plan.Orientation);
        var width = TryDouble(NxReflection.Get(sheet, "Length", "Width"));
        var height = TryDouble(NxReflection.Get(sheet, "Height"));
        if (width is null || height is null) return true;
        const double tolerance = 0.01;
        if (Math.Abs(width.Value - size.Width) <= tolerance && Math.Abs(height.Value - size.Height) <= tolerance) return true;
        context.Report.Issues.Add(new("NX_SHEET_SIZE_POSTCONDITION_FAILED", IssueSeverity.Error,
            $"Лист {plan.Id}: NX сообщает размер {width:0.###}×{height:0.###}, ожидается {size.Width:0.###}×{size.Height:0.###}.",
            SheetId: plan.Id));
        return false;
    }

    private string? ResolveTemplateFile(string templateId)
    {
        var templates = JsonNavigator.GetArray(context.Profile.Root, "$.templateCatalog.templates") ?? [];
        var template = templates.Select(x => x?.AsObject()).FirstOrDefault(x => string.Equals(x?["id"]?.GetValue<string?>(), templateId, StringComparison.OrdinalIgnoreCase));
        var raw = template?["file"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var expander = new VariableExpander(VariableExpander.BuildDefault(context.Profile, context.WorkPart?.FullPath));
        var expanded = expander.Expand(raw, throwOnMissing: false);
        var path = Path.GetFullPath(expanded, context.Profile.BaseDirectory);
        if (!File.Exists(path))
        {
            var envRoot = Environment.GetEnvironmentVariable("NX_ESKD_ROOT");
            var candidates = new[] { context.RootDirectory, envRoot }.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
            foreach (var root in candidates)
            {
                var candidate = Path.GetFullPath(expanded, root!);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return path;
    }

    private static (double Width, double Height) GetFormat(string format, string orientation)
    {
        var size = format.ToUpperInvariant() switch
        {
            "A0" => (1189.0, 841.0), "A1" => (841.0, 594.0), "A2" => (594.0, 420.0),
            "A3" => (420.0, 297.0), _ => (210.0, 297.0)
        };
        if (orientation.Equals("portrait", StringComparison.OrdinalIgnoreCase) && size.Item1 > size.Item2)
            return (size.Item2, size.Item1);
        return size;
    }

    private static (double Numerator, double Denominator) ParseScale(string scale)
    {
        var parts = scale.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && double.TryParse(parts[0].Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1].Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var denominator)
            && numerator > 0 && denominator > 0)
            return (numerator, denominator);
        return (1.0, 1.0);
    }

    private static bool CanAccept(IReadOnlyList<ParameterInfo> parameters, IReadOnlyList<object?> args)
    {
        if (args.Count > parameters.Count) return false;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i >= args.Count)
            {
                if (!parameters[i].HasDefaultValue && !parameters[i].IsOut) return false;
                continue;
            }
            var targetType = parameters[i].ParameterType.IsByRef
                ? parameters[i].ParameterType.GetElementType()!
                : parameters[i].ParameterType;
            var value = args[i];
            if (value is null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null) return false;
            }
            else if (!targetType.IsInstanceOfType(value) && targetType != typeof(string)) return false;
        }
        return true;
    }

    private static double? TryDouble(object? value)
    {
        if (value is not IConvertible convertible) return null;
        try { return convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture); }
        catch { return null; }
    }

    private static void ActivateRequired(object collection, object sheet, string sheetId)
    {
        var activated = false;
        try
        {
            activated = NxReflection.InvokeCommand(collection,
                ["SetCurrentDrawingSheet", "SetCurrentSheet", "Activate"], sheet);
        }
        catch
        {
            activated = false;
        }

        if (!activated)
        {
            try { activated = NxReflection.InvokeCommand(sheet, ["Open", "Activate"]); }
            catch { activated = false; }
        }

        if (!activated)
            throw new InvalidOperationException(
                $"NX не подтвердил активацию целевого листа {sheetId}. Создание видов и аннотаций отменено.");
    }

    private sealed record SheetApplyResult(bool RequiredStateApplied);
}
