namespace NxEskd.Core.Planning;

public sealed record DrawingPackagePlan(
    string PackageId,
    string Name,
    string Revision,
    string? SourceAssembly,
    string OutputDirectory,
    IReadOnlyList<DrawingPackageDocumentPlan> Documents,
    IReadOnlyList<string> ExecutionOrder,
    PackagePublicationPolicy Publication)
{
    public IReadOnlyDictionary<string, DrawingPackageDocumentPlan> DocumentsById
        => Documents.ToDictionary(document => document.DocumentId, StringComparer.OrdinalIgnoreCase);
}

public sealed record DrawingPackageDocumentPlan(
    string DocumentId,
    string SourcePart,
    string ProfilePath,
    string Designation,
    string Name,
    string DocumentKind,
    string NativeOutputPath,
    IReadOnlyList<string> Dependencies,
    bool Enabled = true);

public sealed record PackagePublicationPolicy(
    bool DryRun,
    bool StopOnFailure,
    bool ResumeFromJournal,
    bool AtomicPublish,
    bool RequireAllDocuments,
    string JournalPath);
