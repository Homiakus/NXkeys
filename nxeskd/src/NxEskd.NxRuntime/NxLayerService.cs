using NxEskd.Core.Configuration;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxLayerService(NxServiceContext context)
{
    public void EnsureLayers(DrawingPlan plan)
    {
        // NxLayerService is the first mutation stage called after the main Undo mark is created.
        // Reconciliation therefore executes before new objects are added and remains rollback-safe.
        new NxReconciliationService(context).Reconcile(plan);
        if (context.Report.Issues.Any(x => x.Severity == NxEskd.Core.Diagnostics.IssueSeverity.Error)) return;

        var layerManager = NxReflection.Get(context.WorkPart, "Layers", "LayerManager");
        var layers = JsonNavigator.GetArray(context.Profile.Root, "$.layers.managedLayers") ?? [];
        foreach (var node in layers)
        {
            var obj = node!.AsObject();
            var number = obj["number"]?.GetValue<int?>();
            var name = obj["name"]?.GetValue<string?>();
            if (number is null || string.IsNullOrWhiteSpace(name)) continue;
            try
            {
                if (NxReflection.InvokeCommand(layerManager, ["SetLayerCategoryName", "SetCategoryName", "SetLayerCategory", "SetLayerName", "SetName"], number.Value, name))
                    context.Report.UpdatedObjects.Add($"layer:{number}:{name}");
                else
                    context.Log.Warn($"Слой {number}: NX API не подтвердил установку имени '{name}'.");
            }
            catch (Exception ex)
            {
                context.Log.Warn($"Слой {number}: {ex.Message}");
            }
        }
    }
}
