using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Select;
using Taix.Client.Controls.Window;
using Taix.Client.Logging;
using Taix.Client.Models;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Event;
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

    public object GeneralSettings
    {
        get => _config.General;
        set
        {
            if (value is GeneralModel general && _config.General != general)
            {
                _config.General = general;
                _ = _appConfig.SaveAsync();
                OnPropertyChanged();
            }
        }
    }

    public object BehaviorSettings
    {
        get => _config.Behavior;
        set
        {
            if (value is BehaviorModel behavior && _config.Behavior != behavior)
            {
                _config.Behavior = behavior;
                _ = _appConfig.SaveAsync();
                OnPropertyChanged();
            }
        }
    }

    private int _dataRetentionDays;
    public int DataRetentionDays
    {
        get => _dataRetentionDays;
        set
        {
            if (_dataRetentionDays != value)
            {
                _dataRetentionDays = value;
                _config.General.DataRetentionDays = value;
                _ = _appConfig.SaveAsync();
                OnPropertyChanged();
                UpdateSelectedRetentionOption();
            }
        }
    }

    private List<SelectItemModel> _retentionOptions;
    public List<SelectItemModel> RetentionOptions
    {
        get => _retentionOptions;
        set
        {
            _retentionOptions = value;
            OnPropertyChanged();
        }
    }

    private SelectItemModel _selectedRetentionOption;
    public SelectItemModel SelectedRetentionOption
    {
        get => _selectedRetentionOption;
        set
        {
            if (_selectedRetentionOption != value && value != null)
            {
                _selectedRetentionOption = value;
                OnPropertyChanged();

                if (value.Data is int days)
                {
                    if (days == -1)
                    {
                        // 自定义选项，显示输入框
                        IsCustomRetention = true;
                    }
                    else
                    {
                        IsCustomRetention = false;
                        DataRetentionDays = days;
                    }
                }
            }
        }
    }

    private bool _isCustomRetention;
    public bool IsCustomRetention
    {
        get => _isCustomRetention;
        set
        {
            _isCustomRetention = value;
            OnPropertyChanged();
        }
    }

    private void UpdateSelectedRetentionOption()
    {
        if (_retentionOptions == null) return;

        var matchingOption = _retentionOptions.Find(o => o.Data is int d && d == _dataRetentionDays);
        if (matchingOption != null)
        {
            IsCustomRetention = false;
            _selectedRetentionOption = matchingOption;
        }
        else
        {
            IsCustomRetention = true;
            _selectedRetentionOption = _retentionOptions.Find(o => o.Data is int d && d == -1);
        }
        OnPropertyChanged(nameof(SelectedRetentionOption));
    }

    public ReactiveCommand<object, Unit> OpenURL { get; }
    public ReactiveCommand<Unit, Unit> CheckUpdate { get; }
    public ReactiveCommand<object, Unit> DelDataCommand { get; }
    public ReactiveCommand<object, Unit> ExportDataCommand { get; }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (e.HasChange("General.Language"))
        {
            if (TabbarData.Count > 0) TabbarData[0] = ResourceStrings.General;
            if (TabbarData.Count > 1) TabbarData[1] = ResourceStrings.Behavior;
            if (TabbarData.Count > 2) TabbarData[2] = ResourceStrings.Data;
            if (TabbarData.Count > 3) TabbarData[3] = ResourceStrings.About;
            OnPropertyChanged(nameof(TabbarData));
        }
    }

    private void Initialize()
    {
        Data = _config.General;
        TabbarData = [ResourceStrings.General, ResourceStrings.Behavior, ResourceStrings.Data, ResourceStrings.About];
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty;
        DelDataStartMonthDate = DateTime.Now;
        DelDataEndMonthDate = DateTime.Now;
        ExportDataStartMonthDate = DateTime.Now;
        ExportDataEndMonthDate = DateTime.Now;
        _dataRetentionDays = _config.General.DataRetentionDays;

        var days = ResourceStrings.Days;
        RetentionOptions =
        [
            new SelectItemModel { Name = $"30{days}", Data = 30 },
            new SelectItemModel { Name = $"60{days}", Data = 60 },
            new SelectItemModel { Name = $"90{days}", Data = 90 },
            new SelectItemModel { Name = $"180{days}", Data = 180 },
            new SelectItemModel { Name = $"365{days}", Data = 365 },
            new SelectItemModel { Name = ResourceStrings.Custom, Data = -1 }
        ];
        UpdateSelectedRetentionOption();
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

            var exportStart = new DateTime(ExportDataStartMonthDate.Year, ExportDataStartMonthDate.Month, 1);
            var exportEnd = new DateTime(ExportDataEndMonthDate.Year, ExportDataEndMonthDate.Month, DateTime.DaysInMonth(ExportDataEndMonthDate.Year, ExportDataEndMonthDate.Month));
            await _dataService.ExportToExcelAsync(folder, exportStart, exportEnd);
            await _webDataService.ExportAsync(folder, exportStart, exportEnd);
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
