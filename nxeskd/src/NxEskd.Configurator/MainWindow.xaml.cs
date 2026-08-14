using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.Configurator;

public sealed class PlanOperationDisplayItem
{
    public string OperationId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string ObjectKind { get; init; } = string.Empty;
    public string ChangeKind { get; init; } = string.Empty;
    public string DependenciesText { get; init; } = string.Empty;
}

public partial class MainWindow : Window
{
    private readonly ProfileEditorDocument _document = new();
    private readonly PmiBomSettingsControl _pmiBomSettings = new();
    private string? _nxPartPath;
    private string _workflowId = Guid.NewGuid().ToString("N");
    private string? _lastReportPath;
    private readonly ObservableCollection<EditableSetting> _settings = [];
    private readonly ObservableCollection<PlanOperationDisplayItem> _planOperations = [];
    private List<EditableSetting> _allSectionSettings = [];
    private string? _selectedSection;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        SettingsGrid.ItemsSource = _settings;
        PlanOperationsGrid.ItemsSource = _planOperations;

        DrawingStructureEditor.StructureChanged += TypedWorkspace_Changed;
        DocumentSettingsEditor.SettingsChanged += TypedWorkspace_Changed;
        TechnicalRequirementsEditor.RequirementsChanged += TypedWorkspace_Changed;
        _pmiBomSettings.SettingsChanged += TypedWorkspace_Changed;

        InputsSubTabs.Items.Insert(3, new TabItem
        {
            Header = "PMI и BOM",
            Content = _pmiBomSettings
        });

        var profilePath = ParseArguments(Environment.GetCommandLineArgs().Skip(1).ToArray())
                          ?? FindDefaultProfile();
        LoadProfile(profilePath);
    }

    private void Status(string message) => StatusText.Text = message;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        SaveCurrent();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_document.IsDirty(RawJsonBox.Text)) return;
        var answer = MessageBox.Show(this,
            "Профиль изменён, но не сохранён.\n\nДа — проверить и сохранить.\nНет — закрыть без сохранения.\nОтмена — вернуться в редактор.",
            "Несохранённые изменения",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }
        if (answer == MessageBoxResult.Yes && !SaveCurrent())
            e.Cancel = true;
    }
}
