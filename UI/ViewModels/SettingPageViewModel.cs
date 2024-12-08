using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Models;
using UI.Servicers;
using UI.Servicers.Dialogs;

    
namespace UI.ViewModels
{
    public class SettingPageViewModel : SettingPageModel
    {
        private ConfigModel config;
        private readonly IAppConfig appConfig;
        private readonly MainViewModel mainVM;
        private readonly IData data;
        private readonly IWebData _webData;
        private readonly IUIServicer _uiServicer;
        public ICommand OpenURL { get; set; }
        public ICommand CheckUpdate { get; set; }
        public ICommand DelDataCommand { get; set; }
        public ICommand ExportDataCommand { get; set; }

        public SettingPageViewModel(IAppConfig appConfig, MainViewModel mainVM, IData data, IWebData webData, IUIServicer uiServicer_)
        {
            this.appConfig = appConfig;
            this.mainVM = mainVM;
            this.data = data;
            _webData = webData;
            _uiServicer = uiServicer_;

            //OpenURL = new Command(new Action<object>(OnOpenURL));
            //CheckUpdate = new Command(new Action<object>(OnCheckUpdate));
            //DelDataCommand = new Command(new Action<object>(OnDelData));
            //ExportDataCommand = new Command(new Action<object>(OnExportData));

            Init();
           
        }

        private void Init()
        {
    #if DEBUG
            appConfig.Load();
    #endif
            config = appConfig.GetConfig();

            Data = config.General;

            TabbarData = new System.Collections.ObjectModel.ObservableCollection<string>()
            {
                "常规","关联","行为","数据","关于"
            };

            PropertyChanged += SettingPageVM_PropertyChanged;

            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            DelDataStartMonthDate = DateTime.Now;
            DelDataEndMonthDate = DateTime.Now;

            ExportDataStartMonthDate = DateTime.Now;
            ExportDataEndMonthDate = DateTime.Now;
        }

        private async Task OnDelData(object obj)
        {
            if (DelDataStartMonthDate > DelDataEndMonthDate)
            {
                mainVM.Toast("时间范围选择错误", Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
                return;
            }

            bool isConfirm = await _uiServicer.ShowConfirmDialogAsync("删除确认", "是否执行此操作？");
            if (isConfirm)
            {
                await data.ClearRange(DelDataStartMonthDate, DelDataEndMonthDate);
                await _webData.Clear(DelDataStartMonthDate, DelDataEndMonthDate);
                mainVM.Toast("操作已完成", Controls.Window.ToastType.Success);
            }
        }

        private void OnExportData(object obj)
        {
          
        }


        private void SettingPageVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Data))
            {
                if (TabbarSelectedIndex == 0)
                {
                    config.General = Data as GeneralModel;
                }
                else if (TabbarSelectedIndex == 1)
                {
                    if (Data != null)
                    {
                        var newData = new List<LinkModel>();
                        foreach (var item in Data as IEnumerable<object>)
                        {
                            newData.Add(item as LinkModel);
                            Debug.WriteLine(item.ToString());
                        }
                        config.Links = newData;
                    }

                }
                else if (TabbarSelectedIndex == 2)
                {
                    config.Behavior = Data as BehaviorModel;
                }

                appConfig.Save();
            }

            if (e.PropertyName == nameof(TabbarSelectedIndex))
            {
                if (TabbarSelectedIndex == 0)
                {
                    //  常规
                    Data = config.General;
                }
                else if (TabbarSelectedIndex == 1)
                {
                    //  关联
                    Data = config.Links;
                }
                else if (TabbarSelectedIndex == 2)
                {
                    //  行为
                    Data = config.Behavior;
                }
            }
        }
    }
}
