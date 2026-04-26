using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using Taix.Client.Logging;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class StatusBarIconServicer : IStatusBarIconServicer
{
    public enum IconType
    {
        Normal
    }

    private static readonly TrayIcon _trayIcon = new();
    private readonly IAppConfig _appConfig;
    private readonly MainViewModel _mainVM;
    private readonly MainWindow _mainWindow;

    private readonly IThemeServicer _themeServicer;
    private readonly IUIServicer _uIServicer;
    private NativeMenu _contextMenu;

    private bool isInit;

    public StatusBarIconServicer(IThemeServicer themeServicer,
        MainViewModel mainVM, MainWindow mainWindow,
        IAppConfig appConfig, IUIServicer uiServicer)
    {
        _themeServicer = themeServicer;
        _appConfig = appConfig;
        _uIServicer = uiServicer;
        _mainVM = mainVM;
        _mainWindow = mainWindow;
    }

    public void Init()
    {
        SetIcon(IconType.Normal);
        InitMenu();
    }

    public void ShowMainWindow()
    {
        var desk = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (!isInit)
        {
            isInit = true;
            InitializeMainWindow();
            desk.MainWindow = _mainWindow;
            return;
        }

        if (desk.MainWindow == null)
        {
            InitializeMainWindow();
            desk.MainWindow = _mainWindow;
        }
        else
        {
            ShowExistingWindow(desk.MainWindow);
        }
    }


    private void SetIcon(IconType iconType = IconType.Normal)
    {
        try
        {
            var iconName = iconType switch
            {
                IconType.Normal => "tai32",
                _ => "tai32"
            };
            Dispatcher.UIThread.Invoke(() =>
            {
                _trayIcon.Icon =
                    new WindowIcon(AssetLoader.Open(new Uri($"avares://Taix/Resources/Icons/{iconName}.ico")));
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            Logger.Flush();
        }
    }

    private void InitMenu()
    {
        _contextMenu = new NativeMenu();
        _trayIcon.Command = ReactiveCommand.Create(() => { ShowMainWindow(); });

        _contextMenu.Items.Add(new NativeMenuItem
        {
            Header = Application.Current.FindResource("Open") as string,
            Command = ReactiveCommand.Create(() => { ShowMainWindow(); })
        });
        _contextMenu.Items.Add(new NativeMenuItem
        {
            Header = Application.Current.FindResource("Exit") as string,
            Command = ReactiveCommand.Create(() => { ExitApp(); })
        });
        Dispatcher.UIThread.Invoke(() => { _trayIcon.Menu = _contextMenu; });
    }

    private void ExitApp()
    {
        _trayIcon.IsVisible = false;
        App.Exit();
    }

  
    private void InitializeMainWindow()
    {
        var config = _appConfig.GetConfig();

        if (isInit && config.General.IsSaveWindowSize)
        {
            _mainWindow.Width = config.General.WindowWidth;
            _mainWindow.Height = config.General.WindowHeight;
        }

        _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.DataContext = _mainVM;
        _mainWindow.IsVisible = true;
        _mainWindow.Closing += (s, e) =>
        {
            e.Cancel = true;
            _mainWindow.IsVisible = false;

            var cfg = _appConfig.GetConfig();
            if (cfg.General.IsSaveWindowSize)
            {
                cfg.General.WindowWidth = _mainWindow.Width;
                cfg.General.WindowHeight = _mainWindow.Height;
                _ = _appConfig.SaveAsync();
            }
        };
    }

    private void ShowExistingWindow(Window existingWindow)
    {
        if (!existingWindow.IsVisible) existingWindow.IsVisible = true;

        if (existingWindow.WindowState == WindowState.Minimized) existingWindow.WindowState = WindowState.Normal;
        existingWindow.Activate();
    }
}