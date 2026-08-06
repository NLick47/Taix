using System;
using System.Collections.Generic;

namespace Taix.Client.Foundation.Rx;

public sealed class CompositeDisposable : IDisposable
{
    private readonly object _gate = new();
    private List<IDisposable>? _disposables;

    public CompositeDisposable()
    {
        _disposables = [];
    }

    public int Count
    {
        get
        {
            lock (_gate) return _disposables?.Count ?? 0;
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (_gate) return _disposables is null;
        }
    }

    public void Add(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var disposeNow = false;
        lock (_gate)
        {
            if (_disposables is null)
            {
                disposeNow = true;
            }
            else
            {
                _disposables.Add(item);
            }
        }
        if (disposeNow) item.Dispose();
    }

    public bool Remove(IDisposable item)
    {
        var removed = false;
        lock (_gate)
        {
            if (_disposables is null) return false;
            removed = _disposables.Remove(item);
        }
        if (removed) item.Dispose();
        return removed;
    }

    public void Clear()
    {
        List<IDisposable>? toDispose;
        lock (_gate)
        {
            if (_disposables is null) return;
            toDispose = _disposables;
            _disposables = [];
        }
        DisposeItems(toDispose);
    }

    public bool Contains(IDisposable item)
    {
        lock (_gate)
        {
            return _disposables?.Contains(item) ?? false;
        }
    }

    public void Dispose()
    {
        List<IDisposable>? toDispose;
        lock (_gate)
        {
            toDispose = _disposables;
            _disposables = null;
        }
        DisposeItems(toDispose);
    }

    private static void DisposeItems(List<IDisposable>? items)
    {
        if (items is null) return;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            try
            {
                items[i].Dispose();
            }
            catch
            {
                // 单个项释放失败不影响其余项
            }
        }
    }
}
