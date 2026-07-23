// NX2512_FullFunctionCatalog.cs
// Siemens NX 2512 full function/API inventory journal.
//
// Compile this project to DLL first, then run inside NX 2512:
//   File -> Execute -> NX Open... -> NX2512_FullFunctionCatalog.dll
//
// The journal does not modify the current part.
// It inventories:
//   1) NX UI/MenuScript command definitions (BUTTON IDs);
//   2) public managed NXOpen types and members;
//   3) Builder/Collection/Manager/Service API entry points;
//   4) Open C / UFUN functions found in uf_*.h headers;
//   5) heuristic UI-command -> API candidate matches.
//
// Important:
// There is no official one-to-one mapping between every UI command and one API method.
// A UI command can require a Builder plus several methods, can be UFUN-only,
// or can have no direct public API. Candidate matches are therefore explicitly scored.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NXOpen;

public static class NX2512FullFunctionCatalog
{
    private static readonly BindingFlags DeclaredPublicMembers =
        BindingFlags.Public |
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.DeclaredOnly;

    private static readonly string[] MenuExtensions =
    {
        ".men", ".btn", ".tbr", ".rtb", ".gly", ".abr"
    };

    private static readonly HashSet<string> CrosswalkStopWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "application", "button", "command", "create",
            "dialog", "display", "edit", "feature", "file", "get", "make",
            "menu", "new", "nx", "object", "open", "part", "set", "show",
            "the", "to", "tool", "update", "view"
        };

    [STAThread]
    public static void Main(string[] args)
    {
        Session session = Session.GetSession();

        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            using (CatalogStudioForm form = new CatalogStudioForm(session))
            {
                form.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            try
            {
                ListingWindow nxListing = session.ListingWindow;
                nxListing.Open();
                nxListing.WriteLine("NX Catalog Studio failed:");
                nxListing.WriteLine(ex.ToString());
            }
            catch
            {
                // NX may be shutting down; no secondary failure should hide the original error.
            }

            System.Windows.Forms.MessageBox.Show(
                ex.ToString(),
                "NX Catalog Studio",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    public static CatalogRunResult RunCatalog(
        CatalogOptions options,
        IProgress<CatalogProgress> progress,
        CancellationToken cancellationToken)
    {
        if (options == null)
        {
            throw new ArgumentNullException("options");
        }

        options.Normalize();

        CatalogLogger listing = new CatalogLogger(progress);
        CatalogRuntime.Begin(options, cancellationToken);

        string stamp = DateTime.Now.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);

        string outputBase = options.OutputDirectory;
        if (String.IsNullOrWhiteSpace(outputBase) ||
            !Directory.Exists(outputBase))
        {
            outputBase = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);
        }

        if (String.IsNullOrWhiteSpace(outputBase) ||
            !Directory.Exists(outputBase))
        {
            outputBase = Path.GetTempPath();
        }

        string outputDir = options.CreateTimestampedSubdirectory
            ? Path.Combine(
                outputBase,
                "NX2512_Full_Function_API_Catalog_" + stamp)
            : outputBase;

        Directory.CreateDirectory(outputDir);

        List<string> generatedFiles = new List<string>();
        List<RootRow> roots = new List<RootRow>();
        List<Assembly> assemblies = new List<Assembly>();
        List<TypeRow> typeRows = new List<TypeRow>();
        List<MemberRow> memberRows = new List<MemberRow>();
        List<ApiEntryRow> apiEntries = new List<ApiEntryRow>();
        List<MenuCommandRow> menuRows = new List<MenuCommandRow>();
        List<UfFunctionRow> ufRows = new List<UfFunctionRow>();
        List<CrosswalkRow> crosswalkRows = new List<CrosswalkRow>();

        try
        {
            Report(progress, 2, "Подготовка", "Создание каталога результатов");
            cancellationToken.ThrowIfCancellationRequested();

            Report(progress, 7, "Пути", "Поиск установочных и пользовательских каталогов NX");
            roots = DiscoverRoots(listing);
            AddAdditionalRoots(roots, options.AdditionalRoots);

            if (options.ExportEnvironmentRoots)
            {
                string path = Path.Combine(outputDir, "00_environment_roots.csv");
                ExportRoots(roots, path);
                generatedFiles.Add(path);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (options.ScanManagedApi)
            {
                Report(progress, 16, "NX Open", "Поиск и загрузка NXOpen-сборок");
                assemblies = DiscoverNxOpenAssemblies(listing);

                if (options.ExportAssemblies)
                {
                    string path = Path.Combine(outputDir, "01_nxopen_assemblies.csv");
                    ExportAssemblies(assemblies, path);
                    generatedFiles.Add(path);
                }

                Report(progress, 27, "NX Open", "Чтение публичных типов и членов API");

                int assemblyIndex = 0;
                foreach (Assembly assembly in assemblies.OrderBy(
                    a => a.GetName().Name,
                    StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    assemblyIndex++;

                    Report(
                        progress,
                        27 + Math.Min(18, assemblyIndex * 18 / Math.Max(1, assemblies.Count)),
                        "NX Open",
                        "Сборка " + assembly.GetName().Name);

                    foreach (Type type in GetExportedTypesSafe(assembly, listing)
                        .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        typeRows.Add(CreateTypeRow(assembly, type));

                        if (options.ExportMembers ||
                            options.ExportEntryPoints ||
                            options.BuildCrosswalk)
                        {
                            AddMembers(memberRows, assembly, type);
                        }
                    }
                }

                if (options.ExportNamespaces)
                {
                    string path = Path.Combine(outputDir, "02_nxopen_namespaces.csv");
                    ExportNamespaces(typeRows, memberRows, path);
                    generatedFiles.Add(path);
                }

                if (options.ExportTypes)
                {
                    string path = Path.Combine(outputDir, "03_nxopen_types.csv");
                    ExportTypes(typeRows, path);
                    generatedFiles.Add(path);
                }

                if (options.ExportMembers)
                {
                    string path = Path.Combine(outputDir, "04_nxopen_members.csv");
                    ExportMembers(memberRows, path);
                    generatedFiles.Add(path);
                }

                if (options.ExportEntryPoints || options.BuildCrosswalk)
                {
                    apiEntries = BuildApiEntryRows(typeRows, memberRows);
                }

                if (options.ExportEntryPoints)
                {
                    string path = Path.Combine(outputDir, "05_nxopen_entry_points.csv");
                    ExportApiEntries(apiEntries, path);
                    generatedFiles.Add(path);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (options.ScanUiCommands)
            {
                Report(progress, 53, "UI-команды", "Сканирование MenuScript и BUTTON ID");
                menuRows = ScanMenuCommands(roots, listing);

                if (options.ExportUiCommands)
                {
                    string path = Path.Combine(outputDir, "06_ui_commands_buttons.csv");
                    ExportMenuCommands(menuRows, path);
                    generatedFiles.Add(path);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (options.ScanUfun)
            {
                Report(progress, 66, "Open C / UFUN", "Сканирование заголовков uf_*.h");
                ufRows = ScanUfFunctions(roots, listing);

                if (options.ExportUfunFunctions)
                {
                    string path = Path.Combine(outputDir, "07_ufun_functions.csv");
                    ExportUfFunctions(ufRows, path);
                    generatedFiles.Add(path);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (options.BuildCrosswalk &&
                menuRows.Count > 0 &&
                (apiEntries.Count > 0 || ufRows.Count > 0))
            {
                Report(progress, 79, "Сопоставление", "Поиск кандидатов UI-команда → API");
                crosswalkRows = BuildCrosswalk(menuRows, apiEntries, ufRows);

                if (options.ExportCrosswalk)
                {
                    string path = Path.Combine(
                        outputDir,
                        "08_ui_command_api_candidates.csv");
                    ExportCrosswalk(crosswalkRows, path);
                    generatedFiles.Add(path);
                }

                if (options.ExportUnmapped)
                {
                    string path = Path.Combine(
                        outputDir,
                        "09_ui_commands_without_strong_api_match.csv");
                    ExportUnmapped(menuRows, crosswalkRows, path);
                    generatedFiles.Add(path);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (options.GenerateSummary)
            {
                Report(progress, 92, "Отчёт", "Формирование итоговой сводки");
                ExportSummary(
                    outputDir,
                    assemblies,
                    roots,
                    typeRows,
                    memberRows,
                    apiEntries,
                    menuRows,
                    ufRows,
                    crosswalkRows);

                generatedFiles.Add(Path.Combine(outputDir, "README_CATALOG.md"));
            }

            CatalogRunResult result = new CatalogRunResult
            {
                Success = true,
                Cancelled = false,
                OutputDirectory = outputDir,
                GeneratedFiles = generatedFiles,
                AssemblyCount = assemblies.Count,
                NamespaceCount = typeRows
                    .Select(t => t.Namespace)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                TypeCount = typeRows.Count,
                MemberCount = memberRows.Count,
                ApiEntryPointCount = apiEntries.Count,
                UiCommandCount = menuRows.Count,
                UfunFunctionCount = ufRows.Count,
                CrosswalkCandidateCount = crosswalkRows.Count
            };

            Report(progress, 100, "Готово", "Каталог сформирован");
            return result;
        }
        catch (OperationCanceledException)
        {
            Report(progress, 0, "Остановлено", "Операция отменена пользователем");

            return new CatalogRunResult
            {
                Success = false,
                Cancelled = true,
                OutputDirectory = outputDir,
                GeneratedFiles = generatedFiles,
                ErrorMessage = "Операция отменена пользователем."
            };
        }
        catch (Exception ex)
        {
            string errorPath = Path.Combine(outputDir, "ERROR.txt");

            try
            {
                File.WriteAllText(errorPath, ex.ToString(), new UTF8Encoding(true));
                generatedFiles.Add(errorPath);
            }
            catch
            {
                // Preserve the original exception.
            }

            Report(progress, 0, "Ошибка", ex.Message);

            return new CatalogRunResult
            {
                Success = false,
                Cancelled = false,
                OutputDirectory = outputDir,
                GeneratedFiles = generatedFiles,
                ErrorMessage = ex.ToString()
            };
        }
        finally
        {
            CatalogRuntime.End();
        }
    }

    private static void Report(
        IProgress<CatalogProgress> progress,
        int percent,
        string stage,
        string message)
    {
        if (progress != null)
        {
            progress.Report(new CatalogProgress
            {
                Percent = Math.Max(0, Math.Min(100, percent)),
                Stage = stage ?? String.Empty,
                Message = message ?? String.Empty
            });
        }
    }

    private static void AddAdditionalRoots(
        ICollection<RootRow> roots,
        IEnumerable<string> additionalRoots)
    {
        if (additionalRoots == null)
        {
            return;
        }

        HashSet<string> existing = new HashSet<string>(
            roots.Select(r => r.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (string raw in additionalRoots)
        {
            if (String.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string value = raw.Trim().Trim('"');

            try
            {
                value = Path.GetFullPath(value);
            }
            catch
            {
                // Keep the user supplied value for reporting.
            }

            if (existing.Add(value))
            {
                roots.Add(new RootRow
                {
                    Source = "user",
                    Name = "additional-root",
                    Value = value,
                    Exists = Directory.Exists(value)
                });
            }
        }
    }

    // ---------------------------------------------------------------------
    // Environment and roots
    // ---------------------------------------------------------------------

    private static List<RootRow> DiscoverRoots(CatalogLogger listing)
    {
        string[] variables =
        {
            "UGII_BASE_DIR",
            "UGII_ROOT_DIR",
            "UGOPEN",
            "UGII_USER_DIR",
            "UGII_SITE_DIR",
            "UGII_GROUP_DIR",
            "UGII_CUSTOM_DIRECTORY_FILE"
        };

        List<RootRow> rows = new List<RootRow>();
        HashSet<string> uniqueDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string variable in variables)
        {
            string value = Environment.GetEnvironmentVariable(variable) ?? String.Empty;
            rows.Add(new RootRow
            {
                Source = "environment",
                Name = variable,
                Value = value,
                Exists = PathExists(value)
            });

            AddDirectoryCandidate(uniqueDirectories, value);
        }

        Assembly coreAssembly = typeof(Session).Assembly;
        string nxOpenDir = SafeDirectoryName(SafeAssemblyLocation(coreAssembly));
        rows.Add(new RootRow
        {
            Source = "assembly",
            Name = "NXOpen.dll directory",
            Value = nxOpenDir,
            Exists = Directory.Exists(nxOpenDir)
        });

        AddDirectoryCandidate(uniqueDirectories, nxOpenDir);

        DirectoryInfo current = String.IsNullOrWhiteSpace(nxOpenDir)
            ? null
            : new DirectoryInfo(nxOpenDir);

        for (int i = 0; i < 4 && current != null; i++)
        {
            AddDirectoryCandidate(uniqueDirectories, current.FullName);
            current = current.Parent;
        }

        string baseDir = Environment.GetEnvironmentVariable("UGII_BASE_DIR");
        if (!String.IsNullOrWhiteSpace(baseDir))
        {
            string[] commonSubdirectories =
            {
                "UGII",
                "UGOPEN",
                "NXBIN",
                "MACH",
                "LOCALIZATION",
                "menus",
                "startup",
                "application"
            };

            foreach (string child in commonSubdirectories)
            {
                AddDirectoryCandidate(
                    uniqueDirectories,
                    Path.Combine(baseDir, child));
            }
        }

        foreach (string path in uniqueDirectories.OrderBy(
            p => p,
            StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new RootRow
            {
                Source = "scan-root",
                Name = "directory",
                Value = path,
                Exists = Directory.Exists(path)
            });
        }

        listing.WriteLine("Discovered scan roots: " +
            uniqueDirectories.Count.ToString(CultureInfo.InvariantCulture));

        return rows;
    }

    private static bool PathExists(string path)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Directory.Exists(path) || File.Exists(path);
    }

    private static void AddDirectoryCandidate(
        ISet<string> result,
        string candidate)
    {
        if (String.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        string value = candidate.Trim().Trim('"');

        if (File.Exists(value))
        {
            value = Path.GetDirectoryName(value) ?? String.Empty;
        }

        if (!Directory.Exists(value))
        {
            return;
        }

        try
        {
            result.Add(Path.GetFullPath(value));
        }
        catch
        {
            result.Add(value);
        }
    }

    // ---------------------------------------------------------------------
    // NXOpen managed API reflection
    // ---------------------------------------------------------------------

    private static List<Assembly> DiscoverNxOpenAssemblies(CatalogLogger listing)
    {
        Dictionary<string, Assembly> result =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddAssemblyIfNxOpen(result, loaded);
        }

        Assembly coreAssembly = typeof(Session).Assembly;
        AddAssemblyIfNxOpen(result, coreAssembly);

        string managedDir = SafeDirectoryName(SafeAssemblyLocation(coreAssembly));

        if (Directory.Exists(managedDir))
        {
            foreach (string file in SafeEnumerateFiles(
                managedDir,
                1,
                delegate(string path)
                {
                    return String.Equals(
                        Path.GetExtension(path),
                        ".dll",
                        StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileName(path).StartsWith(
                            "NXOpen",
                            StringComparison.OrdinalIgnoreCase);
                }))
            {
                try
                {
                    AssemblyName candidateName = AssemblyName.GetAssemblyName(file);

                    Assembly alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => String.Equals(
                            a.GetName().Name,
                            candidateName.Name,
                            StringComparison.OrdinalIgnoreCase));

                    Assembly assembly = alreadyLoaded ?? Assembly.LoadFrom(file);
                    AddAssemblyIfNxOpen(result, assembly);
                }
                catch (Exception ex)
                {
                    listing.WriteLine("Skipped assembly: " + file);
                    listing.WriteLine("  " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        return result.Values
            .OrderBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddAssemblyIfNxOpen(
        IDictionary<string, Assembly> result,
        Assembly assembly)
    {
        if (assembly == null)
        {
            return;
        }

        string name = assembly.GetName().Name ?? String.Empty;
        if (!name.StartsWith("NXOpen", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string key = assembly.FullName ?? name;
        if (!result.ContainsKey(key))
        {
            result.Add(key, assembly);
        }
    }

    private static IEnumerable<Type> GetExportedTypesSafe(
        Assembly assembly,
        CatalogLogger listing)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            listing.WriteLine("Partial type load: " + assembly.GetName().Name);

            if (ex.LoaderExceptions != null)
            {
                foreach (Exception loaderException in ex.LoaderExceptions)
                {
                    if (loaderException != null)
                    {
                        listing.WriteLine("  " + loaderException.Message);
                    }
                }
            }

            return ex.Types
                .Where(t => t != null)
                .Cast<Type>();
        }
        catch (Exception ex)
        {
            listing.WriteLine("Unable to read: " + assembly.GetName().Name);
            listing.WriteLine("  " + ex.Message);
            return new Type[0];
        }
    }

    private static TypeRow CreateTypeRow(Assembly assembly, Type type)
    {
        ObsoleteAttribute obsolete = type.GetCustomAttributes(
            typeof(ObsoleteAttribute),
            false).FirstOrDefault() as ObsoleteAttribute;

        return new TypeRow
        {
            Assembly = assembly.GetName().Name ?? String.Empty,
            Namespace = type.Namespace ?? String.Empty,
            FullName = type.FullName ?? type.Name,
            Name = type.Name,
            Kind = GetTypeKind(type),
            Category = GetTypeCategory(type),
            IsAbstract = type.IsAbstract,
            IsSealed = type.IsSealed,
            IsObsolete = obsolete != null,
            ObsoleteMessage = obsolete == null ? String.Empty : obsolete.Message,
            BaseType = type.BaseType == null
                ? String.Empty
                : FormatType(type.BaseType),
            Interfaces = String.Join(
                " | ",
                type.GetInterfaces()
                    .Select(FormatType)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static void AddMembers(
        ICollection<MemberRow> rows,
        Assembly assembly,
        Type type)
    {
        string assemblyName = assembly.GetName().Name ?? String.Empty;
        string typeName = type.FullName ?? type.Name;
        string ns = type.Namespace ?? String.Empty;

        foreach (ConstructorInfo constructor in type.GetConstructors(
            DeclaredPublicMembers))
        {
            rows.Add(CreateMemberRow(
                assemblyName,
                ns,
                typeName,
                "Constructor",
                type.Name,
                constructor.IsStatic,
                BuildMethodSignature(constructor),
                constructor));
        }

        foreach (MethodInfo method in type.GetMethods(DeclaredPublicMembers)
            .Where(m => !m.IsSpecialName))
        {
            rows.Add(CreateMemberRow(
                assemblyName,
                ns,
                typeName,
                "Method",
                method.Name,
                method.IsStatic,
                BuildMethodSignature(method),
                method));
        }

        foreach (PropertyInfo property in type.GetProperties(
            DeclaredPublicMembers))
        {
            MethodInfo accessor = property.GetMethod ?? property.SetMethod;

            string access = String.Join(
                "/",
                new[]
                {
                    property.CanRead ? "get" : null,
                    property.CanWrite ? "set" : null
                }.Where(x => x != null));

            string indexes = String.Join(
                ", ",
                property.GetIndexParameters().Select(FormatParameter));

            string signature = FormatType(property.PropertyType) +
                " " +
                property.Name;

            if (!String.IsNullOrWhiteSpace(indexes))
            {
                signature += "[" + indexes + "]";
            }

            signature += " {" + access + ";}";

            rows.Add(CreateMemberRow(
                assemblyName,
                ns,
                typeName,
                "Property",
                property.Name,
                accessor != null && accessor.IsStatic,
                signature,
                property));
        }

        foreach (FieldInfo field in type.GetFields(DeclaredPublicMembers))
        {
            rows.Add(CreateMemberRow(
                assemblyName,
                ns,
                typeName,
                "Field",
                field.Name,
                field.IsStatic,
                FormatType(field.FieldType) + " " + field.Name,
                field));
        }

        foreach (EventInfo eventInfo in type.GetEvents(
            DeclaredPublicMembers))
        {
            MethodInfo accessor = eventInfo.AddMethod ?? eventInfo.RemoveMethod;

            rows.Add(CreateMemberRow(
                assemblyName,
                ns,
                typeName,
                "Event",
                eventInfo.Name,
                accessor != null && accessor.IsStatic,
                "event " +
                    FormatType(eventInfo.EventHandlerType ?? typeof(void)) +
                    " " +
                    eventInfo.Name,
                eventInfo));
        }
    }

    private static MemberRow CreateMemberRow(
        string assembly,
        string ns,
        string type,
        string kind,
        string name,
        bool isStatic,
        string signature,
        MemberInfo member)
    {
        ObsoleteAttribute obsolete = member.GetCustomAttributes(
            typeof(ObsoleteAttribute),
            false).FirstOrDefault() as ObsoleteAttribute;

        return new MemberRow
        {
            Assembly = assembly,
            Namespace = ns,
            Type = type,
            Kind = kind,
            Name = name,
            IsStatic = isStatic,
            Signature = signature,
            IsObsolete = obsolete != null,
            ObsoleteMessage = obsolete == null ? String.Empty : obsolete.Message
        };
    }

    private static List<ApiEntryRow> BuildApiEntryRows(
        IEnumerable<TypeRow> types,
        IEnumerable<MemberRow> members)
    {
        List<ApiEntryRow> result = new List<ApiEntryRow>();

        foreach (TypeRow type in types)
        {
            if (type.Category == "Builder" ||
                type.Category == "Collection" ||
                type.Category == "Manager" ||
                type.Category == "Service" ||
                type.Category == "Factory")
            {
                result.Add(new ApiEntryRow
                {
                    Assembly = type.Assembly,
                    Namespace = type.Namespace,
                    DeclaringType = type.FullName,
                    EntryKind = type.Category,
                    Name = type.Name,
                    Signature = type.FullName,
                    IsObsolete = type.IsObsolete
                });
            }
        }

        foreach (MemberRow member in members)
        {
            if (member.Kind != "Method" && member.Kind != "Constructor")
            {
                continue;
            }

            string name = member.Name ?? String.Empty;
            if (member.Kind == "Constructor" ||
                name.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("New", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Add", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Set", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Edit", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Remove", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Update", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Builder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Add(new ApiEntryRow
                {
                    Assembly = member.Assembly,
                    Namespace = member.Namespace,
                    DeclaringType = member.Type,
                    EntryKind = member.Kind,
                    Name = member.Name,
                    Signature = member.Signature,
                    IsObsolete = member.IsObsolete
                });
            }
        }

        return result
            .GroupBy(
                x => x.DeclaringType + "|" + x.EntryKind + "|" + x.Signature,
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Namespace, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DeclaringType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---------------------------------------------------------------------
    // MenuScript/UI command inventory
    // ---------------------------------------------------------------------

    private static List<MenuCommandRow> ScanMenuCommands(
        IEnumerable<RootRow> roots,
        CatalogLogger listing)
    {
        HashSet<string> rootPaths = new HashSet<string>(
            roots.Where(r => r.Source == "scan-root" && r.Exists)
                .Select(r => r.Value),
            StringComparer.OrdinalIgnoreCase);

        HashSet<string> files = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string root in rootPaths)
        {
            foreach (string file in SafeEnumerateFiles(
                root,
                CatalogRuntime.Options.ScanDepth,
                delegate(string path)
                {
                    return MenuExtensions.Contains(
                        Path.GetExtension(path),
                        StringComparer.OrdinalIgnoreCase);
                }))
            {
                files.Add(file);
            }
        }

        listing.WriteLine("Menu/UI files found: " +
            files.Count.ToString(CultureInfo.InvariantCulture));

        List<MenuCommandRow> result = new List<MenuCommandRow>();

        foreach (string file in files.OrderBy(
            f => f,
            StringComparer.OrdinalIgnoreCase))
        {
            CatalogRuntime.ThrowIfCancellationRequested();
            try
            {
                ParseMenuFile(file, result);
            }
            catch (Exception ex)
            {
                listing.WriteLine("Menu parse skipped: " + file);
                listing.WriteLine("  " + ex.Message);
            }
        }

        return result
            .OrderBy(r => r.ButtonId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.SourceLine)
            .ToList();
    }

    private static void ParseMenuFile(
        string file,
        ICollection<MenuCommandRow> result)
    {
        string[] lines = File.ReadAllLines(file);
        MenuCommandRow current = null;

        for (int index = 0; index < lines.Length; index++)
        {
            string original = lines[index];
            string line = StripInlineComment(original).Trim();

            if (String.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string keyword;
            string value;
            SplitKeywordValue(line, out keyword, out value);

            if (IsButtonKeyword(keyword))
            {
                FlushMenuRow(current, result);
                current = new MenuCommandRow
                {
                    DefinitionKind = keyword,
                    ButtonId = Unquote(value),
                    SourceFile = file,
                    SourceLine = index + 1
                };
                continue;
            }

            if (current == null)
            {
                continue;
            }

            switch (keyword.ToUpperInvariant())
            {
                case "LABEL":
                    current.Label = AppendValue(current.Label, value);
                    break;
                case "SYNONYMS":
                    current.Synonyms = AppendValue(current.Synonyms, value);
                    break;
                case "ACCELERATOR":
                    current.Accelerator = AppendValue(current.Accelerator, value);
                    break;
                case "ACTIONS":
                case "ACTION":
                    current.Actions = AppendValue(current.Actions, value);
                    break;
                case "APPLICATION":
                case "APPLICATIONS":
                    current.Applications = AppendValue(current.Applications, value);
                    break;
                case "BITMAP":
                    current.Bitmap = AppendValue(current.Bitmap, value);
                    break;
                case "CASCADE":
                    current.Cascade = AppendValue(current.Cascade, value);
                    break;
                case "SENSITIVE":
                    current.Sensitive = AppendValue(current.Sensitive, value);
                    break;
                case "VISIBLE":
                    current.Visible = AppendValue(current.Visible, value);
                    break;
                case "TOGGLE":
                case "TOGGLE_STATE":
                    current.ToggleState = AppendValue(current.ToggleState, value);
                    break;
                default:
                    if (keyword.StartsWith("END_", StringComparison.OrdinalIgnoreCase))
                    {
                        FlushMenuRow(current, result);
                        current = null;
                    }
                    break;
            }
        }

        FlushMenuRow(current, result);
    }

    private static bool IsButtonKeyword(string keyword)
    {
        return String.Equals(keyword, "BUTTON", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(keyword, "TOGGLE_BUTTON", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(keyword, "RADIO_BUTTON", StringComparison.OrdinalIgnoreCase);
    }

    private static void FlushMenuRow(
        MenuCommandRow row,
        ICollection<MenuCommandRow> result)
    {
        if (row == null || String.IsNullOrWhiteSpace(row.ButtonId))
        {
            return;
        }

        row.Label = Unquote(row.Label);
        row.Synonyms = Unquote(row.Synonyms);
        row.Accelerator = Unquote(row.Accelerator);
        row.Actions = Unquote(row.Actions);
        row.Applications = Unquote(row.Applications);
        row.Bitmap = Unquote(row.Bitmap);
        row.Cascade = Unquote(row.Cascade);
        row.Sensitive = Unquote(row.Sensitive);
        row.Visible = Unquote(row.Visible);
        row.ToggleState = Unquote(row.ToggleState);

        result.Add(row);
    }

    private static string StripInlineComment(string line)
    {
        if (line == null)
        {
            return String.Empty;
        }

        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("#") ||
            trimmed.StartsWith("//") ||
            trimmed.StartsWith("!"))
        {
            return String.Empty;
        }

        int slash = line.IndexOf("//", StringComparison.Ordinal);
        if (slash >= 0)
        {
            return line.Substring(0, slash);
        }

        return line;
    }

    private static void SplitKeywordValue(
        string line,
        out string keyword,
        out string value)
    {
        int whitespace = -1;

        for (int i = 0; i < line.Length; i++)
        {
            if (Char.IsWhiteSpace(line[i]))
            {
                whitespace = i;
                break;
            }
        }

        if (whitespace < 0)
        {
            keyword = line.Trim();
            value = String.Empty;
            return;
        }

        keyword = line.Substring(0, whitespace).Trim();
        value = line.Substring(whitespace + 1).Trim();
    }

    private static string AppendValue(string existing, string value)
    {
        value = Unquote(value);
        if (String.IsNullOrWhiteSpace(existing))
        {
            return value;
        }

        if (String.IsNullOrWhiteSpace(value))
        {
            return existing;
        }

        return existing + " | " + value;
    }

    private static string Unquote(string value)
    {
        if (String.IsNullOrWhiteSpace(value))
        {
            return String.Empty;
        }

        string result = value.Trim();

        if (result.Length >= 2 &&
            ((result[0] == '"' && result[result.Length - 1] == '"') ||
             (result[0] == '\'' && result[result.Length - 1] == '\'')))
        {
            result = result.Substring(1, result.Length - 2);
        }

        return result.Trim();
    }

    // ---------------------------------------------------------------------
    // Open C / UFUN inventory
    // ---------------------------------------------------------------------

    private static List<UfFunctionRow> ScanUfFunctions(
        IEnumerable<RootRow> roots,
        CatalogLogger listing)
    {
        HashSet<string> rootPaths = new HashSet<string>(
            roots.Where(r => r.Source == "scan-root" && r.Exists)
                .Select(r => r.Value),
            StringComparer.OrdinalIgnoreCase);

        HashSet<string> headers = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string root in rootPaths)
        {
            foreach (string file in SafeEnumerateFiles(
                root,
                CatalogRuntime.Options.ScanDepth,
                delegate(string path)
                {
                    string name = Path.GetFileName(path);
                    return name.StartsWith("uf_", StringComparison.OrdinalIgnoreCase) &&
                        String.Equals(
                            Path.GetExtension(path),
                            ".h",
                            StringComparison.OrdinalIgnoreCase);
                }))
            {
                headers.Add(file);
            }
        }

        listing.WriteLine("UFUN headers found: " +
            headers.Count.ToString(CultureInfo.InvariantCulture));

        List<UfFunctionRow> result = new List<UfFunctionRow>();

        foreach (string header in headers.OrderBy(
            h => h,
            StringComparer.OrdinalIgnoreCase))
        {
            CatalogRuntime.ThrowIfCancellationRequested();
            try
            {
                ParseUfHeader(header, result);
            }
            catch (Exception ex)
            {
                listing.WriteLine("UF header parse skipped: " + header);
                listing.WriteLine("  " + ex.Message);
            }
        }

        return result
            .GroupBy(
                r => r.FunctionName + "|" + r.Signature,
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.FunctionName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ParseUfHeader(
        string file,
        ICollection<UfFunctionRow> result)
    {
        string text = File.ReadAllText(file);
        text = Regex.Replace(
            text,
            @"/\*.*?\*/",
            delegate(Match match)
            {
                return new string(
                    '\n',
                    match.Value.Count(c => c == '\n'));
            },
            RegexOptions.Singleline);

        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        StringBuilder statement = new StringBuilder();
        int statementStartLine = 1;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = Regex.Replace(lines[index], @"//.*$", String.Empty).Trim();

            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            if (statement.Length == 0)
            {
                statementStartLine = index + 1;
            }

            statement.Append(' ');
            statement.Append(line);

            int semicolon;
            while ((semicolon = statement.ToString().IndexOf(';')) >= 0)
            {
                string complete = statement.ToString().Substring(0, semicolon + 1);
                string remainder = statement.ToString().Substring(semicolon + 1);

                ExtractUfFunction(
                    file,
                    statementStartLine,
                    complete,
                    result);

                statement.Clear();
                statement.Append(remainder.Trim());
                statementStartLine = index + 1;
            }
        }
    }

    private static void ExtractUfFunction(
        string file,
        int sourceLine,
        string statement,
        ICollection<UfFunctionRow> result)
    {
        if (statement.IndexOf("typedef", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        Match match = Regex.Match(
            statement,
            @"\b(UF_[A-Za-z0-9_]+)\s*\(",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return;
        }

        string functionName = match.Groups[1].Value;
        string prefix = statement.Substring(0, match.Index).Trim();
        prefix = Regex.Replace(prefix, @"\s+", " ");

        string signature = Regex.Replace(
            statement.Trim(),
            @"\s+",
            " ");

        result.Add(new UfFunctionRow
        {
            FunctionName = functionName,
            ReturnDeclaration = prefix,
            Signature = signature,
            HeaderFile = file,
            SourceLine = sourceLine
        });
    }

    // ---------------------------------------------------------------------
    // UI -> API candidate crosswalk
    // ---------------------------------------------------------------------

    private static List<CrosswalkRow> BuildCrosswalk(
        IEnumerable<MenuCommandRow> menuRows,
        IEnumerable<ApiEntryRow> apiEntries,
        IEnumerable<UfFunctionRow> ufRows)
    {
        List<SearchCandidate> candidates = new List<SearchCandidate>();

        foreach (ApiEntryRow entry in apiEntries)
        {
            string display = entry.DeclaringType + "." + entry.Name;
            candidates.Add(new SearchCandidate
            {
                Kind = "NXOpen " + entry.EntryKind,
                ApiName = display,
                Signature = entry.Signature,
                SearchText = NormalizeForSearch(
                    entry.Namespace + " " +
                    entry.DeclaringType + " " +
                    entry.Name + " " +
                    entry.Signature)
            });
        }

        foreach (UfFunctionRow uf in ufRows)
        {
            candidates.Add(new SearchCandidate
            {
                Kind = "Open C / UFUN",
                ApiName = uf.FunctionName,
                Signature = uf.Signature,
                SearchText = NormalizeForSearch(
                    uf.FunctionName + " " + uf.Signature)
            });
        }

        Dictionary<string, List<SearchCandidate>> tokenIndex =
            new Dictionary<string, List<SearchCandidate>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (SearchCandidate candidate in candidates)
        {
            foreach (string token in Tokenize(candidate.SearchText))
            {
                List<SearchCandidate> bucket;
                if (!tokenIndex.TryGetValue(token, out bucket))
                {
                    bucket = new List<SearchCandidate>();
                    tokenIndex[token] = bucket;
                }

                bucket.Add(candidate);
            }
        }

        List<CrosswalkRow> result = new List<CrosswalkRow>();

        foreach (MenuCommandRow command in menuRows)
        {
            CatalogRuntime.ThrowIfCancellationRequested();
            string query = String.Join(
                " ",
                new[]
                {
                    command.Label,
                    command.Synonyms,
                    command.ButtonId
                }.Where(x => !String.IsNullOrWhiteSpace(x)));

            List<string> tokens = Tokenize(NormalizeForSearch(query))
                .Where(t => !CrosswalkStopWords.Contains(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            HashSet<SearchCandidate> pool = new HashSet<SearchCandidate>();

            foreach (string token in tokens)
            {
                List<SearchCandidate> bucket;
                if (tokenIndex.TryGetValue(token, out bucket))
                {
                    foreach (SearchCandidate candidate in bucket)
                    {
                        pool.Add(candidate);
                    }
                }
            }

            List<ScoredCandidate> scored = pool
                .Select(candidate => new ScoredCandidate
                {
                    Candidate = candidate,
                    Score = ScoreCandidate(command, tokens, candidate)
                })
                .Where(x => x.Score >= CatalogRuntime.Options.MinimumCandidateScore)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Candidate.ApiName, StringComparer.OrdinalIgnoreCase)
                .Take(CatalogRuntime.Options.CandidateLimit)
                .ToList();

            int rank = 1;
            foreach (ScoredCandidate item in scored)
            {
                result.Add(new CrosswalkRow
                {
                    ButtonId = command.ButtonId,
                    Label = command.Label,
                    Accelerator = command.Accelerator,
                    Actions = command.Actions,
                    SourceFile = command.SourceFile,
                    CandidateRank = rank,
                    CandidateKind = item.Candidate.Kind,
                    ApiName = item.Candidate.ApiName,
                    ApiSignature = item.Candidate.Signature,
                    Score = item.Score,
                    Confidence = ConfidenceFromScore(item.Score),
                    MappingStatus =
                        item.Score >= CatalogRuntime.Options.StrongCandidateScore
                            ? "strong candidate; verify with recorded journal"
                            : item.Score >= CatalogRuntime.Options.MinimumCandidateScore
                                ? "probable search candidate"
                                : "weak search hint"
                });

                rank++;
            }
        }

        return result;
    }

    private static int ScoreCandidate(
        MenuCommandRow command,
        IList<string> queryTokens,
        SearchCandidate candidate)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        string api = candidate.SearchText;
        int matched = queryTokens.Count(token =>
            api.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

        int score = (int)Math.Round(
            60.0 * matched / queryTokens.Count,
            MidpointRounding.AwayFromZero);

        string labelNormalized = NormalizeForSearch(command.Label);
        string buttonNormalized = NormalizeForSearch(command.ButtonId);
        string apiNameNormalized = NormalizeForSearch(candidate.ApiName);

        if (!String.IsNullOrWhiteSpace(labelNormalized) &&
            String.Equals(
                RemoveGenericApiWords(apiNameNormalized),
                RemoveGenericApiWords(labelNormalized),
                StringComparison.OrdinalIgnoreCase))
        {
            score += 45;
        }
        else if (!String.IsNullOrWhiteSpace(labelNormalized) &&
            apiNameNormalized.IndexOf(
                RemoveGenericApiWords(labelNormalized),
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 25;
        }

        if (!String.IsNullOrWhiteSpace(buttonNormalized))
        {
            foreach (string token in Tokenize(buttonNormalized))
            {
                if (token.Length >= 4 &&
                    apiNameNormalized.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 4;
                }
            }
        }

        if (candidate.ApiName.EndsWith(
            "Builder",
            StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (candidate.ApiName.IndexOf(
            ".Create",
            StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 6;
        }

        return Math.Min(score, 100);
    }

    private static string ConfidenceFromScore(int score)
    {
        if (score >= CatalogRuntime.Options.StrongCandidateScore)
        {
            return "HIGH";
        }

        if (score >= CatalogRuntime.Options.MinimumCandidateScore)
        {
            return "MEDIUM";
        }

        return "LOW";
    }

    private static string RemoveGenericApiWords(string value)
    {
        List<string> tokens = Tokenize(value)
            .Where(t => !CrosswalkStopWords.Contains(t))
            .Where(t => !String.Equals(t, "builder", StringComparison.OrdinalIgnoreCase))
            .Where(t => !String.Equals(t, "collection", StringComparison.OrdinalIgnoreCase))
            .Where(t => !String.Equals(t, "manager", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return String.Join(" ", tokens);
    }

    private static string NormalizeForSearch(string value)
    {
        if (String.IsNullOrWhiteSpace(value))
        {
            return String.Empty;
        }

        string camelSeparated = Regex.Replace(
            value,
            @"([a-z0-9])([A-Z])",
            "$1 $2");

        string normalized = Regex.Replace(
            camelSeparated.ToLowerInvariant(),
            @"[^a-z0-9]+",
            " ");

        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static List<string> Tokenize(string normalized)
    {
        if (String.IsNullOrWhiteSpace(normalized))
        {
            return new List<string>();
        }

        return normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToList();
    }

    // ---------------------------------------------------------------------
    // Safe file traversal
    // ---------------------------------------------------------------------

    private static IEnumerable<string> SafeEnumerateFiles(
        string root,
        int maxDepth,
        Func<string, bool> predicate)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        Queue<DirectoryDepth> queue = new Queue<DirectoryDepth>();
        HashSet<string> visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        queue.Enqueue(new DirectoryDepth
        {
            Path = root,
            Depth = 0
        });

        while (queue.Count > 0)
        {
            CatalogRuntime.ThrowIfCancellationRequested();
            DirectoryDepth item = queue.Dequeue();

            if (item.Depth > maxDepth)
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(item.Path);
            }
            catch
            {
                fullPath = item.Path;
            }

            if (!visited.Add(fullPath))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(fullPath);
            }
            catch
            {
                files = new string[0];
            }

            foreach (string file in files)
            {
                bool accepted = false;
                try
                {
                    accepted = predicate(file);
                }
                catch
                {
                    accepted = false;
                }

                if (accepted)
                {
                    yield return file;
                }
            }

            if (item.Depth == maxDepth)
            {
                continue;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(fullPath);
            }
            catch
            {
                directories = new string[0];
            }

            foreach (string directory in directories)
            {
                queue.Enqueue(new DirectoryDepth
                {
                    Path = directory,
                    Depth = item.Depth + 1
                });
            }
        }
    }

    // ---------------------------------------------------------------------
    // Export
    // ---------------------------------------------------------------------

    private static void ExportRoots(
        IEnumerable<RootRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(writer, "source", "name", "value", "exists");

            foreach (RootRow row in rows)
            {
                WriteCsvRow(
                    writer,
                    row.Source,
                    row.Name,
                    row.Value,
                    row.Exists.ToString());
            }
        }
    }

    private static void ExportAssemblies(
        IEnumerable<Assembly> assemblies,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(writer, "assembly", "version", "location", "full_name");

            foreach (Assembly assembly in assemblies)
            {
                AssemblyName name = assembly.GetName();

                WriteCsvRow(
                    writer,
                    name.Name ?? String.Empty,
                    name.Version == null ? String.Empty : name.Version.ToString(),
                    SafeAssemblyLocation(assembly),
                    assembly.FullName ?? String.Empty);
            }
        }
    }

    private static void ExportNamespaces(
        IEnumerable<TypeRow> types,
        IEnumerable<MemberRow> members,
        string path)
    {
        Dictionary<string, int> memberCounts = members
            .GroupBy(m => m.Namespace)
            .ToDictionary(
                g => g.Key,
                g => g.Count(),
                StringComparer.Ordinal);

        var rows = types
            .GroupBy(t => t.Namespace)
            .Select(g => new
            {
                Namespace = g.Key,
                Assemblies = String.Join(
                    " | ",
                    g.Select(t => t.Assembly)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),
                TypeCount = g.Count(),
                MemberCount = memberCounts.ContainsKey(g.Key)
                    ? memberCounts[g.Key]
                    : 0
            })
            .OrderBy(x => x.Namespace, StringComparer.OrdinalIgnoreCase);

        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "namespace",
                "assemblies",
                "public_type_count",
                "public_member_count");

            foreach (var row in rows)
            {
                WriteCsvRow(
                    writer,
                    row.Namespace,
                    row.Assemblies,
                    row.TypeCount.ToString(CultureInfo.InvariantCulture),
                    row.MemberCount.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void ExportTypes(
        IEnumerable<TypeRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "assembly",
                "namespace",
                "full_name",
                "name",
                "kind",
                "category",
                "is_abstract",
                "is_sealed",
                "is_obsolete",
                "obsolete_message",
                "base_type",
                "interfaces");

            foreach (TypeRow row in rows.OrderBy(
                r => r.FullName,
                StringComparer.OrdinalIgnoreCase))
            {
                WriteCsvRow(
                    writer,
                    row.Assembly,
                    row.Namespace,
                    row.FullName,
                    row.Name,
                    row.Kind,
                    row.Category,
                    row.IsAbstract.ToString(),
                    row.IsSealed.ToString(),
                    row.IsObsolete.ToString(),
                    row.ObsoleteMessage,
                    row.BaseType,
                    row.Interfaces);
            }
        }
    }

    private static void ExportMembers(
        IEnumerable<MemberRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "assembly",
                "namespace",
                "type",
                "kind",
                "name",
                "is_static",
                "is_obsolete",
                "obsolete_message",
                "signature");

            foreach (MemberRow row in rows
                .OrderBy(r => r.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Signature, StringComparer.OrdinalIgnoreCase))
            {
                WriteCsvRow(
                    writer,
                    row.Assembly,
                    row.Namespace,
                    row.Type,
                    row.Kind,
                    row.Name,
                    row.IsStatic.ToString(),
                    row.IsObsolete.ToString(),
                    row.ObsoleteMessage,
                    row.Signature);
            }
        }
    }

    private static void ExportApiEntries(
        IEnumerable<ApiEntryRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "assembly",
                "namespace",
                "declaring_type",
                "entry_kind",
                "name",
                "is_obsolete",
                "signature");

            foreach (ApiEntryRow row in rows)
            {
                WriteCsvRow(
                    writer,
                    row.Assembly,
                    row.Namespace,
                    row.DeclaringType,
                    row.EntryKind,
                    row.Name,
                    row.IsObsolete.ToString(),
                    row.Signature);
            }
        }
    }

    private static void ExportMenuCommands(
        IEnumerable<MenuCommandRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "button_id",
                "label",
                "synonyms",
                "definition_kind",
                "accelerator",
                "actions",
                "applications",
                "bitmap",
                "cascade",
                "sensitive",
                "visible",
                "toggle_state",
                "source_file",
                "source_line");

            foreach (MenuCommandRow row in rows)
            {
                WriteCsvRow(
                    writer,
                    row.ButtonId,
                    row.Label,
                    row.Synonyms,
                    row.DefinitionKind,
                    row.Accelerator,
                    row.Actions,
                    row.Applications,
                    row.Bitmap,
                    row.Cascade,
                    row.Sensitive,
                    row.Visible,
                    row.ToggleState,
                    row.SourceFile,
                    row.SourceLine.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void ExportUfFunctions(
        IEnumerable<UfFunctionRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "function_name",
                "return_declaration",
                "signature",
                "header_file",
                "source_line");

            foreach (UfFunctionRow row in rows)
            {
                WriteCsvRow(
                    writer,
                    row.FunctionName,
                    row.ReturnDeclaration,
                    row.Signature,
                    row.HeaderFile,
                    row.SourceLine.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void ExportCrosswalk(
        IEnumerable<CrosswalkRow> rows,
        string path)
    {
        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "button_id",
                "label",
                "accelerator",
                "actions",
                "source_file",
                "candidate_rank",
                "candidate_kind",
                "api_name",
                "api_signature",
                "score",
                "confidence",
                "mapping_status");

            foreach (CrosswalkRow row in rows
                .OrderBy(r => r.ButtonId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.CandidateRank))
            {
                WriteCsvRow(
                    writer,
                    row.ButtonId,
                    row.Label,
                    row.Accelerator,
                    row.Actions,
                    row.SourceFile,
                    row.CandidateRank.ToString(CultureInfo.InvariantCulture),
                    row.CandidateKind,
                    row.ApiName,
                    row.ApiSignature,
                    row.Score.ToString(CultureInfo.InvariantCulture),
                    row.Confidence,
                    row.MappingStatus);
            }
        }
    }

    private static void ExportUnmapped(
        IEnumerable<MenuCommandRow> menuRows,
        IEnumerable<CrosswalkRow> crosswalkRows,
        string path)
    {
        HashSet<string> strong = new HashSet<string>(
            crosswalkRows
                .Where(r => r.Score >= CatalogRuntime.Options.StrongCandidateScore)
                .Select(r => MenuIdentity(r.ButtonId, r.SourceFile)),
            StringComparer.OrdinalIgnoreCase);

        using (StreamWriter writer = CreateCsvWriter(path))
        {
            WriteCsvRow(
                writer,
                "button_id",
                "label",
                "actions",
                "source_file",
                "reason");

            foreach (MenuCommandRow row in menuRows)
            {
                if (strong.Contains(MenuIdentity(row.ButtonId, row.SourceFile)))
                {
                    continue;
                }

                WriteCsvRow(
                    writer,
                    row.ButtonId,
                    row.Label,
                    row.Actions,
                    row.SourceFile,
                    "No NXOpen/UFUN candidate scored 65 or higher. Record a journal or inspect official documentation.");
            }
        }
    }

    private static string MenuIdentity(string buttonId, string sourceFile)
    {
        return (buttonId ?? String.Empty) + "|" + (sourceFile ?? String.Empty);
    }

    private static void ExportSummary(
        string outputDir,
        IReadOnlyCollection<Assembly> assemblies,
        IReadOnlyCollection<RootRow> roots,
        IReadOnlyCollection<TypeRow> types,
        IReadOnlyCollection<MemberRow> members,
        IReadOnlyCollection<ApiEntryRow> apiEntries,
        IReadOnlyCollection<MenuCommandRow> menuRows,
        IReadOnlyCollection<UfFunctionRow> ufRows,
        IReadOnlyCollection<CrosswalkRow> crosswalkRows)
    {
        int high = crosswalkRows.Count(r => r.Confidence == "HIGH");
        int medium = crosswalkRows.Count(r => r.Confidence == "MEDIUM");
        int low = crosswalkRows.Count(r => r.Confidence == "LOW");

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("# NX 2512 — полный каталог функций и API");
        builder.AppendLine();
        builder.AppendLine("- Generated: `" +
            DateTime.Now.ToString("O", CultureInfo.InvariantCulture) + "`");
        builder.AppendLine("- NXOpen core assembly: `" +
            EscapeMarkdown(typeof(Session).Assembly.FullName ?? String.Empty) + "`");
        builder.AppendLine("- NXOpen assemblies: **" +
            assemblies.Count.ToString(CultureInfo.InvariantCulture) + "**");
        builder.AppendLine("- Public NXOpen types: **" +
            types.Count.ToString(CultureInfo.InvariantCulture) + "**");
        builder.AppendLine("- Public NXOpen members: **" +
            members.Count.ToString(CultureInfo.InvariantCulture) + "**");
        builder.AppendLine("- NXOpen API entry points: **" +
            apiEntries.Count.ToString(CultureInfo.InvariantCulture) + "**");
        builder.AppendLine("- UI command definitions / BUTTON IDs: **" +
            menuRows.Count.ToString(CultureInfo.InvariantCulture) + "**");
        builder.AppendLine("- Open C / UFUN functions: **" +
            ufRows.Count.ToString(CultureInfo.InvariantCulture) + "**");
        builder.AppendLine();
        builder.AppendLine("## Файлы");
        builder.AppendLine();
        builder.AppendLine("1. `00_environment_roots.csv` — найденные каталоги NX.");
        builder.AppendLine("2. `01_nxopen_assemblies.csv` — NXOpen DLL и версии.");
        builder.AppendLine("3. `02_nxopen_namespaces.csv` — пространства имён.");
        builder.AppendLine("4. `03_nxopen_types.csv` — классы, интерфейсы, enum и struct.");
        builder.AppendLine("5. `04_nxopen_members.csv` — методы, свойства, поля, события и конструкторы.");
        builder.AppendLine("6. `05_nxopen_entry_points.csv` — Builder, Collection, Manager, Service и фабричные методы.");
        builder.AppendLine("7. `06_ui_commands_buttons.csv` — UI-команды с `BUTTON ID`, label, accelerator и action.");
        builder.AppendLine("8. `07_ufun_functions.csv` — Open C / UFUN функции.");
        builder.AppendLine("9. `08_ui_command_api_candidates.csv` — кандидаты соответствия UI-команд API.");
        builder.AppendLine("10. `09_ui_commands_without_strong_api_match.csv` — команды без сильного совпадения.");
        builder.AppendLine();
        builder.AppendLine("## Точность соответствия команды и API");
        builder.AppendLine();
        builder.AppendLine("| Уровень | Количество строк-кандидатов | Значение |");
        builder.AppendLine("|---|---:|---|");
        builder.AppendLine("| HIGH | " + high.ToString(CultureInfo.InvariantCulture) +
            " | Сильное совпадение имени; всё равно проверить журналом |");
        builder.AppendLine("| MEDIUM | " + medium.ToString(CultureInfo.InvariantCulture) +
            " | Вероятный API для дальнейшего поиска |");
        builder.AppendLine("| LOW | " + low.ToString(CultureInfo.InvariantCulture) +
            " | Только поисковая подсказка |");
        builder.AppendLine();
        builder.AppendLine("> В NX нет гарантированной связи «одна кнопка = один API-метод». " +
            "Для окончательной проверки конкретной операции запишите NX Journal и сопоставьте " +
            "созданный Builder/Collection с таблицами этого каталога.");
        builder.AppendLine();
        builder.AppendLine("## Категории NXOpen типов");
        builder.AppendLine();
        builder.AppendLine("| Категория | Количество |");
        builder.AppendLine("|---|---:|");

        foreach (var category in types
            .GroupBy(t => t.Category)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("| `" + EscapeMarkdown(category.Key) + "` | " +
                category.Count().ToString(CultureInfo.InvariantCulture) + " |");
        }

        builder.AppendLine();
        builder.AppendLine("## Важные ограничения");
        builder.AppendLine();
        builder.AppendLine("- Перечень managed API точен для установленных `NXOpen*.dll`.");
        builder.AppendLine("- UFUN перечень точен для обнаруженных `uf_*.h`.");
        builder.AppendLine("- Если отдельный модуль или Author toolkit не установлен, его DLL/headers не попадут в каталог.");
        builder.AppendLine("- Наличие API не означает наличие runtime-лицензии соответствующего NX-модуля.");
        builder.AppendLine("- Python, C++, Java и .NET используют близкую Common API модель, но сигнатуры и оболочки могут отличаться.");
        builder.AppendLine("- `BUTTON ID` — внутренний идентификатор UI-команды, а не NXOpen-метод.");
        builder.AppendLine("- Для deprecated API смотрите столбцы `is_obsolete` и `obsolete_message`, а также NXOpenReporter/What's Changed.");

        File.WriteAllText(
            Path.Combine(outputDir, "README_CATALOG.md"),
            builder.ToString(),
            new UTF8Encoding(true));
    }

    // ---------------------------------------------------------------------
    // Formatting helpers
    // ---------------------------------------------------------------------

    private static string BuildMethodSignature(MethodBase method)
    {
        string generic = String.Empty;
        MethodInfo methodInfo = method as MethodInfo;

        if (methodInfo != null && methodInfo.IsGenericMethodDefinition)
        {
            generic = "<" + String.Join(
                ", ",
                methodInfo.GetGenericArguments().Select(a => a.Name)) + ">";
        }

        string returnType = methodInfo == null
            ? String.Empty
            : FormatType(methodInfo.ReturnType) + " ";

        string parameters = String.Join(
            ", ",
            method.GetParameters().Select(FormatParameter));

        return returnType + method.Name + generic + "(" + parameters + ")";
    }

    private static string FormatParameter(ParameterInfo parameter)
    {
        string prefix = parameter.IsOut
            ? "out "
            : parameter.ParameterType.IsByRef
                ? "ref "
                : String.Empty;

        Type parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType() ?? parameter.ParameterType
            : parameter.ParameterType;

        string optional = parameter.IsOptional
            ? " = " + FormatDefaultValue(parameter.DefaultValue)
            : String.Empty;

        return prefix +
            FormatType(parameterType) +
            " " +
            (parameter.Name ?? "arg") +
            optional;
    }

    private static string FormatDefaultValue(object value)
    {
        if (value == null || value == DBNull.Value)
        {
            return "null";
        }

        string text = value as string;
        if (text != null)
        {
            return "\"" + text.Replace("\"", "\\\"") + "\"";
        }

        if (value is char)
        {
            return "'" + value.ToString() + "'";
        }

        if (value is bool)
        {
            return (bool)value ? "true" : "false";
        }

        IFormattable formattable = value as IFormattable;
        if (formattable != null)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? String.Empty;
    }

    private static string FormatType(Type type)
    {
        if (type.IsArray)
        {
            return FormatType(type.GetElementType() ?? typeof(object)) +
                "[" +
                new string(',', type.GetArrayRank() - 1) +
                "]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsGenericType)
        {
            string name = type.GetGenericTypeDefinition().FullName ??
                type.GetGenericTypeDefinition().Name;

            int tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name.Substring(0, tick);
            }

            string arguments = String.Join(
                ", ",
                type.GetGenericArguments().Select(FormatType));

            return name + "<" + arguments + ">";
        }

        return type.FullName ?? type.Name;
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsEnum)
        {
            return "Enum";
        }

        if (type.IsInterface)
        {
            return "Interface";
        }

        if (type.BaseType != null &&
            typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
        {
            return "Delegate";
        }

        if (type.IsValueType)
        {
            return "Struct";
        }

        return "Class";
    }

    private static string GetTypeCategory(Type type)
    {
        string name = type.Name;

        if (name.EndsWith("Builder", StringComparison.OrdinalIgnoreCase))
        {
            return "Builder";
        }

        if (name.EndsWith("Collection", StringComparison.OrdinalIgnoreCase))
        {
            return "Collection";
        }

        if (name.EndsWith("Manager", StringComparison.OrdinalIgnoreCase))
        {
            return "Manager";
        }

        if (name.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
        {
            return "Service";
        }

        if (name.EndsWith("Factory", StringComparison.OrdinalIgnoreCase))
        {
            return "Factory";
        }

        if (type.IsEnum)
        {
            return "Enum";
        }

        if (type.IsInterface)
        {
            return "Interface";
        }

        if (type.IsValueType)
        {
            return "ValueType";
        }

        return "Object";
    }

    private static StreamWriter CreateCsvWriter(string path)
    {
        return new StreamWriter(path, false, new UTF8Encoding(true));
    }

    private static void WriteCsvRow(
        TextWriter writer,
        params string[] fields)
    {
        writer.WriteLine(String.Join(",", fields.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        value = value ?? String.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeMarkdown(string value)
    {
        return (value ?? String.Empty)
            .Replace("|", "\\|")
            .Replace("`", "\\`")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private static string SafeAssemblyLocation(Assembly assembly)
    {
        try
        {
            return assembly.Location ?? String.Empty;
        }
        catch
        {
            return String.Empty;
        }
    }

    private static string SafeDirectoryName(string path)
    {
        try
        {
            return String.IsNullOrWhiteSpace(path)
                ? String.Empty
                : Path.GetDirectoryName(path) ?? String.Empty;
        }
        catch
        {
            return String.Empty;
        }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }

    // ---------------------------------------------------------------------
    // Data rows
    // ---------------------------------------------------------------------

    private sealed class RootRow
    {
        public string Source = String.Empty;
        public string Name = String.Empty;
        public string Value = String.Empty;
        public bool Exists;
    }

    private sealed class TypeRow
    {
        public string Assembly = String.Empty;
        public string Namespace = String.Empty;
        public string FullName = String.Empty;
        public string Name = String.Empty;
        public string Kind = String.Empty;
        public string Category = String.Empty;
        public bool IsAbstract;
        public bool IsSealed;
        public bool IsObsolete;
        public string ObsoleteMessage = String.Empty;
        public string BaseType = String.Empty;
        public string Interfaces = String.Empty;
    }

    private sealed class MemberRow
    {
        public string Assembly = String.Empty;
        public string Namespace = String.Empty;
        public string Type = String.Empty;
        public string Kind = String.Empty;
        public string Name = String.Empty;
        public bool IsStatic;
        public bool IsObsolete;
        public string ObsoleteMessage = String.Empty;
        public string Signature = String.Empty;
    }

    private sealed class ApiEntryRow
    {
        public string Assembly = String.Empty;
        public string Namespace = String.Empty;
        public string DeclaringType = String.Empty;
        public string EntryKind = String.Empty;
        public string Name = String.Empty;
        public string Signature = String.Empty;
        public bool IsObsolete;
    }

    private sealed class MenuCommandRow
    {
        public string DefinitionKind = String.Empty;
        public string ButtonId = String.Empty;
        public string Label = String.Empty;
        public string Synonyms = String.Empty;
        public string Accelerator = String.Empty;
        public string Actions = String.Empty;
        public string Applications = String.Empty;
        public string Bitmap = String.Empty;
        public string Cascade = String.Empty;
        public string Sensitive = String.Empty;
        public string Visible = String.Empty;
        public string ToggleState = String.Empty;
        public string SourceFile = String.Empty;
        public int SourceLine;
    }

    private sealed class UfFunctionRow
    {
        public string FunctionName = String.Empty;
        public string ReturnDeclaration = String.Empty;
        public string Signature = String.Empty;
        public string HeaderFile = String.Empty;
        public int SourceLine;
    }

    private sealed class SearchCandidate
    {
        public string Kind = String.Empty;
        public string ApiName = String.Empty;
        public string Signature = String.Empty;
        public string SearchText = String.Empty;
    }

    private sealed class ScoredCandidate
    {
        public SearchCandidate Candidate;
        public int Score;
    }

    private sealed class CrosswalkRow
    {
        public string ButtonId = String.Empty;
        public string Label = String.Empty;
        public string Accelerator = String.Empty;
        public string Actions = String.Empty;
        public string SourceFile = String.Empty;
        public int CandidateRank;
        public string CandidateKind = String.Empty;
        public string ApiName = String.Empty;
        public string ApiSignature = String.Empty;
        public int Score;
        public string Confidence = String.Empty;
        public string MappingStatus = String.Empty;
    }

    private sealed class DirectoryDepth
    {
        public string Path = String.Empty;
        public int Depth;
    }
}
