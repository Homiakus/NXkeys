using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Configurator;

public partial class MainWindow
{
    private void BuildSections()
    {
        SectionsTree.Items.Clear();
        foreach (var pair in _document.Root)
        {
            var description = pair.Value is JsonObject obj
                ? obj["_description"]?.GetValue<string?>()
                : null;
            SectionsTree.Items.Add(new TreeViewItem
            {
                Header = pair.Key,
                Tag = pair.Key,
                ToolTip = description
            });
        }

        if (SectionsTree.Items.Count > 0)
            ((TreeViewItem)SectionsTree.Items[0]).IsSelected = true;
    }

    private void LoadSection(string section)
    {
        _selectedSection = section;
        var node = _document.Root[section];
        if (node is null) return;
        _allSectionSettings = JsonFlattener.Flatten(node, "$." + section)
            .Select(x => new EditableSetting(x.Path, x.Name, x.Value, x.ValueType, x.Description, ApplySetting))
            .ToList();
        ApplySettingFilter();
    }

    private void ApplySetting(EditableSetting setting)
    {
        if (_loading) return;
        try
        {
            JsonEditor.Set(_document.Root, setting.Path, setting.ToJsonNode());
            _document.NotifyRootChanged();
            RawJsonBox.Text = _document.CurrentJson;
            SynchronizeTypedWorkspace(setting.Path);
            DisplayValidation(_document.Validate());
            Status("Изменено: " + setting.Path);
        }
        catch (Exception ex)
        {
            Status("Ошибка значения: " + ex.Message);
        }
    }

    private void ApplySettingFilter()
    {
        var filter = SettingSearchBox.Text.Trim();
        _settings.Clear();
        foreach (var item in _allSectionSettings.Where(x => string.IsNullOrEmpty(filter)
                     || x.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)
                     || x.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
                     || x.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            _settings.Add(item);
    }

    private void BuildFilteredSections()
    {
        var filter = SectionSearchBox.Text.Trim();
        SectionsTree.Items.Clear();
        foreach (var pair in _document.Root.Where(p => string.IsNullOrEmpty(filter)
                     || p.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                     || (p.Value?["_description"]?.GetValue<string?>()
                         ?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)))
            SectionsTree.Items.Add(new TreeViewItem
            {
                Header = pair.Key,
                Tag = pair.Key,
                ToolTip = pair.Value?["_description"]?.GetValue<string?>()
            });
    }

    private void SynchronizeTypedWorkspace(string path)
    {
        if (path.StartsWith("$.job.sheetPlan", StringComparison.Ordinal))
            DrawingStructureEditor.Load(_document.Root);
        if (path.StartsWith("$.job.document", StringComparison.Ordinal)
            || path.StartsWith("$.job.approvals", StringComparison.Ordinal)
            || path.StartsWith("$.output", StringComparison.Ordinal)
            || path.StartsWith("$.execution", StringComparison.Ordinal)
            || path.StartsWith("$.titleBlocks", StringComparison.Ordinal))
            DocumentSettingsEditor.Load(_document.Root);
        if (path.StartsWith("$.technicalRequirements", StringComparison.Ordinal)
            || path.StartsWith("$.job.technicalRequirements", StringComparison.Ordinal))
            TechnicalRequirementsEditor.Load(_document.Root);
        if (path.StartsWith("$.pmiInheritance", StringComparison.Ordinal)
            || path.StartsWith("$.partsListAndBalloons", StringComparison.Ordinal))
            _pmiBomSettings.Load(_document.Root);
    }

    private void TypedWorkspace_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        try
        {
            _document.NotifyRootChanged();
            RawJsonBox.Text = _document.CurrentJson;
            if (!string.IsNullOrWhiteSpace(_selectedSection))
                LoadSection(_selectedSection);
            DisplayValidation(_document.Validate());
            Status(sender switch
            {
                DrawingStructureControl => "Структура листов и видов изменена.",
                TechnicalRequirementsControl => "Технические требования изменены.",
                PmiBomSettingsControl => "Настройки PMI, спецификации или позиций изменены.",
                _ => "Настройки документа или выпуска изменены."
            });
        }
        catch (Exception ex)
        {
            Status("Ошибка предметного редактора: " + ex.Message);
        }
    }

    private void DisplayValidation(ValidationReport report)
    {
        IssuesGrid.ItemsSource = report.Issues;
        ValidationSummaryText.Text =
            $"Ошибок: {report.ErrorCount}; предупреждений: {report.WarningCount}; всего: {report.Issues.Count}";
    }

    private void ReloadTypedWorkspaces()
    {
        DocumentSettingsEditor.Load(_document.Root);
        DrawingStructureEditor.Load(_document.Root);
        TechnicalRequirementsEditor.Load(_document.Root);
        _pmiBomSettings.Load(_document.Root);
    }

    private void SectionsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (SectionsTree.SelectedItem is TreeViewItem item && item.Tag is string section)
            LoadSection(section);
    }

    private void SettingSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySettingFilter();
    private void SectionSearchBox_TextChanged(object sender, TextChangedEventArgs e) => BuildFilteredSections();
}
