using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace NxEskd.Configurator;

public partial class PmiBomSettingsControl : UserControl
{
    private readonly ObservableCollection<PmiViewMappingEditorItem> _mappings = [];
    private readonly ObservableCollection<BomColumnEditorItem> _columns = [];
    private JsonObject? _root;
    private PmiBomSettingsModel? _model;
    private bool _loading;

    public PmiBomSettingsControl()
    {
        InitializeComponent();
        MappingsGrid.ItemsSource = _mappings;
        ColumnsGrid.ItemsSource = _columns;
    }

    public event EventHandler? SettingsChanged;

    public void Load(JsonObject root)
    {
        _loading = true;
        try
        {
            _root = root;
            _model = new PmiBomSettingsModel(root, OnItemChanged);
            DataContext = _model;
            _model.Refresh();
            RebuildMappings();
            RebuildColumns();
        }
        finally
        {
            _loading = false;
        }
    }

    private JsonObject Root => _root ?? throw new InvalidOperationException("Профиль не загружен.");
    private JsonObject Pmi => JsonObjectExtensions.EnsureObject(Root, "pmiInheritance");
    private JsonObject PartsListAndBalloons => JsonObjectExtensions.EnsureObject(Root, "partsListAndBalloons");
    private JsonObject PartsList => JsonObjectExtensions.EnsureObject(PartsListAndBalloons, "partsList");

    private JsonArray MappingNodes
    {
        get
        {
            if (Pmi["viewMapping"] is JsonArray existing) return existing;
            var created = new JsonArray();
            Pmi["viewMapping"] = created;
            return created;
        }
    }

    private JsonArray ColumnNodes
    {
        get
        {
            if (PartsList["columns"] is JsonArray existing) return existing;
            var created = new JsonArray();
            PartsList["columns"] = created;
            return created;
        }
    }

    private void RebuildMappings(JsonObject? selectNode = null)
    {
        var modelViews = ModelViewCandidates();
        var drawingViews = DrawingViewCandidates();
        _mappings.Clear();
        foreach (var node in MappingNodes.OfType<JsonObject>())
            _mappings.Add(new PmiViewMappingEditorItem(node, modelViews, drawingViews, OnItemChanged));
        MappingsGrid.SelectedItem = _mappings.FirstOrDefault(item => ReferenceEquals(item.Node, selectNode))
                                    ?? _mappings.FirstOrDefault();
    }

    private void RebuildColumns(JsonObject? selectNode = null)
    {
        _columns.Clear();
        foreach (var node in ColumnNodes.OfType<JsonObject>())
            _columns.Add(new BomColumnEditorItem(node, OnItemChanged));
        ColumnsGrid.SelectedItem = _columns.FirstOrDefault(item => ReferenceEquals(item.Node, selectNode))
                                   ?? _columns.FirstOrDefault();
    }

    private IReadOnlyList<string> ModelViewCandidates()
    {
        var configured = (Pmi["sourceSelection"]?["modelViewNames"] as JsonArray)?
            .Select(node => node?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray() ?? [];
        return new[] { string.Empty }
            .Concat(configured)
            .Concat(["Front", "Top", "Right", "Left", "Bottom", "Back", "Trimetric", "Isometric", "Flat Pattern"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> DrawingViewCandidates()
        => new[] { string.Empty }
            .Concat((Root["job"]?["sheetPlan"] as JsonArray)?.OfType<JsonObject>()
                .SelectMany(sheet => (sheet["views"] as JsonArray)?.OfType<JsonObject>() ?? [])
                .Select(view => view["id"]?.GetValue<string?>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>() ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void OnItemChanged()
    {
        if (!_loading) SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddMapping_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null) return;
        var target = DrawingViewCandidates().FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var source = ModelViewCandidates().FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Front";
        var node = new JsonObject
        {
            ["sourceModelView"] = source,
            ["targetDrawingViewId"] = target,
            ["fallbackSourceModelView"] = "Front",
            ["orientationToleranceDeg"] = 2.0
        };
        MappingNodes.Add(node);
        RebuildMappings(node);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveMapping_Click(object sender, RoutedEventArgs e)
    {
        if (MappingsGrid.SelectedItem is not PmiViewMappingEditorItem selected) return;
        var answer = MessageBox.Show(Window.GetWindow(this),
            $"Удалить mapping {selected.SourceModelView} → {selected.TargetDrawingViewId}?",
            "Удаление PMI mapping", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        MappingNodes.Remove(selected.Node);
        RebuildMappings();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null) return;
        var existing = ColumnNodes.OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (existing.Contains("column_" + index)) index++;
        var node = new JsonObject
        {
            ["id"] = "column_" + index,
            ["title"] = "Новая колонка",
            ["source"] = "description",
            ["widthMm"] = 30.0,
            ["textStyleId"] = "TEXT_BOM"
        };
        ColumnNodes.Add(node);
        RebuildColumns(node);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveColumn_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnsGrid.SelectedItem is not BomColumnEditorItem selected) return;
        if (ColumnNodes.Count <= 1)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Parts List должна содержать хотя бы одну колонку.",
                "Удаление колонки", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var answer = MessageBox.Show(Window.GetWindow(this),
            $"Удалить колонку «{selected.Title}»?",
            "Удаление колонки", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        ColumnNodes.Remove(selected.Node);
        RebuildColumns();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveColumnUp_Click(object sender, RoutedEventArgs e) => MoveSelectedColumn(-1);
    private void MoveColumnDown_Click(object sender, RoutedEventArgs e) => MoveSelectedColumn(1);

    private void MoveSelectedColumn(int offset)
    {
        if (ColumnsGrid.SelectedItem is not BomColumnEditorItem selected) return;
        var index = ColumnNodes.IndexOf(selected.Node);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= ColumnNodes.Count) return;
        var node = ColumnNodes[index];
        ColumnNodes.RemoveAt(index);
        ColumnNodes.Insert(target, node);
        RebuildColumns(selected.Node);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
