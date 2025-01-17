using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Core.Enums;
using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using Infrastructure.Librarys;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Models;
using UI.Servicers;
using UI.Servicers.Dialogs;


namespace UI.ViewModels
{
    public class SettingPageViewModel : SettingPageModel
    {
        private ConfigModel config;
        private readonly IAppConfig appConfig;
        private readonly MainViewModel mainVM;
        private readonly IData data;
        private readonly IWebData _webData;
        private readonly IUIServicer _uiServicer;
        public ICommand OpenURL { get; set; }
        public ICommand CheckUpdate { get; set; }
        public ICommand DelDataCommand { get; set; }
        public ICommand ExportDataCommand { get; set; }

        public SettingPageViewModel(IAppConfig appConfig, MainViewModel mainVM, IData data, IWebData webData, IUIServicer uiServicer_)
        {
            this.appConfig = appConfig;
            this.mainVM = mainVM;
            this.data = data;
            _webData = webData;
            _uiServicer = uiServicer_;

            OpenURL = ReactiveCommand.Create<object>(OnOpenURL);
            DelDataCommand = ReactiveCommand.CreateFromTask<object>(OnDelData);
            ExportDataCommand = ReactiveCommand.CreateFromTask<object>(OnExportData);
            appConfig.ConfigChanged += ConfigChanged; 
            Init();
        }

        private void ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
        {
            SystemLanguage.CurrentLanguage = (CultureCode)newConfig.General.Language;
            TabbarData[0] = ResourceStrings.General;
            TabbarData[1] = ResourceStrings.Behavior;
            TabbarData[2] = ResourceStrings.Data;
            TabbarData[3] = ResourceStrings.About;
        }

       

        private void Init()
        {
            config = appConfig.GetConfig();

            Data = config.General;

            TabbarData = [ResourceStrings.General, ResourceStrings.Behavior, ResourceStrings.Data, ResourceStrings.About];

            PropertyChanged += SettingPageVM_PropertyChanged;

            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            DelDataStartMonthDate = DateTime.Now;
            DelDataEndMonthDate = DateTime.Now;

            ExportDataStartMonthDate = DateTime.Now;
            ExportDataEndMonthDate = DateTime.Now;
        }

        private async Task OnDelData(object obj)
        {
            if (DelDataStartMonthDate > DelDataEndMonthDate)
            {
                mainVM.Toast(Application.Current.Resources["TimeRangeSelectionError"] as string, Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
                return;
            }

            bool isConfirm = await _uiServicer.ShowConfirmDialogAsync(Application.Current.Resources["DeleteConfirmation"] as string,
                Application.Current.Resources["WantPerformAction"] as string);
            if (isConfirm)
            {
                await data.ClearRangeAsync(DelDataStartMonthDate, DelDataEndMonthDate);
                await _webData.ClearAsync(DelDataStartMonthDate, DelDataEndMonthDate);
                mainVM.Toast(Application.Current.Resources["OperationCompleted"] as string, Controls.Window.ToastType.Success);
            }
        }

        private async Task OnExportData(object obj)
        {
            try
            {
                var desktop = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var storage = desktop.MainWindow.StorageProvider;
                var result = await storage.OpenFolderPickerAsync(new()
                {

                });
                if (result?.Count != 0)
                {
                    var folder = result[0];
                    await data.ExportToExcelAsync(folder.Path.LocalPath, ExportDataStartMonthDate, ExportDataEndMonthDate);
                    await _webData.ExportAsync(folder.Path.LocalPath, ExportDataStartMonthDate, ExportDataEndMonthDate);
                    mainVM.Toast(Application.Current.Resources["DataExportCompleted"] as string, Controls.Window.ToastType.Success);
                }
            }
            catch (Exception ec)
            {
                Logger.Error(ec.ToString());
                mainVM.Toast(Application.Current.Resources["DataExportFailed"] as string, Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
            }
        }

        private void OnOpenURL(object obj)
        {
            Process.Start(new ProcessStartInfo(obj.ToString()) { UseShellExecute = true });
        }

        private void SettingPageVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Data))
            {
                if (TabbarSelectedIndex == 0 && Data is GeneralModel general)
                {
                    config.General = general;
                    appConfig.Save();
                }
                else if (TabbarSelectedIndex == 1 && Data is BehaviorModel behavior)
                {
                    config.Behavior = behavior;
                    appConfig.Save();
                }
            }

            if (e.PropertyName == nameof(TabbarSelectedIndex))
            {
                if (TabbarSelectedIndex == 0)
                {
                    //  常规
                    Data = config.General;
                }
                else if (TabbarSelectedIndex == 1)
                {
                    //  行为
                    Data = config.Behavior;
                }
            }
        }
    }
}
