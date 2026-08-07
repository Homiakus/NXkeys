using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.NxRuntime;

internal sealed class NxAnnotationLayoutService(NxServiceContext context, IReadOnlyDictionary<string, object> views)
{
    public void Arrange()
    {
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.pmiInheritance.placement.collisionAvoidance.enabled", true)) return;
        var aliases = context.ApiMap.Aliases("annotation.autoArrange", "AutoArrangeAnnotations", "ArrangeAnnotations", "OptimizeAnnotationLayout", "OrganizeDimensions");
        var arranged = 0;
        foreach (var pair in views)
        {
            foreach (var target in new[] { pair.Value, NxReflection.Get(pair.Value, "Annotations"), NxReflection.Get(context.WorkPart, "Annotations") }.Where(x => x is not null))
            {
                try
                {
                    var result = NxReflection.Invoke(target, aliases);
                    if (result is not null) { arranged++; break; }
                }
                catch { }
            }
        }
        if (arranged == 0)
            context.Report.Issues.Add(new("NX_ANNOTATION_LAYOUT_MANUAL", IssueSeverity.ManualReview,
                "Автоматический метод раскладки аннотаций не найден. PMI перенесен, но итоговую компоновку размеров необходимо проверить."));
        else context.Log.Info($"Автоматическая раскладка аннотаций выполнена для {arranged} видов.");
    }
}
