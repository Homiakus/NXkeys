using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

internal sealed class DraftingStandardsModel : INotifyPropertyChanged
{
    private readonly JsonObject _root;
    private readonly Action _changed;

    public DraftingStandardsModel(JsonObject root, Action changed)
    {
        _root = root;
        _changed = changed;
    }

    public IReadOnlyList<string> ProjectionMethods { get; } = ["first_angle", "third_angle"];

    public string ProjectionMethod
    {
        get => JsonObjectExtensions.ReadString(TargetEnvironment, "projectionMethod", "first_angle");
        set => Write(TargetEnvironment, "projectionMethod", value);
    }

    public double ArrowLengthMm
    {
        get => JsonObjectExtensions.ReadDouble(Dimensions, "arrowLengthMm", 3.5);
        set => WriteDouble(Dimensions, "arrowLengthMm", value, 0.1);
    }

    public double FirstDimensionOffsetMm
    {
        get => JsonObjectExtensions.ReadDouble(Dimensions, "firstDimensionOffsetMm", 10.0);
        set => WriteDouble(Dimensions, "firstDimensionOffsetMm", value, 0);
    }

    public double ParallelDimensionGapMm
    {
        get => JsonObjectExtensions.ReadDouble(Dimensions, "parallelDimensionGapMm", 7.0);
        set => WriteDouble(Dimensions, "parallelDimensionGapMm", value, 0.1);
    }

    public double HatchingAngleDeg
    {
        get => JsonObjectExtensions.ReadDouble(Hatching, "defaultAngleDeg", 45.0);
        set => WriteDouble(Hatching, "defaultAngleDeg", value, -360);
    }

    public double HatchingSpacingMm
    {
        get => JsonObjectExtensions.ReadDouble(Hatching, "spacingMm", 3.0);
        set => WriteDouble(Hatching, "spacingMm", value, 0.1);
    }

    public double MinimumGapMm
    {
        get => JsonObjectExtensions.ReadDouble(LayoutSolver, "minimumGapMm", 10.0);
        set => WriteDouble(LayoutSolver, "minimumGapMm", value, 0);
    }

    public int MaxLayoutIterations
    {
        get => JsonObjectExtensions.ReadInt(LayoutSolver, "maxIterations", 500);
        set => WriteInt(LayoutSolver, "maxIterations", value, 1);
    }

