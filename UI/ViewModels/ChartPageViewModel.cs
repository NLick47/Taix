using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using Core.Librarys;
using Core.Models;
using Core.Models.Config;
using Core.Models.Db;
using Core.Servicers.Interfaces;
using ReactiveUI;
using SharedLibrary;
using UI.Controls.Charts.Model;
using UI.Controls.Select;
using UI.Models;
using UI.Servicers;
using UI.Views;

namespace UI.ViewModels;

public class ChartPageViewModel : ChartPageModel
{
    private readonly ConfigModel _config;
    private readonly IWebData _webData;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenu;
    private readonly IAppContextMenuServicer appContextMenuServicer;
    private readonly ICategorys categorys;
    private readonly IData data;
    private readonly MainViewModel mainVM;
    private int appCount_;

    private double totalTime_;


    public ChartPageViewModel(IData data, ICategorys categorys, MainViewModel mainVM,
        IWebData webData_, IWebSiteContextMenuServicer webSiteContextMenu_,
        IAppContextMenuServicer appContextMenuServicer, IAppConfig appConfig)
    {
        this.data = data;
        this.categorys = categorys;
        this.mainVM = mainVM;
        this.appContextMenuServicer = appContextMenuServicer;
        _webData = webData_;
        _webSiteContextMenu = webSiteContextMenu_;
        _config = appConfig.GetConfig();
        ToDetailCommand = ReactiveCommand.Create<object>(OnTodetailCommand);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshCommand);

        TabbarData =
        [
            ResourceStrings.Daily,
            ResourceStrings.Weekly,
            ResourceStrings.Monthly,
            ResourceStrings.Yearly
        ];
        List<SelectItemModel> weekOptions =
        [
            new()
            {
                Name = ResourceStrings.ThisWeek
            },
            new()
            {
                Name = ResourceStrings.LastWeek
            }
        ];


        List<SelectItemModel> chartDataModeOptions =
        [
            new()
            {
                Id = 1,
                Name = ResourceStrings.DefaultView
            },
            new()
            {
                Id = 2,
                Name = ResourceStrings.SummaryView
            },
            new()
            {
                Id = 3,
                Name = ResourceStrings.CategoryView
            }
        ];

        ChartDataModeOptions = chartDataModeOptions;
        ChartDataMode = chartDataModeOptions[0];
        ShowType = ShowTypeOptions[0];
        WeekOptions = weekOptions;
        SelectedWeek = weekOptions[0];
        MonthDate = DateTime.Now;
        TabbarSelectedIndex = 0;
        Date = DateTime.Now;
        YearDate = DateTime.Now;

        InitializeAsync();

