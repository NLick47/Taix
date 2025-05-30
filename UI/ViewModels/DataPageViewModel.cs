﻿using Core.Librarys;
using Core.Models;
using Core.Servicers.Interfaces;
using SharedLibrary.Librarys;
using ReactiveUI;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Controls.Charts.Model;
using UI.Models;
using UI.Servicers;
using UI.Views;

namespace UI.ViewModels
{
    public class DataPageViewModel : DataPageModel
    {
        public ICommand ToDetailCommand { get; set; }
        private readonly IData data;
        private readonly MainViewModel main;
        private readonly IAppContextMenuServicer appContextMenuServicer;
        private readonly IAppConfig appConfig;
        private readonly IWebData _webData;
        private readonly IWebSiteContextMenuServicer _webSiteContextMenu;

        public DataPageViewModel(IData data, MainViewModel main, IAppConfig appConfig,
            IWebSiteContextMenuServicer webSiteContextMenu,
            IAppContextMenuServicer contextMenuServicer, IWebData webData)
        {
            this.data = data;
            this.main = main;
            this.appConfig = appConfig;
            this.appContextMenuServicer = contextMenuServicer;
            this._webSiteContextMenu = webSiteContextMenu;
            _webData = webData;
            ToDetailCommand = ReactiveCommand.Create<object>(OnTodetailCommand);
            Init();
        }

        public override void Dispose()
        {
            PropertyChanged -= DataPageVM_PropertyChanged;

            base.Dispose();
        }

        private void Init()
        {
            PropertyChanged += DataPageVM_PropertyChanged;

            TabbarData = [
                ResourceStrings.Daily,ResourceStrings.Monthly,ResourceStrings.Yearly
            ];

            TabbarSelectedIndex = 0;

            AppContextMenu = appContextMenuServicer.GetContextMenu();
        }

        private void OnTodetailCommand(object obj)
        {
            var data = obj as ChartsDataModel;

            if (data != null)
            {
                var model = data.Data as DailyLogModel;
                if (model != null && model.AppModel != null)
                {
                    main.Data = model.AppModel;
                    main.Uri = nameof(DetailPage);
                }
                else
                {
                    main.Data = data.Data as Core.Models.Db.WebSiteModel;
                    main.Uri = nameof(WebSiteDetailPage);
                }
            }
        }

        private async void DataPageVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

            if (e.PropertyName == nameof(DayDate))
            {
                await LoadData(DayDate);
            }
            else if (e.PropertyName == nameof(MonthDate))
            {
                await LoadData(MonthDate);
            }
            else if (e.PropertyName == nameof(YearDate))
            {
                await LoadData(YearDate);
            }
            else if (e.PropertyName == nameof(TabbarSelectedIndex))
            {
                if (TabbarSelectedIndex == 0)
                {
                    if (DayDate == DateTime.MinValue)
                    {
                        DayDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                    }

                }
                else if (TabbarSelectedIndex == 1)
                {
                    if (MonthDate == DateTime.MinValue)
                    {
                        MonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    }
                }
                else if (TabbarSelectedIndex == 2)
                {
                    if (YearDate == DateTime.MinValue)
                    {
                        YearDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    }
                }
            }
            else if (e.PropertyName == nameof(ShowType))
            {
                await LoadData(DayDate, 0);
                await LoadData(MonthDate, 1);
                await LoadData(YearDate, 2);
                if (ShowType.Id == 0)
                {
                    AppContextMenu = appContextMenuServicer.GetContextMenu();
                }
                else
                {
                    AppContextMenu = _webSiteContextMenu.GetContextMenu();
                }
            }
        }

        private async Task LoadData(DateTime date, int dataType_ = -1)
        {

            DateTime dateStart = date, dateEnd = date;

            dataType_ = dataType_ == -1 ? TabbarSelectedIndex : dataType_;

            if (dataType_ == 1)
            {
                dateStart = new DateTime(date.Year, date.Month, 1);
                dateEnd = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
            }
            else if (dataType_ == 0)
            {
                dateStart = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                dateEnd = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);
            }
            else if (dataType_ == 2)
            {
                dateStart = new DateTime(date.Year, 1, 1, 0, 0, 0);
                dateEnd = new DateTime(date.Year, 12, DateTime.DaysInMonth(date.Year, 12), 23, 59, 59);
            }

            List<ChartsDataModel> chartData = new List<ChartsDataModel>();
            if (ShowType.Id == 0)
            {
                var result = await data.GetDateRangelogListAsync(dateStart, dateEnd);
                chartData = MapToChartsData(result);
            }
            else
            {
                var result = await _webData.GetWebSiteLogListAsync(dateStart, dateEnd);
                chartData = MapToChartsWebData(result);
            }


            if (dataType_ == 0)
            {
                Data = chartData;
            }
            else if (dataType_ == 1)
            {
                MonthData = chartData;
            }
            else
            {
                YearData = chartData;
            }
        }

        private List<ChartsDataModel> MapToChartsData(IEnumerable<Core.Models.DailyLogModel> list)
        {
            var resData = new List<ChartsDataModel>();
            try
            {
                var config = appConfig.GetConfig();

                foreach (var item in list)
                {
                    var bindModel = new ChartsDataModel();
                    bindModel.Data = item;
                    bindModel.Name = !string.IsNullOrEmpty(item.AppModel?.Alias) ? item.AppModel.Alias : string.IsNullOrEmpty(item.AppModel?.Description) ? item.AppModel.Name : item.AppModel.Description;
                    bindModel.Value = item.Time;
                    bindModel.Tag = Time.ToString(item.Time);
                    bindModel.PopupText = item.AppModel?.File;
                    bindModel.Icon = item.AppModel?.IconFile;
                    bindModel.BadgeList = new List<ChartBadgeModel>();
                    if (item.AppModel.Category != null)
                    {
                        bindModel.BadgeList.Add(new ChartBadgeModel()
                        {
                            Name = item.AppModel.Category.Name,
                            Color = item.AppModel.Category.Color,
                            Type = ChartBadgeType.Category
                        });
                    }
                    if (config.Behavior.IgnoreProcessList.Contains(item.AppModel.Name))
                    {
                        bindModel.BadgeList.Add(ChartBadgeModel.IgnoreBadge);
                    }
                    resData.Add(bindModel);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }

            return resData;
        }

        private List<ChartsDataModel> MapToChartsWebData(IEnumerable<Core.Models.Db.WebSiteModel> list)
        {
            var resData = new List<ChartsDataModel>();
            try
            {
                var config = appConfig.GetConfig();

                foreach (var item in list)
                {
                    var bindModel = new ChartsDataModel();
                    bindModel.Data = item;
                    bindModel.Name = !string.IsNullOrEmpty(item.Alias) ? item.Alias : item.Title;
                    bindModel.Value = item.Duration;
                    bindModel.Tag = Time.ToString(item.Duration);
                    bindModel.PopupText = item.Domain;
                    bindModel.Icon = item.IconFile;
                    bindModel.BadgeList = new List<ChartBadgeModel>();
                    if (item.Category != null)
                    {
                        bindModel.BadgeList.Add(new ChartBadgeModel()
                        {
                            Name = item.Category.Name,
                            Color = item.Category.Color,
                            Type = ChartBadgeType.Category
                        });
                    }
                    if (config.Behavior.IgnoreURLList.Contains(item.Domain))
                    {
                        bindModel.BadgeList.Add(ChartBadgeModel.IgnoreBadge);
                    }
                    resData.Add(bindModel);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }
            return resData;
        }
    }
}
