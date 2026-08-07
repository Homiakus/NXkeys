using System.Reflection;
using NXOpen;

namespace NxEskd.NxRuntime;

/// <summary>
/// Classifies the active NX model using read-only APIs. Manager availability alone is
/// never treated as proof that a body is sheet metal because FeatureCollection exposes
/// managers independently of the current model contents.
/// </summary>
internal static class NxModelClassifier
{
    public static bool IsSheetMetal(
        object? sheetMetalManager,
        IReadOnlyList<object> bodies,
        object? features,
        NxLog log)
    {
        var successfulQueries = 0;
        if (sheetMetalManager is not null)
        {
            var methods = sheetMetalManager.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.GetParameters().Length == 1
                                 && (method.Name.Equals("IsSheetmetalBody", StringComparison.OrdinalIgnoreCase)
                                     || method.Name.Equals("IsSheetMetalBody", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            foreach (var body in bodies)
            foreach (var method in methods)
            {
                var parameter = method.GetParameters()[0].ParameterType;
                if (!parameter.IsInstanceOfType(body)) continue;
                try
                {
                    var result = method.Invoke(sheetMetalManager, [body]);
                    if (result is not bool isSheetMetalBody) continue;
                    successfulQueries++;
                    if (isSheetMetalBody) return true;
                    break;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    log.Warn($"SheetmetalManager.{method.Name}: {ex.InnerException.Message}");
                }
                catch (Exception ex)
                {
                    log.Warn($"SheetmetalManager.{method.Name}: {ex.Message}");
                }
            }
        }

        // A successfully queried set of bodies is authoritative. Do not classify a normal
        // solid as sheet metal merely because a generic manager or unrelated Bend exists.
        if (successfulQueries > 0) return false;

        return NxReflection.Enumerate(features).Any(feature =>
        {
            var typeName = feature.GetType().FullName ?? feature.GetType().Name;
            return typeName.Contains("FlatPattern", StringComparison.OrdinalIgnoreCase)
                   || typeName.Contains("SheetMetal", StringComparison.OrdinalIgnoreCase)
                   || typeName.Contains("Sheetmetal", StringComparison.OrdinalIgnoreCase);
        });
    }

    public static string[] DatumPlaneNames(Part workPart, object? features)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var datum in NxReflection.Enumerate(NxReflection.Get(workPart, "Datums", "DatumPlanes")))
            AddIfDatumPlane(names, datum);
        foreach (var feature in NxReflection.Enumerate(features))
            AddIfDatumPlane(names, feature);
        return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddIfDatumPlane(ISet<string> names, object candidate)
    {
        var typeName = candidate.GetType().FullName ?? candidate.GetType().Name;
        if (!typeName.Contains("DatumPlane", StringComparison.OrdinalIgnoreCase)
            && !typeName.Contains("Datum.Plane", StringComparison.OrdinalIgnoreCase)) return;
        var name = NxReflection.GetName(candidate);
        if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
    }
}
