using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace NxEskd.Configurator;

public partial class DocumentSettingsControl : UserControl
{
    private DocumentSettingsModel? _model;

    public DocumentSettingsControl()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingsChanged;

    public void Load(JsonObject root)
    {
        _model = new DocumentSettingsModel(root, OnChanged);
        DataContext = _model;
        _model.Refresh();
    }

    private void OnChanged()
        => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
