using Core.Models;
using Core.Servicers.Interfaces;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Models;
using UI.Models.CategoryAppList;
using UI.Views;

namespace UI.ViewModels
{
    public class CategoryAppListPageViewModel : CategoryAppListPageModel
    {
        private readonly IAppData appData;
        private readonly MainViewModel mainVM;
        private List<ChooseAppModel> appList;
        public ICommand ShowChooseCommand { get; set; }
        public ICommand ChoosedCommand { get; set; }
        public ICommand GotoDetailCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand ChooseCloseCommand { get; set; }
        public ICommand DelCommand { get; set; }

        public CategoryAppListPageViewModel(IAppData appData, MainViewModel mainVM)
        {
            this.appData = appData;
            this.mainVM = mainVM;


            ShowChooseCommand = ReactiveCommand.CreateFromTask<object>(OnShowChoose);
            ChoosedCommand = ReactiveCommand.Create<object>(OnChoosed);
            GotoDetailCommand = ReactiveCommand.Create<object>(OnGotoDetail);
            SearchCommand = ReactiveCommand.CreateFromTask<object>(OnSearch);
            ChooseCloseCommand = ReactiveCommand.Create<object>(OnChooseClose);
            DelCommand = ReactiveCommand.Create<object>(OnDel);
            LoadData();
        }

        public override void Dispose()
        {
            base.Dispose();
            Data = null;
            AppList = null;
        }

        private void OnDel(object obj)
        {
            if (SelectedItem != null)
            {
                var list = Data.ToList();

                list.Remove(SelectedItem);

                var app = appData.GetApp(SelectedItem.ID);
                if (app != null)
                {
                    app.CategoryID = 0;
                    app.Category = null;
                    appData.UpdateApp(app);
                }

                Data = list;
            }
        }

        private void OnChooseClose(object obj)
        {
            ChooseVisibility = false;
            SearchInput = "";
        }

        private Task OnSearch(object obj)
        {
            if(obj == null || string.IsNullOrEmpty(SearchInput)) return Task.CompletedTask;
            string keyword = obj.ToString();

            if (keyword == "vscode")
            {
                keyword = "Visual Studio Code";
            }
            else if (keyword == "ps")
            {
                keyword = "Photoshop";
            }
            return Search(keyword);
        }

        private void OnGotoDetail(object obj)
        {
            if (SelectedItem != null)
            {
                mainVM.Data = SelectedItem;
                mainVM.Uri = nameof(DetailPage);
            }
        }

        private void OnChoosed(object obj)
        {
            ChooseVisibility = false;

            SearchInput = "";

            var data = new List<AppModel>();

            foreach (var item in AppList)
            {

                var app = appData.GetApp(item.App.ID);

                if (item.IsChoosed)
                {
                    //  处理选中
                    if (app.CategoryID != Category.Data.ID)
                    {
                        app.CategoryID = Category.Data.ID;
                        app.Category = Category.Data;
                        appData.UpdateApp(app);
                    }

                    data.Add(app);
                }
                else
                {
                    //  处理取选
                    bool isHas = Data.Where(m => m.ID == item.App.ID).Any();
                    if (isHas)
                    {
                        app.CategoryID = 0;
                        app.Category = null;
                        appData.UpdateApp(app);
                    }
                }
            }
            Data = data;
        }

        private Task OnShowChoose(object obj)
        {
            ChooseVisibility = true;
            return LoadApps();

        }
        private void LoadData()
        {
            Category = mainVM.Data as UI.Models.Category.CategoryModel;
            Data = appData.GetAppsByCategoryID(Category.Data.ID);
        }

        private Task LoadApps()
        {
            return Task.Run(() =>
            {
                appList = new List<ChooseAppModel>();

                if (Category == null)
                {
                    return;
                }
                foreach (var item in appData.GetAllApps())
                {
                    var app = new ChooseAppModel();
                    app.App = item;
                    app.IsChoosed = item.CategoryID == Category.Data.ID;
                    app.Value.Name = String.IsNullOrEmpty(item.Description) ? item.Name : item.Description;
                    app.Value.Img = item.IconFile;

                    if (app.IsChoosed || item.CategoryID == 0)
                    {
                        appList.Add(app);
                    }
                }
                appList = appList.OrderBy(m => m.App.Description).ToList();

                AppList = appList;
            });
        }

        private Task Search(string keyword)
        {
            return Task.Run(() =>
             {
                 if (!string.IsNullOrEmpty(keyword))
                 {
                     Debug.WriteLine(keyword);
                     keyword = keyword.ToLower();

                     //var list = appList.Where(m => m.App.Description.ToLower().IndexOf(keyword) != -1 || m.App.Name.ToLower().IndexOf(keyword) != -1).ToList();
                     var list = AppList.ToList();

                     foreach (var item in list)
                     {
                         item.Visibility = item.App.Description != null &&
                         item.App.Description.ToLower().IndexOf(keyword) != -1 ||
                         item.App.Name != null && item.App.Name.ToLower().IndexOf(keyword) != -1;
                        
                     }

                     return list;

                 }
                 else
                 {
                     var list = AppList.ToList();

                     foreach (var item in list)
                     {
                         item.Visibility = true;
                     }

                     return list;

                 }
             })

       .ContinueWith(task =>
       {
           if (task.IsCompletedSuccessfully)
           {
               Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
               {
                   AppList = task.Result;
               });
           }

       });

        }

    }
}
