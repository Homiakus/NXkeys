using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

internal sealed class DocumentSettingsModel : INotifyPropertyChanged
{
    private readonly JsonObject _root;
    private readonly Action _changed;

    public DocumentSettingsModel(JsonObject root, Action changed)
    {
        _root = root;
        _changed = changed;
    }

    public IReadOnlyList<string> DocumentKinds { get; } =
        ["part_drawing", "assembly_drawing", "sheet_metal_drawing", "installation_drawing", "overall_drawing"];
    public IReadOnlyList<string> SaveModes { get; } =
        ["save_current", "save_as", "save_current_or_save_as"];

    public IReadOnlyList<string> TitleBlockDefinitions
        => (TitleBlocks["definitions"] as JsonArray)?.OfType<JsonObject>()
               .Select(item => item["id"]?.GetValue<string?>())
               .Where(value => !string.IsNullOrWhiteSpace(value))
               .Cast<string>()
               .ToArray() ?? [];

    public string DocumentKind { get => JsonObjectExtensions.ReadString(Job, "documentKind", "part_drawing"); set => Write(Job, "documentKind", value); }
    public string Designation { get => JsonObjectExtensions.ReadString(Document, "designation", string.Empty); set => Write(Document, "designation", value); }
    public string Name { get => JsonObjectExtensions.ReadString(Document, "name", string.Empty); set => Write(Document, "name", value); }
    public string Revision { get => JsonObjectExtensions.ReadString(Document, "revision", string.Empty); set => Write(Document, "revision", value); }
    public string Organization { get => JsonObjectExtensions.ReadString(Document, "organization", string.Empty); set => Write(Document, "organization", value); }
    public string Material { get => JsonObjectExtensions.ReadString(Document, "material", string.Empty); set => Write(Document, "material", value); }
    public string Status { get => JsonObjectExtensions.ReadString(Document, "status", string.Empty); set => Write(Document, "status", value); }

    public string DevelopedBy { get => JsonObjectExtensions.ReadString(Approvals, "developedBy", string.Empty); set => Write(Approvals, "developedBy", value); }
    public string CheckedBy { get => JsonObjectExtensions.ReadString(Approvals, "checkedBy", string.Empty); set => Write(Approvals, "checkedBy", value); }
    public string NormControlBy { get => JsonObjectExtensions.ReadString(Approvals, "normControlBy", string.Empty); set => Write(Approvals, "normControlBy", value); }
    public string ApprovedBy { get => JsonObjectExtensions.ReadString(Approvals, "approvedBy", string.Empty); set => Write(Approvals, "approvedBy", value); }

    public string ActiveTitleBlock
    {
        get => JsonObjectExtensions.ReadString(TitleBlocks, "activeDefinition", string.Empty);
        set => Write(TitleBlocks, "activeDefinition", value);
    }

    public bool DryRun { get => JsonObjectExtensions.ReadBool(Execution, "dryRun", false); set => WriteBool(Execution, "dryRun", value); }
    public bool PreserveManualViewPositions { get => JsonObjectExtensions.ReadBool(Execution, "preserveManualViewPositions", true); set => WriteBool(Execution, "preserveManualViewPositions", value); }
    public bool PreserveManualDimensions { get => JsonObjectExtensions.ReadBool(Execution, "preserveManualDimensions", true); set => WriteBool(Execution, "preserveManualDimensions", value); }
    public bool PreserveManualNotes { get => JsonObjectExtensions.ReadBool(Execution, "preserveManualNotes", true); set => WriteBool(Execution, "preserveManualNotes", value); }
    public bool DeleteManagedObjectsMissingFromConfig { get => JsonObjectExtensions.ReadBool(Idempotency, "deleteManagedObjectsMissingFromConfig", false); set => WriteBool(Idempotency, "deleteManagedObjectsMissingFromConfig", value); }
    public bool ConfirmManagedDeletion { get => JsonObjectExtensions.ReadBool(Idempotency, "confirmManagedDeletion", false); set => WriteBool(Idempotency, "confirmManagedDeletion", value); }

