using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Taix.Client.Foundation.Rx;
using Taix.Client.Logging;

namespace Taix.Client.Foundation;

public sealed class AsyncRelayCommand : ICommand, IDisposable, INotifyPropertyChanged
{
    private readonly Action<object?>? _execute;
    private readonly Func<object?, Task>? _executeAsync;
    private Func<object?, bool>? _canExecute;
    private IDisposable? _canExecuteSubscription;
    private volatile bool _observableCanExecute = true;
    private int _isExecuting;

    public event EventHandler? CanExecuteChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private AsyncRelayCommand(Action<object?>? execute, Func<object?, Task>? executeAsync)
    {
        _execute = execute;
        _executeAsync = executeAsync;
    }

    public static AsyncRelayCommand Create(Action execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new AsyncRelayCommand(_ => execute(), null);
    }

    public static AsyncRelayCommand Create<T>(Action<T?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new AsyncRelayCommand(o => execute(o is T t ? t : default), null);
    }

    public static AsyncRelayCommand Create(Action<object?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new AsyncRelayCommand(execute, null);
    }

    public static AsyncRelayCommand CreateFromTask(Func<Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new AsyncRelayCommand(null, _ => execute());
    }

    public static AsyncRelayCommand CreateFromTask<T>(Func<T?, Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new AsyncRelayCommand(null, o => execute(o is T t ? t : default));
    }

    public static AsyncRelayCommand CreateFromTask(Func<object?, Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new AsyncRelayCommand(null, execute);
    }

    public static AsyncRelayCommand CreateFromTask<T>(Func<T?, Task> execute, Func<T?, bool> canExecute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(canExecute);
        return new AsyncRelayCommand(null, o => execute(o is T t ? t : default))
        {
            _canExecute = o => canExecute(o is T t ? t : default)
        };
    }

    public AsyncRelayCommand WithCanExecute(IObservable<bool> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _canExecuteSubscription?.Dispose();
        _canExecuteSubscription = source.Subscribe(value =>
            {
                _observableCanExecute = value;
                RaiseCanExecuteChanged();
            });
        return this;
    }

    public AsyncRelayCommand WithCanExecute(Func<object?, bool> canExecute)
    {
        ArgumentNullException.ThrowIfNull(canExecute);
        _canExecute = canExecute;
        return this;
    }


    public bool CanExecute(object? parameter) =>
        _isExecuting == 0 && _observableCanExecute && (_canExecute?.Invoke(parameter) ?? true);

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        if (_execute != null)
        {
            _execute(parameter);
        }
        else if (_executeAsync != null)
        {
            if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0) return;
            RaiseCanExecuteChanged();
            _ = ExecuteAsync(parameter);
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(IsExecuting));
    }

    private async Task ExecuteAsync(object? parameter)
    {
        try
        {
            await (_executeAsync?.Invoke(parameter) ?? Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an expected command outcome.
        }
        catch (Exception ex)
        {
            Logger.Error($"Async command execution failed: {ex.Message}", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            RaiseCanExecuteChanged();
        }
    }

    public bool IsExecuting => _isExecuting > 0;


    public void Dispose()
    {
        _canExecuteSubscription?.Dispose();
        _canExecuteSubscription = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
