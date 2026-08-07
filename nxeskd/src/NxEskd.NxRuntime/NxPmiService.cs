using System.Reflection;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;
using NxEskd.Core.Utilities;

namespace NxEskd.NxRuntime;

internal sealed class NxPmiService(NxServiceContext context, IReadOnlyDictionary<string, object> drawingViews)
{
    public void Inherit(DrawingPlan plan)
    {
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.pmiInheritance.enabled", true)) return;
        if (!plan.Operations.Any(operation => operation.OperationId.Equals("pmi:inherit", StringComparison.OrdinalIgnoreCase)))
        {
            context.Report.Messages.Add("Наследование PMI не применимо к текущей модели и пропущено.");
            return;
        }

        var mappings = JsonNavigator.GetArray(context.Profile.Root, "$.pmiInheritance.viewMapping") ?? [];
        foreach (var mappingNode in mappings)
        {
            var mapping = mappingNode!.AsObject();
            var sourceName = mapping["sourceModelView"]?.GetValue<string?>();
            var fallback = mapping["fallbackSourceModelView"]?.GetValue<string?>();
            var targetId = mapping["targetDrawingViewId"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(targetId) || !drawingViews.TryGetValue(targetId, out var targetView)) continue;

            var modelView = FindModelView(sourceName) ?? FindModelView(fallback);
            if (modelView is null)
            {
                context.Report.Issues.Add(new("NX_PMI_MODEL_VIEW_MISSING", IssueSeverity.Warning,
                    $"Модельный вид PMI '{sourceName}' не найден.", ObjectId: targetId));
                continue;
            }

            var resolvedSourceName = NxReflection.GetName(modelView) ?? sourceName ?? fallback ?? "UNKNOWN_MODEL_VIEW";
            ApplyViewStyleInheritance(targetView, resolvedSourceName, targetId, flatPattern: false);
        }

        foreach (var flat in plan.Sheets.SelectMany(s => s.Views)
                     .Where(v => v.InheritFlatPatternPmi || v.Type.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase)))
        {
            if (!drawingViews.TryGetValue(flat.Id, out var target)) continue;
            var flatModelView = FindModelView(flat.ModelView ?? "Flat Pattern");
            if (flatModelView is null)
            {
                context.Report.Issues.Add(new("NX_FLAT_PMI_MODEL_VIEW_MISSING", IssueSeverity.Warning,
                    "Модельный вид Flat Pattern не найден.", ObjectId: flat.Id));
                continue;
            }
            ApplyViewStyleInheritance(target, NxReflection.GetName(flatModelView) ?? "Flat Pattern", flat.Id,
                flatPattern: true);
        }
    }

    private void ApplyViewStyleInheritance(object drawingView, string sourceIdentity, string targetId, bool flatPattern)
    {
        var binding = string.Join("|", context.Profile.ProfileId, context.ScopeId, sourceIdentity, targetId,
            flatPattern ? "flat" : "standard");
        var hash = Hashing.Sha256(binding)[..16];
        var markerName = "AUTO_DWG_PMI_" + hash;
        if (string.Equals(NxObjectTools.GetStringAttribute(drawingView, markerName), binding, StringComparison.Ordinal))
        {
            context.Report.Messages.Add($"PMI inheritance пропущен как уже применённый: {sourceIdentity} → {targetId}.");
            return;
        }

        try
        {
            string mechanism;
            var directStyle = NxReflection.Get(drawingView, "Style", "ViewStyle");
            var directInheritance = GetInheritanceSettings(directStyle);
            if (ConfigureInheritance(directInheritance))
            {
                if (!NxReflection.TryInvokeCommand(drawingView, ["Update", "UpdateView", "Regenerate"]))
                    throw new InvalidOperationException("View Style изменён, но NX не подтвердил обновление drawing view.");
                mechanism = drawingView.GetType().Name + ".Style.InheritPmi";
            }
            else
            {
                var collection = NxReflection.Get(context.WorkPart,
                    context.ApiMap.Aliases("view.collection", "DraftingViews", "DrawingViews"));
                var builder = NxReflection.InvokeFactory(collection,
                    context.ApiMap.Aliases("view.drawingBuilder", "CreateDrawingViewBuilder"), drawingView);
                if (builder is null)
                    throw new MissingMethodException(collection?.GetType().FullName, "CreateDrawingViewBuilder");

                var commitOwnsBuilder = false;
                try
                {
                    var builderStyle = NxReflection.Get(builder, "ViewStyle", "Style");
                    var builderInheritance = GetInheritanceSettings(builderStyle);
                    if (!ConfigureInheritance(builderInheritance))
                        throw new InvalidOperationException(
                            "DrawingViewBuilder.ViewStyle.InheritPmi не предоставляет подтверждённые настройки PMI.");
                    mechanism = builder.GetType().Name + ".ViewStyle.InheritPmi";
                    commitOwnsBuilder = true;
                    NxReflection.CommitCommandAndDestroy(builder);
                }
                finally
                {
                    if (!commitOwnsBuilder) NxReflection.Destroy(builder);
                }
            }

            NxObjectTools.SetStringAttribute(drawingView, markerName, binding);
            context.Report.UpdatedObjects.Add("pmi-binding:" + hash);
            context.Log.Info($"PMI View Style применён: {sourceIdentity} → {targetId} ({mechanism}).");
        }
        catch (Exception ex)
        {
            context.Report.Issues.Add(new(flatPattern ? "NX_FLAT_PMI_INHERIT_FAILED" : "NX_PMI_INHERIT_FAILED",
                IssueSeverity.Error,
                $"Наследование PMI {sourceIdentity} → {targetId} завершилось ошибкой: {ex.Message}",
                ObjectId: targetId,
                SuggestedFix: "Запустить Диагностику NX Open API; runtime-карта будет обновлена автоматически."));
        }
    }

    private object? GetInheritanceSettings(object? viewStyle)
        => NxReflection.Get(viewStyle,
            context.ApiMap.Aliases("pmi.inheritStyle", "InheritPmi", "ViewStyleInheritPmi"));

    private bool ConfigureInheritance(object? inheritance)
    {
        if (inheritance is null) return false;

        var toDrawing = NxReflection.TryInvokeCommand(inheritance,
                            context.ApiMap.Aliases("pmi.setToDrawing",
                                "SetInheritPmiToDrawing", "SetPmiToDrawing"), true)
                        || NxReflection.TryInvokeCommand(inheritance,
                            context.ApiMap.Aliases("pmi.setToDrawing",
                                "SetInheritPmiToDrawing", "SetPmiToDrawing"));
        if (!toDrawing)
            toDrawing = NxReflection.Set(inheritance, true,
                "InheritPmiToDrawing", "PmiToDrawing", "ToDrawing");

        var mode = SetFromModelViewMode(inheritance,
            context.ApiMap.Aliases("pmi.setMode", "SetInheritPmiMode", "SetPmi"));
        return toDrawing && mode;
    }

    private static bool SetFromModelViewMode(object inheritance, IReadOnlyList<string> aliases)
    {
        var expected = aliases.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var method in inheritance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                     .Where(method => expected.Contains(method.Name)))
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1) continue;
            var parameterType = parameters[0].ParameterType.IsByRef
                ? parameters[0].ParameterType.GetElementType()!
                : parameters[0].ParameterType;
            if (!parameterType.IsEnum) continue;

            var name = Enum.GetNames(parameterType)
                           .FirstOrDefault(value => value.Contains("FromModelView", StringComparison.OrdinalIgnoreCase))
                       ?? Enum.GetNames(parameterType)
                           .FirstOrDefault(value => value.Contains("FromView", StringComparison.OrdinalIgnoreCase));
            if (name is null) continue;
            try
            {
                method.Invoke(inheritance, [Enum.Parse(parameterType, name)]);
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        foreach (var propertyName in new[] { "InheritPmiMode", "Pmi", "Mode" })
        {
            var property = inheritance.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null || !property.CanWrite || !property.PropertyType.IsEnum) continue;
            var name = Enum.GetNames(property.PropertyType)
                           .FirstOrDefault(value => value.Contains("FromModelView", StringComparison.OrdinalIgnoreCase))
                       ?? Enum.GetNames(property.PropertyType)
                           .FirstOrDefault(value => value.Contains("FromView", StringComparison.OrdinalIgnoreCase));
            if (name is null) continue;
            property.SetValue(inheritance, Enum.Parse(property.PropertyType, name));
            return true;
        }
        return false;
    }

    private object? FindModelView(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return NxReflection.FindByName(NxReflection.Get(context.WorkPart, "ModelingViews"), name)
               ?? NxReflection.FindByName(NxReflection.Get(context.WorkPart, "Views"), name)
               ?? NxReflection.FindByName(NxReflection.Get(context.WorkPart, "ModelViews"), name);
    }
}
