using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

internal sealed class TechnicalRequirementEditorItem : EditorItemBase<JsonObject>
{
    public TechnicalRequirementEditorItem(JsonObject node, IReadOnlyList<string> groups, Action changed)
        : base(node, changed)
    {
        Groups = groups;
    }

    public IReadOnlyList<string> Groups { get; }

    public string Group
    {
        get => JsonObjectExtensions.ReadString(Node, "group", "references");
        set => WriteString("group", value);
    }

    public string Text
    {
        get => JsonObjectExtensions.ReadString(Node, "text");
        set => WriteString("text", value);
    }

    private void WriteString(string name, string? value, [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(JsonObjectExtensions.ReadString(Node, name), normalized,
                StringComparison.Ordinal)) return;
        JsonObjectExtensions.WriteString(Node, name, normalized);
        NotifyAndChange(propertyName);
    }
}
