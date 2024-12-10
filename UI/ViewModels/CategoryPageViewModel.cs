using Avalonia.Controls.ApplicationLifetimes;
using Core.Models;
using Core.Servicers.Interfaces;
using DynamicData.Binding;
using Infrastructure.Librarys;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Models;
using UI.Servicers;
using UI.Views;
using CategoryModel = UI.Models.Category.CategoryModel;
namespace UI.ViewModels
{
    public class CategoryPageViewModel : CategoryPageModel
    {
        private readonly ICategorys categorys;
        private readonly MainViewModel mainVM;
        private readonly IAppData appData;
        private readonly IWebData _webData;
        private readonly IUIServicer _uiServicer;

        public ICommand GotoListCommand { get; private set; }
        /// <summary>
        /// 打开编辑命令
        /// </summary>
        public ICommand EditCommand { get; private set; }
        /// <summary>
        /// 完成编辑命令
        /// </summary>
        public ICommand EditDoneCommand { get; private set; }
        /// <summary>
        /// 取消编辑
        /// </summary>
        public ICommand EditCloseCommand { get; private set; }
        /// <summary>
        /// 删除分类
        /// </summary>
        public ICommand DelCommand { get; private set; }
        /// <summary>
        /// 刷新
        /// </summary>
        public ICommand RefreshCommand { get; private set; }
        /// <summary>
        /// 添加目录
        /// </summary>
        public ICommand AddDirectoryCommand { get; private set; }
        /// <summary>
        /// 目录菜单命令
        /// </summary>
        public ICommand DirectoriesCommand { get; private set; }
        public CategoryPageViewModel(ICategorys categorys, MainViewModel mainVM, 
            IAppData appData, IWebData webData_, IUIServicer uIServicer_)
        {
            this.categorys = categorys;
            this.mainVM = mainVM;
            this.appData = appData;
            this._webData = webData_;
            this._uiServicer = uIServicer_;
            GotoListCommand = ReactiveCommand.Create<object>(OnGotoList);
            EditCommand = ReactiveCommand.Create<object>(OnEdit);
            EditDoneCommand = ReactiveCommand.Create<object>(OnEditDone);
            EditCloseCommand = ReactiveCommand.Create<object>(OnEditClose);
            DelCommand = ReactiveCommand.Create<object>(OnDel);
            RefreshCommand = ReactiveCommand.Create<object>(OnRefresh);
            AddDirectoryCommand = ReactiveCommand.CreateFromTask<object>(OnAddDirectory);
            DirectoriesCommand = ReactiveCommand.Create<object>(OnDirectoriesCommand);
            //LoadData();
        }
        private void OnRefresh(object obj)
        {
            LoadData();
        }


        private void OnDel(object obj)
        {
            if (ShowType.Id == 0)
            {
                DelAppCategory();
            }
            else if (ShowType.Id == 1)
            {
                DelWebSiteCategory();
            }

        }

        private void OnEditClose(object obj)
        {
            EditVisibility = false;
        }

        private void OnEditDone(object obj)
        {
            if (ShowType.Id == 0)
            {
                EditAppCategoryAction();
            }
            else if (ShowType.Id == 1)
            {
                EditWebSiteCategoryAction();
            }
        }

        private async void DelWebSiteCategory()
        {
            if (SelectedWebCategoryItem == null)
            {
                return;
            }
            bool isConfirm = await _uiServicer.ShowConfirmDialogAsync("删除分类", "是否确认删除该分类？");
            if (isConfirm)
            {
                _webData.DeleteWebSiteCategory(SelectedWebCategoryItem.Data);

                //  从界面移除
                WebCategoryData.Remove(SelectedWebCategoryItem);
                if (WebCategoryData.Count == 0)
                {
                    WebCategoryData = new System.Collections.ObjectModel.ObservableCollection<WebCategoryModel>();
                }
                mainVM.Toast("分类已删除", Controls.Window.ToastType.Success);
            }
        }

        private async Task OnAddDirectory(object obj)
        {
            try
            {
                var desk = App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var result = await desk.MainWindow.StorageProvider.OpenFolderPickerAsync(new() { AllowMultiple = false});
                if(result?.Count > 0)
                {
                    var path = result[0].Path.LocalPath;
                    if (EditDirectories.Contains(path))
                    {
                        mainVM.Toast("目录已存在", Controls.Window.ToastType.Error);
                        return;
                    }
                    EditDirectories.Add(path);
                    mainVM.Toast("已添加", Controls.Window.ToastType.Success);
                }
            }
            catch (Exception ec)
            {
                Logger.Error(ec.ToString());
                mainVM.Toast("添加失败，请重试", Controls.Window.ToastType.Error);
            }
        }

        private void OnDirectoriesCommand(object obj)
        {
            string action = obj.ToString();
            switch (action)
            {
                case "remove":
                    if (string.IsNullOrEmpty(EditSelectedDirectory)) return;

                    EditDirectories.Remove(EditSelectedDirectory);
                    break;
            }
        }

