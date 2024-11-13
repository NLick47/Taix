using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Core;
using Core.Servicers.Interfaces;
using Infrastructure.Librarys;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using UI.ViewModels;
using UI.Views;

namespace UI.Servicers
{
    public class StatusBarIconServicer : IStatusBarIconServicer
    {
        private NativeMenu _contextMenu;

        private TrayIcon _trayIcon;

        private readonly IThemeServicer _themeServicer;
        private readonly MainViewModel _mainVM;
        private readonly IAppConfig _appConfig;
        private readonly IUIServicer _uIServicer;
        private MainWindow _mainWindow;

        public StatusBarIconServicer(IThemeServicer themeServicer_, 
            MainViewModel mainVM_,
            IAppConfig appConfig_, IUIServicer uIServicer_)
        {
            this._themeServicer = themeServicer_;
            this._appConfig = appConfig_;
            this._uIServicer = uIServicer_;
            this._mainVM = mainVM_;
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


        private void SetIcon(IconType iconType_ = IconType.Normal)
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
                Dispatcher.UIThread.Invoke(() =>
                {
                    _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://UI/Resources/Icons/{iconName}.ico")));
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
            _contextMenu = new ();
            _contextMenu.Items.Add(new NativeMenuItem
            {
                Header = Application.Current.TryFindResource("Open",out var p) == null ? "打开" : p as string,
                Command = ReactiveCommand.Create(() =>
                {
                    if (_mainWindow.IsVisible)
                    {
                        _mainWindow.Activate();
                    }
                    else
                    {
                        _mainWindow.Show();
                    }
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
            _trayIcon.Menu = _contextMenu;
        }

        private void ExitApp()
        {
            _trayIcon.IsVisible = false;
            App.Exit();
        }

        /// <summary>
        /// 等待程序加载
        /// </summary>
        private async void WatchStateAsync()
        {
            await Task.Run(() =>
            {
                while (AppState.IsLoading)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        _trayIcon.ToolTipText = $"[{AppState.ProcessValue}%] Tai [{AppState.ActionText}]";
                    });
                }
                Dispatcher.UIThread.Invoke(() =>
                {
                    _trayIcon.ToolTipText = "Tai!";
                });
                SetIcon();
            });
        }

        public void Init()
        {
            _trayIcon = new TrayIcon();
            InitMenu();
            SetIcon(IconType.Busy);
            WatchStateAsync();
        }

        public void ShowMainWindow()
        {
            var config = _appConfig.GetConfig();
            if (config == null)
            {
                return;
            }
            if (_mainWindow == null || _mainWindow.IsWindowClosed)
            {
                _mainWindow = new MainWindow();
                _mainWindow.DataContext = _mainVM;
                _mainWindow.Loaded += _mainWindow_Loaded;
                _mainVM.LoadDefaultPage();
            }

            _mainWindow.Show();
            _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Show();
            _mainWindow.Activate();
            _uIServicer.InitWindow(_mainWindow);
        }

        private void _mainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var window = sender as MainWindow;
            _themeServicer.SetMainWindow(window);
        }
    }
}
