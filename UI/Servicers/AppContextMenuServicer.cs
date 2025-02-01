using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Core.Models;
using Core.Servicers.Interfaces;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using UI.Controls.Charts.Model;
using UI.ViewModels;
using UI.Views;

namespace UI.Servicers
{
    public class AppContextMenuServicer : IAppContextMenuServicer
    {
        private readonly MainViewModel main;
        private readonly ICategorys categorys;
        private readonly IAppData appData;
        private readonly IAppConfig appConfig;
        private readonly IThemeServicer theme;
        private readonly IUIServicer _uIServicer;
        private readonly  MainWindow _mainWindow;
        private ContextMenu menu;
        private MenuItem setCategory;
        private MenuItem setLink;
        MenuItem block = new MenuItem();
        MenuItem _whiteList = new MenuItem();
      
        public AppContextMenuServicer(
          MainViewModel main,
          ICategorys categorys,
          IAppData appData,
          IAppConfig appConfig,
          IThemeServicer theme,
          MainWindow mainWindow,
          IUIServicer uIServicer_)
        {
            this.main = main;
            this.categorys = categorys;
            this.appData = appData;
            this.appConfig = appConfig;
            this.theme = theme;
            this._uIServicer = uIServicer_;
            this._mainWindow = mainWindow;

        }


        public void Init()
        {
            CreateMenu();
            _mainWindow.PointerPressed += OnGlobalPointerPressed;
        }

