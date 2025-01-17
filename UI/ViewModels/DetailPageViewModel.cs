using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Core.Librarys;
using Core.Models;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Controls.Charts.Model;
using UI.Controls.Select;
using UI.Models;
using UI.Servicers;


namespace UI.ViewModels
{
    public class DetailPageViewModel : DetailPageModel
    {
        private readonly IData data;
        private readonly MainViewModel main;
        private readonly IAppConfig appConfig;
        private readonly ICategorys categories;
        private readonly IAppData appData;
        private readonly IUIServicer _uIServicer;

        private ConfigModel config;
        public ICommand BlockActionCommand { get; set; }
        public ICommand ClearSelectMonthDataCommand { get; set; }
        public ICommand RefreshCommand { get; set; }
        private MenuItem _setCategoryMenuItem;
        private MenuItem _whiteListMenuItem;

        public DetailPageViewModel(
            IData data,
            MainViewModel main,
            IAppConfig appConfig,
            ICategorys categories,
            IAppData appData,
            IUIServicer uIServicer_)
        {
            this.data = data;
            this.main = main;
            this.appConfig = appConfig;
            this.categories = categories;
            this.appData = appData;

            _uIServicer = uIServicer_;

            BlockActionCommand = ReactiveCommand.Create<object>(OnBlockActionCommand);
            ClearSelectMonthDataCommand = ReactiveCommand.CreateFromTask<object>(OnClearSelectMonthDataCommand);
            RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshCommand);

            Init();
        }

        private async void Init()
        {
            App = main.Data as AppModel;

            Date = DateTime.Now;

            TabbarData =
                [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];

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

            WeekOptions = weekOptions;

            SelectedWeek = weekOptions[0];

            MonthDate = DateTime.Now;

            TabbarSelectedIndex = 0;

            Date = DateTime.Now;

            YearDate = DateTime.Now;

            ChartDate = DateTime.Now;

            PropertyChanged += DetailPageVM_PropertyChanged;

            config = appConfig.GetConfig();

            await LoadData();

            await LoadInfo();

            await LoadDayData();

            //  判断正则忽略
            var regexList =
                config.Behavior.IgnoreProcessList.Where(m => Regex.IsMatch(m, @"[\.|\*|\?|\{|\\|\[|\^|\|]"));
            foreach (string reg in regexList)
            {
                if (RegexHelper.IsMatch(App.Name, reg) || RegexHelper.IsMatch(App.File, reg))
                {
                    IsRegexIgnore = true;
                    break;
                }
            }

            CreateContextMenu();
        }

        private void CreateContextMenu()
        {
            AppContextMenu = new();
            AppContextMenu.Opened += AppContextMenu_Opened;
            MenuItem open = new MenuItem();
            open.Header = ResourceStrings.StartApplication;
            open.Click += (e, c) => { OnInfoMenuActionCommand("open exe"); };

            MenuItem copyProcessName = new MenuItem();
            copyProcessName.Header = ResourceStrings.CopyApplicationProcessName;
            copyProcessName.Click += (e, c) => { OnInfoMenuActionCommand("copy processname"); };

            MenuItem copyProcessFile = new MenuItem();
            copyProcessFile.Header = ResourceStrings.CopyApplicationFilePath;
            copyProcessFile.Click += (e, c) => { OnInfoMenuActionCommand("copy process file"); };

            MenuItem openDir = new MenuItem();
            openDir.Header = ResourceStrings.OpenApplicationDirectory;
            openDir.Click += (e, c) => { OnInfoMenuActionCommand("open dir"); };

            MenuItem reLoadData = new MenuItem();
            reLoadData.Header = ResourceStrings.Refresh;
            reLoadData.Click += async (e, c) =>
            {
                await LoadChartData();
                await LoadData();
            };

            MenuItem clear = new MenuItem();
            clear.Header = ResourceStrings.ClearStatistics;
            clear.Click += ClearAppData_Click;

            _setCategoryMenuItem = new MenuItem();
            _setCategoryMenuItem.Header = ResourceStrings.SetCategory;
            MenuItem editAlias = new MenuItem();
            editAlias.Header = ResourceStrings.EditAlias;
            editAlias.Click += EditAlias_ClickAsync;

            _whiteListMenuItem = new MenuItem();
            _whiteListMenuItem.Click += (e, c) =>
            {
                if (config.Behavior.ProcessWhiteList.Contains(App.Name))
                {
                    config.Behavior.ProcessWhiteList.Remove(App.Name);
                    main.Toast($"{ResourceStrings.RemovedApplicationFromWhitelist} {App.Description}",
                        Controls.Window.ToastType.Success);
                }
                else
                {
                    config.Behavior.ProcessWhiteList.Add(App.Name);
                    main.Toast($"{ResourceStrings.AddedToWhitelist} {App.Description}",
                        Controls.Window.ToastType.Success);
                }
            };

            AppContextMenu.Items.Add(open);
            AppContextMenu.Items.Add(new Separator());
            AppContextMenu.Items.Add(reLoadData);
            AppContextMenu.Items.Add(new Separator());
            AppContextMenu.Items.Add(_setCategoryMenuItem);
            AppContextMenu.Items.Add(editAlias);
            AppContextMenu.Items.Add(new Separator());
            AppContextMenu.Items.Add(copyProcessName);
            AppContextMenu.Items.Add(copyProcessFile);
            AppContextMenu.Items.Add(openDir);
            AppContextMenu.Items.Add(new Separator());
            AppContextMenu.Items.Add(clear);
            AppContextMenu.Items.Add(_whiteListMenuItem);
        }

