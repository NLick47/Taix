using System;
using System.Collections.Generic;

namespace Taix.Client.Foundation.Rx;

public sealed class Subject<T> : IObservable<T>
{
    private readonly object _gate = new();
    private readonly List<ObserverSubscription> _subscribers = [];
    private bool _isStopped;
    private Exception? _error;

    private sealed class ObserverSubscription(Subject<T> subject, IObserver<T> observer) : IDisposable
    {
        public IObserver<T> Observer { get; } = observer;
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            subject.Remove(this);
        }

        public void MarkDisposed() => IsDisposed = true;
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            if (!_isStopped)
            {
                var subscription = new ObserverSubscription(this, observer);
                _subscribers.Add(subscription);
                return subscription;
            }

            if (_error != null) observer.OnError(_error);
            else observer.OnCompleted();
        }
        return Disposable.Empty;
    }

    public void OnNext(T value)
    {
        lock (_gate)
        {
            if (_isStopped) return;
            foreach (var s in _subscribers.ToArray())
            {
                if (s.IsDisposed || !_subscribers.Contains(s)) continue;
                try
                {
                    s.Observer.OnNext(value);
                }
                catch
                {
                    // 订阅者异常不影响其他订阅者
                }
            }
        }
    }

    public void OnError(Exception error)
    {
        ObserverSubscription[] toNotify;
        lock (_gate)
        {
            if (_isStopped) return;
            _isStopped = true;
            _error = error;
            toNotify = [.. _subscribers];
            _subscribers.Clear();
        }
        foreach (var s in toNotify)
        {
            if (!s.IsDisposed)
            {
                try
                {
                    s.Observer.OnError(error);
                }
                catch
                {
                    // 忽略
                }
            }
        }
    }

    public void OnCompleted()
    {
        ObserverSubscription[] toNotify;
        lock (_gate)
        {
            if (_isStopped) return;
            _isStopped = true;
            toNotify = [.. _subscribers];
            _subscribers.Clear();
        }
        foreach (var s in toNotify)
        {
            if (!s.IsDisposed)
            {
                try
                {
                    s.Observer.OnCompleted();
                }
                catch
                {
                    // 忽略
                }
            }
        }
    }

    private void Remove(ObserverSubscription subscription)
    {
        lock (_gate)
        {
            if (subscription.IsDisposed) return;
            subscription.MarkDisposed();
            _subscribers.Remove(subscription);
        }
    }

    public IObservable<T> AsObservable() => this;
}
