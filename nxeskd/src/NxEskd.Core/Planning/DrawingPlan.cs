namespace NxEskd.Core.Planning;

public sealed record DrawingPlan(
    string ProfileId,
    string DocumentKind,
    string Designation,
    string Name,
    IReadOnlyList<SheetPlan> Sheets,
    IReadOnlyDictionary<string, string> ResolvedVariables,
    bool DryRun)
{
    public ModelSnapshot Model { get; init; } = ModelSnapshot.Empty;
    public IReadOnlyList<DrawingOperation> Operations { get; init; } = Array.Empty<DrawingOperation>();
    public DrawingExecutionPolicy ExecutionPolicy { get; init; } = DrawingExecutionPolicy.SafeDefault;
    public DrawingPublicationPlan Publication { get; init; } = DrawingPublicationPlan.SafeDefault;
}

public sealed record DrawingExecutionPolicy(
    bool PreserveManualObjects,
    bool PreserveManualViewPositions,
    bool PreserveManualDimensions,
    bool PreserveManualNotes,
    bool DeleteManagedObjectsMissingFromConfig,
    bool ConfirmManagedDeletion)
{
    public static DrawingExecutionPolicy SafeDefault { get; } = new(
        true,
        true,
        true,
        true,
        false,
        false);
}

public sealed record DrawingPublicationPlan(
    bool SavePart,
    bool UpdateBeforeSave,
    string SaveMode,
    bool NativeDrawingEnabled,
    string? NativeDrawingFile,
    bool AllowOverwriteExisting,
    bool AllowOverwriteReleasedDocument,
    bool PdfEnabled,
    string? PdfFile,
    bool DxfEnabled,
    string? DxfFile)
{
    public static DrawingPublicationPlan SafeDefault { get; } = new(
        true,
        true,
        "save_current_or_save_as",
        false,
        null,
        false,
        false,
        false,
        null,
        false,
        null);
}

public sealed record DrawingOperation(
    string OperationId,
    string TargetId,
    string ObjectKind,
    string ChangeKind,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Preconditions,
    IReadOnlyDictionary<string, object?> Payload);

public sealed record SheetPlan(
    string Id,
    string Role,
    string TemplateId,
    string Format,
    string Orientation,
    string Scale,
    IReadOnlyList<ViewPlan> Views,
    IReadOnlyList<TitleBlockValuePlan> TitleBlockValues);

public sealed record ViewPlan(
    string Id,
    string Type,
    string Name,
    string? ParentViewId,
    string? ProjectionDirection,
    string? ModelView,
    string? FallbackModelView,
    PlacementPlan Placement,
    ScalePlan Scale,
    string HiddenLines,
    bool InheritFlatPatternPmi,
    string? SectionDatumPlane,
    string? SectionDirection)
{
    public ViewPlan(
        string id,
        string type,
        string name,
        string? parentViewId,
        string? modelView,
        string? fallbackModelView,
        PlacementPlan placement,
        ScalePlan scale,
        string hiddenLines,
        bool inheritFlatPatternPmi,
        string? sectionDatumPlane,
        string? sectionDirection)
        : this(id, type, name, parentViewId, null, modelView, fallbackModelView, placement, scale,
            hiddenLines, inheritFlatPatternPmi, sectionDatumPlane, sectionDirection)
    {
    }
}

public sealed record PlacementPlan(string Mode, string? PreferredAnchor, double GapMm, double? X, double? Y);
public sealed record ScalePlan(bool InheritSheet, double? RelativeToMain, string? ExplicitScale);
public sealed record TitleBlockValuePlan(string Label, bool Required, string? SourceKind, string? SourceName, string? JsonPath, string? LiteralValue);
