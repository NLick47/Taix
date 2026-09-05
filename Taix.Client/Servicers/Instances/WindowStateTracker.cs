using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Taix.Client.Logging;
using Taix.Client.Platform;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public sealed class WindowStateTracker
{
    private const string LogScope = "[WindowState]";

    private readonly IWindowStateService _store;
    private readonly IAppConfig _config;
    private readonly IShutdownService _shutdownService;

    private Window? _window;
    private WindowSnapshot _restoreBounds;
    private PixelPoint? _expectedPlacement;
    private WindowStateKind? _pendingState;
    private int _stateAttempts;
    private bool _attached;

    public WindowStateTracker(
        IWindowStateService store,
        IAppConfig config,
        IShutdownService shutdownService)
    {
        _store = store;
        _config = config;
        _shutdownService = shutdownService;
    }

    public void Attach(Window window)
    {
        if (_attached) return;
        _attached = true;
        _window = window;

        if (_config.GetConfig().General.IsSaveWindowSize)
        {
            _store.LoadAsync();
            var saved = _store.Last;
            Logger.Info($"{LogScope} Attach: saved={(saved is { } sv ? Describe(sv) : "none")}");
            if (saved is { } s)
            {
                ApplyRestored(s);
            }
        }
        else
        {
            // 未开启记忆时回落居中启动，避免窗口停在默认的 (0,0)
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.Opened += OnOpened;
        window.Resized += (_, _) => CaptureRestoreBounds();
        window.PositionChanged += (_, _) => CaptureRestoreBounds();
        window.PropertyChanged += OnWindowPropertyChanged;

        _shutdownService.AddHandler(SaveAsync);
    }

    private void ApplyRestored(WindowSnapshot saved)
    {
        var window = _window!;
        if (!saved.IsValid) return;

        _restoreBounds = saved with { State = WindowStateKind.Normal };

        window.Width = saved.Width;
        window.Height = saved.Height;

        _expectedPlacement = ResolvePlacement(saved);
        if (_expectedPlacement is { } placement)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = placement;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        var restored = ResolveRestoreState(saved.State);
        Logger.Info($"{LogScope} ApplyRestored: saved={Describe(saved)} restored={restored} placement={(_expectedPlacement?.ToString() ?? "center")} apply={(restored == WindowStateKind.Normal ? "direct" : WindowStatePolicy.MustApplyStateAfterOpened ? "pending-after-opened" : "immediate")}");
        if (restored != WindowStateKind.Normal)
        {
            if (WindowStatePolicy.MustApplyStateAfterOpened)
            {
                _pendingState = restored;
            }
            else
            {
                window.WindowState = ToAvaloniaState(restored);
            }
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var window = _window!;

        if (_expectedPlacement is { } placement
            && window.WindowState == WindowState.Normal
            && !IsInsideAnyScreen(placement, window))
        {
            window.Position = CenteredPositionOf(window);
        }
        _expectedPlacement = null;

        CaptureRestoreBounds();
        Logger.Info($"{LogScope} OnOpened: state={window.WindowState} pos={window.Position} size={window.Width}x{window.Height} pending={(_pendingState?.ToString() ?? "none")}");
        LogScreens(window);

        if (_pendingState is { } state)
        {
            _pendingState = null;
            TrySetState(state);
        }
    }

    private void TrySetState(WindowStateKind kind)
    {
        var window = _window;
        if (window == null) return;

        if (!window.IsVisible)
        {
            Logger.Info($"{LogScope} SetState: 窗口不可见，延后重试 kind={kind}");
            Dispatcher.UIThread.Post(() => TrySetState(kind), DispatcherPriority.Loaded);
            return;
        }

        var target = ToAvaloniaState(kind);
        if (window.WindowState == target)
        {
            Logger.Info($"{LogScope} SetState: 已是 {kind}");
            return;
        }

        if (_stateAttempts >= 2)
        {
            Logger.Warn($"{LogScope} SetState: 重试 {_stateAttempts} 次仍未生效，放弃（当前 {window.WindowState}）");
            return;
        }

        _stateAttempts++;
        Logger.Info($"{LogScope} SetState: 尝试#{_stateAttempts} kind={kind} target={target}");
        try
        {
            window.WindowState = target;
        }
        catch (Exception ex)
        {
            Logger.Warn($"{LogScope} SetState: 设置抛异常 {ex.Message}");
        }
        Logger.Info($"{LogScope} SetState: 尝试#{_stateAttempts} 后实际={window.WindowState}");
        if (window.WindowState != target)
        {
            Logger.Warn($"{LogScope} SetState: 未生效，队列空闲时重试");
            Dispatcher.UIThread.Post(() => TrySetState(kind), DispatcherPriority.Loaded);
        }
    }

    private void LogScreens(Window window)
    {
        try
        {
            if (window.Screens is { } screens)
            {
                foreach (var s in screens.All)
                {
                    Logger.Info($"{LogScope} Screen: bounds={s.Bounds} working={s.WorkingArea} scale={s.Scaling}");
                }
            }
        }
        catch
        {
        }
    }

    private void CaptureRestoreBounds()
    {
        var window = _window;
        if (window == null || window.WindowState != WindowState.Normal) return;

        var position = window.Position;
        _restoreBounds = new WindowSnapshot(
            position.X, position.Y, window.Width, window.Height, WindowStateKind.Normal);
    }

    private Task SaveAsync()
    {
        var window = _window;
        if (window == null) return Task.CompletedTask;

        if (!_config.GetConfig().General.IsSaveWindowSize)
        {
            Logger.Info($"{LogScope} Save: 跳过（IsSaveWindowSize=false）");
            return Task.CompletedTask;
        }
        if (window.WindowState == WindowState.Minimized)
        {
            Logger.Info($"{LogScope} Save: 跳过（窗口最小化）");
            return Task.CompletedTask;
        }

        var kind = ToKind(window.WindowState);
        var snapshot = kind == WindowStateKind.Normal || !_restoreBounds.IsValid
            ? SnapshotOf(window, kind)
            : _restoreBounds with { State = kind };
        Logger.Info($"{LogScope} Save: state={window.WindowState} kind={kind} restoreBounds={(_restoreBounds.IsValid ? Describe(_restoreBounds) : "none")} -> {Describe(snapshot)}");

        return _store.SaveAsync(snapshot);
    }

    private static WindowSnapshot SnapshotOf(Window window, WindowStateKind kind)
    {
        var position = window.Position;
        return new WindowSnapshot(position.X, position.Y, window.Width, window.Height, kind);
    }

    private static WindowStateKind ResolveRestoreState(WindowStateKind saved) =>
        saved == WindowStateKind.FullScreen && !WindowStatePolicy.CanRestoreFullScreen
            ? WindowStateKind.Maximized
            : saved;

    private static WindowStateKind ToKind(WindowState state) => state switch
    {
        WindowState.Maximized => WindowStateKind.Maximized,
        WindowState.FullScreen => WindowStateKind.FullScreen,
        _ => WindowStateKind.Normal,
    };

    private static WindowState ToAvaloniaState(WindowStateKind kind) => kind switch
    {
        WindowStateKind.Maximized => WindowState.Maximized,
        WindowStateKind.FullScreen => WindowState.FullScreen,
        _ => WindowState.Normal,
    };

    private PixelPoint? ResolvePlacement(WindowSnapshot saved)
    {
        var window = _window!;
        if (saved.X is not { } x || saved.Y is not { } y) return null;

        var point = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
        return IsInsideAnyScreen(point, window) ? point : null;
    }

    private static bool IsInsideAnyScreen(PixelPoint point, Window window)
    {
        var screens = window.Screens;
        if (screens == null) return true; // 拿不到屏幕信息就信任原位置

        try
        {
            foreach (var screen in screens.All)
            {
                var b = screen.Bounds;
                if (point.X >= b.X && point.X < b.X + b.Width
                    && point.Y >= b.Y && point.Y < b.Y + b.Height)
                {
                    return true;
                }
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static PixelPoint CenteredPositionOf(Window window)
    {
        var screens = window.Screens;
        var screen = screens?.ScreenFromWindow(window) ?? screens?.Primary;
        if (screen == null) return new PixelPoint(100, 100);

        var scale = screen.Scaling > 0 ? screen.Scaling : 1.0;
        var area = screen.WorkingArea;
        var x = area.X + (int)Math.Max(0, (area.Width - window.Width * scale) / 2);
        var y = area.Y + (int)Math.Max(0, (area.Height - window.Height * scale) / 2);
        return new PixelPoint(x, y);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        Logger.Info($"{LogScope} WindowState 变化: {e.OldValue} -> {e.NewValue}");
    }

    private static string Describe(WindowSnapshot s) =>
        $"({(s.X is { } x ? x.ToString() : "-")},{(s.Y is { } y ? y.ToString() : "-")} {s.Width}x{s.Height} {s.State})";
}
