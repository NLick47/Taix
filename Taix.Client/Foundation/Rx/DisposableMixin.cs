using System;

namespace Taix.Client.Foundation.Rx;

public static class DisposableMixin
{
    public static T DisposeWith<T>(this T item, CompositeDisposable compositeDisposable)
        where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(compositeDisposable);
        compositeDisposable.Add(item);
        return item;
    }
}
