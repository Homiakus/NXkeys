using System.Reflection;
using System.Text;
using System.Text.Json;
using NXOpen;
using NxEskd.Core.Runtime;

namespace NxEskd.NxRuntime;

internal sealed class NxCapabilityScanner(NxServiceContext context)
{
    private static readonly string[] Keywords =
    [
        "DrawingSheet", "BaseView", "ProjectedView", "SectionView", "OrlandoSectionView", "DetailView", "DrawingViewBuilder",
        "DraftingView", "BordersAndTitleBlock", "TitleBlock", "DraftingNote", "PartsList", "Balloon", "BalloonNote",
        "Pmi", "InheritPmi", "FlatPattern", "SheetmetalManager", "PlotManager", "PrintPDF", "Dxfdwg", "Table",
        "AutoFormat", "AlignView"
    ];

    public void WriteInventory(ExecutionReport report)
    {
        WriteInventory(context.Session, context.RootDirectory, report);
        if (!string.IsNullOrWhiteSpace(NxApiSynchronizer.LastCachePath))
        {
            report.Messages.Add("Runtime API map: " + NxApiSynchronizer.LastCachePath);
            report.Metrics["inventory.apiMapPath"] = NxApiSynchronizer.LastCachePath;
            report.Metrics["inventory.apiMapFingerprint"] = NxApiSynchronizer.LastFingerprint;
            report.Metrics["inventory.apiMapSynchronized"] = NxApiSynchronizer.LastRefreshSucceeded;
        }
        context.Log.Info("Диагностика API и runtime-карта сохранены.");
    }

    public static void WriteInventory(Session session, string rootDirectory, ExecutionReport report)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(report);

        var assemblies = new[] { typeof(Session).Assembly, typeof(NXOpen.UF.UFSession).Assembly }.Distinct().ToArray();
        var records = new List<object>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in SafeTypes(assembly).Where(t => Keywords.Any(k =>
                         t.FullName?.Contains(k, StringComparison.OrdinalIgnoreCase) == true)))
            {
                records.Add(new
                {
                    assembly = assembly.GetName().Name,
                    assemblyVersion = assembly.GetName().Version?.ToString(),
                    location = assembly.Location,
                    type = type.FullName,
                    methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => Keywords.Any(k => m.Name.Contains(k, StringComparison.OrdinalIgnoreCase))
                                    || m.Name.StartsWith("Create", StringComparison.Ordinal))
                        .Select(Signature).Distinct().OrderBy(x => x).ToArray(),
                    properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                        .Select(p => $"{p.PropertyType.FullName} {p.Name}").OrderBy(x => x).ToArray()
                });
            }
        }

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NxEskdGenerator",
            "reports");
        Directory.CreateDirectory(directory);
        var jsonPath = Path.Combine(directory, $"nx2512-api-inventory-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var payload = new
        {
            generatedAt = DateTimeOffset.Now,
            rootDirectory,
            workPart = session.Parts.Work?.FullPath,
            displayPart = session.Parts.Display?.FullPath,
            runtimeApiMap = NxApiSynchronizer.LastCachePath,
            runtimeApiFingerprint = NxApiSynchronizer.LastFingerprint,
            runtimeApiSynchronized = NxApiSynchronizer.LastRefreshSucceeded,
            assemblies = assemblies.Select(x => new
            {
                name = x.GetName().Name,
                version = x.GetName().Version?.ToString(),
                location = x.Location
            }).ToArray(),
            records
        };
        File.WriteAllText(jsonPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        var mdPath = Path.ChangeExtension(jsonPath, ".md");
        var sb = new StringBuilder("# NX 2512 API inventory\n\n");
        sb.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        sb.AppendLine($"WorkPart: `{payload.workPart ?? "<none>"}`");
        sb.AppendLine($"DisplayPart: `{payload.displayPart ?? "<none>"}`");
        sb.AppendLine($"Runtime API map: `{payload.runtimeApiMap ?? "<not generated>"}`");
        sb.AppendLine($"Runtime API fingerprint: `{payload.runtimeApiFingerprint ?? "<none>"}`\n");
        foreach (var assembly in assemblies)
            sb.AppendLine($"- Assembly: `{assembly.FullName}` — `{assembly.Location}`");
        sb.AppendLine();
        foreach (var record in records)
        {
            var element = JsonSerializer.SerializeToElement(record);
            sb.AppendLine("## " + element.GetProperty("type").GetString());
            foreach (var method in element.GetProperty("methods").EnumerateArray())
                sb.AppendLine("- `" + method.GetString() + "`");
            sb.AppendLine();
        }
        File.WriteAllText(mdPath, sb.ToString(), new UTF8Encoding(false));
        report.CreatedObjects.Add("file:" + jsonPath);
        report.CreatedObjects.Add("file:" + mdPath);
        report.Messages.Add("API inventory: " + jsonPath);
        report.Metrics["inventory.recordCount"] = records.Count;
    }

    private static string Signature(MethodInfo method)
        => $"{method.ReturnType.FullName} {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name))})";

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); }
    }
}
