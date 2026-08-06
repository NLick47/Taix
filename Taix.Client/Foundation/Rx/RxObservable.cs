using System;
using System.Threading;
using System.Threading.Tasks;

namespace Taix.Client.Foundation.Rx;

public static class RxObservable
{
    public static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe)
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        return new AnonymousObservable<T>(subscribe);
    }

    public static IObservable<Unit> FromAsync(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new FromAsyncObservable(action);
    }

    public static IObservable<T> Merge<T>(IObservable<T> first, IObservable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return new MergeObservable<T>(first, second);
    }

    // ---- 实现 ----

    private sealed class AnonymousObservable<T>(Func<IObserver<T>, IDisposable> subscribe) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            return subscribe(observer);
        }
    }

    private sealed class FromAsyncObservable(Func<CancellationToken, Task> action) : IObservable<Unit>
    {
        public IDisposable Subscribe(IObserver<Unit> observer)
        {
            var cts = new CancellationTokenSource();
            var gate = new object();
            var stopped = false;
            _ = RunAsync();

            async Task RunAsync()
            {
                Exception? error = null;
                try
                {
                    await action(cts.Token);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                lock (gate)
                {
                    if (stopped || cts.IsCancellationRequested) return;
                    stopped = true;
                    if (error != null) observer.OnError(error);
                    else
                    {
                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    }
                }
            }

            return Disposable.Create(() =>
            {
                lock (gate)
                {
                    if (stopped) return;
                    stopped = true;
                }
                cts.Cancel();
                cts.Dispose();
            });
        }
    }

    private sealed class MergeObservable<T>(IObservable<T> first, IObservable<T> second) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return new MergeSubscription(first, second, observer);
        }

        private sealed class MergeSubscription : IDisposable
        {
            private readonly object _gate = new();
            private readonly IObserver<T> _observer;
            private readonly CompositeDisposable _subscriptions = new();
            private int _completed;
            private bool _stopped;

            public MergeSubscription(IObservable<T> first, IObservable<T> second, IObserver<T> observer)
            {
                _observer = observer;
                _subscriptions.Add(first.Subscribe(new InnerObserver(this)));
                if (!_stopped) _subscriptions.Add(second.Subscribe(new InnerObserver(this)));
            }

            private void Next(T value)
            {
                lock (_gate)
                {
                    if (!_stopped) _observer.OnNext(value);
                }
            }

            private void Error(Exception error)
            {
                lock (_gate)
                {
                    if (_stopped) return;
                    _stopped = true;
                    _observer.OnError(error);
                }
                _subscriptions.Dispose();
            }

            private void Completed()
            {
                lock (_gate)
                {
                    if (_stopped || ++_completed != 2) return;
                    _stopped = true;
                    _observer.OnCompleted();
                }
            }

            public void Dispose()
            {
                lock (_gate) _stopped = true;
                _subscriptions.Dispose();
            }

            private sealed class InnerObserver(MergeSubscription owner) : IObserver<T>
            {
                public void OnNext(T value) => owner.Next(value);
                public void OnError(Exception error) => owner.Error(error);
                public void OnCompleted() => owner.Completed();
            }
        }
    }
}
