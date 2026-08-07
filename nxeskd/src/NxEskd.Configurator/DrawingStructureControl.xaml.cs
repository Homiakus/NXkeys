using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using NxEskd.Core.Planning;

namespace NxEskd.Configurator;

public partial class DrawingStructureControl : UserControl
{
    private readonly ObservableCollection<SheetEditorItem> _sheets = [];
    private readonly ObservableCollection<ViewEditorItem> _views = [];
    private JsonObject? _root;
    private bool _loading;

    public DrawingStructureControl()
    {
        InitializeComponent();
        SheetsGrid.ItemsSource = _sheets;
        ViewsGrid.ItemsSource = _views;
    }

    public event EventHandler? StructureChanged;

    public void Load(JsonObject root)
    {
        _root = root;
        RebuildSheets();
    }

    private JsonArray SheetPlan
    {
        get
        {
            if (_root is null) throw new InvalidOperationException("Профиль не загружен.");
            var job = _root["job"] as JsonObject;
            if (job is null)
            {
                job = new JsonObject();
                _root["job"] = job;
            }
            if (job["sheetPlan"] is JsonArray existing) return existing;
            var created = new JsonArray();
            job["sheetPlan"] = created;
            return created;
        }
    }

    private void RebuildSheets(string? selectId = null)
    {
        if (_root is null) return;
        _loading = true;
        try
        {
            var previousId = selectId ?? (SheetsGrid.SelectedItem as SheetEditorItem)?.Id;
            _sheets.Clear();
            foreach (var node in SheetPlan.OfType<JsonObject>())
                _sheets.Add(new SheetEditorItem(node, OnItemChanged));

            var selected = _sheets.FirstOrDefault(item =>
                               item.Id.Equals(previousId, StringComparison.OrdinalIgnoreCase))
                           ?? _sheets.FirstOrDefault();
            SheetsGrid.SelectedItem = selected;
            RebuildViews(selected);
        }
        finally
        {
            _loading = false;
        }
    }

