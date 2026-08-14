using System.Windows;
using System.Windows.Controls;

namespace NxEskd.Configurator;

public partial class MainWindow
{
    private readonly DraftingStandardsControl _draftingStandards = new();
    private bool _draftingStandardsAttached;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += AttachDraftingStandardsWorkspace;
    }

    private void AttachDraftingStandardsWorkspace(object sender, RoutedEventArgs e)
    {
        if (_draftingStandardsAttached) return;
        _draftingStandardsAttached = true;
        _draftingStandards.StandardsChanged += TypedWorkspace_Changed;
        _draftingStandards.IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true) _draftingStandards.Load(_document.Root);
        };
        _draftingStandards.Load(_document.Root);

        var index = Math.Min(3, InputsSubTabs.Items.Count);
        InputsSubTabs.Items.Insert(index, new TabItem
        {
            Header = "Стандарты и шаблоны",
            Content = _draftingStandards
        });
    }
}
