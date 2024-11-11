using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Core;
using Core.Servicers.Interfaces;
using Infrastructure.Librarys;
using Infrastructure.Servicers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UI.ViewModels;
using UI.Views;

namespace UI.Servicers
{
    public class StatusBarIconServicer : IStatusBarIconServicer
    {
        private ContextMenu _contextMenu;

        private TrayIcon _trayIcon;


        private readonly IAppObserver _appObserver;
        private readonly IThemeServicer _themeServicer;
        private readonly MainViewModel _mainVM;
        private readonly IAppConfig _appConfig;
        private readonly IUIServicer _uIServicer;
        private MainWindow _mainWindow;

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
               
                _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://UI/Resources/Icons/{iconName}.ico")));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Logger.Save(true);
            }
        }

        private void InitMenu()
        {
            _contextMenu = new ContextMenu();
            var mainWindowMenuItem = new MenuItem();
            mainWindowMenuItem.Header = "打开";
            var exitMenuItem = new MenuItem();
            exitMenuItem.Header = "退出";
            exitMenuItem.Click += (s, e) =>
            {
                ExitApp();
            };
            _contextMenu.Items.Add(mainWindowMenuItem);
            _contextMenu.Items.Add(exitMenuItem);

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
            InitMenu();
            _trayIcon = new TrayIcon();
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
