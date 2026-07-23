using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

public sealed class CatalogOptions
{
    public string ProfileName { get; set; } = "Полный каталог NX 2512";
    public string OutputDirectory { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public bool CreateTimestampedSubdirectory { get; set; } = true;

    public bool ScanManagedApi { get; set; } = true;
    public bool ScanUiCommands { get; set; } = true;
    public bool ScanUfun { get; set; } = true;
    public bool BuildCrosswalk { get; set; } = true;

    public bool ExportEnvironmentRoots { get; set; } = true;
    public bool ExportAssemblies { get; set; } = true;
    public bool ExportNamespaces { get; set; } = true;
    public bool ExportTypes { get; set; } = true;
    public bool ExportMembers { get; set; } = true;
    public bool ExportEntryPoints { get; set; } = true;
    public bool ExportUiCommands { get; set; } = true;
    public bool ExportUfunFunctions { get; set; } = true;
    public bool ExportCrosswalk { get; set; } = true;
    public bool ExportUnmapped { get; set; } = true;
    public bool GenerateSummary { get; set; } = true;

    public int ScanDepth { get; set; } = 10;
    public int CandidateLimit { get; set; } = 5;
    public int MinimumCandidateScore { get; set; } = 35;
    public int StrongCandidateScore { get; set; } = 65;

    public bool OpenOutputWhenComplete { get; set; } = true;
    public List<string> AdditionalRoots { get; set; } = new List<string>();

    public void Normalize()
    {
        if (String.IsNullOrWhiteSpace(ProfileName))
        {
            ProfileName = "Профиль NX Catalog Studio";
        }

        if (!String.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = Environment.ExpandEnvironmentVariables(
                OutputDirectory.Trim().Trim('"'));
        }

        if (String.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);
        }

        ScanDepth = Math.Max(1, Math.Min(30, ScanDepth));
        CandidateLimit = Math.Max(1, Math.Min(20, CandidateLimit));
        MinimumCandidateScore = Math.Max(0, Math.Min(100, MinimumCandidateScore));
        StrongCandidateScore = Math.Max(
            MinimumCandidateScore,
            Math.Min(100, StrongCandidateScore));

        if (!ScanManagedApi)
        {
            ExportAssemblies = false;
            ExportNamespaces = false;
            ExportTypes = false;
            ExportMembers = false;
            ExportEntryPoints = false;
        }

        if (!ScanUiCommands)
        {
            ExportUiCommands = false;
        }

        if (!ScanUfun)
        {
            ExportUfunFunctions = false;
        }

        if (!BuildCrosswalk)
        {
            ExportCrosswalk = false;
            ExportUnmapped = false;
        }

        AdditionalRoots ??= new List<string>();
        AdditionalRoots = AdditionalRoots
            .Where(path => !String.IsNullOrWhiteSpace(path))
            .Select(path => Environment.ExpandEnvironmentVariables(
                path.Trim().Trim('"')))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public CatalogOptions Clone()
    {
        string json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<CatalogOptions>(json) ??
            new CatalogOptions();
    }

    public void Save(string path)
    {
        Normalize();

        string directory = Path.GetDirectoryName(path) ?? String.Empty;
        if (!String.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    public static CatalogOptions Load(string path)
    {
        CatalogOptions options = JsonSerializer.Deserialize<CatalogOptions>(
            File.ReadAllText(path)) ?? new CatalogOptions();

        options.Normalize();
        return options;
    }
}

public sealed class CatalogProgress
{
    public int Percent { get; set; }
    public string Stage { get; set; } = String.Empty;
    public string Message { get; set; } = String.Empty;
}

public sealed class CatalogRunResult
{
    public bool Success { get; set; }
    public bool Cancelled { get; set; }
    public string OutputDirectory { get; set; } = String.Empty;
    public List<string> GeneratedFiles { get; set; } = new List<string>();
    public string ErrorMessage { get; set; } = String.Empty;

    public int AssemblyCount { get; set; }
    public int NamespaceCount { get; set; }
    public int TypeCount { get; set; }
    public int MemberCount { get; set; }
    public int ApiEntryPointCount { get; set; }
    public int UiCommandCount { get; set; }
    public int UfunFunctionCount { get; set; }
    public int CrosswalkCandidateCount { get; set; }
}

internal static class CatalogRuntime
{
    private static CatalogOptions currentOptions = new CatalogOptions();
    private static CancellationToken cancellationToken = CancellationToken.None;

    public static CatalogOptions Options
    {
        get { return currentOptions; }
    }

    public static void Begin(
        CatalogOptions options,
        CancellationToken token)
    {
        currentOptions = options ?? new CatalogOptions();
        cancellationToken = token;
    }

    public static void End()
    {
        cancellationToken = CancellationToken.None;
    }

    public static void ThrowIfCancellationRequested()
    {
        cancellationToken.ThrowIfCancellationRequested();
    }
}

internal sealed class CatalogLogger
{
    private readonly IProgress<CatalogProgress> progress;

    public CatalogLogger(IProgress<CatalogProgress> progress)
    {
        this.progress = progress;
    }

    public void WriteLine(string text)
    {
        if (progress == null)
        {
            return;
        }

        progress.Report(new CatalogProgress
        {
            Percent = -1,
            Stage = "Журнал",
            Message = text ?? String.Empty
        });
    }
}
