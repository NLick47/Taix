using System;

namespace Taix.Client.Foundation.Rx;

public static class Disposable
{
    public static readonly IDisposable Empty = new EmptyDisposable();

    private sealed class EmptyDisposable : IDisposable
    {
        public void Dispose() { }
    }

    public static IDisposable Create(Action dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        return new AnonymousDisposable(dispose);
    }

    private sealed class AnonymousDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        private readonly object _gate = new();

        public void Dispose()
        {
            Action? action;
            lock (_gate)
            {
                action = _dispose;
                _dispose = null;
            }
            action?.Invoke();
        }
    }
}