        private async void EditAlias_ClickAsync(object sender, RoutedEventArgs e)
        {
            var app = appData.GetApp(App.ID);
            try
            {
                string input = await _uIServicer.ShowInputModalAsync(ResourceStrings.UpdateAlias,
                    ResourceStrings.EnterAlias, app.Alias, (val) =>
                    {
                        if (val.Length > 15)
                        {
                            main.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                            return false;
                        }

                        return true;
                    });

                //  开始更新别名
                app.Alias = input;
                appData.UpdateApp(app);
                App = app;

                main.Success(ResourceStrings.AliasUpdated);
            }
            catch
            {
                //  输入取消，无需处理异常
            }
        }

        private async void ClearAppData_Click(object sender, RoutedEventArgs e)
        {
            bool isConfirm = await _uIServicer.ShowConfirmDialogAsync(ResourceStrings.ClearConfirmation,
                ResourceStrings.ClearAllStatisticsApplicationTip);
            if (isConfirm)
            {
                await data.ClearAsync(App.ID);
                await LoadChartData();
                await LoadData();
                main.Toast(ResourceStrings.OperationCompleted, Controls.Window.ToastType.Success,
                    Controls.Base.IconTypes.Accept);
            }
        }

        private void AppContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            _setCategoryMenuItem.Items.Clear();

            foreach (var category in categories.GetCategories())
            {
                var categoryMenu = new MenuItem();
                categoryMenu.Header = category.Name;
                categoryMenu.IsChecked = App.CategoryID == category.ID;
                categoryMenu.Click += (s, ea) => { UpdateCategory(category); };
                _setCategoryMenuItem.Items.Add(categoryMenu);
            }

            _setCategoryMenuItem.IsEnabled = _setCategoryMenuItem.Items.Count > 0;

