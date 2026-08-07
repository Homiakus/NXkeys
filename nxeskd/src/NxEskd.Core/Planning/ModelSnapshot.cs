namespace NxEskd.Core.Planning;

public sealed record BoundingBoxSnapshot(
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ)
{
    public double SizeX => Math.Max(0, MaxX - MinX);
    public double SizeY => Math.Max(0, MaxY - MinY);
    public double SizeZ => Math.Max(0, MaxZ - MinZ);
    public double MaxPlanarSize => Math.Max(SizeX, SizeY);
}

public sealed record ModelSnapshot(
    string? PartName,
    string? FullPath,
    string? DisplayPartName,
    bool DisplayEqualsWork,
    string Units,
    int BodyCount,
    int ComponentCount,
    IReadOnlyList<string> ModelViews,
    int PmiCount,
    BoundingBoxSnapshot? BoundingBox,
    bool IsAssembly,
    bool IsSheetMetal,
    string NxOpenAssembly,
    IReadOnlyList<string>? DatumPlanes = null)
{
    public static ModelSnapshot Empty { get; } = new(
        null,
        null,
        null,
        true,
        "unknown",
        0,
        0,
        Array.Empty<string>(),
        0,
        null,
        false,
        false,
        string.Empty,
        Array.Empty<string>());

    public IReadOnlyDictionary<string, object?> ToMetrics() => new Dictionary<string, object?>
    {
        ["partName"] = PartName,
        ["fullPath"] = FullPath,
        ["displayPartName"] = DisplayPartName,
        ["displayEqualsWork"] = DisplayEqualsWork,
        ["units"] = Units,
        ["bodyCount"] = BodyCount,
        ["componentCount"] = ComponentCount,
        ["modelViews"] = ModelViews,
        ["pmiCount"] = PmiCount,
        ["boundingBox"] = BoundingBox,
        ["isAssembly"] = IsAssembly,
        ["isSheetMetal"] = IsSheetMetal,
        ["nxOpenAssembly"] = NxOpenAssembly,
        ["datumPlanes"] = DatumPlanes ?? Array.Empty<string>()
    };
}
