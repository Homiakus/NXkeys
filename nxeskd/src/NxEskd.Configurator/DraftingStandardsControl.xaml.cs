using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace NxEskd.Configurator;

public partial class DraftingStandardsControl : UserControl
{
    private readonly ObservableCollection<TextStyleEditorItem> _textStyles = [];
    private readonly ObservableCollection<TemplateEditorItem> _templates = [];
    private JsonObject? _root;
    private DraftingStandardsModel? _model;
    private bool _loading;

    public DraftingStandardsControl()
    {
        InitializeComponent();
        TextStylesGrid.ItemsSource = _textStyles;
        TemplatesGrid.ItemsSource = _templates;
    }

    public event EventHandler? StandardsChanged;

    public void Load(JsonObject root)
    {
        _loading = true;
        try
        {
            _root = root;
            _model = new DraftingStandardsModel(root, OnItemChanged);
            DataContext = _model;
            _model.Refresh();
            RebuildTextStyles();
            RebuildTemplates();
        }
        finally
        {
            _loading = false;
        }
    }

    private JsonObject Root => _root ?? throw new InvalidOperationException("Профиль не загружен.");
    private JsonObject DraftingStyles => JsonObjectExtensions.EnsureObject(Root, "draftingStyles");
    private JsonObject TemplateCatalog => JsonObjectExtensions.EnsureObject(Root, "templateCatalog");

    private JsonArray TextStyleNodes
    {
        get
        {
            if (DraftingStyles["textStyles"] is JsonArray existing) return existing;
            var created = new JsonArray();
            DraftingStyles["textStyles"] = created;
            return created;
        }
    }

    private JsonArray TemplateNodes
    {
        get
        {
            if (TemplateCatalog["templates"] is JsonArray existing) return existing;
            var created = new JsonArray();
            TemplateCatalog["templates"] = created;
            return created;
        }
    }

    private void RebuildTextStyles(JsonObject? selectNode = null)
    {
        _textStyles.Clear();
        foreach (var node in TextStyleNodes.OfType<JsonObject>())
            _textStyles.Add(new TextStyleEditorItem(node, OnItemChanged));
        TextStylesGrid.SelectedItem = _textStyles.FirstOrDefault(item => ReferenceEquals(item.Node, selectNode))
                                       ?? _textStyles.FirstOrDefault();
    }

    private void RebuildTemplates(JsonObject? selectNode = null)
    {
        _templates.Clear();
        foreach (var node in TemplateNodes.OfType<JsonObject>())
            _templates.Add(new TemplateEditorItem(node, OnItemChanged));
        TemplatesGrid.SelectedItem = _templates.FirstOrDefault(item => ReferenceEquals(item.Node, selectNode))
                                      ?? _templates.FirstOrDefault();
    }

    private void OnItemChanged()
    {
        if (!_loading) StandardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddTextStyle_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null) return;
        var ids = TextStyleNodes.OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (ids.Contains("TEXT_STYLE_" + index)) index++;
        var node = new JsonObject
        {
            ["id"] = "TEXT_STYLE_" + index,
            ["font"] = "Arial",
            ["heightMm"] = 3.5,
            ["widthFactor"] = 1.0,
            ["bold"] = false,
            ["italic"] = false
        };
        TextStyleNodes.Add(node);
        RebuildTextStyles(node);
        StandardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveTextStyle_Click(object sender, RoutedEventArgs e)
    {
        if (TextStylesGrid.SelectedItem is not TextStyleEditorItem selected) return;
        if (TextStyleNodes.Count <= 1)
        {
            MessageBox.Show(Window.GetWindow(this),
                "В профиле должен остаться хотя бы один текстовый стиль.",
                "Удаление стиля", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var answer = MessageBox.Show(Window.GetWindow(this),
            $"Удалить текстовый стиль {selected.Id}?",
            "Удаление стиля", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        TextStyleNodes.Remove(selected.Node);
        RebuildTextStyles();
        StandardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null) return;
        var ids = TemplateNodes.OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (ids.Contains("ESKD_TEMPLATE_" + index)) index++;
        var node = new JsonObject
        {
            ["id"] = "ESKD_TEMPLATE_" + index,
            ["file"] = "templates/template.prt",
            ["format"] = "A3",
            ["orientation"] = "landscape",
            ["borderObjectName"] = "AUTO_BORDER",
            ["titleBlockObjectName"] = "AUTO_TITLE_BLOCK_FORM_1",
            ["documentKinds"] = new JsonArray(
                "part_drawing", "assembly_drawing", "sheet_metal_drawing"),
            ["priority"] = 0
        };
        TemplateNodes.Add(node);
        RebuildTemplates(node);
        StandardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplatesGrid.SelectedItem is not TemplateEditorItem selected) return;
        if (TemplateNodes.Count <= 1)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Каталог должен содержать хотя бы один PRT-шаблон.",
                "Удаление шаблона", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var usedBySheets = (Root["job"]?["sheetPlan"] as JsonArray)?.OfType<JsonObject>()
            .Where(sheet => string.Equals(sheet["templateId"]?.GetValue<string?>(), selected.Id,
                StringComparison.OrdinalIgnoreCase))
            .Select(sheet => sheet["id"]?.GetValue<string?>() ?? "SHEET")
            .ToArray() ?? [];
        if (usedBySheets.Length > 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"Шаблон используется листами: {string.Join(", ", usedBySheets)}.",
                "Удаление шаблона", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var answer = MessageBox.Show(Window.GetWindow(this),
            $"Удалить шаблон {selected.Id}?",
            "Удаление шаблона", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        TemplateNodes.Remove(selected.Node);
        RebuildTemplates();
        StandardsChanged?.Invoke(this, EventArgs.Empty);
    }
}
