using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NxEskd.Configurator;

public partial class MainWindow : Window
{
    private readonly ProfileEditorDocument _document = new();
    private readonly PmiBomSettingsControl _pmiBomSettings = new();
    private string? _requestPath;
    private string? _nxPartPath;
    private readonly ObservableCollection<EditableSetting> _settings = [];
    private List<EditableSetting> _allSectionSettings = [];
    private string? _selectedSection;
    private bool _loading;
    private bool _closeAfterRequest;

    public MainWindow()
    {
        InitializeComponent();
        SettingsGrid.ItemsSource = _settings;
        DrawingStructureEditor.StructureChanged += TypedWorkspace_Changed;
        DocumentSettingsEditor.SettingsChanged += TypedWorkspace_Changed;
        TechnicalRequirementsEditor.RequirementsChanged += TypedWorkspace_Changed;
        _pmiBomSettings.SettingsChanged += TypedWorkspace_Changed;
        MainTabs.Items.Insert(3, new TabItem
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
        if (_closeAfterRequest || !_document.IsDirty(RawJsonBox.Text)) return;
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
