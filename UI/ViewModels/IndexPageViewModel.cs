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

            await LoadData();

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
                await LoadData();
            }
        }


        private void OnTodetailCommand(object obj)
        {
            var data = obj as ChartsDataModel;

            if (data != null)
            {
                if (data.Data is DailyLogModel)
                {
                    var model = data.Data as DailyLogModel;
                    if (model != null && model.AppModel != null)
                    {
                        main.Data = model.AppModel;
                        main.Uri = nameof(DetailPage);
                    }
                }
                else if (data.Data is WebSiteModel)
                {
                    main.Data = data.Data;
                    main.Uri = nameof(WebSiteDetailPage);
                }

            }
        }

        private Task OnRefreshCommand(object obj)
        {
            return LoadData();
        }

        private async Task LoadData()
        {
            if (IsLoading)
            {
                return;
            }

            FrequentUseNum = appConfig.GetConfig().General.IndexPageFrequentUseNum + 1;
            MoreNum = appConfig.GetConfig().General.IndexPageMoreNum + 1;

            if (TabbarSelectedIndex == 0)
            {
                await LoadTodayData();
                await LoadTodayMoreData();
            }
            else if (TabbarSelectedIndex == 1)
            {
                await LoadThisWeekData();
                await LoadThisWeekMoreData();
            }
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
            var resData = new List<ChartsDataModel>();

            foreach (var item in list)
            {
                var bindModel = new ChartsDataModel();
                bindModel.Data = item;
                bindModel.Name = !string.IsNullOrEmpty(item.AppModel?.Alias) ? item.AppModel.Alias : string.IsNullOrEmpty(item.AppModel?.Description) ? item.AppModel.Name : item.AppModel.Description;
                bindModel.Value = item.Time;
                bindModel.Tag = Time.ToString(item.Time);
                bindModel.PopupText = item.AppModel?.File;
                bindModel.Icon = item.AppModel?.IconFile;
                bindModel.DateTime = item.Date;
                resData.Add(bindModel);
            }

            return resData;
        }
        private List<ChartsDataModel> MapToChartsData(IEnumerable<Core.Models.Db.WebSiteModel> list)
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
}

