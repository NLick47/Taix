using Core.Models.Db;
using Core.Models;
using Core.Servicers.Interfaces;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;
using UI.Controls.Select;
using UI.Models;
using System.Reactive;
using Core.Librarys;
using UI.Views;
using Avalonia;
using Avalonia.Controls;
using UI.Servicers;

namespace UI.ViewModels
{
    public class IndexPageViewModel : IndexPageModel
    {
        public ReactiveCommand<object, Unit> ToDetailCommand { get; }
        public ReactiveCommand<object, Unit> RefreshCommand { get; }
        private readonly IData data;
        private readonly MainViewModel main;
        private readonly IMain mainServicer;
        private readonly IAppConfig appConfig;
        private readonly IWebData _webData;
        private readonly IWebSiteContextMenuServicer _webSiteContextMenu;
        private readonly IAppContextMenuServicer appContextMenuServicer;

        public List<SelectItemModel> MoreTypeOptions { get; private set; }

        public IndexPageViewModel(
          IData data,
          MainViewModel main,
          IMain mainServicer,
          IAppConfig appConfig,
          IWebSiteContextMenuServicer webSiteContext_,
           IAppContextMenuServicer appContextMenuServicer,
          IWebData webData_)
        {
            this.data = data;
            this.main = main;
            this.mainServicer = mainServicer;
            this.appConfig = appConfig;
            _webData = webData_;
            _webSiteContextMenu = webSiteContext_;
            this.appContextMenuServicer = appContextMenuServicer;
            ToDetailCommand = ReactiveCommand.Create<object>(OnTodetailCommand);
            RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshCommand);

            Init();
        }

        private async void Init()
        {
            Application.Current.TryFindResource("Today", out var d);
            Application.Current.TryFindResource("Thisweek", out var w);
            TabbarData = new System.Collections.ObjectModel.ObservableCollection<string>()
            {
                d as string,
                w as string
            };
            TabbarSelectedIndex = 0;
            AppContextMenu = appContextMenuServicer.GetContextMenu();
            WebSiteContextMenu = _webSiteContextMenu.GetContextMenu();
            PropertyChanged += IndexPageVM_PropertyChanged;

            await LoadDataAsync();

            MoreTypeOptions =
            [
                new ()
                {
                    Id=0,
                    Name="应用"
                },
                new ()
                {
                    Id=1,
                    Name="网站"
                }
            ];
            MoreType = MoreTypeOptions[0];
        }

        private async void IndexPageVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabbarSelectedIndex))
            {
                await LoadDataAsync();
            }
        }


        private void OnTodetailCommand(object obj)
        {
            if (obj is ChartsDataModel data)
            {
                if (data.Data is DailyLogModel dailyModel && dailyModel.AppModel != null)
                {
                    main.Data = dailyModel.AppModel;
                    main.Uri = nameof(DetailPage);
                }
                else if (data.Data is WebSiteModel webSiteModel)
                {
                    main.Data = webSiteModel;
                    main.Uri = nameof(WebSiteDetailPage);
                }
            }
        }

        private Task OnRefreshCommand(object obj)
        {
            return LoadDataAsync();
        }

        private Task LoadDataAsync()
        {
            if (IsLoading)
            {
                return Task.CompletedTask;
            }

            FrequentUseNum = appConfig.GetConfig().General.IndexPageFrequentUseNum + 1;
            MoreNum = appConfig.GetConfig().General.IndexPageMoreNum + 1;

            if (TabbarSelectedIndex == 0)
            {
                return Task.WhenAll(LoadTodayData(), LoadTodayMoreData());
            }
            else if (TabbarSelectedIndex == 1)
            {
                return Task.WhenAll(LoadThisWeekData(), LoadThisWeekMoreData());
            }
            return Task.CompletedTask;
        }


        #region 本周数据
        private async Task LoadThisWeekData()
        {
            IsLoading = true;
            var list = await data.GetThisWeeklogListAsync();
            var res = MapToChartsData(list);
            var week = Time.GetThisWeekDate();
            var topWebList = await _webData.GetDateRangeWebSiteListAsync(week[0], week[1], FrequentUseNum);
            IsLoading = false;
            WeekData = res;
            WebFrequentUseData = MapToChartsData(topWebList);

        }
        private async Task LoadThisWeekMoreData()
        {
            IsLoading = true;
            var week = Time.GetThisWeekDate();
            var appMoreData = await data.GetDateRangelogListAsync(week[0], week[1], MoreNum, FrequentUseNum);
            var webMoreData = await _webData.GetDateRangeWebSiteListAsync(week[0], week[1], MoreNum, FrequentUseNum);
            IsLoading = false;
            AppMoreData = MapToChartsData(appMoreData);
            WebMoreData = MapToChartsData(webMoreData);
        }
        #endregion



        #region 今日数据
        private async Task LoadTodayData()
        {
            IsLoading = true;
            var list = await data.GetDateRangelogListAsync(DateTime.Now.Date, DateTime.Now.Date);
            var res = MapToChartsData(list);
            var topWebList = await _webData.GetDateRangeWebSiteListAsync(DateTime.Now, DateTime.Now, FrequentUseNum);

            IsLoading = false;
            WeekData = res;
            WebFrequentUseData = MapToChartsData(topWebList);
        }

        private async Task LoadTodayMoreData()
        {
            IsLoading = true;
            var appMoreData = await data.GetDateRangelogListAsync(DateTime.Now.Date, DateTime.Now.Date, MoreNum, FrequentUseNum);
            var webMoreData = await _webData.GetDateRangeWebSiteListAsync(DateTime.Now.Date, DateTime.Now.Date, MoreNum, FrequentUseNum);
            IsLoading = false;
            AppMoreData = MapToChartsData(appMoreData);
            WebMoreData = MapToChartsData(webMoreData);
        }
        #endregion

        #region 处理数据
        private List<ChartsDataModel> MapToChartsData(IEnumerable<Core.Models.DailyLogModel> list)
        {
#pragma warning disable CS8601 
            return list.Select(v => new ChartsDataModel
            {
                Data = v,
                Name = !string.IsNullOrEmpty(v.AppModel?.Alias) ? v.AppModel.Alias :
                string.IsNullOrEmpty(v.AppModel?.Description) ? v.AppModel.Name : v.AppModel?.Description,
                Value = v.Time,
                Tag = Time.ToString(v.Time),
                PopupText = v.AppModel?.File,
                Icon = v.AppModel?.IconFile,
                DateTime = v.Date,

            }).OrderByDescending(x => x.Value).ToList();
#pragma warning restore CS8601
        }
        private List<ChartsDataModel> MapToChartsData(IEnumerable<Core.Models.Db.WebSiteModel> list)
        {
#pragma warning disable CS8601
            return list.Select(v => new ChartsDataModel
            {
                Data = v,
                Name = !string.IsNullOrEmpty(v.Alias) ? v.Alias : v.Title,
                Value = v.Duration,
                Tag = Time.ToString(v.Duration),
                PopupText = v.Domain,
                Icon = v.IconFile,
            }).OrderByDescending(x => x.Value).ToList();
#pragma warning restore CS8601
        }
        #endregion
    }
}

