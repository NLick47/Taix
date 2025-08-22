using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Core.Librarys;
using Core.Models.Db;
using Core.Servicers.Interfaces;
using ReactiveUI;
using SharedLibrary;
using UI.Controls.Charts.Model;
using UI.Controls.Select;
using UI.Models;
using UI.Servicers;

namespace UI.ViewModels;

public class WebSiteDetailPageViewModel : WebSiteDetailPageModel
{
    private readonly IAppConfig _appConfig;
    private readonly MainViewModel _mainVM;
    private readonly IUIServicer _uIServicer;
    private readonly IWebData _webData;
    private readonly IWebFilter _webFilter;
    private MenuItem _blockMenuItem;
    private MenuItem _clear;
    private MenuItem _copyDomain;
    private MenuItem _editAlias;


    private MenuItem _open;
    private MenuItem _reLoadData;

    private MenuItem _setCategoryMenuItem;

    public WebSiteDetailPageViewModel(IWebData webData_, MainViewModel mainVM_, IAppConfig appConfig_,
        IWebFilter webFilter_, IUIServicer uIServicer_)
    {
        _webData = webData_;
        _mainVM = mainVM_;
        _appConfig = appConfig_;
        _webFilter = webFilter_;
        _uIServicer = uIServicer_;
        Init();
    }

    public ICommand PageCommand { get; set; }

    private Task OnPageCommand(object obj)
    {
        if (WebPageSelectedItem == null) return Task.CompletedTask;
        var desk = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var clipboard = TopLevel.GetTopLevel(desk.MainWindow).Clipboard;
        var url = WebPageSelectedItem.Url.Url.IndexOf("://") == -1
            ? "http://" + WebPageSelectedItem.Url.Url
            : WebPageSelectedItem.Url.Url;
        switch (obj.ToString())
        {
            case "Open":
                Process.Start(url);
                break;
            case "CopyURL":
                return clipboard.SetTextAsync(WebPageSelectedItem.Url.Url);

            case "CopyTitle":
                return clipboard.SetTextAsync(WebPageSelectedItem.Url.Title);
        }

        return Task.CompletedTask;
    }

    private void Init()
    {
        if (!(_mainVM.Data is WebSiteModel))
        {
            _mainVM.Error(ResourceStrings.InvalidParameter);
            return;
        }

        WebSite = _mainVM.Data as WebSiteModel;

        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];

        var weekOptions = new List<SelectItemModel>
        {
            new()
            {
                Name = ResourceStrings.ThisWeek
            },
            new()
            {
                Name = ResourceStrings.LastWeek
            }
        };

        WeekOptions = weekOptions;
        SelectedWeek = weekOptions[0];
        MonthDate = DateTime.Now;
        TabbarSelectedIndex = 0;
        YearDate = DateTime.Now;
        ChartDate = DateTime.Now;

        Load();
        InitializeMenuItems();
        SystemLanguage.CurrentLanguageChanged += (s, e) => UpdateMenuTexts();
        PropertyChanged += WebSiteDetailPageVM_PropertyChanged;


