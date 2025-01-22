using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Core;
using Core.Servicers.Interfaces;
using SharedLibrary.Librarys;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using UI.Controls.Window;
using UI.ViewModels;
using UI.Views;

namespace UI.Servicers
{
    public class StatusBarIconServicer : IStatusBarIconServicer
    {
        private NativeMenu _contextMenu;

        private static TrayIcon _trayIcon = new TrayIcon();

        private readonly IThemeServicer _themeServicer;
        private readonly MainViewModel _mainVM;
        private readonly IAppConfig _appConfig;
        private readonly IUIServicer _uIServicer;
        private MainWindow _mainWindow;

        public StatusBarIconServicer(IThemeServicer themeServicer_,
            MainViewModel mainVM_, MainWindow mainWindow_,
            IAppConfig appConfig_, IUIServicer uIServicer_)
        {
            this._themeServicer = themeServicer_;
            this._appConfig = appConfig_;
            this._uIServicer = uIServicer_;
            this._mainVM = mainVM_;
            this._mainWindow = mainWindow_;
        }


        public enum IconType
        {
            /// <summary>
            /// 正常
            /// </summary>
            Normal,
            /// <summary>
            /// 繁忙中
            /// </summary>
            Busy
        }


        private async Task SetIcon(IconType iconType_ = IconType.Normal)
        {

            try
            {
                string iconName = "tai32";
                switch (iconType_)
                {
                    case IconType.Busy:
                        iconName = "taibusy";
                        break;
                    default:
                        iconName = "tai32";
                        break;
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {

                    _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://Taix/Resources/Icons/{iconName}.ico")));
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
            _contextMenu = new();
            _trayIcon.Command = ReactiveCommand.Create(() =>
            {
                ShowMainWindow();
            });

            _contextMenu.Items.Add(new NativeMenuItem
            {
                Header = Application.Current.TryFindResource("Open", out var p) == null ? "打开" : p as string,
                Command = ReactiveCommand.Create(() =>
                {
                    ShowMainWindow();
                })

            });
            _contextMenu.Items.Add(new NativeMenuItem
            {
                Header = Application.Current.TryFindResource("Exit", out var e) == null ? "退出" : e as string,
                Command = ReactiveCommand.Create(() =>
                {
                    ExitApp();
                })
            });
            Dispatcher.UIThread.Invoke(() =>
            {
                _trayIcon.Menu = _contextMenu;
            });

        }

        private void ExitApp()
        {
            _trayIcon.IsVisible = false;
            App.Exit();
        }
        /// <summary>
        /// 等待程序加载
        /// </summary>
        private Task WatchStateAsync()
        {
            string previousText = string.Empty;
            return Task.Run(async () =>
            {
                while (AppState.IsLoading)
                {
                    await Task.Delay(500);
                    var newText = $"[{AppState.ProcessValue}%] Taix [{AppState.ActionText}]";
                    if (newText != previousText)
                    {
                        previousText = newText;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _trayIcon.ToolTipText = newText;
                        });
                    }
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _trayIcon.ToolTipText = "Taix!";
                });

                await SetIcon();
            });

        }

        public async Task Init()
        {
            await SetIcon(IconType.Busy);
            await WatchStateAsync();
            InitMenu();
        }

        private bool isInit = false;

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
            if (!existingWindow.IsVisible)
            {
                existingWindow.IsVisible = true;
            }

            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }
        }
    }
}
