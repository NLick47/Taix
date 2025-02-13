using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Core.Models.Db;
using Core.Servicers.Interfaces;
using SharedLibrary.Librarys;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;
using UI.ViewModels;
using Avalonia.Controls.Documents;
using Core.Servicers.Instances;

namespace UI.Servicers
{
    public class WebSiteContextMenuServicer : IWebSiteContextMenuServicer
    {


        private readonly MainViewModel _main;
        private readonly IAppConfig _appConfig;
        private readonly IThemeServicer _theme;
        private readonly IWebData _webData;
        private readonly IUIServicer _uIServicer;

        private ContextMenu _menu;
        private MenuItem _setCategory;
        private MenuItem _block;
        private MenuItem _site;
        private MenuItem _open;
        private MenuItem _editAlias;
        public WebSiteContextMenuServicer(
            MainViewModel main_,
            IAppConfig appConfig_,
            IThemeServicer theme_,
            IWebData webData_,
             IUIServicer uIServicer_)
        {
            _main = main_;
            _appConfig = appConfig_;
            _theme = theme_;
            _webData = webData_;
            _uIServicer = uIServicer_;
        }

        public void Init()
        {
            InitializeMenuItems();
            SystemLanguage.CurrentLanguageChanged += (s, e) => UpdateMenuTexts();
        }


        private void UpdateMenuTexts()
        {
            _open.Header = ResourceStrings.OpenWebsite;
            _setCategory.Header = ResourceStrings.SetCategory;
            _editAlias.Header = ResourceStrings.EditAlias;
            _block.Header = ResourceStrings.IgnoreSite;
        }

        private void InitializeMenuItems()
        {
            if (_menu != null)
            {
                _menu.Opening -= _menu_ContextMenuOpening;
                _menu.Items.Clear();
            }
            _menu = new ContextMenu();
            _open = new MenuItem();
            _open.PointerPressed += Open_Click; ;
            _setCategory = new MenuItem();

            _editAlias = new MenuItem();

            _editAlias.Click += EditAlias_ClickAsync;

            _block = new MenuItem();

            _block.PointerPressed += Block_Click;

            _site = new MenuItem();
            _site.IsEnabled = false;

            _menu.Items.Add(_site);
            _menu.Items.Add(_open);
            _menu.Items.Add(new Separator());
            _menu.Items.Add(_setCategory);
            _menu.Items.Add(_editAlias);
            _menu.Items.Add(new Separator());
            _menu.Items.Add(_block);
            _menu.Opening += _menu_ContextMenuOpening;
            UpdateMenuTexts();
        }

        private async void EditAlias_ClickAsync(object sender, RoutedEventArgs e)
        {
            var data = _menu.Tag as ChartsDataModel;
            var site = data.Data as WebSiteModel;

            try
            {
                string input = await _uIServicer.ShowInputModalAsync(ResourceStrings.EditAlias, ResourceStrings.EnterAlias, site.Alias, (val) =>
                {
                    if (val?.Length > 15)
                    {
                        _main.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                        return false;
                    }
                    return true;
                });

                //  开始更新别名

                data.Name = string.IsNullOrEmpty(input) ? site.Title : input;
                site.Alias = input;

                await _webData.UpdateAsync(site);

                _main.Success(ResourceStrings.AliasUpdated);
                Debug.WriteLine("输入内容：" + input);
            }
            catch
            {
                //  输入取消，无需处理异常
            }
        }

        private void _menu_ContextMenuOpening(object sender, CancelEventArgs e)
        {
            if (_menu.Tag == null)
            {
                return;
            }
            var data = _menu.Tag as ChartsDataModel;
            var site = data.Data as WebSiteModel;
            _site.Header = site.Title;

            var config = _appConfig.GetConfig();
            if (config.Behavior.IgnoreURLList.Contains(site.Domain))
            {
                _block.Header = ResourceStrings.UnignoreSite;
            }
            else
            {
                _block.Header = ResourceStrings.IgnoreSite;
            }

            UpdateCategoryMenu();
        }

        private void Open_Click(object sender, PointerPressedEventArgs e)
        {
            var data = _menu.Tag as ChartsDataModel;
            var site = data.Data as WebSiteModel;
            if (!string.IsNullOrEmpty(site.Domain))
            {
                _main.Info(ResourceStrings.OperationCompleted);
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = $"http://{site.Domain}",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Logger.Error("打开网址链接" + ex);
                }
            }
        }



        private void Block_Click(object sender, PointerPressedEventArgs e)
        {
            var data = _menu.Tag as ChartsDataModel;
            var site = data.Data as WebSiteModel;
            if (site == null) { return; }

            var newBadgeList = new List<ChartBadgeModel>();
            if (data.BadgeList != null)
            {
                var categoryBadge = data.BadgeList.Where(m => m.Type != ChartBadgeType.Ignore).ToList();
                newBadgeList.AddRange(categoryBadge);
            }

            var config = _appConfig.GetConfig();
            if (config.Behavior.IgnoreURLList.Contains(site.Domain))
            {
                config.Behavior.IgnoreURLList.Remove(site.Domain);
                _main.Toast(string.Format(ResourceStrings.UnignoredDomain, site.Domain), Controls.Window.ToastType.Success);
            }
            else
            {
                config.Behavior.IgnoreURLList.Add(site.Domain);
                _main.Toast(string.Format(ResourceStrings.IgnoredDomain, site.Domain), Controls.Window.ToastType.Success);

                newBadgeList.Add(ChartBadgeModel.IgnoreBadge);
            }
            data.BadgeList = newBadgeList;
        }



        private async void UpdateCategoryMenu()
        {
            _setCategory.Items.Clear();

            var data = _menu.Tag as ChartsDataModel;
            var site = data.Data as WebSiteModel;
            var categories = await _webData.GetWebSiteCategoriesAsync();
            var siteCategoryId = (await _webData.GetWebSiteAsync(site.ID)).CategoryID;
            foreach (var category in categories)
            {
                var categoryMenu = new MenuItem();
                categoryMenu.ToggleType = MenuItemToggleType.Radio;
                categoryMenu.IsChecked = siteCategoryId == category.ID;
                categoryMenu.Header = category.Name;
                categoryMenu.Click += (s, e) =>
                {
                    UpdateSiteCategory(data, category.ID);
                };
                _setCategory.Items.Add(categoryMenu);
            }
            if (siteCategoryId != 0)
            {
                _setCategory.Items.Add(new Separator());
                var un = new MenuItem();
                un.Header = ResourceStrings.Uncategorized;
                un.Click += (s, e) =>
                {
                    ClearSiteCategory();
                };
                _setCategory.Items.Add(un);
            }
        }

        private async void ClearSiteCategory()
        {
            var data = _menu.Tag as ChartsDataModel;
            if(data != null)
            {
                await _webData.UpdateWebSitesCategoryAsync(new int[] { (data.Data as WebSiteModel).ID }, 0);
                data.BadgeList = new();
            }
        }



        private async void UpdateSiteCategory(ChartsDataModel data, int categoryId_)
        {

            var category = await _webData.GetWebSiteCategoryAsync(categoryId_);
            if (category != null)
            {
                WebSiteModel site_ = data.Data as WebSiteModel;
                await _webData.UpdateWebSitesCategoryAsync(new int[] { site_.ID }, categoryId_);
                site_.CategoryID = categoryId_;
                site_.Category = category;

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
            }
        }

        public ContextMenu GetContextMenu()
        {
            return _menu;
        }
    }
}