        private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed )
            {
                CloseAllContextMenus(_mainWindow);
            }
        }
        
        private void CloseAllContextMenus(Visual visual)
        {
            if (visual is Control ctl
                && ctl.ContextMenu != null 
                && ctl.ContextMenu.IsOpen )
            {
                ctl.ContextMenu.Close();
                return;
            }

            foreach (var child in visual.GetVisualChildren())
            {
                CloseAllContextMenus(child);
            }
        }

        private void CreateMenu()
        {
            if (menu != null)
            {
                menu.Opening -= SetCategory_ContextMenuOpening;
            }
            menu = new ContextMenu();
            menu.Items.Clear();

            MenuItem run = new MenuItem();
            run.Header = ResourceStrings.StartApplication;
            run.PointerPressed += Run_Click;

            MenuItem openDir = new MenuItem();
            openDir.Header = ResourceStrings.OpenApplicationDirectory;
            openDir.PointerPressed += OpenDir_Click;

            setCategory = new MenuItem();
            setCategory.Header = ResourceStrings.SetCategory;

            MenuItem editAlias = new MenuItem();
            editAlias.Header = ResourceStrings.EditAlias;
            editAlias.Click += EditAlias_ClickAsync;

            setLink = new MenuItem();
            setLink.Header = ResourceStrings.AddAssociation;

            block.Header = ResourceStrings.IgnoreThisApplication;
            block.PointerPressed += Block_Click;

            _whiteList.Header = ResourceStrings.AddWhitelist;
            _whiteList.PointerPressed += _whiteList_Click;

            menu.Items.Add(run);
            menu.Items.Add(new Separator());
            menu.Items.Add(setCategory);
            menu.Items.Add(setLink);
            menu.Items.Add(editAlias);
            menu.Items.Add(new Separator());

            menu.Items.Add(openDir);
            menu.Items.Add(block);
            menu.Items.Add(_whiteList);
            
            menu.Opening += SetCategory_ContextMenuOpening;
        
        }


        private async void EditAlias_ClickAsync(object sender, RoutedEventArgs e)
        {
            var data = menu.Tag as ChartsDataModel;
            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }

            try
            {
                string input = await _uIServicer.ShowInputModalAsync(ResourceStrings.UpdateAlias, ResourceStrings.EnterAlias, app.Alias, (val) =>
                {
                    if (val.Length > 15)
                    {
                        main.Error(string.Format(ResourceStrings.AliasMaxLengthTip,15));
                        return false;
                    }
                    return true;
                });

                //  开始更新别名
                var editApp = appData.GetApp(app.ID);
                editApp.Alias = input;
                appData.UpdateApp(editApp);
                data.Name = string.IsNullOrEmpty(input) ? editApp.Description : input;

                main.Success(ResourceStrings.AliasUpdated);
                Debug.WriteLine("输入内容：" + input);
            }
            catch
            {
                //  输入取消，无需处理异常
            }
        }

       

        private void Block_Click(object sender, PointerPressedEventArgs e)
        {
            var data = menu.Tag as ChartsDataModel;
            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }
            var newBadgeList = new List<ChartBadgeModel>();
            if (data.BadgeList != null)
            {
                var categoryBadge = data.BadgeList.Where(m => m.Type != ChartBadgeType.Ignore).ToList();
                newBadgeList.AddRange(categoryBadge);
            }

            var config = appConfig.GetConfig();
            if (config.Behavior.IgnoreProcessList.Contains(app.Name))
            {
                config.Behavior.IgnoreProcessList.Remove(app.Name);
                main.Toast(string.Format(ResourceStrings.IgnoringApplicationCancelled,app.Description), Controls.Window.ToastType.Success);
            }
            else
            {
                config.Behavior.IgnoreProcessList.Add(app.Name);
                main.Toast(string.Format(ResourceStrings.ApplicationNowIgnored,app.Description), Controls.Window.ToastType.Success);
                newBadgeList.Add(ChartBadgeModel.IgnoreBadge);
            }
            data.BadgeList = newBadgeList;
        }

        private void SetCategory_ContextMenuOpening(object sender, CancelEventArgs e)
        {
            if (menu.Tag == null)
            {
                return;
            }
            var data = menu.Tag as ChartsDataModel;
            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }


            var config = appConfig.GetConfig();
            if (config.Behavior.IgnoreProcessList.Contains(app.Name))
            {
                block.Header = ResourceStrings.Unignore;
            }
            else
            {
                block.Header = ResourceStrings.IgnoreThisApplication;
            }

            if (config.Behavior.ProcessWhiteList.Contains(app.Name))
            {
                _whiteList.Header = ResourceStrings.RemoveWhitelist;
            }
            else
            {
                _whiteList.Header = ResourceStrings.AddWhitelist;
            }

            UpdateCategory();

            setLink.IsEnabled = config.Links.Count > 0;
            UpdateLinks();
        }


        private void UpdateCategory()
        {
            setCategory.Items.Clear();

            var data = menu.Tag as ChartsDataModel;
            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }

            var categories = categorys.GetCategories();
            foreach (var category in categories)
            {
                var categoryMenu = new MenuItem();
                categoryMenu.Header = category.Name;
                categoryMenu.IsChecked = app.CategoryID == category.ID;
                categoryMenu.Click += (s, e) =>
                {
                    SetAppCategory(data, app.ID, category);
                };
                setCategory.Items.Add(categoryMenu);
            }

            setCategory.IsEnabled = setCategory.Items.Count > 0;

        }


        private void UpdateLinks()
        {
            setLink.Items.Clear();

            var data = menu.Tag as ChartsDataModel;
            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }
            var config = appConfig.GetConfig();

            var links = config.Links;
            foreach (var link in links)
            {
                var categoryMenu = new MenuItem();
                categoryMenu.Header = link.Name;
                categoryMenu.Click += (s, e) =>
                {
                    SetLink(app, link.Name);
                };
                setLink.Items.Add(categoryMenu);
            }

        }


        private void SetLink(AppModel app, string linkName)
        {

            var config = appConfig.GetConfig();
            var links = config.Links;
            var link = links.Where(m => m.ProcessList.Contains(app.Name)).FirstOrDefault();
            if (link != null)
            {
                link.ProcessList.Remove(app.Name);
            }

            link = links.Where(m => m.Name == linkName).FirstOrDefault();
            if (link != null)
            {
                link.ProcessList.Add(app.Name);
                appConfig.Save();

                main.Toast(ResourceStrings.AssociationSuccessful, Controls.Window.ToastType.Success);
            }
            else
            {
                main.Toast(ResourceStrings.AssociationConfigurationNotExist, Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
            }
        }

        private void SetAppCategory(ChartsDataModel data, int appId, CategoryModel category)
        {
            var newBadgeList = new List<ChartBadgeModel>();
            if (data.BadgeList != null)
            {
                var otherBadge = data.BadgeList.Where(m => m.Type != ChartBadgeType.Category).ToList();
                newBadgeList.AddRange(otherBadge);
            }

            newBadgeList.Add(new ChartBadgeModel()
            {
                Name = category.Name,
                Color = category.Color,
                Type = ChartBadgeType.Category
            });

            data.BadgeList = newBadgeList;

            var app = appData.GetApp(appId);
            app.CategoryID = category.ID;
            app.Category = category;
            appData.UpdateApp(app);

            main.Toast(ResourceStrings.OperationCompleted, Controls.Window.ToastType.Success);
        }


        private void OpenDir_Click(object sender, PointerPressedEventArgs e)
        {
            var data = menu.Tag as ChartsDataModel;

            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }

            if (File.Exists(app.File))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select, " + app.File);
            }
            else
            {
                main.Toast(ResourceStrings.ApplicationFileExist, Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
            }
        }

        private void Run_Click(object sender, PointerPressedEventArgs e)
        {
            var data = menu.Tag as ChartsDataModel;

            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }

            if (File.Exists(app.File))
            {
                System.Diagnostics.Process.Start(app.File);
                main.Toast(ResourceStrings.OperationCompleted, Controls.Window.ToastType.Info);

            }
            else
            {
                main.Toast(ResourceStrings.ApplicationFileExist, Controls.Window.ToastType.Error, Controls.Base.IconTypes.IncidentTriangle);
            }
        }

        public ContextMenu GetContextMenu()
        {
            return menu;
        }



        private void _whiteList_Click(object sender, PointerPressedEventArgs e)
        {
            var data = menu.Tag as ChartsDataModel;
            var log = data.Data as DailyLogModel;
            var app = log != null ? log.AppModel : null;

            if (log == null)
            {
                app = (data.Data as HoursLogModel).AppModel;
            }
            var config = appConfig.GetConfig();
            if (config.Behavior.ProcessWhiteList.Contains(app.Name))
            {
                config.Behavior.ProcessWhiteList.Remove(app.Name);
                main.Toast($"{ResourceStrings.RemovedApplicationFromWhitelist} {app.Description}", Controls.Window.ToastType.Success);
            }
            else
            {
                config.Behavior.ProcessWhiteList.Add(app.Name);
                main.Toast($"{ResourceStrings.AddedToWhitelist} {app.Description}", Controls.Window.ToastType.Success);
            }
        }
    }
}
