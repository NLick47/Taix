using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Controls.Select;
using Taix.Client.Events;
using Taix.Client.Foundation;
using Taix.Client.Foundation.Rx;
using Taix.Client.Logging;
using Taix.Client.Servicers.Interfaces;

namespace Taix.Client.Models;

public class ModelBase : UINotifyPropertyChanged, IDisposable
{
    private SelectItemModel _showType;
    private bool _isLoading;
    private int _loadingCount;
    private bool _isRestoringState;
    protected readonly CompositeDisposable Disposables = new();
    private readonly object _loadCtsGate = new();
    private readonly List<CancellationTokenSource> _retiredLoadCts = [];
    private CancellationTokenSource _loadCts = new();
    private bool _disposed;

    public ModelBase()
    {
        ShowType = ShowTypeOptions[0];
    }

    /// <summary>
    /// 状态恢复期间属性变化不触发副作用
    /// </summary>
    public bool IsRestoringState
    {
        get => _isRestoringState;
        set => _isRestoringState = value;
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 展示类型
    /// </summary>
    public SelectItemModel ShowType
    {
        get => _showType;
        set
        {
            _showType = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 展示类型选项
    /// </summary>
    public List<SelectItemModel> ShowTypeOptions { get; } =
    [
        new()
        {
            Id = 0,
            Name = ResourceStrings.App
        },
        new()
        {
            Id = 1,
            Name = ResourceStrings.Website
        }
    ];

    protected CancellationToken LoadToken
    {
        get
        {
            lock (_loadCtsGate) return _loadCts.Token;
        }
    }

    protected void CancelAndResetLoadToken()
    {
        CancellationTokenSource previous;
        lock (_loadCtsGate)
        {
            if (_disposed) return;
            previous = _loadCts;
            _loadCts = new CancellationTokenSource();
            _retiredLoadCts.Add(previous);
        }
        previous.Cancel();
    }

    protected async Task ExecuteAsync(Func<CancellationToken, Task> action, bool trackLoading = true)
    {
        if (trackLoading) Interlocked.Increment(ref _loadingCount);
        IsLoading = _loadingCount > 0;
        CancellationToken token;
        lock (_loadCtsGate)
        {
            if (_disposed)
            {
                if (trackLoading) Interlocked.Decrement(ref _loadingCount);
                IsLoading = _loadingCount > 0;
                return;
            }
            token = _loadCts.Token;
        }
        try
        {
            await action(token);
        }
        catch (OperationCanceledException)
        {
            // 取消异常是预期行为，忽略
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }
        finally
        {
            if (trackLoading) Interlocked.Decrement(ref _loadingCount);
            IsLoading = _loadingCount > 0;
        }
    }

    protected static IDisposable WhenPropertyChanged<TSource, TProperty>(
        TSource source,
        Expression<Func<TSource, TProperty>> property,
        Func<TProperty, Task> handler,
        bool skipInitial = true) where TSource : ModelBase
    {
        var observable = ObservablePropertyChangedExtensions.WhenPropertyChanged(source, property);
        if (skipInitial) observable = observable.Skip(1);

        return observable
            .Where(_ => !source.IsRestoringState)
            .ObserveOn(AvaloniaContextScheduler.Instance)
            .Do(_ => source.CancelAndResetLoadToken())
            .Select(value => RxObservable.FromAsync(async _ =>
            {
                try
                {
                    await handler(value);
                }
                catch (OperationCanceledException)
                {
                    // 取消异常是预期行为，忽略
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message, ex);
                }
            }))
            .Switch()
            .Subscribe()
            .DisposeWith(source.Disposables);
    }

    /// <summary>属性或数据变更后，在 UI 线程合并连续通知并取消上一轮刷新。</summary>
    protected void RefreshOnChange<T>(IObservable<T> source, Func<Task> refreshAsync)
    {
        SubscribeRefresh(source.Select(_ => Unit.Default), refreshAsync);
    }

    /// <summary>合并两个数据变更源，连续通知只触发一次刷新。</summary>
    protected void RefreshOnChange<TFirst, TSecond>(
        IObservable<TFirst> first,
        IObservable<TSecond> second,
        Func<Task> refreshAsync)
    {
        SubscribeRefresh(
            RxObservable.Merge(
                first.Select(_ => Unit.Default),
                second.Select(_ => Unit.Default)),
            refreshAsync);
    }

    private void SubscribeRefresh(IObservable<Unit> source, Func<Task> refreshAsync)
    {
        source
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ObserveOn(AvaloniaContextScheduler.Instance)
            .Do(_ => CancelAndResetLoadToken())
            .Select(_ => RxObservable.FromAsync(_ => refreshAsync()))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);
    }

    /// <summary>
    /// 返回导航时优先使用页面数据缓存；离开期间数据发生变化或缓存缺失时重新加载。
    /// </summary>
    protected Task RestoreCachedDataOrLoadAsync(
        bool restored,
        IStateService stateService,
        IAppEventService eventService,
        Func<Task> loadAsync)
    {
        var cachedVersion = stateService.Get<string, CachedDataVersion>(GetDataVersionKey());
        return restored && cachedVersion?.Version == eventService.ChangeVersion
            ? Task.CompletedTask
            : loadAsync();
    }

    /// <summary>记录当前页面数据缓存对应的全局数据版本。</summary>
    protected void SaveCachedDataVersion(IStateService stateService, IAppEventService eventService)
    {
        stateService.Set(GetDataVersionKey(), new CachedDataVersion(eventService.ChangeVersion));
    }

    private string GetDataVersionKey() => $"{GetType().FullName}:DataVersion";

    private sealed record CachedDataVersion(long Version);

    public virtual void OnNavigatedFrom()
    {
        CancelAndResetLoadToken();
    }

    public virtual Task OnNavigatedToAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task RefreshAsync()
    {
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        List<CancellationTokenSource> sources;
        lock (_loadCtsGate)
        {
            if (_disposed) return;
            _disposed = true;
            sources = [.. _retiredLoadCts, _loadCts];
            _retiredLoadCts.Clear();
        }
        foreach (var source in sources) source.Cancel();
        Disposables.Dispose();
        foreach (var source in sources) source.Dispose();
    }
}