        private bool IsEditVerify()
        {
            if (string.IsNullOrEmpty(EditName))
            {
                IsEditError = true;
                EditErrorText = "分类名称不能为空";
                return false;
            }
            if (string.IsNullOrEmpty(EditIconFile))
            {
                mainVM.Toast("请选择一个分类图标", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                return false;
            }
            if (string.IsNullOrEmpty(EditColor))
            {
                mainVM.Toast("请设置一个分类颜色", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                return false;
            }
            if (EditIconFile.IndexOf("pack://") == -1 && new FileInfo(EditIconFile).Length > 1000000)
            {
                mainVM.Toast("图标文件不能超过1MB", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                return false;
            }
            return true;
        }

        private async void EditAppCategoryAction()
        {
            try
            {
                if (!IsEditVerify()) return;

                string directoriesStr = JsonConvert.SerializeObject(EditDirectories.ToList());

                if (IsCreate)
                {
                    if (Data.Where(m => m.Data.Name == EditName).Any())
                    {
                        mainVM.Toast("分类名称已存在", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                        return;
                    }
                    mainVM.Toast("创建完成", Controls.Window.ToastType.Success);

                    EditVisibility = false;

                    var res = await categorys.Create(new Core.Models.CategoryModel()
                    {
                        Name = EditName,
                        IconFile = EditIconFile,
                        Color = EditColor,
                        IsDirectoryMath = EditIsDirectoryMath,
                        Directories = directoriesStr,
                    });

                    var item = new UI.Models.Category.CategoryModel()
                    {
                        Data = res,
                        Count = 0
                    };
                    if (Data.Count == 0)
                    {
                        var list = new System.Collections.ObjectModel.ObservableCollection<CategoryModel>();
                        list.Add(item);
                        Data = list;
                    }
                    else
                    {
                        Data.Add(item);
                    }

                }
                else
                {
                    //  编辑分类

                    //  判断重复分类名称
                    if (Data.Where(m => m.Data.Name == EditName && m.Data.ID != SelectedAppCategoryItem.Data.ID).Any())
                    {
                        mainVM.Toast("分类名称已存在", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                        return;
                    }
                    mainVM.Toast("已更新", Controls.Window.ToastType.Success);
                    var category = categorys.GetCategory(SelectedAppCategoryItem.Data.ID);
                    if (category != null)
                    {
                        category.Name = EditName;
                        category.IconFile = EditIconFile;
                        category.Color = EditColor;
                        category.IsDirectoryMath = EditIsDirectoryMath;
                        category.Directories = directoriesStr;

                        await categorys.Update(category);
                    }

                    var item = Data.Where(m => m.Data.ID == SelectedAppCategoryItem.Data.ID).FirstOrDefault();

                    var changedItem = new UI.Models.Category.CategoryModel()
                    {
                        Count = item.Count,
                        Data = new Core.Models.CategoryModel()
                        {
                            ID = item.Data.ID,
                            Name = EditName,
                            IconFile = EditIconFile,
                            Color = EditColor,
                            IsDirectoryMath = EditIsDirectoryMath,
                            Directories = directoriesStr
                        }
                    };

                    int editItemIndex = Data.IndexOf(item);
                    if (editItemIndex != -1)
                    {
                        Data[editItemIndex] = changedItem;
                    }

                    EditVisibility = false;
                }
            }
            catch (Exception ec)
            {
                Logger.Error(ec.ToString());
                mainVM.Toast("操作失败，请重试", Controls.Window.ToastType.Error);
            }
        }

        private async void DelAppCategory()
        {
            if (SelectedAppCategoryItem == null)
            {
                return;
            }
            bool isConfirm = await _uiServicer.ShowConfirmDialogAsync("删除分类", "是否确认删除该分类？");
            if (isConfirm)
            {
                var category = categorys.GetCategory(SelectedAppCategoryItem.Data.ID);
                if (category != null)
                {
                    await categorys.Delete(category);
                    var apps = appData.GetAppsByCategoryID(category.ID);
                    foreach (var app in apps)
                    {
                        app.CategoryID = 0;
                        app.Category = null;
                        appData.UpdateApp(app);
                    }

                    //  从界面移除

                    Data.Remove(SelectedAppCategoryItem);
                    if (Data.Count == 0)
                    {
                        Data = new System.Collections.ObjectModel.ObservableCollection<CategoryModel>();
                    }
                    mainVM.Toast("分类已删除", Controls.Window.ToastType.Success);

                }
            }
        }

        private async void EditWebSiteCategoryAction()
        {
            if (!IsEditVerify()) return;

            if (IsCreate)
            {
                //  创建分类

                //  判断重复分类名称
                if (WebCategoryData.Where(m => m.Data.Name == EditName).Any())
                {
                    mainVM.Toast("分类名称已存在", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                    return;
                }
                if (WebCategoryData.Where(m => m.Data.Color == EditColor).Any())
                {
                    mainVM.Toast("颜色已存在，请重新选择", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                    return;
                }

                var category = await _webData.CreateWebSiteCategory(new Core.Models.Db.WebSiteCategoryModel()
                {
                    Color = EditColor,
                    IconFile = EditIconFile,
                    Name = EditName,
                });

                if (category != null)
                {
                    mainVM.Toast("创建完成", Controls.Window.ToastType.Success);
                    EditVisibility = false;

                    var webCategory = new WebCategoryModel()
                    {
                        Count = 0,
                        Data = category,
                    };
                    if (WebCategoryData.Count == 0)
                    {
                        WebCategoryData = new System.Collections.ObjectModel.ObservableCollection<WebCategoryModel>()
                        {
                            webCategory
                        };
                    }
                    else
                    {
                        WebCategoryData.Add(webCategory);
                    }

                }
                else
                {
                    mainVM.Toast("创建失败", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                }



            }
            else
            {
                //  编辑分类

                //  判断重复分类名称
                if (WebCategoryData.Where(m => m.Data.Name == EditName && m.Data.ID != SelectedWebCategoryItem.Data.ID).Any())
                {
                    mainVM.Toast("分类名称已存在", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                    return;
                }
                if (WebCategoryData.Where(m => m.Data.Color == EditColor && m.Data.ID != SelectedWebCategoryItem.Data.ID).Any())
                {
                    mainVM.Toast("颜色已存在，请重新选择", Controls.Window.ToastType.Error, Controls.Base.IconTypes.ImportantBadge12);
                    return;
                }
                if (EditName == SelectedWebCategoryItem.Data.Name && EditIconFile == SelectedWebCategoryItem.Data.IconFile && EditColor == SelectedWebCategoryItem.Data.Color)
                {
                    mainVM.Toast("没有修改");
                    EditVisibility = false;
                    return;
                }
                mainVM.Toast("已更新", Controls.Window.ToastType.Success);

                var category = SelectedWebCategoryItem.Data;
                category.Name = EditName;
                category.IconFile = EditIconFile;
                category.Color = EditColor;

                _webData.UpdateWebSiteCategory(category);

                var item = WebCategoryData.Where(m => m.Data.ID == category.ID).FirstOrDefault();
                var index = WebCategoryData.IndexOf(item);
                WebCategoryData[index] = new WebCategoryModel()
                {
                    Count = SelectedWebCategoryItem.Count,
                    Data = category
                };
                EditVisibility = false;
            }
        }

        private void OnEdit(object obj)
        {
            EditVisibility = true;
            IsCreate = obj == null;

            if (obj != null)
            {
                var appCategory = obj as CategoryModel;
                var webCategory = obj as WebCategoryModel;

                EditName = appCategory == null ? webCategory.Data.Name : appCategory.Data.Name;
                EditIconFile = appCategory == null ? webCategory.Data.IconFile : appCategory.Data.IconFile;
                EditColor = string.IsNullOrEmpty(appCategory == null ? webCategory.Data.Color : appCategory.Data.Color) ? "#00FFAB" : appCategory == null ? webCategory.Data.Color : appCategory.Data.Color;
                if (ShowType.Id == 0)
                {
                    EditIsDirectoryMath = appCategory.Data.IsDirectoryMath;
                    if (appCategory.Data.Directories != null)
                    {
                        EditDirectories = new System.Collections.ObjectModel.ObservableCollection<string>(appCategory.Data.DirectoryList);
                    }
                    else
                    {
                        EditDirectories = new System.Collections.ObjectModel.ObservableCollection<string>();
                    }
                }
            }
            else
            {
                EditName = "";
                EditIconFile = "avares://Taix/Resources/Emoji/(1).png";
                EditColor = "#00FFAB";
                EditIsDirectoryMath = false;
                EditDirectories = new System.Collections.ObjectModel.ObservableCollection<string>();
            }
        }

        private void CategoryPageVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedAppCategoryItem))
            {
                //  ();
            }
        }

        private void OnGotoList(object obj)
        {
            GotoList();
        }

        private async void LoadData()
        {
            Data = new ObservableCollection<CategoryModel>();
            foreach (var item in categorys.GetCategories())
            {
                Data.Add(new CategoryModel()
                {
                    Count = appData.GetAppsByCategoryID(item.ID).Count,
                    Data = item
                });
            }

            var webCategories = await _webData.GetWebSiteCategories();
            var webCategoryData = new List<WebCategoryModel>();

            foreach (var item in webCategories)
            {
                webCategoryData.Add(new WebCategoryModel()
                {
                    Data = item,
                    Count = await _webData.GetWebSitesCount(item.ID)
                });
            }
            WebCategoryData = new ObservableCollection<WebCategoryModel>(webCategoryData);
        }

        private void GotoList()
        {
            if (ShowType.Id == 0 && SelectedAppCategoryItem != null)
            {
                mainVM.Data = SelectedAppCategoryItem;
                mainVM.Uri = nameof(CategoryAppListPage);
                SelectedAppCategoryItem = null;
            }
            else if (ShowType.Id == 1 && SelectedWebCategoryItem != null)
            {
                mainVM.Data = SelectedWebCategoryItem.Data;
                mainVM.Uri = nameof(CategoryWebSiteListPage);
                SelectedWebCategoryItem = null;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            PropertyChanged -= CategoryPageVM_PropertyChanged;
            Data = null;
        }

    }
}
