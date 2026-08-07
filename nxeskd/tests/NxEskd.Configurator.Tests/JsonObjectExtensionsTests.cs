using System.Text.Json.Nodes;

namespace NxEskd.Configurator.Tests;

public sealed class JsonObjectExtensionsTests
{
    [Fact]
    public void EnsureObjectCreatesWhenMissing()
    {
        var root = new JsonObject();
        var created = JsonObjectExtensions.EnsureObject(root, "newSection");
        Assert.NotNull(created);
        Assert.Same(created, root["newSection"]?.AsObject());
    }

    [Fact]
    public void EnsureObjectReturnsExistingWhenPresent()
    {
        var root = new JsonObject();
        var existing = new JsonObject();
        root["section"] = existing;
        var result = JsonObjectExtensions.EnsureObject(root, "section");
        Assert.Same(existing, result);
    }

    [Fact]
    public void EnsureObjectCreatesNestedPath()
    {
        var root = new JsonObject();
        var child = JsonObjectExtensions.EnsureObject(root, "parent");
        var grandchild = JsonObjectExtensions.EnsureObject(child, "child");
        Assert.Same(grandchild, root["parent"]?["child"]?.AsObject());
    }

    [Fact]
    public void ReadStringReturnsValueOrDefault()
    {
        var owner = new JsonObject { ["name"] = "hello" };
        Assert.Equal("hello", JsonObjectExtensions.ReadString(owner, "name"));
        Assert.Equal("", JsonObjectExtensions.ReadString(owner, "missing"));
        Assert.Equal("fallback", JsonObjectExtensions.ReadString(owner, "missing", "fallback"));
    }

    [Fact]
    public void ReadBoolReturnsValueOrDefault()
    {
        var owner = new JsonObject { ["flag"] = true };
        Assert.True(JsonObjectExtensions.ReadBool(owner, "flag"));
        Assert.False(JsonObjectExtensions.ReadBool(owner, "missing"));
        Assert.True(JsonObjectExtensions.ReadBool(owner, "missing", true));
    }

    [Fact]
    public void ReadIntReturnsValueOrDefault()
    {
        var owner = new JsonObject { ["count"] = 42 };
        Assert.Equal(42, JsonObjectExtensions.ReadInt(owner, "count"));
        Assert.Equal(0, JsonObjectExtensions.ReadInt(owner, "missing"));
        Assert.Equal(10, JsonObjectExtensions.ReadInt(owner, "missing", 10));
    }

    [Fact]
    public void ReadDoubleReturnsValueOrDefault()
    {
        var owner = new JsonObject { ["gap"] = 3.5 };
        Assert.Equal(3.5, JsonObjectExtensions.ReadDouble(owner, "gap"));
        Assert.Equal(0.0, JsonObjectExtensions.ReadDouble(owner, "missing"));
        Assert.Equal(1.0, JsonObjectExtensions.ReadDouble(owner, "missing", 1.0));
    }

    [Fact]
    public void WriteStringSetsValue()
    {
        var owner = new JsonObject();
        JsonObjectExtensions.WriteString(owner, "key", "  value  ");
        Assert.Equal("value", owner["key"]?.GetValue<string>());
    }

    [Fact]
    public void WriteStringWithNullSetsEmpty()
    {
        var owner = new JsonObject();
        JsonObjectExtensions.WriteString(owner, "key", null);
        Assert.Equal("", owner["key"]?.GetValue<string>());
    }

    [Fact]
    public void WriteBoolSetsValue()
    {
        var owner = new JsonObject();
        JsonObjectExtensions.WriteBool(owner, "flag", true);
        Assert.True(owner["flag"]?.GetValue<bool>());
    }

    [Fact]
    public void WriteIntSetsValue()
    {
        var owner = new JsonObject();
        JsonObjectExtensions.WriteInt(owner, "count", 7);
        Assert.Equal(7, owner["count"]?.GetValue<int>());
    }

    [Fact]
    public void WriteDoubleSetsValue()
    {
        var owner = new JsonObject();
        JsonObjectExtensions.WriteDouble(owner, "gap", 5.0);
        Assert.Equal(5.0, owner["gap"]?.GetValue<double>());
    }

    [Fact]
    public void EditorItemBaseFiresPropertyChangedAndCallback()
    {
        var node = new JsonObject { ["name"] = "test" };
        var callbackCount = 0;
        var item = new TestEditorItem(node, () => callbackCount++);

        var propertyNames = new List<string?>();
        item.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        item.Name = "changed";

        Assert.Single(propertyNames);
        Assert.Equal(nameof(TestEditorItem.Name), propertyNames[0]);
        Assert.Equal(1, callbackCount);
        Assert.Equal("changed", node["name"]?.GetValue<string>());
    }

    [Fact]
    public void EditorItemBaseSkipsNotificationWhenValueUnchanged()
    {
        var node = new JsonObject { ["name"] = "same" };
        var callbackCount = 0;
        var item = new TestEditorItem(node, () => callbackCount++);

        var fired = false;
        item.PropertyChanged += (_, _) => fired = true;

        item.Name = "same";

        Assert.False(fired);
        Assert.Equal(0, callbackCount);
    }

    private sealed class TestEditorItem : EditorItemBase<JsonObject>
    {
        public TestEditorItem(JsonObject node, Action changed) : base(node, changed) { }

        public string Name
        {
            get => JsonObjectExtensions.ReadString(Node, "name");
            set => SetValue("name", value);
        }

        private void SetValue(string key, string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(JsonObjectExtensions.ReadString(Node, key), normalized, StringComparison.Ordinal))
                return;
            JsonObjectExtensions.WriteString(Node, key, normalized);
            NotifyAndChange(nameof(Name));
        }
    }
}
