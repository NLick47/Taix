using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Taix.Client.Controls.Select;
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
    private CancellationTokenSource _loadCts = new();

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

    protected CancellationToken LoadToken => _loadCts.Token;

    protected void CancelAndResetLoadToken()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
    }

    protected async Task ExecuteAsync(Func<CancellationToken, Task> action, bool trackLoading = true)
    {
        if (trackLoading) Interlocked.Increment(ref _loadingCount);
        IsLoading = _loadingCount > 0;
        var capturedCts = _loadCts;
        try
        {
            await action(capturedCts.Token);
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
        var observable = source.WhenAnyValue(property);
        if (skipInitial) observable = observable.Skip(1);

        return observable
            .ObserveOn(AvaloniaScheduler.Instance)
            .Do(_ => source.CancelAndResetLoadToken())
            .Select(value => Observable.FromAsync(async _ =>
            {
                // 状态恢复期间跳过执行
                if (source.IsRestoringState) return;

                var cts = source._loadCts;
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

    public virtual void OnNavigatedFrom()
    {
        CancelAndResetLoadToken();
    }

    public virtual Task OnNavigatedToAsync()
    {
        return TryRefreshIfNeededAsync();
    }

    /// <summary>
    /// 检测刷新标记，如有则调用 RefreshAsync
    /// </summary>
    protected async Task TryRefreshIfNeededAsync()
    {
        if (ServiceLocator.GetService<IStateService>() is { } stateService && stateService.HasState<string>("PageRefresh"))
        {
            stateService.Remove<string>("PageRefresh");
            await RefreshAsync();
        }
    }

    public virtual Task RefreshAsync()
    {
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        CancelAndResetLoadToken();
        Disposables.Dispose();
    }
}