    public string AllowedScales
    {
        get => string.Join(", ", AllowedScaleNodes
            .Select(node => node?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        set
        {
            var parsed = value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (parsed.Length == 0)
                throw new InvalidOperationException("Должен остаться хотя бы один допустимый масштаб.");
            AllowedScaleNodes.Clear();
            foreach (var item in parsed) AllowedScaleNodes.Add(item);
            Changed();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly string[] RefreshProperties =
    [
        nameof(ProjectionMethod),
        nameof(ArrowLengthMm),
        nameof(FirstDimensionOffsetMm),
        nameof(ParallelDimensionGapMm),
        nameof(HatchingAngleDeg),
        nameof(HatchingSpacingMm),
        nameof(MinimumGapMm),
        nameof(MaxLayoutIterations),
        nameof(AllowedScales),
    ];

    public void Refresh()
    {
        foreach (var propertyName in RefreshProperties)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private JsonObject TargetEnvironment => JsonObjectExtensions.EnsureObject(_root, "targetEnvironment");
    private JsonObject DraftingStyles => JsonObjectExtensions.EnsureObject(_root, "draftingStyles");
    private JsonObject Dimensions => JsonObjectExtensions.EnsureObject(DraftingStyles, "dimensions");
    private JsonObject Hatching => JsonObjectExtensions.EnsureObject(DraftingStyles, "hatching");
    private JsonObject LayoutSolver => JsonObjectExtensions.EnsureObject(_root, "layoutSolver");
    private JsonObject ScalePolicy => JsonObjectExtensions.EnsureObject(_root, "scalePolicy");

    private JsonArray AllowedScaleNodes
    {
        get
        {
            if (ScalePolicy["allowed"] is JsonArray existing) return existing;
            var created = new JsonArray("1:1", "1:2", "1:5", "1:10");
            ScalePolicy["allowed"] = created;
            return created;
        }
    }

    private void Write(JsonObject owner, string name, string? value,
        [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteString(owner, name, value);
        Changed(propertyName);
    }

    private void WriteDouble(JsonObject owner, string name, double value, double minimum,
        [CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value) || value < minimum)
            throw new ArgumentOutOfRangeException(propertyName);
        JsonObjectExtensions.WriteDouble(owner, name, value);
        Changed(propertyName);
    }

    private void WriteInt(JsonObject owner, string name, int value, int minimum,
        [CallerMemberName] string? propertyName = null)
    {
        if (value < minimum) throw new ArgumentOutOfRangeException(propertyName);
        JsonObjectExtensions.WriteInt(owner, name, value);
        Changed(propertyName);
    }

    private void Changed([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}

internal sealed class TextStyleEditorItem : EditorItemBase<JsonObject>
{
    public TextStyleEditorItem(JsonObject node, Action changed) : base(node, changed) { }

    public string Id { get => JsonObjectExtensions.ReadString(Node, "id"); set => WriteString("id", value); }
    public string Font { get => JsonObjectExtensions.ReadString(Node, "font", "Arial"); set => WriteString("font", value); }
    public double HeightMm { get => JsonObjectExtensions.ReadDouble(Node, "heightMm", 3.5); set => WriteDouble("heightMm", value, 0.1); }
    public double WidthFactor { get => JsonObjectExtensions.ReadDouble(Node, "widthFactor", 1.0); set => WriteDouble("widthFactor", value, 0.1); }
    public bool Bold { get => JsonObjectExtensions.ReadBool(Node, "bold"); set => WriteBool("bold", value); }
    public bool Italic { get => JsonObjectExtensions.ReadBool(Node, "italic"); set => WriteBool("italic", value); }

    private void WriteString(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteString(Node, name, value);
        NotifyAndChange(propertyName);
    }

    private void WriteDouble(string name, double value, double minimum,
        [CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value) || value < minimum) throw new ArgumentOutOfRangeException(propertyName);
        JsonObjectExtensions.WriteDouble(Node, name, value);
        NotifyAndChange(propertyName);
    }

    private void WriteBool(string name, bool value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteBool(Node, name, value);
        NotifyAndChange(propertyName);
    }
}

internal sealed class TemplateEditorItem : EditorItemBase<JsonObject>
{
    public TemplateEditorItem(JsonObject node, Action changed) : base(node, changed) { }

    public IReadOnlyList<string> Formats { get; } = ["A0", "A1", "A2", "A3", "A4"];
    public IReadOnlyList<string> Orientations { get; } = ["landscape", "portrait"];

    public string Id { get => JsonObjectExtensions.ReadString(Node, "id"); set => WriteString("id", value); }
    public string File { get => JsonObjectExtensions.ReadString(Node, "file"); set => WriteString("file", value); }
    public string Format { get => JsonObjectExtensions.ReadString(Node, "format", "A3"); set => WriteString("format", value); }
    public string Orientation { get => JsonObjectExtensions.ReadString(Node, "orientation", "landscape"); set => WriteString("orientation", value); }
    public string BorderObjectName { get => JsonObjectExtensions.ReadString(Node, "borderObjectName"); set => WriteOptional("borderObjectName", value); }
    public string TitleBlockObjectName { get => JsonObjectExtensions.ReadString(Node, "titleBlockObjectName"); set => WriteOptional("titleBlockObjectName", value); }
    public int Priority { get => JsonObjectExtensions.ReadInt(Node, "priority"); set => WriteInt("priority", value); }

    private void WriteString(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteString(Node, name, value);
        NotifyAndChange(propertyName);
    }

    private void WriteOptional(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrWhiteSpace(value)) Node.Remove(name);
        else Node[name] = value.Trim();
        NotifyAndChange(propertyName);
    }

    private void WriteInt(string name, int value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteInt(Node, name, value);
        NotifyAndChange(propertyName);
    }
}