    private void RebuildViews(SheetEditorItem? sheet, string? selectId = null)
    {
        _loading = true;
        try
        {
            _views.Clear();
            if (sheet is null)
            {
                ViewsHintText.Text = "Выберите лист";
                return;
            }

            ViewsHintText.Text = $"Лист {sheet.Id}: зависимости видов проверяются до сохранения";
            var nodes = EnsureViews(sheet.Node).OfType<JsonObject>().ToArray();
            var allIds = nodes.Select(node => node["id"]?.GetValue<string?>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray();
            foreach (var node in nodes)
            {
                var id = node["id"]?.GetValue<string?>() ?? string.Empty;
                var parentCandidates = new[] { string.Empty }
                    .Concat(allIds.Where(candidate => !candidate.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                _views.Add(new ViewEditorItem(node, parentCandidates, OnItemChanged));
            }

            ViewsGrid.SelectedItem = _views.FirstOrDefault(item =>
                                         item.Id.Equals(selectId, StringComparison.OrdinalIgnoreCase))
                                     ?? _views.FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }
    }

    private static JsonArray EnsureViews(JsonObject sheet)
    {
        if (sheet["views"] is JsonArray existing) return existing;
        var created = new JsonArray();
        sheet["views"] = created;
        return created;
    }

    private void OnItemChanged()
    {
        if (_loading) return;
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SheetsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        RebuildViews(SheetsGrid.SelectedItem as SheetEditorItem);
    }

    private void AddSheet_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null) return;
        var id = NextId("SHEET", SheetPlan.OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string?>()));
        var templateId = (_root["templateCatalog"]?["templates"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string?>())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var sheet = new JsonObject
        {
            ["id"] = id,
            ["role"] = "main",
            ["templateId"] = templateId,
            ["format"] = "A3",
            ["orientation"] = "landscape",
            ["scale"] = "auto",
            ["views"] = new JsonArray()
        };
        SheetPlan.Add(sheet);
        RebuildSheets(id);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSheet_Click(object sender, RoutedEventArgs e)
    {
        if (SheetsGrid.SelectedItem is not SheetEditorItem selected) return;
        if (SheetPlan.Count <= 1)
        {
            MessageBox.Show(Window.GetWindow(this),
                "В профиле должен остаться хотя бы один лист.",
                "Удаление листа", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var viewIds = EnsureViews(selected.Node).OfType<JsonObject>()
            .Select(node => node["id"]?.GetValue<string?>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var externalDependents = SheetPlan.OfType<JsonObject>()
            .Where(sheet => !ReferenceEquals(sheet, selected.Node))
            .SelectMany(sheet => EnsureViews(sheet).OfType<JsonObject>())
            .Where(view => viewIds.Contains(view["parentViewId"]?.GetValue<string?>() ?? string.Empty))
            .Select(view => view["id"]?.GetValue<string?>() ?? "VIEW")
            .ToArray();
        if (externalDependents.Length > 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Лист нельзя удалить: на его виды ссылаются " + string.Join(", ", externalDependents),
                "Зависимости видов", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var answer = MessageBox.Show(Window.GetWindow(this),
            $"Удалить лист {selected.Id} и все его виды?",
            "Удаление листа", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        SheetPlan.Remove(selected.Node);
        RebuildSheets();
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddView_Click(object sender, RoutedEventArgs e)
    {
        if (SheetsGrid.SelectedItem is not SheetEditorItem sheet) return;
        var views = EnsureViews(sheet.Node);
        var allIds = SheetPlan.OfType<JsonObject>()
            .SelectMany(item => EnsureViews(item).OfType<JsonObject>())
            .Select(node => node["id"]?.GetValue<string?>());
        var id = NextId("VIEW", allIds);
        var parent = views.OfType<JsonObject>()
            .FirstOrDefault(node => string.Equals(node["type"]?.GetValue<string?>(),
                DrawingViewKinds.Base, StringComparison.OrdinalIgnoreCase));

        JsonObject view;
        if (parent is null)
        {
            view = new JsonObject
            {
                ["id"] = id,
                ["type"] = DrawingViewKinds.Base,
                ["name"] = "Главный вид",
                ["modelView"] = "Front",
                ["fallbackModelView"] = "Front",
                ["placement"] = new JsonObject
                {
                    ["mode"] = "auto",
                    ["preferredAnchor"] = "center_left",
                    ["gapMm"] = 20.0
                },
                ["scale"] = new JsonObject { ["inheritSheet"] = true },
                ["hiddenLines"] = "removed"
            };
        }
        else
        {
            view = new JsonObject
            {
                ["id"] = id,
                ["type"] = DrawingViewKinds.Projected,
                ["name"] = "Проекционный вид",
                ["parentViewId"] = parent["id"]?.GetValue<string?>(),
                ["direction"] = FirstFreeProjectionDirection(views, parent["id"]?.GetValue<string?>()),
                ["placement"] = new JsonObject
                {
                    ["mode"] = "aligned_auto",
                    ["gapMm"] = 20.0
                },
                ["scale"] = new JsonObject { ["inheritSheet"] = true },
                ["hiddenLines"] = "removed"
            };
        }

        views.Add(view);
        RebuildViews(sheet, id);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveView_Click(object sender, RoutedEventArgs e)
    {
        if (SheetsGrid.SelectedItem is not SheetEditorItem sheet
            || ViewsGrid.SelectedItem is not ViewEditorItem selected) return;

        var allViews = SheetPlan.OfType<JsonObject>()
            .SelectMany(item => EnsureViews(item).OfType<JsonObject>())
            .ToArray();
        var dependents = allViews.Where(view =>
                string.Equals(view["parentViewId"]?.GetValue<string?>(), selected.Id,
                    StringComparison.OrdinalIgnoreCase))
            .Select(view => view["id"]?.GetValue<string?>() ?? "VIEW")
            .ToArray();
        if (dependents.Length > 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Вид нельзя удалить: от него зависят " + string.Join(", ", dependents),
                "Зависимости видов", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var answer = MessageBox.Show(Window.GetWindow(this),
            $"Удалить вид {selected.Id}?",
            "Удаление вида", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        EnsureViews(sheet.Node).Remove(selected.Node);
        RebuildViews(sheet);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string NextId(string prefix, IEnumerable<string?> existing)
    {
        var ids = existing.Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < 10000; index++)
        {
            var candidate = $"{prefix}_{index}";
            if (!ids.Contains(candidate)) return candidate;
        }
        return prefix + "_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    private static string FirstFreeProjectionDirection(JsonArray views, string? parentId)
    {
        var used = views.OfType<JsonObject>()
            .Where(view => string.Equals(view["parentViewId"]?.GetValue<string?>(), parentId,
                StringComparison.OrdinalIgnoreCase))
            .Select(view => view["direction"]?.GetValue<string?>())
            .Where(direction => !string.IsNullOrWhiteSpace(direction))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new[] { "right", "top", "left", "bottom" }.FirstOrDefault(direction => !used.Contains(direction))
               ?? "right";
    }
}
