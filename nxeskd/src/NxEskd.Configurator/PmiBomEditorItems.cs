using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

internal sealed class PmiViewMappingEditorItem : EditorItemBase<JsonObject>
{
    public PmiViewMappingEditorItem(
        JsonObject node,
        IReadOnlyList<string> modelViewCandidates,
        IReadOnlyList<string> drawingViewCandidates,
        Action changed)
        : base(node, changed)
    {
        ModelViewCandidates = modelViewCandidates;
        DrawingViewCandidates = drawingViewCandidates;
    }

    public IReadOnlyList<string> ModelViewCandidates { get; }
    public IReadOnlyList<string> DrawingViewCandidates { get; }

    public string SourceModelView
    {
        get => JsonObjectExtensions.ReadString(Node, "sourceModelView");
        set => WriteOptional("sourceModelView", value);
    }

    public string FallbackSourceModelView
    {
        get => JsonObjectExtensions.ReadString(Node, "fallbackSourceModelView");
        set => WriteOptional("fallbackSourceModelView", value);
    }

    public string TargetDrawingViewId
    {
        get => JsonObjectExtensions.ReadString(Node, "targetDrawingViewId");
        set => WriteOptional("targetDrawingViewId", value);
    }

    public double OrientationToleranceDeg
    {
        get => JsonObjectExtensions.ReadDouble(Node, "orientationToleranceDeg", 2.0);
        set
        {
            if (!double.IsFinite(value) || value is < 0 or > 180)
                throw new ArgumentOutOfRangeException(nameof(value));
            JsonObjectExtensions.WriteDouble(Node, "orientationToleranceDeg", value);
            NotifyAndChange();
        }
    }

    private void WriteOptional(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) Node.Remove(name);
        else Node[name] = normalized;
        NotifyAndChange(propertyName);
    }
}

internal sealed class BomColumnEditorItem : EditorItemBase<JsonObject>
{
    public BomColumnEditorItem(JsonObject node, Action changed) : base(node, changed) { }

    public IReadOnlyList<string> Sources { get; } =
        ["position", "part_number", "description", "revision", "material", "mass", "quantity", "identity"];

    public string Id { get => JsonObjectExtensions.ReadString(Node, "id"); set => WriteString("id", value); }
    public string Title { get => JsonObjectExtensions.ReadString(Node, "title"); set => WriteString("title", value); }
    public string Source { get => JsonObjectExtensions.ReadString(Node, "source"); set => WriteString("source", value); }

    public double WidthMm
    {
        get => JsonObjectExtensions.ReadDouble(Node, "widthMm", 20.0);
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            JsonObjectExtensions.WriteDouble(Node, "widthMm", value);
            NotifyAndChange();
        }
    }

    private void WriteString(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteString(Node, name, value);
        NotifyAndChange(propertyName);
    }
}

internal sealed class PmiBomSettingsModel : INotifyPropertyChanged
{
    private readonly JsonObject _root;
    private readonly Action _changed;

    public PmiBomSettingsModel(JsonObject root, Action changed)
    {
        _root = root;
        _changed = changed;
    }

    public IReadOnlyList<string> OrphanPolicies { get; } =
        ["warning_and_place_on_best_view", "warning", "error", "ignore"];
    public IReadOnlyList<string> LostAssociationPolicies { get; } = ["error", "warning", "manual_review"];
    public IReadOnlyList<string> BomModes { get; } = ["structured", "flat"];
    public IReadOnlyList<string> LevelModes { get; } = ["top_level", "all_levels"];
    public IReadOnlyList<string> QuantityModes { get; } = ["rolled_up", "per_occurrence"];
    public IReadOnlyList<string> BalloonShapes { get; } = ["circle", "split_circle", "hexagon", "rectangle"];

    public bool PmiEnabled { get => JsonObjectExtensions.ReadBool(Pmi, "enabled", true); set => WriteBool(Pmi, "enabled", value); }
    public string OrphanPmiPolicy { get => JsonObjectExtensions.ReadString(SourceSelection, "orphanPmiPolicy", "warning_and_place_on_best_view"); set => Write(SourceSelection, "orphanPmiPolicy", value); }
    public bool AssociativityRequired { get => JsonObjectExtensions.ReadBool(Associativity, "required", true); set => WriteBool(Associativity, "required", value); }
    public bool KeepSourceLink { get => JsonObjectExtensions.ReadBool(Associativity, "keepSourceLink", true); set => WriteBool(Associativity, "keepSourceLink", value); }
    public bool UpdateWhenModelChanges { get => JsonObjectExtensions.ReadBool(Associativity, "updateWhenModelChanges", true); set => WriteBool(Associativity, "updateWhenModelChanges", value); }
    public string OnLostAssociation { get => JsonObjectExtensions.ReadString(Associativity, "onLostAssociation", "error"); set => Write(Associativity, "onLostAssociation", value); }

