using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Models.Config;
using ReactiveUI;
using UI.Views;

namespace UI.Servicers
{
    public class ThemeServicer : IThemeServicer
    {
        /// <summary>
        /// 当前主题名称
        /// </summary>
        private string themeName;
        private MainWindow mainWindow;
        private IReadOnlyList<IResourceProvider> MergedDictionaries;
        private readonly ThemeVariant[] themeOptions = { ThemeVariant.Default,ThemeVariant.Light,ThemeVariant.Dark};
        private readonly IAppConfig appConfig;

        public event EventHandler OnThemeChanged;


        public ThemeServicer(IAppConfig appConfig,MainWindow main)
        {
            this.appConfig = appConfig;
            this.mainWindow = main;
            appConfig.ConfigChanged += AppConfig_ConfigChanged;
            
        }

        private void AppConfig_ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
        {
            if (oldConfig.General.Theme != newConfig.General.Theme)
            {
                LoadTheme(themeOptions[newConfig.General.Theme]);
                OnThemeChanged?.Invoke(this, EventArgs.Empty);
            }

            if (oldConfig.General.ThemeColor != newConfig.General.ThemeColor)
            {
                LoadTheme(themeOptions[newConfig.General.Theme], true);
                OnThemeChanged?.Invoke(this, EventArgs.Empty);
            }

            if (oldConfig.General.IsSaveWindowSize != newConfig.General.IsSaveWindowSize)
            {
                HandleWindowSizeChangedEvent();
            }

        }
        
        private IDisposable? windowSizeSubscription;

        private void HandleWindowSizeChangedEvent()
        {
            if (mainWindow == null || mainWindow.IsWindowClosed) return;
            
            var config = appConfig.GetConfig();
            
            windowSizeSubscription?.Dispose();
            windowSizeSubscription = null;
            if (config.General.IsSaveWindowSize)
            {
                windowSizeSubscription = mainWindow.WhenAnyValue(x => x.Width, x => x.Height)
                    .Throttle(TimeSpan.FromMilliseconds(1000 * 3)) 
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(tuple =>
                    {
                        var (width, height) = tuple;
                        var config = appConfig.GetConfig();
                        if (config.General.IsSaveWindowSize)
                        {
                            config.General.WindowWidth = width;
                            config.General.WindowHeight = height;
                            appConfig.Save();
                        }
                    });
            }
        }

        
        public void Init()
        {
            var config = appConfig.GetConfig();
            LoadTheme(themeOptions[appConfig.GetConfig().General.Theme]);
            HandleWindowSizeChangedEvent();
        }

        public void LoadTheme(ThemeVariant theme, bool isRefresh = false)
        {
            mainWindow.RequestedThemeVariant = theme;
            UpdateThemeColor();
        }

        private void UpdateThemeColor()
        {

            var config = appConfig.GetConfig();
            if (string.IsNullOrEmpty(config.General.ThemeColor))
            {
                StateData.ThemeColor = Application.Current.Resources["ThemeColor"].ToString();
                return;
            }

            StateData.ThemeColor = config.General.ThemeColor;
            Application.Current.Resources["ThemeColor"] = Color.Parse(config.General.ThemeColor);
            Application.Current.Resources["ThemeBrush"] = UI.Base.Color.Colors.GetFromString(config.General.ThemeColor);
        }


        public void SetMainWindow(MainWindow mainWindow)
        {
           
        }
    }
}
