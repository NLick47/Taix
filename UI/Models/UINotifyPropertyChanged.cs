using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI.Models;

public class UINotifyPropertyChanged : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        var handler = PropertyChanged;
        if (handler != null) handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}