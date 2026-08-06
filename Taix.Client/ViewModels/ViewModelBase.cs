using System;
using Taix.Client.Foundation;
using Taix.Client.Foundation.Rx;
using Taix.Client.Models;

namespace Taix.Client.ViewModels;

public class ViewModelBase : UINotifyPropertyChanged, IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    public virtual void Dispose()
    {
        Disposables.Dispose();
    }
}
