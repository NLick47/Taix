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

namespace Taix.Client.Models;

public class ModelBase : UINotifyPropertyChanged
{
    private SelectItemModel _showType;
    private bool _isLoading;
    private int _loadingCount;
    protected readonly CompositeDisposable Disposables = new();
    private CancellationTokenSource _loadCts = new();

    public ModelBase()
    {
        ShowType = ShowTypeOptions[0];
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
    /// /// </summary>
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
        catch (OperationCanceledException) when (capturedCts.Token.IsCancellationRequested)
        {
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
                var cts = source._loadCts;
                try
                {
                    await handler(value);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
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
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        CancelAndResetLoadToken();
        Disposables.Dispose();
    }
}
