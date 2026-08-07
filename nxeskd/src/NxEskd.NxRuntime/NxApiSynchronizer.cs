using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NXOpen;

namespace NxEskd.NxRuntime;

/// <summary>
/// Builds a machine-local API alias map from the exact NXOpen assemblies loaded by the
/// current NX process. The generated map is version/fingerprint scoped and is never
/// shipped with the product because NXOpen binaries are installation-specific.
/// </summary>
internal static class NxApiSynchronizer
{
    private static readonly object Gate = new();

    public static string? LastCachePath { get; private set; }
    public static string? LastFingerprint { get; private set; }
    public static bool LastRefreshSucceeded { get; private set; }

    public static JsonObject LoadOrRefresh(string rootDirectory, JsonObject configuredRoot)
    {
        lock (Gate)
        {
            try
            {
                var assemblies = RuntimeAssemblies();
                var fingerprint = Fingerprint(assemblies);
                var cachePath = CachePath(assemblies, fingerprint);
                LastCachePath = cachePath;
                LastFingerprint = fingerprint;

                var cached = TryReadCache(cachePath, fingerprint);
                if (cached is not null)
                {
                    LastRefreshSucceeded = true;
                    return cached;
                }

                var aliases = Discover(configuredRoot, assemblies);
                WriteCache(cachePath, rootDirectory, fingerprint, assemblies, aliases);
                LastRefreshSucceeded = true;
                return aliases;
            }
            catch
            {
                // Synchronization is an additive compatibility layer. A failure must not
                // prevent loading the conservative repository map and built-in fallbacks.
                LastRefreshSucceeded = false;
                return new JsonObject();
            }
        }
    }

    private static Assembly[] RuntimeAssemblies()
        => new[] { typeof(Session).Assembly, typeof(NXOpen.UF.UFSession).Assembly }
            .Distinct()
            .ToArray();