        PageCommand = ReactiveCommand.CreateFromTask<object>(OnPageCommand);
    }

    private async void Load()
    {
        IsIgnore = _webFilter.IsIgnore(WebSite.Domain);
        await LoadData();
        await LoadCategories();
    }

    private void InitializeMenuItems()
    {
        WebSiteContextMenu = new ContextMenu();
        WebSiteContextMenu.Opened += WebSiteContextMenu_Opened;
        _open = new MenuItem();

        _open.Click += Open_Click;

        _copyDomain = new MenuItem();
        _copyDomain.Click += CopyDomain_Click;

        _reLoadData = new MenuItem();
        _reLoadData.Click += ReLoadData_Click;

        _clear = new MenuItem();
        _clear.Click += ClearData_Click;

        _setCategoryMenuItem = new MenuItem();

        _editAlias = new MenuItem();

        _editAlias.Click += EditAlias_ClickAsync;

        _blockMenuItem = new MenuItem();

        _blockMenuItem.Click += _blockMenuItem_Click;
        WebSiteContextMenu.Items.Add(_open);
        WebSiteContextMenu.Items.Add(_reLoadData);
        WebSiteContextMenu.Items.Add(_copyDomain);
        WebSiteContextMenu.Items.Add(new Separator());
        WebSiteContextMenu.Items.Add(_setCategoryMenuItem);
        WebSiteContextMenu.Items.Add(_editAlias);
        WebSiteContextMenu.Items.Add(new Separator());
        WebSiteContextMenu.Items.Add(_blockMenuItem);
        WebSiteContextMenu.Items.Add(_clear);

        UpdateMenuTexts();
    }

    private void UpdateMenuTexts()
    {
        _open.Header = ResourceStrings.OpenWebsite;
        _copyDomain.Header = ResourceStrings.CopyDomain;
        _reLoadData.Header = ResourceStrings.Refresh;
        _clear.Header = ResourceStrings.ClearStatistics;
        _editAlias.Header = ResourceStrings.EditAlias;
        _setCategoryMenuItem.Header = ResourceStrings.SetCategory;
        _blockMenuItem.Header = ResourceStrings.IgnoreWebsite;
    }

    private async void EditAlias_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = await _uIServicer.ShowInputModalAsync(ResourceStrings.EditAlias, ResourceStrings.EnterAlias,
                WebSite.Alias, val =>
                {
                    if (val?.Length > 15)
                    {
                        _mainVM.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                        return false;
                    }

                    return true;
                });

            //  开始更新别名

            WebSite.Alias = input;
            WebSite = await _webData.UpdateAsync(WebSite);

            _mainVM.Success(ResourceStrings.AliasUpdated);
            Debug.WriteLine("输入内容：" + input);
        }
        catch
        {
            //  输入取消，无需处理异常
        }
    }

    private async void ClearData_Click(object sender, RoutedEventArgs e)
    {
        var isConfirm = await _uIServicer.ShowConfirmDialogAsync(ResourceStrings.ClearConfirmation,
            ResourceStrings.ClearAllStatisticsSiteTip);
        if (isConfirm)
        {
            await _webData.ClearAsync(WebSite.ID);
            Load();
            _mainVM.Success(ResourceStrings.OperationCompleted);
        }
    }

    private async void CopyDomain_Click(object sender, RoutedEventArgs e)
    {
        var desk = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var clipboard = TopLevel.GetTopLevel(desk.MainWindow).Clipboard;
        await clipboard.SetTextAsync(WebSite.Domain);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("http://" + WebSite.Domain) { UseShellExecute = true });
    }

    private void ReLoadData_Click(object sender, RoutedEventArgs e)
    {
        Load();
    }

    private void _blockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var config = _appConfig.GetConfig();

        if (IsIgnore)
            //  取消忽略
            config.Behavior.IgnoreURLList.Remove(WebSite.Domain);
        else
            //  添加域名忽略
            config.Behavior.IgnoreURLList.Add(WebSite.Domain);

        IsIgnore = !IsIgnore;

        _mainVM.Success(ResourceStrings.OperationCompleted);
    }

    private async void WebSiteContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _setCategoryMenuItem.Items.Clear();
        var categoryId = (await _webData.GetWebSiteAsync(WebSite.ID)).CategoryID;
        foreach (var category in Categories)
        {
            var categoryMenu = new MenuItem();
            categoryMenu.Header = category.Name;
            categoryMenu.ToggleType = MenuItemToggleType.Radio;

            categoryMenu.IsChecked = categoryId == category.Id;
            categoryMenu.Click += async (s, ea) => { await UpdateCategory(category.Id); };
            _setCategoryMenuItem.Items.Add(categoryMenu);
        }

        if (categoryId != 0)
        {
            _setCategoryMenuItem.Items.Add(new Separator());
            var un = new MenuItem();
            un.Header = ResourceStrings.Uncategorized;
            un.Click += (s, e) => { ClearSiteCategory(); };
            _setCategoryMenuItem.Items.Add(un);
        }

        _blockMenuItem.Header = IsIgnore ? ResourceStrings.Unignore : ResourceStrings.IgnoreTheSite;

        _blockMenuItem.IsEnabled = !_webFilter.IsRegexIgnore(WebSite.Domain);
    }

    private async void ClearSiteCategory()
    {
        await _webData.UpdateWebSitesCategoryAsync(new[] { WebSite.ID }, 0);
        Category = null;
    }


    private async void WebSiteDetailPageVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        string[] updateNames =
        {
            nameof(TabbarSelectedIndex), nameof(ChartDate), nameof(SelectedWeek), nameof(MonthDate), nameof(YearDate)
        };
        if (updateNames.Contains(e.PropertyName)) await LoadData();
        if (e.PropertyName == nameof(Category) && Category != null) await UpdateCategory(Category.Id);
    }

    /// <summary>
    ///     加载浏览数据
    /// </summary>
    private async Task LoadData()
    {
        var startDate = DateTime.Now;
        var endDate = DateTime.Now;
        string[] colNames = { };
        NameIndexStart = 0;
        if (TabbarSelectedIndex == 0)
        {
            //  按天
            startDate = endDate = ChartDate;
        }
        else if (TabbarSelectedIndex == 1)
        {
            //  按周
            var culture = SystemLanguage.CurrentCultureInfo;
            var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
                ? Time.GetThisWeekDate()
                : Time.GetLastWeekDate();
            WeekDateStr = weekDateArr[0].ToString("d", culture) + $" {Application.Current.Resources["To"]} " +
                          weekDateArr[1].ToString("d", culture);

            startDate = weekDateArr[0];
            endDate = weekDateArr[1];
            colNames =
            [
                ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
                ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
            ];
        }
        else if (TabbarSelectedIndex == 2)
        {
            //  按月
            var dateArr = Time.GetMonthDate(MonthDate);
            startDate = dateArr[0];
            endDate = dateArr[1];
            NameIndexStart = 1;
        }
        else if (TabbarSelectedIndex == 3)
        {
            //  按年
            startDate = new DateTime(YearDate.Year, 1, 1);
            endDate = new DateTime(YearDate.Year, 12, DateTime.DaysInMonth(YearDate.Year, 12), 23, 59, 59);

            colNames = new string[12];
            for (var i = 0; i < 12; i++) colNames[i] = Application.Current.Resources[$"{i + 1}Month"] as string;
        }

        //  柱形图数据
        var list = await _webData.GetBrowseDataStatisticsAsync(startDate, endDate, WebSite.ID);
        var chartData = new List<ChartsDataModel>
        {
            new()
            {
                Name = !string.IsNullOrEmpty(WebSite.Alias) ? WebSite.Alias : WebSite.Title,
                Values = list.Select(m => m.Value).ToArray(),
                ColumnNames = colNames,
                Color = StateData.ThemeColor
            }
        };
        ChartData = chartData;
        //  详细访问数据
        WebPageData = (await _webData.GetBrowseLogListAsync(startDate, endDate, WebSite.ID))
            .OrderByDescending(m => m.ID).ToList();
    }

    /// <summary>
    ///     加载所有分类
    /// </summary>
    private async Task LoadCategories()
    {
        var data = await _webData.GetWebSiteCategoriesAsync();
        var list = new List<SelectItemModel>();
        foreach (var category in data)
        {
            var item = new SelectItemModel();
            item.Name = category.Name;
            item.Img = category.IconFile;
            item.Id = category.ID;
            list.Add(item);
        }

        Categories = list;
        Category = Categories.Where(m => m.Id == WebSite.CategoryID).FirstOrDefault();
    }

    /// <summary>
    ///     更新分类
    /// </summary>
    private async Task UpdateCategory(int categoryId_)
    {
        await _webData.UpdateWebSitesCategoryAsync(new[] { WebSite.ID }, categoryId_);
        WebSite.CategoryID = categoryId_;
        if (Category == null || categoryId_ != Category.Id)
            Category = Categories.Where(m => m.Id == WebSite.CategoryID).FirstOrDefault();
    }
}