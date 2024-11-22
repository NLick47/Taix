using Avalonia.Controls;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
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
        public Command BlockActionCommand { get; set; }
        public Command ClearSelectMonthDataCommand { get; set; }
        public Command RefreshCommand { get; set; }
        private MenuItem _setCategoryMenuItem;
        private MenuItem _whiteListMenuItem;
    }
}