    public bool PartsListEnabled { get => JsonObjectExtensions.ReadBool(PartsList, "enabled", true); set => WriteBool(PartsList, "enabled", value); }
    public string BomMode { get => JsonObjectExtensions.ReadString(PartsList, "bomMode", "structured"); set => Write(PartsList, "bomMode", value); }
    public string LevelMode { get => JsonObjectExtensions.ReadString(PartsList, "levelMode", "top_level"); set => Write(PartsList, "levelMode", value); }
    public string QuantityMode { get => JsonObjectExtensions.ReadString(PartsList, "quantityMode", "rolled_up"); set => Write(PartsList, "quantityMode", value); }
    public bool MergeIdentical { get => JsonObjectExtensions.ReadBool(MergeIdenticalNode, "enabled", true); set => WriteBool(MergeIdenticalNode, "enabled", value); }
    public bool PreserveExistingPositions { get => JsonObjectExtensions.ReadBool(PositionNumbering, "preserveExisting", true); set => WriteBool(PositionNumbering, "preserveExisting", value); }
    public int PositionStart { get => JsonObjectExtensions.ReadInt(PositionNumbering, "start", 1); set => WriteInt(PositionNumbering, "start", value, 1); }
    public int PositionIncrement { get => JsonObjectExtensions.ReadInt(PositionNumbering, "increment", 1); set => WriteInt(PositionNumbering, "increment", value, 1); }

    public bool BalloonsEnabled { get => JsonObjectExtensions.ReadBool(Balloons, "enabled", true); set => WriteBool(Balloons, "enabled", value); }
    public string BalloonShape { get => JsonObjectExtensions.ReadString(Balloons, "shape", "circle"); set => Write(Balloons, "shape", value); }
    public bool GroupIdenticalComponents { get => JsonObjectExtensions.ReadBool(Balloons, "groupIdenticalComponents", true); set => WriteBool(Balloons, "groupIdenticalComponents", value); }
    public bool AvoidCrossingLeaders { get => JsonObjectExtensions.ReadBool(Balloons, "avoidCrossingLeaders", true); set => WriteBool(Balloons, "avoidCrossingLeaders", value); }
    public bool AvoidGeometry { get => JsonObjectExtensions.ReadBool(Balloons, "avoidGeometry", true); set => WriteBool(Balloons, "avoidGeometry", value); }
    public double MinimumBalloonGapMm { get => JsonObjectExtensions.ReadDouble(Balloons, "minimumGapMm", 5.0); set => WriteDouble(Balloons, "minimumGapMm", value, 0); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly string[] RefreshProperties =
    [
        nameof(PmiEnabled),
        nameof(OrphanPmiPolicy),
        nameof(AssociativityRequired),
        nameof(KeepSourceLink),
        nameof(UpdateWhenModelChanges),
        nameof(OnLostAssociation),
        nameof(PartsListEnabled),
        nameof(BomMode),
        nameof(LevelMode),
        nameof(QuantityMode),
        nameof(MergeIdentical),
        nameof(PreserveExistingPositions),
        nameof(PositionStart),
        nameof(PositionIncrement),
        nameof(BalloonsEnabled),
        nameof(BalloonShape),
        nameof(GroupIdenticalComponents),
        nameof(AvoidCrossingLeaders),
        nameof(AvoidGeometry),
        nameof(MinimumBalloonGapMm),
    ];

    public void Refresh()
    {
        foreach (var propertyName in RefreshProperties)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private JsonObject Pmi => JsonObjectExtensions.EnsureObject(_root, "pmiInheritance");
    private JsonObject SourceSelection => JsonObjectExtensions.EnsureObject(Pmi, "sourceSelection");
    private JsonObject Associativity => JsonObjectExtensions.EnsureObject(Pmi, "associativity");
    private JsonObject PartsListAndBalloons => JsonObjectExtensions.EnsureObject(_root, "partsListAndBalloons");
    private JsonObject PartsList => JsonObjectExtensions.EnsureObject(PartsListAndBalloons, "partsList");
    private JsonObject MergeIdenticalNode => JsonObjectExtensions.EnsureObject(PartsList, "mergeIdentical");
    private JsonObject PositionNumbering => JsonObjectExtensions.EnsureObject(PartsList, "positionNumbering");
    private JsonObject Balloons => JsonObjectExtensions.EnsureObject(PartsListAndBalloons, "balloons");

    private void Write(JsonObject owner, string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteString(owner, name, value);
        Changed(propertyName);
    }

    private void WriteBool(JsonObject owner, string name, bool value, [CallerMemberName] string? propertyName = null)
    {
        JsonObjectExtensions.WriteBool(owner, name, value);
        Changed(propertyName);
    }

    private void WriteInt(JsonObject owner, string name, int value, int minimum, [CallerMemberName] string? propertyName = null)
    {
        if (value < minimum) throw new ArgumentOutOfRangeException(propertyName);
        JsonObjectExtensions.WriteInt(owner, name, value);
        Changed(propertyName);
    }

    private void WriteDouble(JsonObject owner, string name, double value, double minimum, [CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value) || value < minimum) throw new ArgumentOutOfRangeException(propertyName);
        JsonObjectExtensions.WriteDouble(owner, name, value);
        Changed(propertyName);
    }

    private void Changed(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}
