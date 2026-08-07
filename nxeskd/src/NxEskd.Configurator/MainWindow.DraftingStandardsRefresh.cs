namespace NxEskd.Configurator;

public partial class MainWindow
{
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (_draftingStandardsAttached && _draftingStandards.IsVisible)
            _draftingStandards.Load(_document.Root);
    }
}
