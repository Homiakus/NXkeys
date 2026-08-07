using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using NxEskd.Core.Planning;

namespace NxEskd.Configurator;

internal sealed class SheetEditorItem : EditorItemBase<JsonObject>
{
    public SheetEditorItem(JsonObject node, Action changed) : base(node, changed) { }

    public string Id => JsonObjectExtensions.ReadString(Node, "id", "SHEET");
    public IReadOnlyList<string> Roles { get; } = ["main", "flat_pattern", "assembly", "notes", "detail"];
    public IReadOnlyList<string> Formats { get; } = ["A0", "A1", "A2", "A3", "A4"];
    public IReadOnlyList<string> Orientations { get; } = ["landscape", "portrait"];
    public IReadOnlyList<string> Scales { get; } = ["auto", "10:1", "5:1", "2:1", "1:1", "1:2", "1:2.5", "1:4", "1:5", "1:10", "1:20", "1:50", "1:100"];

    public string Role { get => JsonObjectExtensions.ReadString(Node, "role", "main"); set => WriteString("role", value); }
    public string TemplateId { get => JsonObjectExtensions.ReadString(Node, "templateId"); set => WriteString("templateId", value); }
    public string Format { get => JsonObjectExtensions.ReadString(Node, "format", "A3"); set => WriteString("format", value); }
    public string Orientation { get => JsonObjectExtensions.ReadString(Node, "orientation", "landscape"); set => WriteString("orientation", value); }
    public string Scale { get => JsonObjectExtensions.ReadString(Node, "scale", "auto"); set => WriteString("scale", value); }

    private void WriteString(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(JsonObjectExtensions.ReadString(Node, name), normalized, StringComparison.Ordinal)) return;
        JsonObjectExtensions.WriteString(Node, name, normalized);
        NotifyAndChange(propertyName);
    }
}

internal sealed class ViewEditorItem : EditorItemBase<JsonObject>
{
    public ViewEditorItem(JsonObject node, IReadOnlyList<string> parentCandidates, Action changed)
        : base(node, changed)
    {
        ParentCandidates = parentCandidates;
    }

    public string Id => JsonObjectExtensions.ReadString(Node, "id", "VIEW");
    public IReadOnlyList<string> Types => DrawingViewKinds.RuntimeSupported;
    public IReadOnlyList<string> ParentCandidates { get; }
    public IReadOnlyList<string> Directions { get; } = ["", "top", "bottom", "left", "right"];
    public IReadOnlyList<string> PlacementModes { get; } = ["auto", "aligned_auto", "fixed"];
    public IReadOnlyList<string> HiddenLineModes { get; } = ["removed", "visible"];
    public IReadOnlyList<string> ScaleValues { get; } = ["inherit_sheet", "10:1", "5:1", "2:1", "1:1", "1:2", "1:2.5", "1:4", "1:5", "1:10", "1:20", "1:50", "1:100"];

    public string Type { get => JsonObjectExtensions.ReadString(Node, "type", DrawingViewKinds.Base); set => WriteString("type", value); }
    public string Name { get => JsonObjectExtensions.ReadString(Node, "name", Id); set => WriteString("name", value); }
    public string ParentViewId { get => JsonObjectExtensions.ReadString(Node, "parentViewId"); set => WriteOptional("parentViewId", value); }
    public string Direction { get => JsonObjectExtensions.ReadString(Node, "direction"); set => WriteOptional("direction", value); }
    public string ModelView { get => JsonObjectExtensions.ReadString(Node, "modelView"); set => WriteOptional("modelView", value); }
    public string HiddenLines { get => JsonObjectExtensions.ReadString(Node, "hiddenLines", "removed"); set => WriteString("hiddenLines", value); }

    public string PlacementMode
    {
        get => Placement["mode"]?.GetValue<string?>() ?? "auto";
        set => WriteNested(Placement, "mode", value);
    }

    public string PreferredAnchor
    {
        get => Placement["preferredAnchor"]?.GetValue<string?>() ?? string.Empty;
        set => WriteNestedOptional(Placement, "preferredAnchor", value);
    }

    public double GapMm
    {
        get => Placement["gapMm"]?.GetValue<double?>() ?? 20.0;
        set => WriteNestedNumber(Placement, "gapMm", value);
    }

    public string Scale
    {
        get
        {
            var scale = ScaleNode;
            if (scale["relativeToMain"]?.GetValue<double?>() is double relative)
                return relative.ToString("0.###", CultureInfo.InvariantCulture) + "x";
            var explicitScale = scale["explicit"]?.GetValue<string?>();
            return string.IsNullOrWhiteSpace(explicitScale) ? "inherit_sheet" : explicitScale;
        }
        set
        {
            var scale = ScaleNode;
            if (value == "inherit_sheet")
            {
                scale["inheritSheet"] = true;
                scale.Remove("explicit");
                scale.Remove("relativeToMain");
            }
            else
            {
                scale["inheritSheet"] = false;
                scale["explicit"] = value;
                scale.Remove("relativeToMain");
            }
            NotifyAndChange(nameof(Scale));
        }
    }

    private JsonObject Placement => JsonObjectExtensions.EnsureObject(Node, "placement");
    private JsonObject ScaleNode => JsonObjectExtensions.EnsureObject(Node, "scale");

    private void WriteString(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(JsonObjectExtensions.ReadString(Node, name), normalized, StringComparison.Ordinal)) return;
        JsonObjectExtensions.WriteString(Node, name, normalized);
        NotifyAndChange(propertyName);
    }

    private void WriteOptional(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) Node.Remove(name);
        else Node[name] = normalized;
        NotifyAndChange(propertyName);
    }

    private void WriteNested(JsonObject owner, string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        owner[name] = value?.Trim() ?? string.Empty;
        NotifyAndChange(propertyName);
    }

    private void WriteNestedOptional(JsonObject owner, string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrWhiteSpace(value)) owner.Remove(name);
        else owner[name] = value.Trim();
        NotifyAndChange(propertyName);
    }

    private void WriteNestedNumber(JsonObject owner, string name, double value, [CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(propertyName);
        owner[name] = value;
        NotifyAndChange(propertyName);
    }
}
