using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace NxEskd.Configurator;

public partial class TechnicalRequirementsControl : UserControl
{
    private readonly ObservableCollection<TechnicalRequirementEditorItem> _items = [];
    private JsonObject? _root;
    private bool _loading;

    public TechnicalRequirementsControl()
    {
        InitializeComponent();
        RequirementsGrid.ItemsSource = _items;
        RequirementsGrid.AlternationCount = int.MaxValue;
    }

    public event EventHandler? RequirementsChanged;

    public void Load(JsonObject root)
    {
        _root = root;
        _loading = true;
        try
        {
            EnabledCheckBox.IsChecked = TechnicalSettings["enabled"]?.GetValue<bool?>() ?? true;
            UnspecifiedToleranceBox.Text = JobTechnicalRequirements["unspecifiedTolerances"]?.GetValue<string?>()
                                           ?? string.Empty;
            RebuildItems();
        }
        finally
        {
            _loading = false;
        }
    }

    private JsonObject TechnicalSettings => JsonObjectExtensions.EnsureObject(Root, "technicalRequirements");
    private JsonObject Job => JsonObjectExtensions.EnsureObject(Root, "job");
    private JsonObject JobTechnicalRequirements => JsonObjectExtensions.EnsureObject(Job, "technicalRequirements");

    private JsonArray ItemNodes
    {
        get
        {
            if (JobTechnicalRequirements["items"] is JsonArray existing) return existing;
            var created = new JsonArray();
            JobTechnicalRequirements["items"] = created;
            return created;
        }
    }

    private JsonObject Root => _root ?? throw new InvalidOperationException("Профиль не загружен.");

    private void RebuildItems(string? selectedText = null)
    {
        var groups = LoadGroups();
        _items.Clear();
        foreach (var node in ItemNodes.OfType<JsonObject>())
            _items.Add(new TechnicalRequirementEditorItem(node, groups, OnItemChanged));
        RequirementsGrid.SelectedItem = _items.FirstOrDefault(item =>
                                             item.Text.Equals(selectedText, StringComparison.Ordinal))
                                         ?? _items.FirstOrDefault();
    }

    private IReadOnlyList<string> LoadGroups()
    {
        var configured = (TechnicalSettings["groupOrder"] as JsonArray)?
            .Select(node => node?.GetValue<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray() ?? [];
        return configured.Length > 0
            ? configured
            : [
                "material_and_blank",
                "dimensions_and_tolerances",
                "surface_and_coatings",
                "heat_treatment",
                "assembly",
                "marking",
                "quality_control",
                "references"
            ];
    }

    private void OnItemChanged()
    {
        if (!_loading) RequirementsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Enabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _root is null) return;
        TechnicalSettings["enabled"] = EnabledCheckBox.IsChecked == true;
        RequirementsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UnspecifiedToleranceBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || _root is null) return;
        JobTechnicalRequirements["unspecifiedTolerances"] = UnspecifiedToleranceBox.Text.Trim();
        RequirementsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null) return;
        var node = new JsonObject
        {
            ["group"] = LoadGroups().FirstOrDefault() ?? "references",
            ["text"] = "Новое техническое требование"
        };
        ItemNodes.Add(node);
        RebuildItems("Новое техническое требование");
        RequirementsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (RequirementsGrid.SelectedItem is not TechnicalRequirementEditorItem selected) return;
        var answer = MessageBox.Show(Window.GetWindow(this),
            "Удалить выбранное техническое требование?",
            "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        ItemNodes.Remove(selected.Node);
        RebuildItems();
        RequirementsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
        => MoveSelected(-1);

    private void MoveDown_Click(object sender, RoutedEventArgs e)
        => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        if (RequirementsGrid.SelectedItem is not TechnicalRequirementEditorItem selected) return;
        var index = ItemNodes.IndexOf(selected.Node);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= ItemNodes.Count) return;

        var node = ItemNodes[index];
        ItemNodes.RemoveAt(index);
        ItemNodes.Insert(target, node);
        RebuildItems(selected.Text);
        RequirementsChanged?.Invoke(this, EventArgs.Empty);
    }
}