    private static string Fingerprint(IEnumerable<Assembly> assemblies)
    {
        var material = string.Join("|", assemblies.OrderBy(x => x.GetName().Name, StringComparer.Ordinal)
            .Select(x =>
            {
                var fileVersion = string.IsNullOrWhiteSpace(x.Location)
                    ? string.Empty
                    : FileVersionInfo.GetVersionInfo(x.Location).ProductVersion ?? string.Empty;
                return $"{x.FullName};{x.ManifestModule.ModuleVersionId:N};{fileVersion};{x.Location}";
            }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static string CachePath(IReadOnlyList<Assembly> assemblies, string fingerprint)
    {
        var version = assemblies.FirstOrDefault(x => x.GetName().Name == "NXOpen")?.GetName().Version?.ToString()
                      ?? "unknown";
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NxEskdGenerator",
            "api-cache");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"nx-api-map-{Sanitize(version)}-{fingerprint[..16]}.json");
    }

    private static JsonObject? TryReadCache(string path, string fingerprint)
    {
        if (!File.Exists(path)) return null;
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if (!string.Equals(root?["fingerprint"]?.GetValue<string?>(), fingerprint, StringComparison.Ordinal))
            return null;
        return root?["aliases"] as JsonObject;
    }

    private static JsonObject Discover(JsonObject configuredRoot, IReadOnlyList<Assembly> assemblies)
    {
        var aliases = new JsonObject();
        var evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configured = configuredRoot["aliases"] as JsonObject ?? new JsonObject();

        string[] Candidates(string key, params string[] defaults)
            => Read(configured, key).Concat(defaults)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        Session? session = null;
        try { session = Session.GetSession(); } catch { }
        var workPart = session?.Parts?.Work;

        if (workPart is not null)
        {
            var sheetCollectionNames = Candidates("sheet.collection", "DrawingSheets", "DrawingSheetCollection");
            AddProperty(aliases, evidence, "sheet.collection", workPart, sheetCollectionNames,
                p => p.PropertyType.Name.Contains("DrawingSheet", StringComparison.OrdinalIgnoreCase));
            var sheets = NxReflection.Get(workPart, Read(aliases, "sheet.collection").Concat(sheetCollectionNames).ToArray());
            AddMethod(aliases, evidence, "sheet.builder", sheets,
                Candidates("sheet.builder", "CreateDrawingSheetBuilder", "DrawingSheetBuilder", "CreateSheetBuilder"),
                returnTypeToken: "DrawingSheetBuilder");

            var viewCollectionNames = Candidates("view.collection", "DraftingViews", "DrawingViews", "DraftingViewCollection");
            AddProperty(aliases, evidence, "view.collection", workPart, viewCollectionNames,
                p => p.PropertyType.Name.Contains("DraftingView", StringComparison.OrdinalIgnoreCase));
            var views = NxReflection.Get(workPart, Read(aliases, "view.collection").Concat(viewCollectionNames).ToArray());
            AddMethod(aliases, evidence, "view.baseBuilder", views,
                Candidates("view.baseBuilder", "CreateBaseViewBuilder", "BaseViewBuilder"),
                returnTypeToken: "BaseViewBuilder");
            AddMethod(aliases, evidence, "view.projectedBuilder", views,
                Candidates("view.projectedBuilder", "CreateProjectedViewBuilder", "ProjectedViewBuilder"),
                returnTypeToken: "ProjectedViewBuilder");
            AddMethod(aliases, evidence, "view.sectionBuilder", views,
                Candidates("view.sectionBuilder", "CreateSectionViewBuilder", "SectionViewBuilder", "CreateOrlandoSectionViewBuilder", "OrlandoSectionViewBuilder"),
                returnTypeToken: "SectionViewBuilder");
            AddMethod(aliases, evidence, "view.detailBuilder", views,
                Candidates("view.detailBuilder", "CreateDetailViewBuilder", "DetailViewBuilder"),
                returnTypeToken: "DetailViewBuilder");
            AddMethod(aliases, evidence, "view.drawingBuilder", views,
                Candidates("view.drawingBuilder", "CreateDrawingViewBuilder", "DrawingViewBuilder"),
                returnTypeToken: "DrawingViewBuilder");

            var features = NxReflection.Get(workPart, "Features");
            var sheetMetalNames = Candidates("sheetMetal.manager", "SheetmetalManager", "SheetMetalManager");
            AddProperty(aliases, evidence, "sheetMetal.manager", features, sheetMetalNames,
                p => p.PropertyType.Name.Contains("SheetmetalManager", StringComparison.OrdinalIgnoreCase)
                     || p.PropertyType.FullName?.Contains("SheetMetal.SheetmetalManager", StringComparison.OrdinalIgnoreCase) == true);
            var sheetMetalManager = NxReflection.Get(features,
                                        Read(aliases, "sheetMetal.manager").Concat(sheetMetalNames).ToArray())
                                    ?? NxReflection.Get(workPart, sheetMetalNames);
            AddMethod(aliases, evidence, "flatPattern.builder", sheetMetalManager,
                Candidates("flatPattern.builder", "CreateFlatPatternBuilder", "CreateFlatSolidBuilder", "FlatPatternBuilder"),
                returnTypeToken: "FlatPatternBuilder");

            var plotNames = Candidates("export.plotManager", "PlotManager", "PrintManager");
            AddProperty(aliases, evidence, "export.plotManager", workPart, plotNames,
                p => p.PropertyType.Name.Contains("PlotManager", StringComparison.OrdinalIgnoreCase));
            var plotManager = NxReflection.Get(workPart,
                Read(aliases, "export.plotManager").Concat(plotNames).ToArray());
            AddMethod(aliases, evidence, "export.pdfBuilder", plotManager,
                Candidates("export.pdfBuilder",
                    "CreatePrintPdfbuilder", "CreatePrintPdfBuilder", "CreatePrintPDFBuilder", "CreatePdfBuilder", "PrintPDFBuilder"),
                returnTypeToken: "PrintPDFBuilder");

            AddAssemblyProperty(aliases, evidence, assemblies, "pmi.inheritStyle",
                Candidates("pmi.inheritStyle", "InheritPmi", "ViewStyleInheritPmi"),
                declaringTypeToken: "ViewStyle", propertyTypeToken: "InheritPmi");
            AddAssemblyMethod(aliases, evidence, assemblies, "pmi.setMode",
                Candidates("pmi.setMode", "SetInheritPmiMode", "SetPmi"),
                declaringTypeToken: "InheritPmi");
            AddAssemblyMethod(aliases, evidence, assemblies, "pmi.setToDrawing",
                Candidates("pmi.setToDrawing", "SetInheritPmiToDrawing", "SetPmiToDrawing"),
                declaringTypeToken: "InheritPmi");
        }
        else
        {
            // Offline catalog fallback: Populate configured aliases from NX 2512 API catalog
            foreach (var property in configured)
            {
                if (property.Value is JsonArray arr)
                {
                    var key = property.Key;
                    foreach (var val in arr.Select(x => x?.GetValue<string?>()).Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        AddAlias(aliases, key, val!);
                    }
                    evidence[key] = "NX 2512 API Catalog Offline Fallback";
                }
            }
            evidence["$mode"] = "Offline Catalog Validation";
        }

        var evidenceObject = new JsonObject();
        foreach (var item in evidence.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            evidenceObject[item.Key] = item.Value;
        aliases["$evidence"] = evidenceObject;

        return aliases;
    }

    private static void AddProperty(
        JsonObject aliases,
        IDictionary<string, string> evidence,
        string key,
        object? target,
        IReadOnlyList<string> candidates,
        Func<PropertyInfo, bool> semanticMatch)
    {
        if (target is null) return;
        var properties = target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var property = candidates.Select(name => properties.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(p => p is not null)
            ?? properties.FirstOrDefault(semanticMatch);
        if (property is null) return;
        AddAlias(aliases, key, property.Name);
        evidence[key] = $"{target.GetType().FullName}.{property.Name}";
    }

    private static void AddMethod(
        JsonObject aliases,
        IDictionary<string, string> evidence,
        string key,
        object? target,
        IReadOnlyList<string> candidates,
        string returnTypeToken)
    {
        if (target is null) return;
        var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var method = candidates.Select(name => methods.FirstOrDefault(m =>
                string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(m => m is not null)
            ?? methods.FirstOrDefault(m =>
                m.Name.StartsWith("Create", StringComparison.OrdinalIgnoreCase)
                && m.ReturnType.Name.Contains(returnTypeToken, StringComparison.OrdinalIgnoreCase));
        if (method is null) return;
        AddAlias(aliases, key, method.Name);
        evidence[key] = $"{target.GetType().FullName}.{Signature(method)}";
    }

    private static void AddAssemblyProperty(
        JsonObject aliases,
        IDictionary<string, string> evidence,
        IEnumerable<Assembly> assemblies,
        string key,
        IReadOnlyList<string> candidates,
        string declaringTypeToken,
        string propertyTypeToken)
    {
        foreach (var type in assemblies.SelectMany(SafeTypes)
                     .Where(t => t.FullName?.Contains(declaringTypeToken, StringComparison.OrdinalIgnoreCase) == true))
        {
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var property = candidates.Select(name => properties.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(p => p is not null)
                ?? properties.FirstOrDefault(p =>
                    p.PropertyType.Name.Contains(propertyTypeToken, StringComparison.OrdinalIgnoreCase));
            if (property is null) continue;
            AddAlias(aliases, key, property.Name);
            evidence[key] = $"{type.FullName}.{property.Name}";
            return;
        }
    }

    private static void AddAssemblyMethod(
        JsonObject aliases,
        IDictionary<string, string> evidence,
        IEnumerable<Assembly> assemblies,
        string key,
        IReadOnlyList<string> candidates,
        string declaringTypeToken)
    {
        foreach (var type in assemblies.SelectMany(SafeTypes)
                     .Where(t => t.FullName?.Contains(declaringTypeToken, StringComparison.OrdinalIgnoreCase) == true))
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var method = candidates.Select(name => methods.FirstOrDefault(m =>
                    string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(m => m is not null);
            if (method is null) continue;
            AddAlias(aliases, key, method.Name);
            evidence[key] = $"{type.FullName}.{Signature(method)}";
            return;
        }
    }

    private static void AddAlias(JsonObject aliases, string key, string value)
    {
        if (aliases[key] is not JsonArray array)
        {
            array = new JsonArray();
            aliases[key] = array;
        }
        if (!array.Select(x => x?.GetValue<string?>()).Any(x =>
                string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
            array.Add(value);
    }

    private static IEnumerable<string> Read(JsonObject aliases, string key)
        => (aliases[key] as JsonArray)?
               .Select(x => x?.GetValue<string?>())
               .Where(x => !string.IsNullOrWhiteSpace(x))
               .Cast<string>()
           ?? [];

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(x => x is not null).Cast<Type>(); }
    }

    private static string Signature(MethodInfo method)
        => $"{method.ReturnType.FullName} {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName))})";

    private static void WriteCache(
        string path,
        string rootDirectory,
        string fingerprint,
        IEnumerable<Assembly> assemblies,
        JsonObject aliases)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["generatedAt"] = DateTimeOffset.Now.ToString("O"),
            ["fingerprint"] = fingerprint,
            ["rootDirectory"] = rootDirectory,
            ["aliases"] = aliases.DeepClone()
        };
        var assemblyArray = new JsonArray();
        foreach (var assembly in assemblies)
        {
            var file = string.IsNullOrWhiteSpace(assembly.Location)
                ? null
                : FileVersionInfo.GetVersionInfo(assembly.Location);
            assemblyArray.Add(new JsonObject
            {
                ["name"] = assembly.GetName().Name,
                ["assemblyVersion"] = assembly.GetName().Version?.ToString(),
                ["productVersion"] = file?.ProductVersion,
                ["location"] = assembly.Location,
                ["moduleVersionId"] = assembly.ManifestModule.ModuleVersionId.ToString("D")
            });
        }
        root["assemblies"] = assemblyArray;

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private static string Sanitize(string value)
        => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' ? ch : '_'));
}
