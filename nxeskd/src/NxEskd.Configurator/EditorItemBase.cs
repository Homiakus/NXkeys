using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NxEskd.Configurator;

public abstract class EditorItemBase : INotifyPropertyChanged
{
    private readonly Action _changed;

    protected EditorItemBase(Action changed)
    {
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyAndChange([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}
