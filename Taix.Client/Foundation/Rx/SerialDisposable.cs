using System;

namespace Taix.Client.Foundation.Rx;

public sealed class SerialDisposable : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _current;

    public bool IsDisposed { get; private set; }

    public IDisposable? Disposable
    {
        get
        {
            lock (_gate) return _current;
        }
        set
        {
            IDisposable? toDispose;
            lock (_gate)
            {
                if (IsDisposed)
                {
                    toDispose = value;
                    _current = null;
                }
                else
                {
                    toDispose = _current;
                    _current = value;
                }
            }
            toDispose?.Dispose();
        }
    }

    public void Dispose()
    {
        IDisposable? toDispose;
        lock (_gate)
        {
            if (IsDisposed) return;
            IsDisposed = true;
            toDispose = _current;
            _current = null;
        }
        toDispose?.Dispose();
    }
}
