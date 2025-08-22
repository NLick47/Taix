using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Core.Models.Db;
using Core.Servicers.Interfaces;
using DynamicData;
using Newtonsoft.Json;
using ReactiveUI;
using SharedLibrary.Librarys;
using UI.Controls.Base;
using UI.Controls.Window;
using UI.Models;
using UI.Servicers;
using UI.Views;
using CategoryModel = UI.Models.Category.CategoryModel;

namespace UI.ViewModels;

public class CategoryPageViewModel : CategoryPageModel
{
    private readonly IUIServicer _uiServicer;
    private readonly IWebData _webData;
    private readonly IAppData appData;
    private readonly ICategorys categorys;
    private readonly MainViewModel mainVM;

    public CategoryPageViewModel(ICategorys categorys, MainViewModel mainVM,
        IAppData appData, IWebData webData_, IUIServicer uIServicer_)
    {
        this.categorys = categorys;
        this.mainVM = mainVM;
        this.appData = appData;
        _webData = webData_;
        _uiServicer = uIServicer_;
        GotoListCommand = ReactiveCommand.Create<object>(OnGotoList);
        EditCommand = ReactiveCommand.Create<object>(OnEdit);
        EditDoneCommand = ReactiveCommand.CreateFromTask<object>(OnEditDone);
        EditCloseCommand = ReactiveCommand.Create<object>(OnEditClose);
        DelCommand = ReactiveCommand.CreateFromTask<object>(OnDel);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefresh);
        AddDirectoryCommand = ReactiveCommand.CreateFromTask<object>(OnAddDirectory);
        DirectoriesCommand = ReactiveCommand.Create<object>(OnDirectoriesCommand);
        ListBoxContextRequestedCommand = ReactiveCommand.Create<object>(ListBoxContextRequested);
        RestoreSystemCategoryCommand = ReactiveCommand.CreateFromTask<object>(RestoreSystemCategory);
        LoadData().ConfigureAwait(false).GetAwaiter();
    }

    public ICommand GotoListCommand { get; private set; }

    /// <summary>
    ///     打开编辑命令
    /// </summary>
    public ICommand EditCommand { get; private set; }

    /// <summary>
    ///     完成编辑命令
    /// </summary>
    public ICommand EditDoneCommand { get; private set; }

    /// <summary>
    ///     取消编辑
    /// </summary>
    public ICommand EditCloseCommand { get; private set; }

    /// <summary>
    ///     删除分类
    /// </summary>
    public ICommand DelCommand { get; private set; }

    /// <summary>
    ///     刷新
    /// </summary>
    public ICommand RefreshCommand { get; private set; }

    /// <summary>
    ///     添加目录
    /// </summary>
    public ICommand AddDirectoryCommand { get; private set; }

    public ICommand ListBoxContextRequestedCommand { get; private set; }

    public ICommand RestoreSystemCategoryCommand { get; private set; }

    /// <summary>
    ///     目录菜单命令
    /// </summary>
    public ICommand DirectoriesCommand { get; private set; }

    private Task OnRefresh(object obj)
    {
        return LoadData();
    }

    public Task RestoreSystemCategory(object arg)
    {
        var selected = arg as CategoryModel;
        if (selected != null && selected.Data != null && selected.Data.ID == 0)
        {
            var defaultSys = Core.Models.CategoryModel.DefaultSystemCategory();
            var editItemIndex = Data.IndexOf(selected);
            if (editItemIndex != -1)
                Data[editItemIndex] = new CategoryModel
                {
                    Count = Data[editItemIndex].Count,
                    Data = defaultSys
                };

            return categorys.UpdateAsync(defaultSys);
        }

        return Task.CompletedTask;
    }


    public void ListBoxContextRequested(object arg)
    {
        var selected = arg as CategoryModel;
        if (selected != null && selected.Data != null) IsSelectedSysCategory = selected.Data.ID == 0;
    }

    private Task OnDel(object obj)
    {
        if (ShowType.Id == 0) return DelAppCategory();

        if (ShowType.Id == 1) return DelWebSiteCategory();
        return Task.CompletedTask;
    }

    private void OnEditClose(object obj)
    {
        EditVisibility = false;
    }

    private Task OnEditDone(object obj)
    {
        if (ShowType.Id == 0) return EditAppCategoryAction();

        if (ShowType.Id == 1) return EditWebSiteCategoryAction();
        return Task.CompletedTask;
    }

    private async Task DelWebSiteCategory()
    {
        if (SelectedWebCategoryItem == null) return;
        var isConfirm = await _uiServicer.ShowConfirmDialogAsync(
            Application.Current.Resources["DeleteCategory"] as string,
            Application.Current.Resources["WantDeleteCategory"] as string);
        if (isConfirm)
        {
            await _webData.DeleteWebSiteCategoryAsync(SelectedWebCategoryItem.Data);

            //  从界面移除
            WebCategoryData.Remove(SelectedWebCategoryItem);
            if (WebCategoryData.Count == 0) WebCategoryData = new ObservableCollection<WebCategoryModel>();
            mainVM.Toast(Application.Current.Resources["CategoryDeleted"] as string, ToastType.Success);
        }
    }

    private async Task OnAddDirectory(object obj)
    {
        try
        {
            var desk = App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var result = await desk.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                { AllowMultiple = false });
            if (result?.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                if (EditDirectories.Contains(path))
                {
                    mainVM.Toast(Application.Current.Resources["DirectoryExists"] as string, ToastType.Error);
                    return;
                }

                EditDirectories.Add(path);
                mainVM.Toast(Application.Current.Resources["Added"] as string, ToastType.Success);
            }
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
            mainVM.Toast(Application.Current.Resources["AdditionFailed"] as string, ToastType.Error);
        }
    }

    private void OnDirectoriesCommand(object obj)
    {
        var action = obj.ToString();
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
            EditErrorText = Application.Current.Resources["CategoryCannotEmpty"] as string;
            return false;
        }

        if (string.IsNullOrEmpty(EditIconFile))
        {
            mainVM.Toast(Application.Current.Resources["SelectCategoryIcon"] as string, ToastType.Error,
                IconTypes.ImportantBadge12);
            return false;
        }

        if (string.IsNullOrEmpty(EditColor))
        {
            mainVM.Toast(Application.Current.Resources["CategoryColor"] as string, ToastType.Error,
                IconTypes.ImportantBadge12);
            return false;
        }

        if (EditIconFile.IndexOf("avares://") == -1 && new FileInfo(EditIconFile).Length > 1000000)
        {
            mainVM.Toast(Application.Current.Resources["IconFileCannotExceed"] as string, ToastType.Error,
                IconTypes.ImportantBadge12);
            return false;
        }

        return true;
    }

    private async Task EditAppCategoryAction()
    {
        try
        {
            if (!IsEditVerify()) return;

            var directoriesStr = JsonConvert.SerializeObject(EditDirectories.ToList());

            if (IsCreate)
            {
                if (Data.Where(m => m.Data.Name == EditName).Any())
                {
                    mainVM.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }

                mainVM.Toast(Application.Current.Resources["CreationCompleted"] as string, ToastType.Success);

                EditVisibility = false;

                var res = await categorys.CreateAsync(new Core.Models.CategoryModel
                {
                    Name = EditName,
                    IconFile = EditIconFile,
                    Color = EditColor,
                    IsDirectoryMath = EditIsDirectoryMath,
                    Directories = directoriesStr
                });

                var item = new CategoryModel
                {
                    Data = res,
                    Count = 0
                };
                if (Data.Count == 0)
                {
                    var list = new ObservableCollection<CategoryModel>();
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
                    mainVM.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }

                mainVM.Toast(Application.Current.Resources["Updated"] as string, ToastType.Success);
                var category = categorys.GetCategory(SelectedAppCategoryItem.Data.ID);
                if (category != null)
                {
                    category.Name = EditName;
                    category.IconFile = EditIconFile;
                    category.Color = EditColor;
                    category.IsDirectoryMath = EditIsDirectoryMath;
                    category.Directories = directoriesStr;

                    await categorys.UpdateAsync(category);
                }

                var item = Data.Where(m => m.Data.ID == SelectedAppCategoryItem.Data.ID).FirstOrDefault();

                var changedItem = new CategoryModel
                {
                    Count = item.Count,
                    Data = new Core.Models.CategoryModel
                    {
                        ID = item.Data.ID,
                        Name = EditName,
                        IconFile = EditIconFile,
                        Color = EditColor,
                        IsDirectoryMath = EditIsDirectoryMath,
                        Directories = directoriesStr
                    }
                };

                var editItemIndex = Data.IndexOf(item);
                if (editItemIndex != -1) Data[editItemIndex] = changedItem;

                EditVisibility = false;
            }
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
            mainVM.Toast(Application.Current.Resources["OperationFailedRetry"] as string, ToastType.Error);
        }
    }

    private async Task DelAppCategory()
    {
        if (SelectedAppCategoryItem == null) return;
        var isConfirm = await _uiServicer.ShowConfirmDialogAsync(
            Application.Current.Resources["DeleteCategory"] as string,
            Application.Current.Resources["WantDeleteCategory"] as string);
        if (isConfirm)
        {
            var category = categorys.GetCategory(SelectedAppCategoryItem.Data.ID);
            if (category != null)
            {
                await categorys.DeleteAsync(category);
                var apps = appData.GetAppsByCategoryID(category.ID);
                foreach (var app in apps)
                {
                    app.CategoryID = 0;
                    app.Category = null;
                    appData.UpdateApp(app);
                }

                //  从界面移除

                Data.Remove(SelectedAppCategoryItem);
                mainVM.Toast(Application.Current.Resources["CategoryDeleted"] as string, ToastType.Success);
            }
        }
    }

    private async Task EditWebSiteCategoryAction()
    {
        if (!IsEditVerify()) return;

        if (IsCreate)
        {
            //  创建分类

            //  判断重复分类名称
            if (WebCategoryData.Where(m => m.Data.Name == EditName).Any())
            {
                mainVM.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
                return;
            }

            if (WebCategoryData.Where(m => m.Data.Color == EditColor).Any())
            {
                mainVM.Toast(Application.Current.Resources["ColoreExists"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
                return;
            }

            var category = await _webData.CreateWebSiteCategoryAsync(new WebSiteCategoryModel
            {
                Color = EditColor,
                IconFile = EditIconFile,
                Name = EditName
            });

            if (category != null)
            {
                mainVM.Toast(Application.Current.Resources["CreationCompleted"] as string, ToastType.Success);
                EditVisibility = false;

                var webCategory = new WebCategoryModel
                {
                    Count = 0,
                    Data = category
                };
                if (WebCategoryData.Count == 0)
                    WebCategoryData = new ObservableCollection<WebCategoryModel>
                    {
                        webCategory
                    };
                else
                    WebCategoryData.Add(webCategory);
            }
            else
            {
                mainVM.Toast(Application.Current.Resources["CreationFailed"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
            }
        }
        else
        {
            //  编辑分类

            //  判断重复分类名称
            if (WebCategoryData.Where(m => m.Data.Name == EditName && m.Data.ID != SelectedWebCategoryItem.Data.ID)
                .Any())
            {
                mainVM.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
                return;
            }

            if (WebCategoryData.Where(m => m.Data.Color == EditColor && m.Data.ID != SelectedWebCategoryItem.Data.ID)
                .Any())
            {
                mainVM.Toast(Application.Current.Resources["ColoreExists"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
                return;
            }

            if (EditName == SelectedWebCategoryItem.Data.Name &&
                EditIconFile == SelectedWebCategoryItem.Data.IconFile &&
                EditColor == SelectedWebCategoryItem.Data.Color)
            {
                mainVM.Toast(Application.Current.Resources["NoChangesMade"] as string);
                EditVisibility = false;
                return;
            }

            mainVM.Toast(Application.Current.Resources["Updated"] as string, ToastType.Success);

            var category = SelectedWebCategoryItem.Data;
            category.Name = EditName;
            category.IconFile = EditIconFile;
            category.Color = EditColor;

            await _webData.UpdateWebSiteCategoryAsync(category);

            var item = WebCategoryData.Where(m => m.Data.ID == category.ID).FirstOrDefault();
            var index = WebCategoryData.IndexOf(item);
            WebCategoryData[index] = new WebCategoryModel
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
        IsSysCategory = false;
        if (EditDirectories.Count != 0) EditDirectories.Clear();
        if (obj != null)
        {
            var appCategory = obj as CategoryModel;
            var webCategory = obj as WebCategoryModel;
            IsSysCategory = appCategory != null && appCategory.Data.ID == 0;
            EditName = appCategory == null ? webCategory.Data.Name : appCategory.Data.Name;
            EditIconFile = appCategory == null ? webCategory.Data.IconFile : appCategory.Data.IconFile;
            EditColor = string.IsNullOrEmpty(appCategory == null ? webCategory.Data.Color : appCategory.Data.Color)
                ?
                "#00FFAB"
                : appCategory == null
                    ? webCategory.Data.Color
                    : appCategory.Data.Color;
            if (ShowType.Id == 0)
            {
                EditIsDirectoryMath = appCategory.Data.IsDirectoryMath;
                if (appCategory.Data.Directories != null) EditDirectories.AddRange(appCategory.Data.DirectoryList);
            }
        }
        else
        {
            EditName = "";
            EditIconFile = "avares://Taix/Resources/Emoji/(1).png";
            EditColor = "#00FFAB";
            EditIsDirectoryMath = false;
        }
    }

    private void OnGotoList(object obj)
    {
        GotoList();
    }

    private async Task LoadData()
    {
        Data ??= new ObservableCollection<CategoryModel>();
        Data.Clear();
        foreach (var item in categorys.GetCategories(true))
            Data.Add(new CategoryModel
            {
                Count = appData.GetAppsByCategoryID(item.ID).Count,
                Data = item
            });

        var webCategories = await _webData.GetWebSiteCategoriesAsync();
        var webCategoryData = new List<WebCategoryModel>();

        foreach (var item in webCategories)
            webCategoryData.Add(new WebCategoryModel
            {
                Data = item,
                Count = await _webData.GetWebSitesCountAsync(item.ID)
            });
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
        Data = null;
    }
}