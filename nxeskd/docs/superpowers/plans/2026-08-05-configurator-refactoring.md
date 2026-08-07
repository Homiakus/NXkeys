# Configurator Refactoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate duplicated JsonObject helpers, editor item boilerplate, reflection-based Refresh(), and duplicated tests in NxEskd.Configurator without changing behavior.

**Architecture:** Extract shared utility classes (`JsonObjectExtensions`, `EditorItemBase`) into the Configurator project, replace reflection-based model Refresh with explicit property lists, split MainWindow concerns into focused partials, and deduplicate test code into shared fixtures.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, System.Text.Json

## Global Constraints

- NxEskd.Configurator must not reference NX Open DLLs (build isolation rule from `docs/ARCHITECTURE_AND_CONFIGURATOR.md` §11)
- No reflection for UI operations (rule from `docs/ARCHITECTURE_AND_CONFIGURATOR.md` §10.3)
- All existing tests must stay green
- No behavior changes — this is a pure refactor
- Follow existing naming: Russian UI strings, English identifiers

---

## File Structure

```
src/NxEskd.Configurator/
├── JsonObjectExtensions.cs          (NEW)     — EnsureObject, Read/Write scalar helpers
├── EditorItemBase.cs                (NEW)     — base class with _changed + OnPropertyChanged
├── EditorItemBase{T}.cs             (NEW)     — typed variant with Node property
├── MainWindow.xaml.cs               (MODIFY)  — slim down, delegate to partials
├── MainWindow.Commands.cs           (NEW)     — NX command dispatch
├── MainWindow.Profile.cs            (NEW)     — profile load/save/validate pipeline
├── MainWindow.Sections.cs           (NEW)     — section tree, settings grid, filtering
├── MainWindow.DraftingStandards.cs  (no change, already split)
├── MainWindow.DraftingStandardsRefresh.cs (no change)
├── MainWindow.RiskyOperations.cs    (DELETE)  — merged into Commands.cs
├── ProfileEditorDocument.cs         (MODIFY)  — use JsonObjectExtensions
├── EditableSetting.cs               (MODIFY)  — use JsonObjectExtensions
├── DraftingStandardsControl.xaml.cs (MODIFY)  — use JsonObjectExtensions
├── DraftingStandardsModels.cs       (MODIFY)  — use JsonObjectExtensions, EditorItemBase, replace reflection
├── DocumentSettingsModel.cs         (MODIFY)  — use JsonObjectExtensions, replace reflection
├── DocumentSettingsControl.xaml.cs  (no change needed)
├── DrawingStructureControl.xaml.cs  (MODIFY)  — use JsonObjectExtensions
├── DrawingStructureItems.cs         (MODIFY)  — use EditorItemBase
├── PmiBomSettingsControl.xaml.cs    (MODIFY)  — use JsonObjectExtensions
├── PmiBomEditorItems.cs             (MODIFY)  — use JsonObjectExtensions, EditorItemBase, replace reflection
├── TechnicalRequirementsControl.xaml.cs (MODIFY) — use JsonObjectExtensions
└── TechnicalRequirementEditorItem.cs (MODIFY) — use EditorItemBase

tests/NxEskd.Configurator.Tests/
├── TestRepositoryLocator.cs         (NEW)     — shared LocateRepositoryRoot
├── ProfileFixture.cs                (NEW)     — extracted from ConfiguratorContractTests
├── JsonObjectExtensionsTests.cs     (NEW)     — unit tests for extracted helpers
├── ConfiguratorContractTests.cs     (MODIFY)  — use TestRepositoryLocator + ProfileFixture
├── XamlContractTests.cs             (MODIFY)  — use TestRepositoryLocator
├── DraftingStandardsContractTests.cs (MODIFY) — add Refresh regression test
└── DraftingStandardsBehaviorTests.cs (DELETE) — content fully duplicated in ContractTests
```

---

### Task 1: Extract `JsonObjectExtensions` utility class

**Files:**
- Create: `src/NxEskd.Configurator/JsonObjectExtensions.cs`
- Test: `tests/NxEskd.Configurator.Tests/JsonObjectExtensionsTests.cs`

