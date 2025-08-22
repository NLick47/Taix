using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Core;
using Core.Servicers.Interfaces;
using ReactiveUI;
using SharedLibrary.Librarys;
using UI.ViewModels;
using UI.Views;

namespace UI.Servicers;

public class StatusBarIconServicer : IStatusBarIconServicer
{
    public enum IconType
    {
        /// <summary>
        ///     正常
        /// </summary>
        Normal,

        /// <summary>
        ///     繁忙中
        /// </summary>
        Busy
    }

    private static readonly TrayIcon _trayIcon = new();
    private readonly IAppConfig _appConfig;
    private readonly MainViewModel _mainVM;
    private readonly MainWindow _mainWindow;

    private readonly IThemeServicer _themeServicer;
    private readonly IUIServicer _uIServicer;
    private NativeMenu _contextMenu;

    private bool isInit;

    public StatusBarIconServicer(IThemeServicer themeServicer_,
        MainViewModel mainVM_, MainWindow mainWindow_,
        IAppConfig appConfig_, IUIServicer uIServicer_)
    {
        _themeServicer = themeServicer_;
        _appConfig = appConfig_;
        _uIServicer = uIServicer_;
        _mainVM = mainVM_;
        _mainWindow = mainWindow_;
    }

    public async Task Init()
    {
        SetIcon(IconType.Busy);
        await WatchStateAsync();
        InitMenu();
    }

    public void ShowMainWindow()
    {
        var desk = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var config = _appConfig.GetConfig();
        if (!isInit)
        {
            isInit = true;

            if (config.General.IsStartupShowMainWindow)
            {
                InitializeMainWindow();
                desk.MainWindow = _mainWindow;
            }

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


    private void SetIcon(IconType iconType_ = IconType.Normal)
    {
        try
        {
            var iconName = iconType_ switch
            {
                IconType.Busy => "taibusy",
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
            Logger.Error(ex.Message);
            Logger.Save(true);
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

    /// <summary>
    ///     等待程序加载
    /// </summary>
    private Task WatchStateAsync()
    {
        var previousText = string.Empty;
        return Task.Run(async () =>
        {
            while (AppState.IsLoading)
            {
                await Task.Delay(500);
                var newText = $"[{AppState.ProcessValue}%] Taix [{AppState.ActionText}]";
                if (newText != previousText)
                {
                    previousText = newText;
                    Dispatcher.UIThread.Invoke(() => { _trayIcon.ToolTipText = newText; });
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
#if DEBUG
                _trayIcon.ToolTipText = "Taix debug";
#elif !DEBUG
                     _trayIcon.ToolTipText = "Taix!";
#endif
            });

            SetIcon();
        });
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
        };
    }

    private void ShowExistingWindow(Window existingWindow)
    {
        if (!existingWindow.IsVisible) existingWindow.IsVisible = true;

        if (existingWindow.WindowState == WindowState.Minimized) existingWindow.WindowState = WindowState.Normal;
        existingWindow.Activate();
    }
}