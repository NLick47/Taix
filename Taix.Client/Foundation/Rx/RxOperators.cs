using System;
using System.Collections.Generic;
using System.Threading;
using Taix.Client.Logging;

namespace Taix.Client.Foundation.Rx;

public static class RxOperators
{
    public static IDisposable Subscribe<T>(this IObservable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Subscribe(new AnonymousObserver<T>(_ => { }, LogUnhandledError, () => { }));
    }

    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return source.Subscribe(new AnonymousObserver<T>(onNext, LogUnhandledError, () => { }));
    }

    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        ArgumentNullException.ThrowIfNull(onError);
        return source.Subscribe(new AnonymousObserver<T>(onNext, onError, () => { }));
    }

    private static void LogUnhandledError(Exception error) =>
        Logger.Error($"Observable sequence failed: {error.Message}", error);

    // ---- 变换 ----

    public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new WhereObservable<T>(source, predicate);
    }

    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new SelectObservable<TSource, TResult>(source, selector);
    }

    public static IObservable<T> Do<T>(this IObservable<T> source, Action<T> onNext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);
        return new DoObservable<T>(source, onNext);
    }

    public static IObservable<T> Skip<T>(this IObservable<T> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return new SkipObservable<T>(source, count);
    }

    public static IObservable<T> StartWith<T>(this IObservable<T> source, T value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new StartWithObservable<T>(source, value);
    }

    public static IObservable<T> DistinctUntilChanged<T>(this IObservable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DistinctUntilChangedObservable<T>(source);
    }

    public static IObservable<T> Throttle<T>(this IObservable<T> source, TimeSpan dueTime)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ThrottleObservable<T>(source, dueTime);
    }

    public static IObservable<T> Switch<T>(this IObservable<IObservable<T>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return new SwitchObservable<T>(sources);
    }

    public static IObservable<T> ObserveOn<T>(this IObservable<T> source, IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scheduler);
        return new ObserveOnObservable<T>(source, scheduler);
    }

    // ---- 简单转发实现 ----

    private sealed class ForwardingObservable<T>(
        IObservable<T> source,
        Func<IObserver<T>, IObserver<T>> observerFactory) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var downstream = observerFactory(observer);
            return source.Subscribe(downstream);
        }
    }

    private sealed class WhereObservable<T>(IObservable<T> source, Func<T, bool> predicate) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var worker = new WhereObserver(observer, predicate);
            worker.SetUpstream(source.Subscribe(worker));
            return worker;
        }

        private sealed class WhereObserver(IObserver<T> downstream, Func<T, bool> predicate) : IObserver<T>, IDisposable
        {
            private readonly SerialDisposable _upstream = new();
            private bool _stopped;

            public void SetUpstream(IDisposable upstream) => _upstream.Disposable = upstream;

            public void OnNext(T value)
            {
                if (_stopped) return;
                bool matches;
                try
                {
                    matches = predicate(value);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                    return;
                }
                if (matches) downstream.OnNext(value);
            }

            public void OnError(Exception error)
            {
                if (_stopped) return;
                _stopped = true;
                downstream.OnError(error);
                _upstream.Dispose();
            }

            public void OnCompleted()
            {
                if (_stopped) return;
                _stopped = true;
                downstream.OnCompleted();
                _upstream.Dispose();
            }

            public void Dispose()
            {
                _stopped = true;
                _upstream.Dispose();
            }
        }
    }

    private sealed class SelectObservable<TSource, TResult>(
        IObservable<TSource> source, Func<TSource, TResult> selector) : IObservable<TResult>
    {
        public IDisposable Subscribe(IObserver<TResult> observer)
        {
            var worker = new SelectObserver(observer, selector);
            worker.SetUpstream(source.Subscribe(worker));
            return worker;
        }

        private sealed class SelectObserver(IObserver<TResult> downstream, Func<TSource, TResult> selector)
            : IObserver<TSource>, IDisposable
        {
            private readonly SerialDisposable _upstream = new();
            private bool _stopped;

            public void SetUpstream(IDisposable upstream) => _upstream.Disposable = upstream;

            public void OnNext(TSource value)
            {
                if (_stopped) return;
                TResult result;
                try
                {
                    result = selector(value);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                    return;
                }
                downstream.OnNext(result);
            }

            public void OnError(Exception error)
            {
                if (_stopped) return;
                _stopped = true;
                downstream.OnError(error);
                _upstream.Dispose();
            }

            public void OnCompleted()
            {
                if (_stopped) return;
                _stopped = true;
                downstream.OnCompleted();
                _upstream.Dispose();
            }

            public void Dispose()
            {
                _stopped = true;
                _upstream.Dispose();
            }
        }
    }

    private sealed class DoObservable<T>(IObservable<T> source, Action<T> onNext) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var worker = new DoObserver(observer, onNext);
            worker.SetUpstream(source.Subscribe(worker));
            return worker;
        }

        private sealed class DoObserver(IObserver<T> downstream, Action<T> onNext) : IObserver<T>, IDisposable
        {
            private readonly SerialDisposable _upstream = new();
            private bool _stopped;

            public void SetUpstream(IDisposable upstream) => _upstream.Disposable = upstream;

            public void OnNext(T value)
            {
                if (_stopped) return;
                try
                {
                    onNext(value);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                    return;
                }
                downstream.OnNext(value);
            }

            public void OnError(Exception error)
            {
                if (_stopped) return;
                _stopped = true;
                downstream.OnError(error);
                _upstream.Dispose();
            }

            public void OnCompleted()
            {
                if (_stopped) return;
                _stopped = true;
                downstream.OnCompleted();
                _upstream.Dispose();
            }

            public void Dispose()
            {
                _stopped = true;
                _upstream.Dispose();
            }
        }
    }

    private sealed class SkipObservable<T>(IObservable<T> source, int count) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return source.Subscribe(new SkipObserver(observer, count));
        }

        private sealed class SkipObserver(IObserver<T> downstream, int count) : IObserver<T>
        {
            private int _remaining = count;

            public void OnNext(T value)
            {
                if (_remaining > 0)
                {
                    _remaining--;
                    return;
                }
                downstream.OnNext(value);
            }

            public void OnError(Exception error) => downstream.OnError(error);
            public void OnCompleted() => downstream.OnCompleted();
        }
    }

    private sealed class StartWithObservable<T>(IObservable<T> source, T value) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            return source.Subscribe(observer);
        }
    }

    private sealed class DistinctUntilChangedObservable<T>(IObservable<T> source) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return source.Subscribe(new DistinctUntilChangedObserver(observer));
        }

        private sealed class DistinctUntilChangedObserver(IObserver<T> downstream) : IObserver<T>
        {
            private T? _last;
            private bool _hasLast;

            public void OnNext(T value)
            {
                if (_hasLast && EqualityComparer<T>.Default.Equals(_last, value)) return;
                _last = value;
                _hasLast = true;
                downstream.OnNext(value);
            }

            public void OnError(Exception error) => downstream.OnError(error);
            public void OnCompleted() => downstream.OnCompleted();
        }
    }

    // ---- Throttle ----

    private sealed class ThrottleObservable<T>(IObservable<T> source, TimeSpan dueTime) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var worker = new ThrottleObserver(observer, dueTime);
            var upstream = source.Subscribe(worker);
            var disposables = new CompositeDisposable();
            disposables.Add(worker);
            disposables.Add(upstream);
            return disposables;
        }

        private sealed class ThrottleObserver(IObserver<T> downstream, TimeSpan dueTime)
            : IObserver<T>, IDisposable
        {
            private readonly object _gate = new();
            private Timer? _timer;
            private T? _latest;
            private bool _hasValue;
            private bool _disposed;
            private long _generation;

            public void OnNext(T value)
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _latest = value;
                    _hasValue = true;
                    _timer?.Dispose();
                    var generation = ++_generation;
                    _timer = new Timer(_ => Emit(generation), null, dueTime, Timeout.InfiniteTimeSpan);
                }
            }

            private void Emit(long generation)
            {
                lock (_gate)
                {
                    if (_disposed || !_hasValue || generation != _generation) return;
                    var value = _latest!;
                    _hasValue = false;
                    _timer?.Dispose();
                    _timer = null;
                    downstream.OnNext(value);
                }
            }

            public void OnError(Exception error)
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    _timer?.Dispose();
                    _timer = null;
                }
                downstream.OnError(error);
            }

            public void OnCompleted()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    if (_hasValue)
                    {
                        _hasValue = false;
                        downstream.OnNext(_latest!);
                    }
                    _disposed = true;
                    _timer?.Dispose();
                    _timer = null;
                    downstream.OnCompleted();
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    _timer?.Dispose();
                    _timer = null;
                }
            }
        }
    }

    // ---- Switch ----

    private sealed class SwitchObservable<T>(IObservable<IObservable<T>> sources) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var worker = new SwitchObserver(observer);
            worker.SetUpstream(sources.Subscribe(worker));
            return worker;
        }

        private sealed class SwitchObserver(IObserver<T> downstream)
            : IObserver<IObservable<T>>, IDisposable
        {
            private readonly object _gate = new();
            private readonly SerialDisposable _innerSubscription = new();
            private readonly SerialDisposable _upstream = new();
            private bool _disposed;
            private bool _outerCompleted;
            private bool _hasInner;
            private long _generation;

            public void SetUpstream(IDisposable upstream) => _upstream.Disposable = upstream;

            public void OnNext(IObservable<T> inner)
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _hasInner = true;
                    var generation = ++_generation;
                    _innerSubscription.Disposable = inner.Subscribe(new SwitchInnerObserver(this, generation));
                }
            }

            private void InnerNext(long generation, T value)
            {
                lock (_gate)
                {
                    if (!_disposed && generation == _generation) downstream.OnNext(value);
                }
            }

            private void InnerError(long generation, Exception error)
            {
                lock (_gate)
                {
                    if (_disposed || generation != _generation) return;
                    _disposed = true;
                    downstream.OnError(error);
                }
                _innerSubscription.Dispose();
                _upstream.Dispose();
            }

            private void InnerCompleted(long generation)
            {
                lock (_gate)
                {
                    if (_disposed || generation != _generation) return;
                    _hasInner = false;
                    if (!_outerCompleted) return;
                    _disposed = true;
                    downstream.OnCompleted();
                }
            }

            private sealed class SwitchInnerObserver(SwitchObserver owner, long generation) : IObserver<T>
            {
                public void OnNext(T value) => owner.InnerNext(generation, value);
                public void OnError(Exception error) => owner.InnerError(generation, error);
                public void OnCompleted() => owner.InnerCompleted(generation);
            }

            public void OnError(Exception error)
            {
                Dispose();
                downstream.OnError(error);
            }

            public void OnCompleted()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _outerCompleted = true;
                    if (_hasInner) return;
                    _disposed = true;
                    downstream.OnCompleted();
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    _innerSubscription.Dispose();
                    _upstream.Dispose();
                }
            }
        }
    }

    // ---- ObserveOn ----

    private sealed class ObserveOnObservable<T>(IObservable<T> source, IScheduler scheduler) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var worker = new ObserveOnObserver(observer, scheduler);
            var upstream = source.Subscribe(worker);
            var disposables = new CompositeDisposable();
            disposables.Add(worker);
            disposables.Add(upstream);
            return disposables;
        }

        private sealed class ObserveOnObserver(IObserver<T> downstream, IScheduler scheduler)
            : IObserver<T>, IDisposable
        {
            private readonly object _gate = new();
            private readonly Queue<Action> _queue = new();
            private bool _scheduled;
            private bool _disposed;

            public void OnNext(T value)
            {
                Enqueue(() => downstream.OnNext(value));
            }

            public void OnError(Exception error)
            {
                Enqueue(() => downstream.OnError(error));
            }

            public void OnCompleted()
            {
                Enqueue(() => downstream.OnCompleted());
            }

            private void Enqueue(Action item)
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _queue.Enqueue(item);
                    if (_scheduled) return;
                    _scheduled = true;
                    scheduler.Schedule(Drain);
                }
            }

            private void Drain()
            {
                try
                {
                    while (true)
                    {
                        Action item;
                        lock (_gate)
                        {
                            if (_disposed || _queue.Count == 0)
                            {
                                _scheduled = false;
                                return;
                            }
                            item = _queue.Dequeue();
                        }
                        item();
                    }
                }
                catch (Exception ex)
                {
                    lock (_gate)
                    {
                        _disposed = true;
                        _scheduled = false;
                        _queue.Clear();
                    }
                    Logger.Error($"Observable callback failed: {ex.Message}", ex);
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    _queue.Clear();
                }
            }
        }
    }
}
