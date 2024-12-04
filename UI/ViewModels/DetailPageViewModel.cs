using Avalonia.Controls;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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
    }
}
