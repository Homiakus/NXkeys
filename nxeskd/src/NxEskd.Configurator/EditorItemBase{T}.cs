using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

public abstract class EditorItemBase<T> : EditorItemBase where T : JsonNode
{
    protected EditorItemBase(T node, Action changed) : base(changed)
    {
        Node = node;
    }

    public T Node { get; }
}