            if (config.Behavior.ProcessWhiteList.Contains(App.Name))
            {
                _whiteListMenuItem.Header = ResourceStrings.RemoveWhitelist;
            }
            else
            {
                _whiteListMenuItem.Header = ResourceStrings.AddWhitelist;
            }
        }

        /// <summary>
        /// 更新分类
        /// </summary>
        private void UpdateCategory(CategoryModel category_)
        {
            Task.Run(() =>
            {
                var app = appData.GetApp(App.ID);
                app.CategoryID = category_.ID;
                app.Category = category_;
                appData.UpdateApp(app);

                if (Category == null || category_.ID != Category.Id)
                {
                    Category = Categorys.Where(m => m.Id == category_.ID).FirstOrDefault();
                }
            });
        }


        private async Task OnRefreshCommand(object obj)
        {
            //  刷新
            await LoadChartData();
            await LoadData();
        }

        private void OnInfoMenuActionCommand(string action_)
        {
            //switch (action_)
            //{
            //    case "copy processname":
            //        Clipboard.SetText(App.Name);

            //        break;
            //    case "copy process file":
            //        Clipboard.SetText(App.File);
            //        break;
            //    case "open dir":
            //        if (File.Exists(App.File))
            //        {
            //            System.Diagnostics.Process.Start("explorer.exe", "/select, " + App.File);
            //        }
            //        else
            //        {
            //            main.Toast("应用文件似乎不存在", Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
            //        }
            //        break;
            //    case "open exe":
            //        if (File.Exists(App.File))
            //        {
            //            System.Diagnostics.Process.Start(App.File);
            //        }
            //        else
            //        {
            //            main.Toast("应用似乎不存在", Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
            //        }
            //        break;
            //}
        }

        private async Task OnClearSelectMonthDataCommand(object obj)
        {
            bool isConfirm = await _uIServicer.ShowConfirmDialogAsync(ResourceStrings.ClearConfirmation,
                string.Format(ResourceStrings.WantClearData, Date.Year, Date.Month));
            if (isConfirm)
            {
                await Clear();
            }
        }

        private void OnBlockActionCommand(object obj)
        {
            if (obj.ToString() == "block")
            {
                config.Behavior.IgnoreProcessList.Add(App.Name);
                IsIgnore = true;
                main.Toast(string.Format(ResourceStrings.ApplicationNowIgnored, App.Name),
                    Controls.Window.ToastType.Success);
            }
            else
            {
                config.Behavior.IgnoreProcessList.Remove(App.Name);
                IsIgnore = false;
                main.Toast(string.Format(ResourceStrings.IgnoringApplicationCancelled, App.Name),
                    Controls.Window.ToastType.Success);
            }

            appConfig.Save();
        }

        private async void DetailPageVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Date))
            {
                await LoadData();
            }

            if (e.PropertyName == nameof(Category))
            {
                if (App.Category == null && Category != null || Category != null && App.Category.Name != Category.Name)
                {
                    App.Category = Category.Data as CategoryModel;

                    var app = appData.GetApp(App.ID);
                    if (app != null)
                    {
                        app.CategoryID = App.Category.ID;
                        appData.UpdateApp(app);
                    }
                }
            }

            //  处理图表数据加载
            if (e.PropertyName == nameof(ChartDate))
            {
                await LoadDayData();
            }

            if (e.PropertyName == nameof(TabbarSelectedIndex))
            {
                await LoadChartData();
            }

            if (e.PropertyName == nameof(SelectedWeek))
            {
                await LoadWeekData();
            }

            if (e.PropertyName == nameof(MonthDate))
            {
                await LoadMonthlyData();
            }

            if (e.PropertyName == nameof(YearDate))
            {
                await LoadYearData();
            }
        }

        /// <summary>
        /// 加载柱状图图表数据
        /// </summary>
        private Task LoadChartData()
        {
            if (TabbarSelectedIndex == 0)
            {
                NameIndexStart = 0;

                return LoadDayData();
            }
            else if (TabbarSelectedIndex == 1)
            {
                return LoadWeekData();
            }
            else if (TabbarSelectedIndex == 2)
            {
                NameIndexStart = 1;

                return LoadMonthlyData();
            }
            else if (TabbarSelectedIndex == 3)
            {
                return LoadYearData();
            }

            return Task.CompletedTask;
        }

        private void LoadCategorys(string categoryName)
        {
            var list = new List<SelectItemModel>();
            foreach (var item in categories.GetCategories())
            {
                var option = new SelectItemModel()
                {
                    Id = item.ID,
                    Data = item,
                    Img = item.IconFile,
                    Name = item.Name,
                };
                list.Add(option);
                if (categoryName == option.Name)
                {
                    Category = option;
                }
            }

            Categorys = list;
        }

        private Task LoadInfo()
        {
            return Task.Run(() =>
                {
                    if (App != null)
                    {
                        //  判断是否是忽略的进程
                        IsIgnore = config.Behavior.IgnoreProcessList.Contains(App.Name);

                        LoadCategorys(App.Category?.Name);

                        //  读取关联应用数据
                        var link = config.Links.Where(m => m.ProcessList.Contains(App.Name)).FirstOrDefault();
                        if (link != null)
                        {
                            LinkApps = appData.GetAllApps()
                                .Where(m => link.ProcessList.Contains(m.Name) && m.Name != App.Name).ToList();
                        }
                    }
                }
            );
        }

        private async Task LoadData()
        {
            if (App != null)
            {
                var monthData = await data.GetProcessMonthLogListAsync(App.ID, Date);
                int monthTotal = monthData.Sum(m => m.Time);
                Total = Time.ToString(monthTotal);

                DateTime start = new DateTime(Date.Year, Date.Month, 1);
                DateTime end = new DateTime(Date.Year, Date.Month, DateTime.DaysInMonth(Date.Year, Date.Month));
                var monthAllData = await data.GetDateRangelogListAsync(start, end);

                LongDay = ResourceStrings.NoData;
                if (monthData.Count > 0)
                {
                    var longDayData = monthData.OrderByDescending(m => m.Time).FirstOrDefault();
                    LongDay = string.Format(ResourceStrings.LongDayTips, longDayData.Date.ToString("dd"),
                        Time.ToString(longDayData.Time));
                }


                int monthAllTotal = monthAllData.Sum(m => m.Time);
                if (monthAllTotal > 0)
                {
                    Ratio = (((double)monthTotal / (double)monthAllTotal)).ToString("P");
                }
                else
                {
                    Ratio = ResourceStrings.NoData;
                }

                Data = MapToChartsData(monthData);
                if (Data.Count == 0)
                {
                    Data.Add(new ChartsDataModel()
                    {
                        DateTime = Date,
                    });
                }
            }
        }

        private Task Clear()
        {
            return Task.Run(async () =>
                {
                    if (App != null)
                    {
                        main.Toast(ResourceStrings.Processing);
                        await data.ClearAsync(App.ID, Date);

                        await LoadData();
                        await LoadInfo();
                        main.Toast(ResourceStrings.Cleared);
                    }
                }
            );
        }


        private List<ChartsDataModel> MapToChartsData(IReadOnlyCollection<Core.Models.DailyLogModel> list)
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
                bindModel.PopupText = item.AppModel.File;
                bindModel.Icon = item.AppModel.IconFile;
                bindModel.DateTime = item.Date;
                resData.Add(bindModel);
            }

            return resData;
        }

        #region 柱状图图表数据加载

        /// <summary>
        /// 加载天数据
        /// </summary>
        private async Task LoadDayData()
        {
            DataMaximum = 3600;

            var list = await data.GetAppDayDataAsync(App.ID, ChartDate);

            var chartData = new List<ChartsDataModel>();

            foreach (var item in list)
            {
                chartData.Add(new ChartsDataModel()
                {
                    Name = App.Description,
                    Icon = App.IconFile,
                    Values = item.Values,
                });
            }

            ChartData = chartData;
        }

        /// <summary>
        /// 加载周数据
        /// </summary>
        private async Task LoadWeekData()
        {
            DataMaximum = 0;
            var culture = SystemLanguage.CurrentCultureInfo;
            var weekDateArr = SelectedWeek.Name == ResourceStrings.ThisWeek
                ? Time.GetThisWeekDate()
                : Time.GetLastWeekDate();

            WeekDateStr = weekDateArr[0].ToString("d", culture) + $" {Application.Current.Resources["To"]} " +
                          weekDateArr[1].ToString("d", culture);

            var list = await data.GetAppRangeDataAsync(App.ID, weekDateArr[0], weekDateArr[1]);

            var chartData = new List<ChartsDataModel>();

            string[] weekNames =
            [
                ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
                ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
            ];
            foreach (var item in list)
            {
                chartData.Add(new ChartsDataModel()
                {
                    Name = App.Description,
                    Icon = App.IconFile,
                    Values = item.Values,
                    ColumnNames = weekNames
                });
            }

            ChartData = chartData;
        }

        /// <summary>
        /// 加载月数据
        /// </summary>
        private async Task LoadMonthlyData()
        {
            DataMaximum = 0;
            var dateArr = Time.GetMonthDate(MonthDate);

            var list = await data.GetAppRangeDataAsync(App.ID, dateArr[0], dateArr[1]);

            var chartData = new List<ChartsDataModel>();

            foreach (var item in list)
            {
                chartData.Add(new ChartsDataModel()
                {
                    Name = App.Description,
                    Icon = App.IconFile,
                    Values = item.Values,
                });
            }

            ChartData = chartData;
        }

        /// <summary>
        /// 加载年份数据
        /// </summary>
        private async Task LoadYearData()
        {
            DataMaximum = 0;

            var list = await data.GetAppYearDataAsync(App.ID, YearDate);

            var chartData = new List<ChartsDataModel>();

            string[] names = new string[12];
            for (int i = 0; i < 12; i++)
            {
                names[i] = Application.Current.Resources[$"{i + 1}Month"] as string;
            }

            foreach (var item in list)
            {
                chartData.Add(new ChartsDataModel()
                {
                    Name = App.Description,
                    Icon = App.IconFile,
                    Values = item.Values,
                    ColumnNames = names
                });
            }

            ChartData = chartData;
        }

        #endregion
    }
}