**Interfaces:**
- Produces: `JsonObjectExtensions.EnsureObject(JsonObject owner, string name) → JsonObject`
- Produces: `JsonObjectExtensions.ReadString(JsonObject owner, string name, string fallback = "") → string`
- Produces: `JsonObjectExtensions.ReadBool(JsonObject owner, string name, bool fallback = false) → bool`
- Produces: `JsonObjectExtensions.ReadInt(JsonObject owner, string name, int fallback = 0) → int`
- Produces: `JsonObjectExtensions.ReadDouble(JsonObject owner, string name, double fallback = 0.0) → double`
- Produces: `JsonObjectExtensions.WriteString(JsonObject owner, string name, string? value) → void`
- Produces: `JsonObjectExtensions.WriteBool(JsonObject owner, string name, bool value) → void`
- Produces: `JsonObjectExtensions.WriteInt(JsonObject owner, string name, int value) → void`
- Produces: `JsonObjectExtensions.WriteDouble(JsonObject owner, string name, double value) → void`

- [ ] **Step 1: Write the failing test**

```csharp
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
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~JsonObjectExtensionsTests"`
Expected: FAIL — "JsonObjectExtensions does not exist"

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Globalization;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

internal static class JsonObjectExtensions
{
    public static JsonObject EnsureObject(JsonObject owner, string name)
    {
        if (owner[name] is JsonObject existing) return existing;
        var created = new JsonObject();
        owner[name] = created;
        return created;
    }

    public static string ReadString(JsonObject owner, string name, string fallback = "")
        => owner[name]?.GetValue<string?>() ?? fallback;

    public static bool ReadBool(JsonObject owner, string name, bool fallback = false)
        => owner[name]?.GetValue<bool?>() ?? fallback;

    public static int ReadInt(JsonObject owner, string name, int fallback = 0)
        => owner[name]?.GetValue<int?>() ?? fallback;

    public static double ReadDouble(JsonObject owner, string name, double fallback = 0.0)
        => owner[name]?.GetValue<double?>() ?? fallback;

    public static void WriteString(JsonObject owner, string name, string? value)
    {
        owner[name] = value?.Trim() ?? string.Empty;
    }

    public static void WriteBool(JsonObject owner, string name, bool value)
    {
        owner[name] = value;
    }

    public static void WriteInt(JsonObject owner, string name, int value)
    {
        owner[name] = value;
    }

