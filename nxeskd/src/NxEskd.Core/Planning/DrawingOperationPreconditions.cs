namespace NxEskd.Core.Planning;

public static class DrawingOperationPreconditions
{
    public const string SheetMetalCapability = "sheet_metal_capability";
    public const string StationaryFaceResolved = "stationary_face_resolved";
    public const string XDirectionResolved = "x_direction_resolved";
    public const string TemplateAvailable = "template_available";
    public const string FormatSupported = "format_supported";
    public const string RequiredTitleValuesResolved = "required_title_values_resolved";
    public const string SourceModelViewResolved = "source_model_view_resolved";
    public const string ScaleResolved = "scale_resolved";
    public const string PlacementResolved = "placement_resolved";
    public const string PmiCapability = "pmi_capability";
    public const string SourceIdentityResolved = "source_identity_resolved";
    public const string DraftingLicense = "drafting_license";
    public const string AssemblySnapshotAvailable = "assembly_snapshot_available";
    public const string TokensResolved = "tokens_resolved";
    public const string BendDataAvailable = "bend_data_available";
    public const string NxUpdateCompleted = "nx_update_completed";
    public const string NoBlockingDiagnostics = "no_blocking_diagnostics";

    private static readonly HashSet<string> KnownSet = new(StringComparer.OrdinalIgnoreCase)
    {
        SheetMetalCapability,
        StationaryFaceResolved,
        XDirectionResolved,
        TemplateAvailable,
        FormatSupported,
        RequiredTitleValuesResolved,
        SourceModelViewResolved,
        ScaleResolved,
        PlacementResolved,
        PmiCapability,
        SourceIdentityResolved,
        DraftingLicense,
        AssemblySnapshotAvailable,
        TokensResolved,
        BendDataAvailable,
        NxUpdateCompleted,
        NoBlockingDiagnostics
    };

    public static IReadOnlyCollection<string> Known => KnownSet;

    public static bool IsKnown(string? value)
        => !string.IsNullOrWhiteSpace(value) && KnownSet.Contains(value);
}
