using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using Taix.Client.Controls;
using Taix.Client.Logging;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ShortcutService : IShortcutService
{
    private readonly IAppConfig _appConfig;
    private readonly INavigationService _navigationService;
    private readonly ISearchService _searchService;

    private Window? _window;
    private IDisposable? _configSubscription;

    public ShortcutService(
        IAppConfig appConfig,
        INavigationService navigationService,
        ISearchService searchService)
    {
        _appConfig = appConfig;
        _navigationService = navigationService;
        _searchService = searchService;
    }

    public void Attach(Window window)
    {
        _window = window;
        RebuildBindings();
        _configSubscription?.Dispose();
        _configSubscription = _appConfig.WhenShortcutsChanged(RebuildBindings);

        // 鼠标侧键回退，Tunnel 阶段挂载
        window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed,
            RoutingStrategies.Tunnel);
    }

    public void Detach()
    {
        _configSubscription?.Dispose();
        _configSubscription = null;
        if (_window != null)
        {
            _window.KeyBindings.Clear();
            _window.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _window = null;
        }
    }

    public Task TriggerRefreshAsync()
    {
        var vm = ResolveCurrentViewModel();
        if (vm == null) return Task.CompletedTask;
        return vm.RefreshAsync();
    }

    public Task TriggerSearchAsync() => _searchService.ShowAsync();

    public Task TriggerNavigateBackAsync()
    {
        _navigationService.GoBack();
        return Task.CompletedTask;
    }

    private ModelBase? ResolveCurrentViewModel()
    {
        if (_navigationService is MainWindowModel mwm && mwm.PageContainer is PageContainer pc)
        {
            return pc.CurrentViewModel;
        }
        return null;
    }

    private void RebuildBindings()
    {
        if (_window == null) return;

        _window.KeyBindings.Clear();

        var shortcut = _appConfig.GetConfig().Shortcut ?? new ShortcutModel();

        TryBind(shortcut.Refresh, ReactiveCommand.CreateFromTask(TriggerRefreshAsync));
        TryBind(shortcut.Search, ReactiveCommand.CreateFromTask(TriggerSearchAsync));
        TryBind(shortcut.NavigateBack, ReactiveCommand.CreateFromTask(TriggerNavigateBackAsync));
    }

    private void TryBind(string? gestureText, System.Windows.Input.ICommand command)
    {
        if (_window == null) return;
        if (string.IsNullOrWhiteSpace(gestureText)) return;

        try
        {
            var gesture = KeyGesture.Parse(gestureText);
            _window.KeyBindings.Add(new KeyBinding
            {
                Gesture = gesture,
                Command = command
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"无效快捷键 \"{gestureText}\"：{ex.Message}");
        }
    }

    // 鼠标后退键（XButton1）触发导航回退
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(_window).Properties;
        if (props.IsXButton1Pressed)
        {
            _navigationService.GoBack();
            e.Handled = true;
        }
    }
}
