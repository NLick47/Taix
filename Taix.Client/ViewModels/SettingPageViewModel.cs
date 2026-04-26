using System;
using System.Reflection;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Window;
using Taix.Client.Logging;
using Taix.Client.Models;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class SettingPageViewModel : SettingPageModel
{
    private readonly IDialogService _dialogService;
    private readonly UpdateCheckerService _updateCheckerService;
    private readonly IWebData _webDataService;
    private readonly IAppConfig _appConfig;
    private readonly IData _dataService;
    private readonly IProcessService _processService;
    private readonly IToastService _toastService;
    private readonly ConfigModel _config;

    public SettingPageViewModel(
        IAppConfig appConfig,
        IToastService toastService,
        IData data,
        IWebData webData,
        IDialogService dialogService,
        IProcessService processService,
        UpdateCheckerService updateCheckerService)
    {
        _appConfig = appConfig;
        _toastService = toastService;
        _dataService = data;
        _webDataService = webData;
        _dialogService = dialogService;
        _processService = processService;
        _updateCheckerService = updateCheckerService;
        _config = appConfig.GetConfig();

        OpenURL = ReactiveCommand.Create<object>(OnOpenURL).DisposeWith(Disposables);
        DelDataCommand = ReactiveCommand.CreateFromTask<object>(OnDelDataAsync).DisposeWith(Disposables);
        ExportDataCommand = ReactiveCommand.CreateFromTask<object>(OnExportDataAsync).DisposeWith(Disposables);
        CheckUpdate = ReactiveCommand.CreateFromTask(OnCheckUpdateAsync).DisposeWith(Disposables);

        appConfig.ConfigChanged += OnConfigChanged;

        Initialize();
    }

    public ReactiveCommand<object, Unit> OpenURL { get; }
    public ReactiveCommand<Unit, Unit> CheckUpdate { get; }
    public ReactiveCommand<object, Unit> DelDataCommand { get; }
    public ReactiveCommand<object, Unit> ExportDataCommand { get; }

    private void OnConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
    {
        SystemLanguage.CurrentLanguage = (CultureCode)newConfig.General.Language;
        if (TabbarData.Count > 0) TabbarData[0] = ResourceStrings.General;
        if (TabbarData.Count > 1) TabbarData[1] = ResourceStrings.Behavior;
        if (TabbarData.Count > 2) TabbarData[2] = ResourceStrings.Data;
        if (TabbarData.Count > 3) TabbarData[3] = ResourceStrings.About;
        OnPropertyChanged(nameof(TabbarData));
    }

    private void Initialize()
    {
        Data = _config.General;
        TabbarData = [ResourceStrings.General, ResourceStrings.Behavior, ResourceStrings.Data, ResourceStrings.About];
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        DelDataStartMonthDate = DateTime.Now;
        DelDataEndMonthDate = DateTime.Now;
        ExportDataStartMonthDate = DateTime.Now;
        ExportDataEndMonthDate = DateTime.Now;

        WhenPropertyChanged(this, x => x.Data, async _ =>
        {
            if (TabbarSelectedIndex == 0 && Data is GeneralModel general)
            {
                _config.General = general;
                await _appConfig.SaveAsync();
            }
            else if (TabbarSelectedIndex == 1 && Data is BehaviorModel behavior)
            {
                _config.Behavior = behavior;
                await _appConfig.SaveAsync();
            }
            await Task.CompletedTask;
        });

        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, async _ =>
        {
            if (TabbarSelectedIndex == 0)
                Data = _config.General;
            else if (TabbarSelectedIndex == 1)
                Data = _config.Behavior;
            await Task.CompletedTask;
        });
    }

    private async Task OnDelDataAsync(object obj)
    {
        if (DelDataStartMonthDate > DelDataEndMonthDate)
        {
            _toastService.Toast(ResourceStrings.TimeRangeSelectionError, ToastType.Error, IconTypes.IncidentTriangle);
            return;
        }

        var isConfirm = await _dialogService.ShowConfirmDialogAsync(
            ResourceStrings.DeleteConfirmation,
            ResourceStrings.WantPerformAction);
        if (isConfirm)
        {
            await _dataService.ClearRangeAsync(DelDataStartMonthDate, DelDataEndMonthDate);
            await _webDataService.ClearAsync(DelDataStartMonthDate, DelDataEndMonthDate);
            _toastService.Success(ResourceStrings.OperationCompleted);
        }
    }

    private async Task OnExportDataAsync(object obj)
    {
        try
        {
            var folder = await _dialogService.ShowFolderPickerAsync();
            if (string.IsNullOrEmpty(folder)) return;

            await _dataService.ExportToExcelAsync(folder, ExportDataStartMonthDate, ExportDataEndMonthDate);
            await _webDataService.ExportAsync(folder, ExportDataStartMonthDate, ExportDataEndMonthDate);
            _toastService.Success(ResourceStrings.DataExportCompleted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            _toastService.Toast(ResourceStrings.DataExportFailed, ToastType.Error, IconTypes.IncidentTriangle);
        }
    }

    private Task OnCheckUpdateAsync() => _updateCheckerService.ManualCheckForUpdatesAsync();

    private void OnOpenURL(object obj)
    {
        var url = obj?.ToString();
        if (!string.IsNullOrEmpty(url))
            _processService.OpenUrl(url);
    }

    public override void Dispose()
    {
        _appConfig.ConfigChanged -= OnConfigChanged;
        base.Dispose();
    }
}
