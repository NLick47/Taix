using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Librarys;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class DetailPageViewModel : DetailPageModel
{
    private readonly IAppConfig _appConfig;
    private readonly IAppData _appDataService;
    private readonly ICategorys _categoryService;
    private readonly IClipboardService _clipboardService;
    private readonly IData _dataService;
    private readonly IDialogService _dialogService;
    private readonly EventHandler _languageChangedHandler;
    private readonly IProcessService _processService;
    private readonly IToastService _toastService;
    private readonly INavigationDataService _navigationData;
    private readonly ConfigModel _config;

    private MenuItem _clearMenuItem = new();
    private MenuItem _copyProcessFileMenuItem = new();
    private MenuItem _copyProcessNameMenuItem = new();
    private MenuItem _editAliasMenuItem = new();
    private MenuItem _openDirMenuItem = new();
    private MenuItem _openExeMenuItem = new();
    private MenuItem _reloadDataMenuItem = new();
    private MenuItem _setCategoryMenuItem = new();
    private MenuItem _whiteListMenuItem = new();

    public DetailPageViewModel(
        IData data,
        INavigationDataService navigationData,
        IAppConfig appConfig,
        ICategorys categories,
        IAppData appData,
        IDialogService dialogService,
        IClipboardService clipboardService,
        IProcessService processService,
        IToastService toastService)
    {
        _dataService = data;
        _navigationData = navigationData;
        _appConfig = appConfig;
        _categoryService = categories;
        _appDataService = appData;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _processService = processService;
        _toastService = toastService;
        _config = appConfig.GetConfig();

        _languageChangedHandler = (s, e) => UpdateMenuTexts();

        BlockActionCommand = ReactiveCommand.CreateFromTask<object>(OnBlockActionAsync);
        ClearSelectMonthDataCommand = ReactiveCommand.CreateFromTask<object>(OnClearSelectMonthDataAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync);

        BlockActionCommand.DisposeWith(Disposables);
        ClearSelectMonthDataCommand.DisposeWith(Disposables);
        RefreshCommand.DisposeWith(Disposables);

        Initialize();
    }

    public ReactiveCommand<object, Unit> BlockActionCommand { get; }
    public ReactiveCommand<object, Unit> ClearSelectMonthDataCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }

    private void Initialize()
    {
        if (_navigationData.Data is not AppModel app)
        {
            _toastService.Error(ResourceStrings.InvalidParameter);
            return;
        }

        App = app;

        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];
        WeekOptions =
        [
            new SelectItemModel { Name = ResourceStrings.ThisWeek },
            new SelectItemModel { Name = ResourceStrings.LastWeek }
        ];

        Date = DateTime.Now;
        ChartDate = DateTime.Now;
        SelectedWeek = WeekOptions[0];
        MonthDate = DateTime.Now;
        YearDate = DateTime.Now;
        TabbarSelectedIndex = 0;

        SystemLanguage.CurrentLanguageChanged += _languageChangedHandler;

        InitializeMenuItems();
        UpdateMenuTexts();

        WhenPropertyChanged(this, x => x.Date, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.Category, _ => OnCategoryChangedAsync());
        WhenPropertyChanged(this, x => x.ChartDate, _ => LoadDayDataAsync());
        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, _ => LoadChartDataAsync());
        WhenPropertyChanged(this, x => x.SelectedWeek, _ => LoadWeekDataAsync());
        WhenPropertyChanged(this, x => x.MonthDate, _ => LoadMonthlyDataAsync());
        WhenPropertyChanged(this, x => x.YearDate, _ => LoadYearDataAsync());

        _ = ExecuteAsync(async ct =>
        {
            await LoadDataCoreAsync(ct);
            await LoadChartDataAsync();
            await LoadInfoAsync(ct);

            var regexList = _config.Behavior.IgnoreProcessList.Where(m => Regex.IsMatch(m, @"[\.\*\?\{\\\[\^\|]"));
            foreach (var reg in regexList)
            {
                if (App != null && (RegexHelper.IsMatch(App.Name, reg) || RegexHelper.IsMatch(App.File, reg)))
                {
                    IsRegexIgnore = true;
                    break;
                }
            }
        });
    }

    private Task LoadDataAsync() => ExecuteAsync(LoadDataCoreAsync);

    private async Task LoadDataCoreAsync(CancellationToken cancellationToken)
    {
        if (App == null) return;

        var monthData = await _dataService.GetProcessMonthLogListAsync(App.ID, Date);
        cancellationToken.ThrowIfCancellationRequested();
        var monthTotal = monthData.Sum(m => m.Time);
        Total = Time.ToString(monthTotal);

        var start = new DateTime(Date.Year, Date.Month, 1);
        var end = new DateTime(Date.Year, Date.Month, DateTime.DaysInMonth(Date.Year, Date.Month));
        var monthAllData = await _dataService.GetDateRangelogListAsync(start, end);
        cancellationToken.ThrowIfCancellationRequested();

        LongDay = ResourceStrings.NoData;
        if (monthData.Count > 0)
        {
            var longDayData = monthData.OrderByDescending(m => m.Time).First();
            LongDay = string.Format(ResourceStrings.LongDayTips, longDayData.Date.ToString("dd"), Time.ToString(longDayData.Time));
        }

        var monthAllTotal = monthAllData.Sum(m => m.Time);
        Ratio = monthAllTotal > 0 ? (monthTotal / (double)monthAllTotal).ToString("P") : ResourceStrings.NoData;

        var chartData = ChartDataMapper.MapFromDailyLogs(monthData, orderByValue: false);
        if (chartData.Count == 0)
            chartData.Add(new ChartsDataModel { DateTime = Date });
        Data = chartData;
    }

    private async Task LoadInfoAsync(CancellationToken cancellationToken)
    {
        if (App == null) return;

        await Task.Run(() =>
        {
            IsIgnore = _config.Behavior.IgnoreProcessList.Contains(App.Name);
            LoadCategorys(App.Category?.Name);

            var link = _config.Links.FirstOrDefault(m => m.ProcessList.Contains(App.Name));
            if (link != null)
                LinkApps = _appDataService.GetAllApps().Where(m => link.ProcessList.Contains(m.Name) && m.Name != App.Name).ToList();
        });
    }

    private Task LoadChartDataAsync() => ExecuteAsync(async ct =>
    {
        switch (TabbarSelectedIndex)
        {
            case 0:
                NameIndexStart = 0;
                await LoadDayDataAsync(ct);
                break;
            case 1:
                NameIndexStart = 0;
                await LoadWeekDataAsync(ct);
                break;
            case 2:
                NameIndexStart = 1;
                await LoadMonthlyDataAsync(ct);
                break;
            case 3:
                NameIndexStart = 1;
                await LoadYearDataAsync(ct);
                break;
        }
    });

    private Task OnRefreshAsync(object _) => LoadChartDataAsync();

    private void LoadCategorys(string? categoryName)
    {
        var list = new List<SelectItemModel>();
        foreach (var item in _categoryService.GetCategories())
        {
            var option = new SelectItemModel
            {
                Id = item.ID,
                Data = item,
                Img = item.IconFile,
                Name = item.Name
            };
            list.Add(option);
            if (categoryName == option.Name) Category = option;
        }
        Categorys = list;
    }

    private async Task OnCategoryChangedAsync()
    {
        if (App == null || Category?.Data is not CategoryModel cat) return;
        if ((App.Category == null && Category != null) || (Category != null && App.Category?.Name != Category.Name))
        {
            App.Category = cat;
            var app = _appDataService.GetApp(App.ID);
            if (app != null)
            {
                app.CategoryID = cat.ID;
                _appDataService.UpdateApp(app);
            }
        }
        await Task.CompletedTask;
    }

    private async Task OnClearSelectMonthDataAsync(object _)
    {
        var isConfirm = await _dialogService.ShowConfirmDialogAsync(
            ResourceStrings.ClearConfirmation,
            string.Format(ResourceStrings.WantClearData, Date.Year, Date.Month));
        if (isConfirm) await ClearAsync();
    }

    private async Task ClearAsync()
    {
        if (App == null) return;
        _toastService.Toast(ResourceStrings.Processing);
        await _dataService.ClearAsync(App.ID, Date);
        await LoadDataAsync();
        await ExecuteAsync(LoadInfoAsync, trackLoading: false);
        _toastService.Toast(ResourceStrings.Cleared);
    }

    private async Task OnBlockActionAsync(object obj)
    {
        if (App == null) return;
        if (obj.ToString() == "block")
        {
            _config.Behavior.IgnoreProcessList.Add(App.Name);
            IsIgnore = true;
            _toastService.Success(string.Format(ResourceStrings.ApplicationNowIgnored, App.Name));
        }
        else
        {
            _config.Behavior.IgnoreProcessList.Remove(App.Name);
            IsIgnore = false;
            _toastService.Success(string.Format(ResourceStrings.IgnoringApplicationCancelled, App.Name));
        }
        await _appConfig.SaveAsync();
    }

    private void InitializeMenuItems()
    {
        AppContextMenu = new ContextMenu();
        AppContextMenu.Opened += OnAppContextMenuOpened;

        _openExeMenuItem = new MenuItem();
        _openExeMenuItem.Command = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrEmpty(App?.File))
                _processService.OpenFile(App.File);
            else
                _toastService.Error(ResourceStrings.ApplicationExist);
        });

        _copyProcessNameMenuItem = new MenuItem();
        _copyProcessNameMenuItem.Command = ReactiveCommand.CreateFromTask(async () =>
        {
            if (App?.Name != null)
                await _clipboardService.SetTextAsync(App.Name);
        });

        _copyProcessFileMenuItem = new MenuItem();
        _copyProcessFileMenuItem.Command = ReactiveCommand.CreateFromTask(async () =>
        {
            if (App?.File != null)
                await _clipboardService.SetTextAsync(App.File);
        });

        _openDirMenuItem = new MenuItem();
        _openDirMenuItem.Command = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrEmpty(App?.File))
                _processService.OpenDirectory(App.File);
            else
                _toastService.Error(ResourceStrings.ApplicationFileExist);
        });

        _reloadDataMenuItem = new MenuItem();
        _reloadDataMenuItem.Command = ReactiveCommand.CreateFromTask(async () => await OnRefreshAsync(null));

        _setCategoryMenuItem = new MenuItem();

        _editAliasMenuItem = new MenuItem();
        _editAliasMenuItem.Command = ReactiveCommand.CreateFromTask(OnEditAliasAsync);

        _whiteListMenuItem = new MenuItem();
        _whiteListMenuItem.Command = ReactiveCommand.Create(OnWhiteListAction);

        _clearMenuItem = new MenuItem();
        _clearMenuItem.Command = ReactiveCommand.CreateFromTask(async () => await ClearAsync());

        AppContextMenu.Items.Add(_openExeMenuItem);
        AppContextMenu.Items.Add(new Separator());
        AppContextMenu.Items.Add(_reloadDataMenuItem);
        AppContextMenu.Items.Add(new Separator());
        AppContextMenu.Items.Add(_setCategoryMenuItem);
        AppContextMenu.Items.Add(_editAliasMenuItem);
        AppContextMenu.Items.Add(new Separator());
        AppContextMenu.Items.Add(_copyProcessNameMenuItem);
        AppContextMenu.Items.Add(_copyProcessFileMenuItem);
        AppContextMenu.Items.Add(_openDirMenuItem);
        AppContextMenu.Items.Add(new Separator());
        AppContextMenu.Items.Add(_clearMenuItem);
        AppContextMenu.Items.Add(_whiteListMenuItem);
    }

    private void OnAppContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        _ = RefreshMenuItemsAsync();
    }

    private void UpdateMenuTexts()
    {
        _openExeMenuItem.Header = ResourceStrings.OpenWebsite;
        _copyProcessNameMenuItem.Header = ResourceStrings.CopyApplicationProcessName;
        _copyProcessFileMenuItem.Header = ResourceStrings.CopyApplicationFilePath;
        _openDirMenuItem.Header = ResourceStrings.OpenApplicationDirectory;
        _reloadDataMenuItem.Header = ResourceStrings.Refresh;
        _clearMenuItem.Header = ResourceStrings.ClearStatistics;
        _setCategoryMenuItem.Header = ResourceStrings.SetCategory;
        _editAliasMenuItem.Header = ResourceStrings.EditAlias;
    }

    private async Task RefreshMenuItemsAsync()
    {
        if (App == null) return;
        _setCategoryMenuItem.Items.Clear();

        var categoryId = _appDataService.GetApp(App.ID).CategoryID;
        foreach (var category in Categorys)
        {
            var categoryMenu = new MenuItem
            {
                Header = category.Name,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = App.CategoryID == category.Id,
                Command = ReactiveCommand.Create(() => UpdateCategory(category.Data as CategoryModel))
            };
            _setCategoryMenuItem.Items.Add(categoryMenu);
        }

        if (categoryId != 0)
        {
            _setCategoryMenuItem.Items.Add(new Separator());
            var un = new MenuItem
            {
                Header = ResourceStrings.Uncategorized,
                Command = ReactiveCommand.Create(() => ClearCategory(App.ID))
            };
            _setCategoryMenuItem.Items.Add(un);
        }

        _setCategoryMenuItem.IsEnabled = _setCategoryMenuItem.Items.Count > 0;
        _whiteListMenuItem.Header = _config.Behavior.ProcessWhiteList.Contains(App.Name)
            ? ResourceStrings.RemoveWhitelist
            : ResourceStrings.AddWhitelist;
    }

    private async Task OnEditAliasAsync()
    {
        if (App == null) return;
        var app = _appDataService.GetApp(App.ID);
        try
        {
            var input = await _dialogService.ShowInputModalAsync(
                ResourceStrings.UpdateAlias,
                ResourceStrings.EnterAlias,
                app.Alias,
                val =>
                {
                    if (val?.Length > 15)
                    {
                        _toastService.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                        return false;
                    }
                    return true;
                });

            app.Alias = input;
            _appDataService.UpdateApp(app);
            App = app;
            _toastService.Success(ResourceStrings.AliasUpdated);
        }
        catch
        {
            // 输入取消，无需处理
        }
    }

    private void OnWhiteListAction()
    {
        if (App == null) return;
        if (_config.Behavior.ProcessWhiteList.Contains(App.Name))
        {
            _config.Behavior.ProcessWhiteList.Remove(App.Name);
            _toastService.Success($"{ResourceStrings.RemovedApplicationFromWhitelist} {App.Description}");
        }
        else
        {
            _config.Behavior.ProcessWhiteList.Add(App.Name);
            _toastService.Success($"{ResourceStrings.AddedToWhitelist} {App.Description}");
        }
    }

    private void ClearCategory(int appId)
    {
        var app = _appDataService.GetApp(appId);
        app.CategoryID = 0;
        app.Category = null;
        _appDataService.UpdateApp(app);
    }

    private void UpdateCategory(CategoryModel? category)
    {
        if (App == null || category == null) return;
        var app = _appDataService.GetApp(App.ID);
        app.CategoryID = category.ID;
        app.Category = category;
        _appDataService.UpdateApp(app);

        if (Category == null || category.ID != Category.Id)
            Category = Categorys.FirstOrDefault(m => m.Id == category.ID);
    }

    #region 柱状图图表数据加载

    private async Task LoadDayDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 0) return;
        DataMaximum = 3600;
        if (App == null) return;
        var list = await _dataService.GetAppDayDataAsync(App.ID, ChartDate);
        cancellationToken.ThrowIfCancellationRequested();
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values
        }).ToList();
    }

    private async Task LoadWeekDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 1) return;
        DataMaximum = 0;
        if (App == null) return;
        var culture = SystemLanguage.CurrentCultureInfo;
        var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
            ? Time.GetThisWeekDate()
            : Time.GetLastWeekDate();
        var toText = Application.Current?.Resources["To"] as string ?? "To";
        WeekDateStr = $"{weekDateArr[0].ToString("d", culture)} {toText} {weekDateArr[1].ToString("d", culture)}";

        var list = await _dataService.GetAppRangeDataAsync(App.ID, weekDateArr[0], weekDateArr[1]);
        cancellationToken.ThrowIfCancellationRequested();
        string[] weekNames =
        [
            ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
            ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
        ];
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values,
            ColumnNames = weekNames
        }).ToList();
    }

    private async Task LoadMonthlyDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 2) return;
        DataMaximum = 0;
        if (App == null) return;
        var dateArr = Time.GetMonthDate(MonthDate);
        var list = await _dataService.GetAppRangeDataAsync(App.ID, dateArr[0], dateArr[1]);
        cancellationToken.ThrowIfCancellationRequested();
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values
        }).ToList();
    }

    private async Task LoadYearDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 3) return;
        DataMaximum = 0;
        if (App == null) return;
        var list = await _dataService.GetAppYearDataAsync(App.ID, YearDate);
        cancellationToken.ThrowIfCancellationRequested();
        var names = new string[12];
        for (var i = 0; i < 12; i++)
            names[i] = Application.Current?.Resources[$"{i + 1}Month"] as string ?? $"{i + 1}";
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values,
            ColumnNames = names
        }).ToList();
    }

    #endregion

    public override void Dispose()
    {
        SystemLanguage.CurrentLanguageChanged -= _languageChangedHandler;
        if (AppContextMenu != null)
            AppContextMenu.Opened -= OnAppContextMenuOpened;
        base.Dispose();
    }
}