    public static void WriteDouble(JsonObject owner, string name, double value)
    {
        owner[name] = value;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~JsonObjectExtensionsTests"`
Expected: 12 PASS

- [ ] **Step 5: Verify all existing tests still pass**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all existing tests PASS

- [ ] **Step 6: Commit**

```bash
git add src/NxEskd.Configurator/JsonObjectExtensions.cs tests/NxEskd.Configurator.Tests/JsonObjectExtensionsTests.cs
git commit -m "refactor(configurator): extract JsonObjectExtensions utility

Consolidates EnsureObject, Read*, and Write* helpers duplicated across
DocumentSettingsModel, DraftingStandardsModels, PmiBomEditorItems,
DrawingStructureItems, and multiple UserControls into one class.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Create `EditorItemBase` to eliminate duplicated INotifyPropertyChanged boilerplate

**Files:**
- Create: `src/NxEskd.Configurator/EditorItemBase.cs`
- Create: `src/NxEskd.Configurator/EditorItemBase{T}.cs`

**Interfaces:**
- Consumes: `JsonObjectExtensions` from Task 1
- Produces: `abstract class EditorItemBase : INotifyPropertyChanged` with `NotifyAndChange(string? propertyName)`
- Produces: `abstract class EditorItemBase<T> : EditorItemBase` with `T Node { get; }` constructor taking `(T node, Action changed)` where T : JsonObject

- [ ] **Step 1: Write the failing test**

Add to `JsonObjectExtensionsTests.cs`:

```csharp
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
        NotifyAndChange();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~EditorItemBase"`
Expected: FAIL — EditorItemBase does not exist

- [ ] **Step 3: Write minimal implementation**

```csharp
// EditorItemBase.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NxEskd.Configurator;

public abstract class EditorItemBase : INotifyPropertyChanged
{
    private readonly Action _changed;

    protected EditorItemBase(Action changed)
    {
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyAndChange([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}
```

```csharp
// EditorItemBase{T}.cs
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

public abstract class EditorItemBase<T> : EditorItemBase where T : JsonObject
{
    protected EditorItemBase(T node, Action changed) : base(changed)
    {
        Node = node;
    }

    public T Node { get; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~EditorItemBase"`
Expected: 2 PASS

- [ ] **Step 5: Verify all existing tests still pass**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all existing tests PASS

- [ ] **Step 6: Commit**

```bash
git add src/NxEskd.Configurator/EditorItemBase.cs src/NxEskd.Configurator/EditorItemBase{T}.cs tests/NxEskd.Configurator.Tests/JsonObjectExtensionsTests.cs
git commit -m "refactor(configurator): add EditorItemBase to eliminate INotifyPropertyChanged boilerplate

Provides a common base for all editor item view models:
- EditorItemBase: shared _changed callback + NotifyAndChange()
- EditorItemBase<T>: typed Node accessor for JsonObject-based items

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Migrate editor item classes to use `JsonObjectExtensions` and `EditorItemBase`

**Files:**
- Modify: `src/NxEskd.Configurator/DrawingStructureItems.cs` (SheetEditorItem, ViewEditorItem)
- Modify: `src/NxEskd.Configurator/DraftingStandardsModels.cs` (TextStyleEditorItem, TemplateEditorItem)
- Modify: `src/NxEskd.Configurator/TechnicalRequirementEditorItem.cs`
- Modify: `src/NxEskd.Configurator/PmiBomEditorItems.cs` (PmiViewMappingEditorItem, BomColumnEditorItem)

**Interfaces:**
- Consumes: `JsonObjectExtensions` from Task 1, `EditorItemBase`/`EditorItemBase<T>` from Task 2
- Produces: Same public API — no interface changes, all existing tests must pass

- [ ] **Step 1: Migrate SheetEditorItem to use EditorItemBase<JsonObject>**

Replace `SheetEditorItem` in `DrawingStructureItems.cs`:

```csharp
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
```

- [ ] **Step 2: Migrate ViewEditorItem**

Replace `ViewEditorItem` in `DrawingStructureItems.cs` with equivalent using `EditorItemBase<JsonObject>` and `JsonObjectExtensions`. Preserve all nested object access (Placement, ScaleNode) unchanged.

- [ ] **Step 3: Migrate TextStyleEditorItem and TemplateEditorItem**

In `DraftingStandardsModels.cs`, convert `TextStyleEditorItem` and `TemplateEditorItem` to extend `EditorItemBase<JsonObject>`. Replace inline Read/Write/ReadBool/WriteBool/ReadDouble/WriteDouble with `JsonObjectExtensions` calls.

- [ ] **Step 4: Migrate TechnicalRequirementEditorItem**

Convert to extend `EditorItemBase<JsonObject>`. Replace inline Read/Write with `JsonObjectExtensions`.

- [ ] **Step 5: Migrate PmiViewMappingEditorItem and BomColumnEditorItem**

In `PmiBomEditorItems.cs`, convert both to extend `EditorItemBase<JsonObject>`. Replace inline helpers with `JsonObjectExtensions`.

- [ ] **Step 6: Run all tests**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all existing tests PASS (they test behavior, not implementation)

- [ ] **Step 7: Commit**

```bash
git add src/NxEskd.Configurator/DrawingStructureItems.cs src/NxEskd.Configurator/DraftingStandardsModels.cs src/NxEskd.Configurator/TechnicalRequirementEditorItem.cs src/NxEskd.Configurator/PmiBomEditorItems.cs
git commit -m "refactor(configurator): migrate editor items to JsonObjectExtensions and EditorItemBase

Eliminates duplicated Read/Write/PropertyChanged patterns in:
- SheetEditorItem, ViewEditorItem
- TextStyleEditorItem, TemplateEditorItem
- TechnicalRequirementEditorItem
- PmiViewMappingEditorItem, BomColumnEditorItem

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Migrate model classes and controls to use `JsonObjectExtensions`

**Files:**
- Modify: `src/NxEskd.Configurator/DocumentSettingsModel.cs`
- Modify: `src/NxEskd.Configurator/DraftingStandardsModels.cs` (DraftingStandardsModel)
- Modify: `src/NxEskd.Configurator/PmiBomEditorItems.cs` (PmiBomSettingsModel)
- Modify: `src/NxEskd.Configurator/DraftingStandardsControl.xaml.cs`
- Modify: `src/NxEskd.Configurator/PmiBomSettingsControl.xaml.cs`
- Modify: `src/NxEskd.Configurator/TechnicalRequirementsControl.xaml.cs`
- Modify: `src/NxEskd.Configurator/DrawingStructureControl.xaml.cs`
- Modify: `src/NxEskd.Configurator/ProfileEditorDocument.cs`
- Modify: `src/NxEskd.Configurator/EditableSetting.cs`

**Interfaces:**
- Consumes: `JsonObjectExtensions` from Task 1
- Produces: Same public API — all existing tests must pass

- [ ] **Step 1: Replace static `EnsureObject` in each file with `JsonObjectExtensions.EnsureObject`**

In each file that has a private static `EnsureObject` method:
- `DocumentSettingsModel.cs` — delete the private method, replace all calls with `JsonObjectExtensions.EnsureObject`
- `DraftingStandardsModels.cs` (DraftingStandardsModel) — same
- `PmiBomEditorItems.cs` (PmiBomSettingsModel) — same
- `DraftingStandardsControl.xaml.cs` — same
- `TechnicalRequirementsControl.xaml.cs` — same
- `PmiBomSettingsControl.xaml.cs` — same

- [ ] **Step 2: Replace Read/Write/ReadBool/WriteBool/ReadDouble/WriteDouble/ReadInt/WriteInt helpers**

In `DocumentSettingsModel.cs`:
- Replace private `Read(JsonObject, string, string)` calls with `JsonObjectExtensions.ReadString`
- Replace private `ReadBool(JsonObject, string, bool)` calls with `JsonObjectExtensions.ReadBool`
- Replace private `Write(JsonObject, string, string?, string?)` with `JsonObjectExtensions.WriteString` + `NotifyAndChange` — note: DocumentSettingsModel doesn't extend EditorItemBase, so it keeps its own `Changed()` method

In `DraftingStandardsModels.cs` (DraftingStandardsModel):
- Same replacements for Read/ReadDouble/ReadInt/Write/WriteDouble/WriteInt

In `PmiBomEditorItems.cs` (PmiBomSettingsModel):
- Same replacements

- [ ] **Step 3: Run all tests**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all tests PASS

- [ ] **Step 4: Commit**

```bash
git add src/NxEskd.Configurator/
git commit -m "refactor(configurator): use JsonObjectExtensions in models and controls

Replaces duplicated EnsureObject and Read/Write helpers across:
DocumentSettingsModel, DraftingStandardsModel, PmiBomSettingsModel,
all UserControls, ProfileEditorDocument, and EditableSetting.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Replace reflection-based `Refresh()` with explicit property lists

**Files:**
- Modify: `src/NxEskd.Configurator/DraftingStandardsModels.cs`
- Modify: `src/NxEskd.Configurator/DocumentSettingsModel.cs`
- Modify: `src/NxEskd.Configurator/PmiBomEditorItems.cs`

**Interfaces:**
- Consumes: `JsonObjectExtensions` from Task 1
- Produces: Same `Refresh()` method signature, no reflection

- [ ] **Step 1: Write the regression test**

Add to `tests/NxEskd.Configurator.Tests/DraftingStandardsContractTests.cs`:

```csharp
[Fact]
public void RefreshNotifiesAllExpectedProperties()
{
    var root = new JsonObject();
    var model = new DraftingStandardsModel(root, () => { });

    var notified = new List<string?>();
    model.PropertyChanged += (_, args) => notified.Add(args.PropertyName);
    model.Refresh();

    Assert.NotEmpty(notified);
    Assert.Contains(nameof(DraftingStandardsModel.ProjectionMethod), notified);
    Assert.Contains(nameof(DraftingStandardsModel.ArrowLengthMm), notified);
    Assert.Contains(nameof(DraftingStandardsModel.MinimumGapMm), notified);
    Assert.Contains(nameof(DraftingStandardsModel.AllowedScales), notified);
}
```

- [ ] **Step 2: Run test — it passes with current reflection-based code**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~RefreshNotifiesAllExpectedProperties"`
Expected: PASS (current reflection-based Refresh already fires PropertyChanged for all properties)

- [ ] **Step 3: Replace reflection in DraftingStandardsModel.Refresh()**

```csharp
// Before:
public void Refresh()
{
    foreach (var property in GetType().GetProperties().Where(property => property.CanRead))
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.Name));
}

// After:
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
```

- [ ] **Step 4: Replace reflection in DocumentSettingsModel.Refresh()**

Create an explicit `RefreshProperties` array with all readable property names from the class:
`DocumentKind`, `Designation`, `Name`, `Revision`, `Organization`, `Material`, `Status`,
`DevelopedBy`, `CheckedBy`, `NormControlBy`, `ApprovedBy`, `ActiveTitleBlock`,
`DryRun`, `PreserveManualViewPositions`, `PreserveManualDimensions`, `PreserveManualNotes`,
`DeleteManagedObjectsMissingFromConfig`, `ConfirmManagedDeletion`,
`SavePart`, `SaveMode`, `NativeDrawingEnabled`, `NativeDrawingFile`,
`AllowOverwriteExisting`, `AllowOverwriteReleasedDocument`,
`PdfEnabled`, `PdfFile`, `DxfEnabled`, `DxfFile`, and `TitleBlockDefinitions`.

- [ ] **Step 5: Replace reflection in PmiBomSettingsModel.Refresh()**

Create explicit `RefreshProperties` with all readable property names from `PmiBomSettingsModel`.

- [ ] **Step 6: Run all tests**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all tests PASS, including `RefreshNotifiesAllExpectedProperties`

- [ ] **Step 7: Commit**

```bash
git add src/NxEskd.Configurator/DraftingStandardsModels.cs src/NxEskd.Configurator/DocumentSettingsModel.cs src/NxEskd.Configurator/PmiBomEditorItems.cs tests/NxEskd.Configurator.Tests/DraftingStandardsContractTests.cs
git commit -m "refactor(configurator): replace reflection-based Refresh with explicit property lists

Eliminates reflection usage in DraftingStandardsModel, DocumentSettingsModel,
and PmiBomSettingsModel per architecture rule §10.3.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: Split `MainWindow.xaml.cs` into focused partial classes

**Files:**
- Modify: `src/NxEskd.Configurator/MainWindow.xaml.cs`
- Create: `src/NxEskd.Configurator/MainWindow.Commands.cs`
- Create: `src/NxEskd.Configurator/MainWindow.Profile.cs`
- Create: `src/NxEskd.Configurator/MainWindow.Sections.cs`
- Delete: `src/NxEskd.Configurator/MainWindow.RiskyOperations.cs`

**Interfaces:**
- Produces: Three new partial class files
- MainWindow.xaml.cs keeps: constructor, field declarations, simple event handlers (Open, Save, SaveAs, Close, Ctrl+S), Status(), window-level events (Closing, PreviewKeyDown)
- MainWindow.Commands.cs: NX command dispatch (Request), ConfirmRiskyExecution (moved from RiskyOperations.cs), ConfirmDiscardOrSave, Generate/Update/Validate/Preview/Inventory click handlers
- MainWindow.Profile.cs: ParseArguments, FindDefaultProfile, LoadProfile, SaveCurrent, ApplyAndValidateCurrentJson
- MainWindow.Sections.cs: BuildSections, LoadSection, ApplySetting, ApplySettingFilter, BuildFilteredSections, SynchronizeTypedWorkspace, TypedWorkspace_Changed, DisplayValidation, ReloadTypedWorkspaces, SectionsTree_SelectedItemChanged, SettingSearchBox_TextChanged, SectionSearchBox_TextChanged

- [ ] **Step 1: Move command dispatch to MainWindow.Commands.cs**

Move `Request`, `ConfirmRiskyExecution`, `ConfirmDiscardOrSave`, and the five command click handlers (`Generate_Click`, `Update_Click`, `ValidateNx_Click`, `Preview_Click`, `Inventory_Click`) from `MainWindow.xaml.cs` and `MainWindow.RiskyOperations.cs` into `MainWindow.Commands.cs`.

- [ ] **Step 2: Move profile lifecycle to MainWindow.Profile.cs**

Move `ParseArguments`, `FindDefaultProfile`, `LoadProfile`, `SaveCurrent`, `ApplyAndValidateCurrentJson`, `OpenProfile_Click`, `Save_Click`, `SaveAs_Click`, `ApplyRawJson_Click`, `RefreshRawJson_Click`, `ValidateProfile_Click` into `MainWindow.Profile.cs`.

- [ ] **Step 3: Move section/settings logic to MainWindow.Sections.cs**

Move `BuildSections`, `LoadSection`, `ApplySetting`, `ApplySettingFilter`, `BuildFilteredSections`, `SynchronizeTypedWorkspace`, `TypedWorkspace_Changed`, `DisplayValidation`, `ReloadTypedWorkspaces`, `SectionsTree_SelectedItemChanged`, `SettingSearchBox_TextChanged`, `SectionSearchBox_TextChanged` into `MainWindow.Sections.cs`.

- [ ] **Step 4: Delete MainWindow.RiskyOperations.cs**

`ConfirmRiskyExecution` is now in `MainWindow.Commands.cs`.

- [ ] **Step 5: Build the Configurator**

Run: `dotnet build src/NxEskd.Configurator/NxEskd.Configurator.csproj -c Release`
Expected: build succeeds

- [ ] **Step 6: Run XAML contract tests**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~XamlContract"`
Expected: PASS (event handlers found across partial class files)

- [ ] **Step 7: Run all tests**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all tests PASS

- [ ] **Step 8: Commit**

```bash
git add src/NxEskd.Configurator/MainWindow*.cs
git rm src/NxEskd.Configurator/MainWindow.RiskyOperations.cs
git commit -m "refactor(configurator): split MainWindow into focused partial classes

MainWindow.xaml.cs — constructor, fields, simple handlers
MainWindow.Commands.cs — NX command dispatch and risky execution confirm
MainWindow.Profile.cs — profile load/save/validate pipeline
MainWindow.Sections.cs — section tree, settings grid, filtering

Removes MainWindow.RiskyOperations.cs (merged into Commands.cs).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: Deduplicate tests

**Files:**
- Create: `tests/NxEskd.Configurator.Tests/TestRepositoryLocator.cs`
- Create: `tests/NxEskd.Configurator.Tests/ProfileFixture.cs`
- Modify: `tests/NxEskd.Configurator.Tests/ConfiguratorContractTests.cs`
- Modify: `tests/NxEskd.Configurator.Tests/XamlContractTests.cs`
- Delete: `tests/NxEskd.Configurator.Tests/DraftingStandardsBehaviorTests.cs`

**Interfaces:**
- Produces: `TestRepositoryLocator.Locate() → string` — shared repository root finder
- Produces: `TestRepositoryLocator.ConfiguratorDirectory() → string`
- Produces: `ProfileFixture : IDisposable` — extracted from ConfiguratorContractTests nested class

- [ ] **Step 1: Extract TestRepositoryLocator**

```csharp
namespace NxEskd.Configurator.Tests;

internal static class TestRepositoryLocator
{
    public static string Locate()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName,
                        "config", "active-profile.example.json")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    public static string ConfiguratorDirectory()
        => Path.Combine(Locate(), "src", "NxEskd.Configurator");
}
```

- [ ] **Step 2: Extract ProfileFixture to its own file**

Move the `ProfileFixture` nested class from `ConfiguratorContractTests` into `tests/NxEskd.Configurator.Tests/ProfileFixture.cs`.

- [ ] **Step 3: Update ConfiguratorContractTests and XamlContractTests**

Replace inline `LocateRepositoryRoot()` with `TestRepositoryLocator.Locate()` and `TestRepositoryLocator.ConfiguratorDirectory()`.

- [ ] **Step 4: Delete DraftingStandardsBehaviorTests.cs**

It contains 5 tests, all of which have identical or equivalent versions in `DraftingStandardsContractTests.cs`:
- `AllowedScalesAreNormalizedAndDeduplicated` → identical test in ContractTests
- `EmptyAllowedScaleListIsRejected` → identical test in ContractTests
- `InvalidLayoutValuesDoNotWriteInvalidProperties` → covered by `InvalidLayoutValuesAreRejectedBeforeJsonMutation` in ContractTests
- `TextStyleRejectsNonPositiveHeight` → identical test in ContractTests
- `TemplateOptionalObjectNamesAreRemovedWhenCleared` → identical test in ContractTests

- [ ] **Step 5: Run all tests**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: all tests PASS (test count decreases by 5 from the deleted file)

- [ ] **Step 6: Commit**

```bash
git add tests/NxEskd.Configurator.Tests/TestRepositoryLocator.cs tests/NxEskd.Configurator.Tests/ProfileFixture.cs tests/NxEskd.Configurator.Tests/ConfiguratorContractTests.cs tests/NxEskd.Configurator.Tests/XamlContractTests.cs
git rm tests/NxEskd.Configurator.Tests/DraftingStandardsBehaviorTests.cs
git commit -m "refactor(configurator): deduplicate test utilities and remove redundant test file

- Extract TestRepositoryLocator (shared by ConfiguratorContractTests and XamlContractTests)
- Extract ProfileFixture to its own file
- Remove DraftingStandardsBehaviorTests (all 5 tests duplicated in ContractTests)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: Final verification — build, tests, and XAML contract check

- [ ] **Step 1: Run the full Configurator test suite**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release`
Expected: ALL tests PASS

- [ ] **Step 2: Run Core tests to verify no cross-project breakage**

Run: `dotnet test tests/NxEskd.Core.Tests/NxEskd.Core.Tests.csproj -c Release`
Expected: ALL tests PASS

- [ ] **Step 3: Build the full solution**

Run: `dotnet build NxEskdDrawingAutomation.sln -c Release`
Expected: build succeeds with no warnings

- [ ] **Step 4: Verify XAML contracts**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~XamlContract"`
Expected: `EveryXamlClassHasMatchingPartialCodeBehind` and `XamlEventHandlersExistInPartialClassSources` both PASS

- [ ] **Step 5: Verify Refresh regression test**

Run: `dotnet test tests/NxEskd.Configurator.Tests/NxEskd.Configurator.Tests.csproj -c Release --filter "FullyQualifiedName~Refresh"`
Expected: `RefreshNotifiesAllExpectedProperties` PASS

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "chore(configurator): final verification after refactoring — all tests green

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Self-Review

### Spec Coverage
The "spec" in this case is the set of refactoring targets identified during codebase analysis:

| Issue | Task(s) |
|---|---|
| Duplicated `EnsureObject` (5 copies) | Task 1, Task 4 |
| Duplicated Read/Write helpers (7+ classes) | Task 1, Task 3, Task 4 |
| Duplicated `_changed`/`PropertyChanged` boilerplate (7 classes) | Task 2, Task 3 |
| Reflection-based `Refresh()` in 3 model classes | Task 5 |
| MainWindow.xaml.cs 417 lines, mixed concerns | Task 6 |
| Duplicated tests | Task 7 |
| Duplicated `LocateRepositoryRoot` in tests | Task 7 |

### Placeholder Scan
No TBD, TODO, "implement later", "add appropriate error handling", "write tests for the above", or "similar to Task N" patterns found.

### Type Consistency
- `JsonObjectExtensions.EnsureObject(JsonObject owner, string name)` → used across all files
- `EditorItemBase` → `NotifyAndChange(string? propertyName)` → used in all editor items
- `EditorItemBase<T> where T : JsonObject` → `T Node { get; }` → used in all typed editor items
- `TestRepositoryLocator.Locate()` and `ConfiguratorDirectory()` → used in both test files
- `ProfileFixture` → used in ConfiguratorContractTests