    public bool SavePart { get => JsonObjectExtensions.ReadBool(Output, "savePart", true); set => WriteBool(Output, "savePart", value); }
    public string SaveMode { get => JsonObjectExtensions.ReadString(Output, "saveMode", "save_current_or_save_as"); set => Write(Output, "saveMode", value); }
    public bool NativeDrawingEnabled { get => JsonObjectExtensions.ReadBool(NativeDrawing, "enabled", false); set => WriteBool(NativeDrawing, "enabled", value); }
    public string NativeDrawingFile { get => JsonObjectExtensions.ReadString(NativeDrawing, "file", string.Empty); set => Write(NativeDrawing, "file", value); }
    public bool AllowOverwriteExisting { get => JsonObjectExtensions.ReadBool(Output, "allowOverwriteExisting", false); set => WriteBool(Output, "allowOverwriteExisting", value); }
    public bool AllowOverwriteReleasedDocument { get => JsonObjectExtensions.ReadBool(Output, "allowOverwriteReleasedDocument", false); set => WriteBool(Output, "allowOverwriteReleasedDocument", value); }
    public bool PdfEnabled { get => JsonObjectExtensions.ReadBool(Pdf, "enabled", true); set => WriteBool(Pdf, "enabled", value); }
    public string PdfFile { get => JsonObjectExtensions.ReadString(Pdf, "file", string.Empty); set => Write(Pdf, "file", value); }
    public bool DxfEnabled { get => JsonObjectExtensions.ReadBool(Dxf, "enabled", false); set => WriteBool(Dxf, "enabled", value); }
    public string DxfFile { get => JsonObjectExtensions.ReadString(Dxf, "file", string.Empty); set => Write(Dxf, "file", value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly string[] RefreshProperties =
    [
        nameof(DocumentKind),
        nameof(Designation),
        nameof(Name),
        nameof(Revision),
        nameof(Organization),
        nameof(Material),
        nameof(Status),
        nameof(DevelopedBy),
        nameof(CheckedBy),
        nameof(NormControlBy),
        nameof(ApprovedBy),
        nameof(ActiveTitleBlock),
        nameof(DryRun),
        nameof(PreserveManualViewPositions),
        nameof(PreserveManualDimensions),
        nameof(PreserveManualNotes),
        nameof(DeleteManagedObjectsMissingFromConfig),
        nameof(ConfirmManagedDeletion),
        nameof(SavePart),
        nameof(SaveMode),
        nameof(NativeDrawingEnabled),
        nameof(NativeDrawingFile),
        nameof(AllowOverwriteExisting),
        nameof(AllowOverwriteReleasedDocument),
        nameof(PdfEnabled),
        nameof(PdfFile),
        nameof(DxfEnabled),
        nameof(DxfFile),
        nameof(TitleBlockDefinitions),
    ];

    public void Refresh()
    {
        foreach (var propertyName in RefreshProperties)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private JsonObject Job => JsonObjectExtensions.EnsureObject(_root, "job");
    private JsonObject Document => JsonObjectExtensions.EnsureObject(Job, "document");
    private JsonObject Approvals => JsonObjectExtensions.EnsureObject(Job, "approvals");
    private JsonObject TitleBlocks => JsonObjectExtensions.EnsureObject(_root, "titleBlocks");
    private JsonObject Execution => JsonObjectExtensions.EnsureObject(_root, "execution");
    private JsonObject Idempotency => JsonObjectExtensions.EnsureObject(Execution, "idempotency");
    private JsonObject Output => JsonObjectExtensions.EnsureObject(_root, "output");
    private JsonObject NativeDrawing => JsonObjectExtensions.EnsureObject(Output, "nativeDrawing");
    private JsonObject Pdf => JsonObjectExtensions.EnsureObject(Output, "pdf");
    private JsonObject SheetMetal => JsonObjectExtensions.EnsureObject(_root, "sheetMetalFlatPattern");
    private JsonObject Dxf => JsonObjectExtensions.EnsureObject(SheetMetal, "dxfExport");

    private void Write(JsonObject owner, string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(JsonObjectExtensions.ReadString(owner, name, string.Empty), normalized, StringComparison.Ordinal)) return;
        JsonObjectExtensions.WriteString(owner, name, normalized);
        Changed(propertyName);
    }

    private void WriteBool(JsonObject owner, string name, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (JsonObjectExtensions.ReadBool(owner, name, !value) == value) return;
        JsonObjectExtensions.WriteBool(owner, name, value);
        Changed(propertyName);
    }

    private void Changed(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}
