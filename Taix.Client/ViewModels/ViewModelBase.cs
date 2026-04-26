using System;
using System.Reactive.Disposables;
using ReactiveUI;

namespace Taix.Client.ViewModels;

public class ViewModelBase : ReactiveObject, IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    public virtual void Dispose()
    {
        Disposables.Dispose();
    }
}