        PropertyChanged += ChartPageVM_PropertyChanged;
        AppContextMenu = appContextMenuServicer.GetContextMenu();
        WebSiteContextMenu = _webSiteContextMenu.GetContextMenu();
    }

    public ICommand ToDetailCommand { get; set; }
    public ICommand RefreshCommand { get; set; }
    public List<SelectItemModel> ChartDataModeOptions { get; set; }

    private async void InitializeAsync()
    {
        await LoadDayData();
        await LoadTrendChartData(); 
    }
    
    /// <summary>
    /// 加载趋势图数据
    /// </summary>
    private async Task LoadTrendChartData()
    {
      
        await Task.Run(async () =>
        {
            var trendData = new List<TrendDataPoint>();
            if (TabbarSelectedIndex == 0)
            {
                await LoadDailyTrendData(trendData);
            }
            else if (TabbarSelectedIndex == 1)
            {
                await LoadWeeklyTrendData(trendData);
            }
            else if (TabbarSelectedIndex == 2)
            {
                await LoadMonthlyTrendData(trendData);
            }
            else if (TabbarSelectedIndex == 3)
            {
                await LoadYearlyTrendData(trendData);
            }

            Dispatcher.UIThread.Post(() => { TrendChartDataPoints  = trendData; });
        });
    }
    
    private async Task LoadDailyTrendData(List<TrendDataPoint> trendDataPoints)
    {
        var hourlyData = await data.GetRangeTotalDataAsync(Date, Date);
        
        for (int i = 0; i < 24; i++)
        {
            double value = 0;
            if (i < hourlyData.Length)
            {
                value = hourlyData[i];
            }
            
            trendDataPoints.Add(new TrendDataPoint 
            { 
                Label = i.ToString(), 
                Value = value 
            });
        }
    }
    
    
    private async Task LoadWeeklyTrendData(List<TrendDataPoint> trendDataPoints)
    {
        var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
            ? Time.GetThisWeekDate()
            : Time.GetLastWeekDate();
            
        var dailyData = await data.GetRangeTotalDataAsync(weekDateArr[0], weekDateArr[1]);
        
        string[] dayNames = 
        {
            ResourceStrings.Monday, 
            ResourceStrings.Tuesday, 
            ResourceStrings.Wednesday, 
            ResourceStrings.Thursday,
            ResourceStrings.Friday, 
            ResourceStrings.Saturday, 
            ResourceStrings.Sunday
        };
        
        for (int i = 0; i < 7; i++)
        {
            double value = 0;
            if (i < dailyData.Length)
            {
                value = dailyData[i];
            }
            
            trendDataPoints.Add(new TrendDataPoint 
            { 
                Label = dayNames[i], 
                Value = value 
            });
        }
    }
    
    
    private async Task LoadMonthlyTrendData(List<TrendDataPoint> trendDataPoints)
    {
        var dateArr = Time.GetMonthDate(MonthDate);
        var daysInMonth = DateTime.DaysInMonth(MonthDate.Year, MonthDate.Month);
        var dailyData = await data.GetRangeTotalDataAsync(dateArr[0], dateArr[1]);
        
        for (int i = 0; i < daysInMonth; i++)
        {
            double value = 0;
            if (i < dailyData.Length)
            {
                value = dailyData[i];
            }
            
            trendDataPoints.Add(new TrendDataPoint 
            { 
                Label = (i + 1).ToString(), 
                Value = value 
            });
        }
    }
    
    
    private async Task LoadYearlyTrendData(List<TrendDataPoint> trendDataPoints)
    {
        var monthlyData = await data.GetMonthTotalDataAsync(YearDate);
        
        for (int i = 0; i < 12; i++)
        {
            double value = 0;
            if (i < monthlyData.Length)
            {
                value = monthlyData[i];
            }
            
            trendDataPoints.Add(new TrendDataPoint 
            { 
                Label = (i + 1).ToString(), 
                Value = value 
            });
        }
    }


    public override void Dispose()
    {
        base.Dispose();
        PropertyChanged -= ChartPageVM_PropertyChanged;
        Data = null;
        TopData = null;
        TrendChartDataPoints = null; 
    }

    private void OnTodetailCommand(object obj)
    {
        var data = obj as ChartsDataModel;

        if (data != null)
        {
            if (data.Data is WebSiteModel)
            {
                mainVM.Data = data.Data;
                mainVM.Uri = nameof(WebSiteDetailPage);
            }
            else
            {
                var model = data.Data as DailyLogModel;
                var app = model != null ? model.AppModel : null;

                if (model == null) app = (data.Data as HoursLogModel).AppModel;

                if (app != null)
                {
                    mainVM.Data = app;
                    mainVM.Uri = nameof(DetailPage);
                }
            }
        }
    }

    private Task OnRefreshCommand(object obj)
    {
        return LoadData();
    }


    private async void ChartPageVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Date))
        {
            await LoadDayData();
            await LoadTrendChartData();
        }
        if (e.PropertyName == nameof(TabbarSelectedIndex))
        {
            IsCanColumnSelect = true;
            await LoadData();
            await LoadTrendChartData(); 
        }

        if (e.PropertyName == nameof(SelectedWeek)) 
        {
            await LoadWeekData();
            await LoadTrendChartData();
        }

        if (e.PropertyName == nameof(MonthDate))
        {
            await LoadMonthlyData();
            await LoadTrendChartData();
        }

        if (e.PropertyName == nameof(YearDate))
        {
            await LoadYearData();
            await LoadTrendChartData();
        }

        if (e.PropertyName == nameof(ColumnSelectedIndex))
        {
            await LoadSelectedColData();
        }
        if (e.PropertyName == nameof(ChartDataMode))
        {
            if (ChartDataMode.Id == 1)
                IsChartStack = true;
            else
                IsChartStack = false;
            await LoadData();
        }

        if (e.PropertyName == nameof(WebColSelectedIndex)) await LoadWebSitesColSelectedData();
    }


    private Task LoadData()
    {
        if (TabbarSelectedIndex == 0)
        {
            NameIndexStart = 0;

            return LoadDayData();
        }

        if (TabbarSelectedIndex == 1) return LoadWeekData();

        if (TabbarSelectedIndex == 2)
        {
            NameIndexStart = 1;

            return LoadMonthlyData();
        }

        if (TabbarSelectedIndex == 3) return LoadYearData();

        return LoadSelectedColData();
    }

    /// <summary>
    ///     加载天数据
    /// </summary>
    private Task LoadDayData()
    {
        return Task.Run(async () =>
        {
            //  应用数据
            Dispatcher.UIThread.Invoke(() => { DataMaximum = 3600; });
            var chartData = new List<ChartsDataModel>();
            var sumData = new List<ChartsDataModel>();

            var list = await data.GetCategoryHoursDataAsync(Date);
            foreach (var item in list)
            {
                var category = categorys.GetCategory(item.CategoryID);
                if (category != null)
                {
                    var dataItem = new ChartsDataModel
                    {
                        Name = category.Name,
                        Icon = category.IconFile,
                        Values = item.Values,
                        Color = category.Color
                    };
                    chartData.Add(dataItem);
                }
            }

            if (ChartDataMode.Id == 1 || ChartDataMode.Id == 3)
            {
                Dispatcher.UIThread.Invoke(() => { Data = chartData; });
            }
            else
            {
                //  汇总
                var values = await data.GetRangeTotalDataAsync(Date, Date);


                var dataItem = new ChartsDataModel
                {
                    Values = values
                };

                sumData.Add(dataItem);
                Dispatcher.UIThread.Invoke(() => { Data = sumData; });
            }

            var totalUse = Data.Sum(m => m.Values.Sum());
            totalTime_ = totalUse;

            Dispatcher.UIThread.Post(() =>
            {
                RadarData = chartData;
                ColumnSelectedIndex = -1;
                WebColSelectedIndex = -1;
                TotalHours = Time.ToHoursString(totalUse);
            });
            await LoadTopData();

            //  网页数据
            await LoadWebData(Date, Date);
        });
    }

    /// <summary>
    ///     加载周数据
    /// </summary>
    private async Task LoadWeekData()
    {
        ColumnSelectedIndex = -1;
        WebColSelectedIndex = -1;

        DataMaximum = 0;
        var culture = SystemLanguage.CurrentCultureInfo;
        var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
            ? Time.GetThisWeekDate()
            : Time.GetLastWeekDate();
        WeekDateStr = weekDateArr[0].ToString("d", culture) + " " + Application.Current.Resources["To"] + " " +
                      weekDateArr[1].ToString("d", culture);
        string[] weekNames =
        [
            ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
            ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
        ];
        var chartData = new List<ChartsDataModel>();
        var sumData = new List<ChartsDataModel>();

        var list = await data.GetCategoryRangeDataAsync(weekDateArr[0], weekDateArr[1]);

        foreach (var item in list)
        {
            var category = categorys.GetCategory(item.CategoryID);
            if (category != null)
            {
                var dataItem = new ChartsDataModel
                {
                    Name = category.Name,
                    Icon = category.IconFile,
                    Values = item.Values,
                    ColumnNames = weekNames,
                    Color = category.Color
                };
                chartData.Add(dataItem);
            }
        }

        if (ChartDataMode.Id == 1 || ChartDataMode.Id == 3)
        {
            Data = chartData;
        }
        else
        {
            //  汇总
            var values = await data.GetRangeTotalDataAsync(weekDateArr[0], weekDateArr[1]);


            var dataItem = new ChartsDataModel
            {
                Values = values,
                ColumnNames = weekNames
            };

            sumData.Add(dataItem);
            Data = sumData;
        }

        RadarData = chartData;
        var totalUse = Data.Sum(m => m.Values.Sum());
        totalTime_ = totalUse;
        TotalHours = Time.ToHoursString(totalUse);

        //  网页数据
        await LoadWebData(weekDateArr[0], weekDateArr[1]);
        await LoadTopData();
    }

    /// <summary>
    ///     加载月数据
    /// </summary>
    private async Task LoadMonthlyData()
    {
        ColumnSelectedIndex = -1;
        WebColSelectedIndex = -1;
        DataMaximum = 0;

        var chartData = new List<ChartsDataModel>();
        var sumData = new List<ChartsDataModel>();
        var dateArr = Time.GetMonthDate(MonthDate);

        var list = await data.GetCategoryRangeDataAsync(dateArr[0], dateArr[1]);


        foreach (var item in list)
        {
            var category = categorys.GetCategory(item.CategoryID);
            if (category != null)
            {
                var dataItem = new ChartsDataModel
                {
                    Name = category.Name,
                    Icon = category.IconFile,
                    Values = item.Values,
                    Color = category.Color
                };

                chartData.Add(dataItem);
            }
        }

        if (ChartDataMode.Id == 1 || ChartDataMode.Id == 3)
        {
            Data = chartData;
        }
        else
        {
            //  汇总
            var values = await data.GetRangeTotalDataAsync(dateArr[0], dateArr[1]);

            var dataItem = new ChartsDataModel
            {
                Values = values
            };
            sumData.Add(dataItem);
            Data = sumData;
        }

        RadarData = chartData;
        var totalUse = Data.Sum(m => m.Values.Sum());
        totalTime_ = totalUse;
        TotalHours = Time.ToHoursString(totalUse);
        await LoadTopData();
        //  网页数据
        await LoadWebData(dateArr[0], dateArr[1]);
    }

    /// <summary>
    ///     加载年份数据
    /// </summary>
    private async Task LoadYearData()
    {
        ColumnSelectedIndex = -1;
        WebColSelectedIndex = -1;

        DataMaximum = 0;

        var chartData = new List<ChartsDataModel>();
        var sumData = new List<ChartsDataModel>();
        string[] names = new string[12];
        for (var i = 0; i < 12; i++) names[i] = Application.Current.Resources[$"{i + 1}Month"] as string;

        var list = await data.GetCategoryYearDataAsync(YearDate);

        foreach (var item in list)
        {
            var category = categorys.GetCategory(item.CategoryID);
            if (category != null)
            {
                var dataItem = new ChartsDataModel
                {
                    Name = category.Name,
                    Icon = category.IconFile,
                    Values = item.Values,
                    ColumnNames = names,
                    Color = category.Color
                };

                chartData.Add(dataItem);
            }
        }

        if (ChartDataMode.Id == 1 || ChartDataMode.Id == 3)
        {
            Data = chartData;
        }
        else
        {
            //  汇总
            var values = await data.GetMonthTotalDataAsync(YearDate);
            var dataItem = new ChartsDataModel
            {
                Values = values,
                ColumnNames = names
            };
            sumData.Add(dataItem);

            Data = sumData;
        }

        RadarData = chartData;

        var totalUse = Data.Sum(m => m.Values.Sum());
        totalTime_ = totalUse;
        TotalHours = Time.ToHoursString(totalUse);
        await LoadTopData();

        //  网页数据
        var dateArr = Time.GetYearDate(YearDate);
        await LoadWebData(dateArr[0], dateArr[1]);
    }

    private async Task LoadTopData()
    {
        var dateStart = Date.Date;
        var dateEnd = Date.Date;
        if (TabbarSelectedIndex == 1)
        {
            //  周
            var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
                ? Time.GetThisWeekDate()
                : Time.GetLastWeekDate();

            dateStart = weekDateArr[0];
            dateEnd = weekDateArr[1];
        }
        else if (TabbarSelectedIndex == 2)
        {
            //  月
            var dateArr = Time.GetMonthDate(MonthDate);
            dateStart = dateArr[0];
            dateEnd = dateArr[1];
        }
        else if (TabbarSelectedIndex == 3)
        {
            //  年
            var dateArr = Time.GetYearDate(YearDate);
            dateStart = dateArr[0];
            dateEnd = dateArr[1];
        }

        var list = await data.GetDateRangelogListAsync(dateStart, dateEnd, 5);
        TopData = MapToChartsData(list);

        TopHours = TopData.Count > 0 ? Time.ToHoursString(TopData[0].Value) : "0";

        appCount_ = await data.GetDateRangeAppCountAsync(dateStart, dateEnd);
        AppCount = appCount_.ToString();
        Top1App = null;

        if (TopData.Count > 0)
        {
            var model = TopData[0].Data as DailyLogModel;
            Top1App = model != null ? model.AppModel : null;
        }

        await LoadDiffData();
    }

    private async Task LoadDiffData()
    {
        var dateStart = Date.Date.AddDays(-1);
        var dateEnd = Date.Date.AddDays(-1);
        if (TabbarSelectedIndex == 1)
        {
            //  周
            var weekDateArr = Time.GetLastWeekDate();
            dateStart = weekDateArr[0];
            dateEnd = weekDateArr[1];
        }
        else if (TabbarSelectedIndex == 2)
        {
            //  月
            var dateArr = Time.GetMonthDate(MonthDate.AddMonths(-1));
            dateStart = dateArr[0];
            dateEnd = dateArr[1];
        }
        else if (TabbarSelectedIndex == 3)
        {
            //  年
            var dateArr = Time.GetYearDate(YearDate.AddYears(-1));
            dateStart = dateArr[0];
            dateEnd = dateArr[1];
        }

        //  应用量
        var lastAppCount = await data.GetDateRangeAppCountAsync(dateStart, dateEnd);
        //  使用总时长
        var lastTotalTime = (await data.GetRangeTotalDataAsync(dateStart, dateEnd)).Sum();

        var diffTotalTime = (totalTime_ - lastTotalTime) / lastTotalTime * 100;
        if (totalTime_ > 0 && lastTotalTime == 0)
            diffTotalTime = 100;
        else if (totalTime_ == 0 && lastTotalTime == 0) diffTotalTime = 0;
        if (diffTotalTime > 0)
            DiffTotalTimeType = "1";
        else if (diffTotalTime < 0)
            DiffTotalTimeType = "-1";
        else
            DiffTotalTimeType = "0";
        DiffTotalTimeValue = DiffTotalTimeType == "0" ? string.Empty :
            diffTotalTime == 100 ? "100%" : Math.Abs(diffTotalTime).ToString("f2") + "%";

        var diffAppCount = appCount_ - lastAppCount;
        if (diffAppCount > 0)
            DiffAppCountType = "1";
        else if (diffAppCount < 0)
            DiffAppCountType = "-1";
        else
            DiffAppCountType = "0";
        DiffAppCountValue = DiffAppCountType == "0" ? string.Empty : Math.Abs(diffAppCount).ToString();

        LastWebTotalTime = await _webData.GetBrowseDurationTotalAsync(dateStart, dateEnd);
        LastWebSiteCount = await _webData.GetBrowseSitesTotalAsync(dateStart, dateEnd);
        LastWebPageCount = await _webData.GetBrowsePagesTotalAsync(dateStart, dateEnd);
    }

    private List<ChartsDataModel> MapToChartsData(IEnumerable<DailyLogModel> list)
    {
        var resData = new List<ChartsDataModel>();

        foreach (var item in list)
        {
            var bindModel = new ChartsDataModel();
            bindModel.Data = item;
            bindModel.Name = !string.IsNullOrEmpty(item.AppModel?.Alias) ? item.AppModel.Alias :
                string.IsNullOrEmpty(item.AppModel?.Description) ? item.AppModel.Name : item.AppModel.Description;
            bindModel.Value = item.Time;
            bindModel.Tag = Time.ToString(item.Time);
            bindModel.PopupText = item.AppModel?.File;
            bindModel.Icon = item.AppModel?.IconFile;
            resData.Add(bindModel);
        }

        return resData;
    }

    private async Task LoadSelectedColData()
    {
        var culture = SystemLanguage.CurrentCultureInfo;
        if (ColumnSelectedIndex < 0)
        {
            DayHoursSelectedTime = string.Empty;
            return;
        }

        IEnumerable<HoursLogModel> hoursModelList = new List<HoursLogModel>();
        IEnumerable<DailyLogModel> daysModelList = new List<DailyLogModel>();

        var chartsDatas = new List<ChartsDataModel>();

        if (TabbarSelectedIndex == 0)
        {
            //  天
            var time = new DateTime(Date.Year, Date.Month, Date.Day, ColumnSelectedIndex, 0, 0);
            var format = $"{culture.DateTimeFormat.ShortDatePattern} HH";
            DayHoursSelectedTime = time.ToString(format, culture);
            hoursModelList = await data.GetTimeRangelogListAsync(time);
        }
        else if (TabbarSelectedIndex == 1)
        {
            //  周
            var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
                ? Time.GetThisWeekDate()
                : Time.GetLastWeekDate();
            var time = weekDateArr[0].AddDays(ColumnSelectedIndex);
            DayHoursSelectedTime = time.ToString("d", culture);
            daysModelList = await data.GetDateRangelogListAsync(time, time);
        }
        else if (TabbarSelectedIndex == 2)
        {
            //  月
            var dateArr = Time.GetMonthDate(MonthDate);
            var time = dateArr[0].AddDays(ColumnSelectedIndex);
            DayHoursSelectedTime = time.ToString("d", culture);
            daysModelList = await data.GetDateRangelogListAsync(time, time);
        }
        else if (TabbarSelectedIndex == 3)
        {
            //  年
            var dateStart = new DateTime(YearDate.Year, ColumnSelectedIndex + 1, 1);
            var dateEnd = new DateTime(dateStart.Year, dateStart.Month,
                DateTime.DaysInMonth(dateStart.Year, dateStart.Month), 23, 59, 59);

            DayHoursSelectedTime = dateStart.ToString(culture.DateTimeFormat.YearMonthPattern, culture);
            daysModelList = await data.GetDateRangelogListAsync(dateStart, dateEnd);
        }

        if (TabbarSelectedIndex == 0)
            foreach (var item in hoursModelList)
            {
                var bindModel = new ChartsDataModel();
                bindModel.Data = item;
                bindModel.Name = !string.IsNullOrEmpty(item.AppModel?.Alias) ? item.AppModel.Alias :
                    string.IsNullOrEmpty(item.AppModel?.Description) ? item.AppModel.Name : item.AppModel.Description;
                bindModel.Value = item.Time;
                bindModel.Tag = Time.ToString(item.Time);
                bindModel.PopupText = item.AppModel?.File;
                bindModel.Icon = item.AppModel?.IconFile;
                bindModel.BadgeList = new List<ChartBadgeModel>();
                if (item.AppModel.Category != null)
                    bindModel.BadgeList.Add(new ChartBadgeModel
                    {
                        Name = item.AppModel.Category.Name,
                        Color = item.AppModel.Category.Color,
                        Type = ChartBadgeType.Category
                    });
                if (_config.Behavior.IgnoreProcessList.Contains(item.AppModel.Name))
                    bindModel.BadgeList.Add(ChartBadgeModel.IgnoreBadge);
                chartsDatas.Add(bindModel);
            }
        else
            foreach (var item in daysModelList)
            {
                var bindModel = new ChartsDataModel();
                bindModel.Data = item;
                bindModel.Name = !string.IsNullOrEmpty(item.AppModel?.Alias) ? item.AppModel.Alias :
                    string.IsNullOrEmpty(item.AppModel?.Description) ? item.AppModel.Name : item.AppModel.Description;
                bindModel.Value = item.Time;
                bindModel.Tag = Time.ToString(item.Time);
                bindModel.PopupText = item.AppModel?.File;
                bindModel.Icon = item.AppModel?.IconFile;
                bindModel.BadgeList = new List<ChartBadgeModel>();
                if (item.AppModel.Category != null)
                    bindModel.BadgeList.Add(new ChartBadgeModel
                    {
                        Name = item.AppModel.Category.Name,
                        Color = item.AppModel.Category.Color,
                        Type = ChartBadgeType.Category
                    });
                if (_config.Behavior.IgnoreProcessList.Contains(item.AppModel.Name))
                    bindModel.BadgeList.Add(ChartBadgeModel.IgnoreBadge);

                chartsDatas.Add(bindModel);
            }

        DayHoursData = chartsDatas;
    }


    #region 网页数据

    private async Task LoadWebData(DateTime start_, DateTime end_)
    {
        await Task.WhenAll(LoadCategoriesStatistics(start_, end_), LoadWebSitesTopData(start_, end_),
            LoadWebBrowseDataStatistics(start_, end_)).ConfigureAwait(false);
        WebTotalTime = await _webData.GetBrowseDurationTotalAsync(start_, end_);
        WebSiteCount = await _webData.GetBrowseSitesTotalAsync(start_, end_);
        WebPageCount = await _webData.GetBrowsePagesTotalAsync(start_, end_);
        WebTotalTimeText = Time.ToHoursString(WebTotalTime);
    }

    /// <summary>
    ///     分类饼图
    /// </summary>
    /// <param name="start_"></param>
    /// <param name="end_"></param>
    private async Task LoadCategoriesStatistics(DateTime start_, DateTime end_)
    {
        var chartsDatas = new List<ChartsDataModel>();
        var data = await _webData.GetCategoriesStatisticsAsync(start_, end_);
        foreach (var item in data)
        {
            var category = await _webData.GetWebSiteCategoryAsync(item.ID);
            var bindModel = new ChartsDataModel();
            bindModel.Name = item.ID == 0 ? ResourceStrings.Uncategorized : item.Name;
            bindModel.Value = item.Value;
            bindModel.Data = item;
            bindModel.Color = item.ID == 0 ? "#ccc" : category.Color;
            bindModel.PopupText = bindModel.Name + " " + Time.ToString((int)item.Value);
            bindModel.Icon = item.ID == 0 ? "" : category.IconFile;
            chartsDatas.Add(bindModel);
        }

        WebCategoriesPieData = chartsDatas.OrderByDescending(m => m.Value).ToList();
    }

    private async Task LoadWebBrowseDataStatistics(DateTime start_, DateTime end_)
    {
        var chartData = new List<ChartsDataModel>();
        var data = await _webData.GetBrowseDataByCategoryStatisticsAsync(start_, end_);
        //  转换为图表格式数据
        var emptyCategory = new WebSiteCategoryModel
        {
            ID = 0,
            Name = ResourceStrings.Uncategorized,
            IconFile = "avares://Taix/Resources/Icons/tai32.ico"
        };

        string[] colNames = { };

        if (TabbarSelectedIndex == 1)
        {
            colNames =
            [
                ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
                ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
            ];
        }
        else if (TabbarSelectedIndex == 3)
        {
            colNames = new string[12];
            for (var i = 0; i < 12; i++) colNames[i] = i + 1 + ResourceStrings.Month;
        }

        foreach (var item in data)
        {
            var category = await _webData.GetWebSiteCategoryAsync(item.CategoryID);
            if (item.CategoryID == 0) category = emptyCategory;
            if (category != null)
            {
                var dataItem = new ChartsDataModel
                {
                    Name = category.Name,
                    Icon = category.IconFile,
                    Values = item.Values,
                    Color = category.Color,
                    ColumnNames = colNames
                };
                if (category.ID == 0) dataItem.Color = "#E5F7F6F2";
                chartData.Add(dataItem);
            }
        }

        WebBrowseStatisticsData = chartData;
    }

    private async Task LoadWebSitesTopData(DateTime start_, DateTime end_)
    {
        var data = await _webData.GetDateRangeWebSiteListAsync(start_, end_, 10);
        WebSitesTopData = MapToChartData(data);
    }

    private async Task LoadWebSitesColSelectedData()
    {
        if (WebColSelectedIndex < 0)
        {
            WebSitesColSelectedTimeText = string.Empty;
            WebSitesColSelectedData = new List<ChartsDataModel>();
            return;
        }

        var chartData = new List<ChartsDataModel>();
        var isTime = false;
        DateTime startTime = DateTime.Now, endTime = DateTime.Now;
        var culture = SystemLanguage.CurrentCultureInfo;
        if (TabbarSelectedIndex == 0)
        {
            //  天
            var format = $"{culture.DateTimeFormat.ShortDatePattern} HH";
            var time = new DateTime(Date.Year, Date.Month, Date.Day, WebColSelectedIndex, 0, 0);
            WebSitesColSelectedTimeText = time.ToString(format, culture);
            isTime = true;
            startTime = endTime = time;
        }
        else if (TabbarSelectedIndex == 1)
        {
            //  周
            var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
                ? Time.GetThisWeekDate()
                : Time.GetLastWeekDate();
            var time = weekDateArr[0].AddDays(WebColSelectedIndex);
            WebSitesColSelectedTimeText = time.ToString("d", culture);
            startTime = endTime = time;
        }
        else if (TabbarSelectedIndex == 2)
        {
            //  月
            var dateArr = Time.GetMonthDate(MonthDate);
            var time = dateArr[0].AddDays(WebColSelectedIndex);
            WebSitesColSelectedTimeText = time.ToString("d", culture);
            startTime = endTime = time;
        }
        else if (TabbarSelectedIndex == 3)
        {
            //  年
            var dateStart = new DateTime(YearDate.Year, WebColSelectedIndex + 1, 1);
            var dateEnd = new DateTime(dateStart.Year, dateStart.Month,
                DateTime.DaysInMonth(dateStart.Year, dateStart.Month), 23, 59, 59);

            WebSitesColSelectedTimeText = dateStart.ToString(culture.DateTimeFormat.YearMonthPattern);

            startTime = dateStart;
            endTime = dateEnd;
        }

        chartData = MapToChartData(await _webData.GetDateRangeWebSiteListAsync(startTime, endTime, 0, -1, isTime));

        WebSitesColSelectedData = chartData;
    }

    private List<ChartsDataModel> MapToChartData(IEnumerable<WebSiteModel> list)
    {
        var resData = new List<ChartsDataModel>();

        foreach (var item in list)
        {
            var bindModel = new ChartsDataModel();
            bindModel.Data = item;
            bindModel.Name = !string.IsNullOrEmpty(item.Alias) ? item.Alias : item.Title;
            bindModel.Value = item.Duration;
            bindModel.Tag = Time.ToString(item.Duration);
            bindModel.PopupText = item.Domain;
            bindModel.Icon = item.IconFile;
            resData.Add(bindModel);
        }

        return resData;
    }

    #endregion
}