using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Core.Models.Config;
using Core.Models.Data;
using Core.Servicers.Interfaces;
using ReactiveUI;
using SharedLibrary;
using SharedLibrary.Enums;
using SharedLibrary.Librarys;
using UI.Controls.Base;
using UI.Controls.Window;
using UI.Models;
using UI.Servicers;
using UI.Servicers.Updater;

namespace UI.ViewModels;

public class SettingPageViewModel : SettingPageModel
{
    private readonly IUIServicer _uiServicer;

    private readonly UpdateCheckerService _updateCheckerService;
    private readonly IWebData _webData;
    private readonly IAppConfig appConfig;
    private readonly IData data;
    private readonly MainViewModel mainVM;
    private ConfigModel config;

    public SettingPageViewModel(IAppConfig appConfig, MainViewModel mainVM, IData data, IWebData webData,
        IUIServicer uiServicer_, UpdateCheckerService updateCheckerService)
    {
        this.appConfig = appConfig;
        this.mainVM = mainVM;
        this.data = data;
        _webData = webData;
        _uiServicer = uiServicer_;
        _updateCheckerService = updateCheckerService;
        OpenURL = ReactiveCommand.Create<object>(OnOpenURL);
        DelDataCommand = ReactiveCommand.CreateFromTask<object>(OnDelData);
        ExportDataCommand = ReactiveCommand.CreateFromTask<object>(OnExportData);
        CheckUpdate = ReactiveCommand.CreateFromTask(OnCheckUpdate);
        appConfig.ConfigChanged += ConfigChanged;
        Init();
    }

    public ICommand OpenURL { get; set; }
    public ICommand CheckUpdate { get; set; }
    public ICommand DelDataCommand { get; set; }
    public ICommand ExportDataCommand { get; set; }

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

        TabbarData =
            [ResourceStrings.General, ResourceStrings.Behavior, ResourceStrings.Data, ResourceStrings.About];

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
            mainVM.Toast(Application.Current.Resources["TimeRangeSelectionError"] as string,
                ToastType.Error, IconTypes.IncidentTriangle);
            return;
        }

        var isConfirm = await _uiServicer.ShowConfirmDialogAsync(
            Application.Current.Resources["DeleteConfirmation"] as string,
            Application.Current.Resources["WantPerformAction"] as string);
        if (isConfirm)
        {
            await data.ClearRangeAsync(DelDataStartMonthDate, DelDataEndMonthDate);
            await _webData.ClearAsync(DelDataStartMonthDate, DelDataEndMonthDate);
            mainVM.Toast(Application.Current.Resources["OperationCompleted"] as string,
                ToastType.Success);
        }
    }

    private async Task OnExportData(object obj)
    {
        try
        {
            var desktop = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var storage = desktop.MainWindow.StorageProvider;
            var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (result?.Count != 0)
            {
                var folder = result[0];
                var options = new ExportOptions
                {
                    Website = new ExportOptions.WebsiteExportConfig
                    {
                        SheetName = "Sheet1",
                        Columns = new[]
                        {
                            ResourceStrings.Column7, ResourceStrings.Column8, ResourceStrings.Column9,
                            ResourceStrings.Column4
                        },
                        StatisticsLabel = ResourceStrings.WebsiteStatistics
                    },
                    Application = new ExportOptions.AppExportConfig
                    {
                        DailySheetName = ResourceStrings.ExportDaily,
                        TimePeriodSheetName = ResourceStrings.ExportTimePeriod,
                        DailyColumns = new[]
                        {
                            ResourceStrings.Column6, ResourceStrings.Column2, ResourceStrings.Column3,
                            ResourceStrings.Column4, ResourceStrings.Column5
                        },
                        TimePeriodColumns = new[]
                        {
                            ResourceStrings.Column1, ResourceStrings.Column2,
                            ResourceStrings.Column3, ResourceStrings.Column4, ResourceStrings.Column5
                        },
                        StatisticsLabel = ResourceStrings.AppliedStatistics
                    },
                    FileNamePrefix = "Taix",
                    UncategorizedLabel = ResourceStrings.Uncategorized,
                    Culture = SystemLanguage.CurrentCultureInfo
                };
                await data.ExportToExcelAsync(folder.Path.LocalPath, ExportDataStartMonthDate,
                    ExportDataEndMonthDate, options);
                await _webData.ExportAsync(folder.Path.LocalPath, ExportDataStartMonthDate, ExportDataEndMonthDate,
                    options);
                mainVM.Toast(Application.Current.Resources["DataExportCompleted"] as string,
                    ToastType.Success);
            }
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
            mainVM.Toast(Application.Current.Resources["DataExportFailed"] as string,
                ToastType.Error, IconTypes.IncidentTriangle);
        }
    }

    private Task OnCheckUpdate()
    {
        return _updateCheckerService.ManualCheckForUpdatesAsync();
    }

    private void OnOpenURL(object obj)
    {
        Process.Start(new ProcessStartInfo(obj.ToString()) { UseShellExecute = true });
    }

    private void SettingPageVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
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
                //  常规
                Data = config.General;
            else if (TabbarSelectedIndex == 1)
                //  行为
                Data = config.Behavior;
        }
    }
}