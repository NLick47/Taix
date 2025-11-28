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
    private readonly IAppData _appData;
    private readonly ICategorys _categorys;
    private readonly MainViewModel _mainVm;

    public CategoryPageViewModel(ICategorys categorys, MainViewModel mainVm,
        IAppData appData, IWebData webData, IUIServicer uIServicer)
    {
        _categorys = categorys;
        _mainVm = mainVm;
        _appData = appData;
        _webData = webData;
        _uiServicer = uIServicer;
        InitializeCommands();
        Data = new ObservableCollection<CategoryModel>();
        WebCategoryData = new ObservableCollection<WebCategoryModel>();
        EditDirectories = new ObservableCollection<string>();
        _ = InitializeAsync();
    }

    private void InitializeCommands()
    {
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
    }

    private async Task InitializeAsync()
    {
        await LoadData();
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

    public async Task RestoreSystemCategory(object arg)
    {
        try
        {
            var selected = arg as CategoryModel;
            if (selected == null || selected.Data == null || selected.Data.ID != 0) 
                return;

            var defaultSys = Core.Models.CategoryModel.DefaultSystemCategory();
            var editItemIndex = Data.IndexOf(selected);
            if (editItemIndex == -1) 
                return;
            
            var originalCount = selected.Count;
            
            var newItem = new CategoryModel
            {
                Count = originalCount,
                Data = defaultSys
            };
            
            Data[editItemIndex] = newItem;

            await _categorys.UpdateAsync(defaultSys);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
    }

    public void ListBoxContextRequested(object arg)
    {
        try
        {
            // 处理应用分类
            var appCategory = arg as CategoryModel;
            if (appCategory != null && appCategory.Data != null)
            {
                IsSelectedSysCategory = appCategory.Data.ID == 0;
                return;
            }
            
            var webCategory = arg as WebCategoryModel;
            if (webCategory != null)
            {
                IsSelectedSysCategory = false; 
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
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
        ClearEditFields();
    }

    private void ClearEditFields()
    {
        EditName = "";
        EditIconFile = "avares://Taix/Resources/Emoji/(1).png";
        EditColor = "#00FFAB";
        EditIsDirectoryMath = false;
        EditDirectories.Clear();
        IsEditError = false;
        EditErrorText = "";
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
        
        try
        {
            var isConfirm = await _uiServicer.ShowConfirmDialogAsync(
                Application.Current.Resources["DeleteCategory"] as string,
                Application.Current.Resources["WantDeleteCategory"] as string);
                
            if (!isConfirm) return;

            await _webData.DeleteWebSiteCategoryAsync(SelectedWebCategoryItem.Data);
            
            // 从界面移除
            WebCategoryData.Remove(SelectedWebCategoryItem);
            SelectedWebCategoryItem = null;
            
            _mainVm.Toast(Application.Current.Resources["CategoryDeleted"] as string, ToastType.Success);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
    }

    private async Task OnAddDirectory(object obj)
    {
        try
        {
            var desk = App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desk?.MainWindow == null)
            {
                _mainVm.Toast(Application.Current.Resources["MainWindowNotFound"] as string, ToastType.Error);
                return;
            }

            var result = await desk.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            { 
                AllowMultiple = false,
                Title = Application.Current.Resources["SelectDirectory"] as string
            });
            
            if (result?.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                if (EditDirectories.Contains(path))
                {
                    _mainVm.Toast(Application.Current.Resources["DirectoryExists"] as string, ToastType.Warning);
                    return;
                }
                
                EditDirectories.Add(path);
                _mainVm.Toast(Application.Current.Resources["Added"] as string, ToastType.Success);
            }
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
        }
    }

    private void OnDirectoriesCommand(object obj)
    {
        try
        {
            var action = obj?.ToString();
            if (string.IsNullOrEmpty(action) || EditSelectedDirectory == null) 
                return;

            switch (action.ToLower())
            {
                case "remove":
                    if (EditDirectories.Contains(EditSelectedDirectory))
                    {
                        EditDirectories.Remove(EditSelectedDirectory);
                        EditSelectedDirectory = null;
                    }
                    break;
                default:
                    Logger.Warn($"Unknown directory command: {action}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
            _mainVm.Toast(Application.Current.Resources["OperationFailedRetry"] as string, ToastType.Error);
        }
    }

    private bool IsEditVerify()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EditName))
            {
                IsEditError = true;
                EditErrorText = Application.Current.Resources["CategoryCannotEmpty"] as string;
                return false;
            }

            if (string.IsNullOrWhiteSpace(EditIconFile))
            {
                _mainVm.Toast(Application.Current.Resources["SelectCategoryIcon"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
                return false;
            }

            if (string.IsNullOrWhiteSpace(EditColor))
            {
                _mainVm.Toast(Application.Current.Resources["CategoryColor"] as string, ToastType.Error,
                    IconTypes.ImportantBadge12);
                return false;
            }

            // 检查文件大小，但只针对本地文件
            if (!EditIconFile.StartsWith("avares://") && File.Exists(EditIconFile))
            {
                var fileInfo = new FileInfo(EditIconFile);
                if (fileInfo.Length > 1000000) 
                {
                    _mainVm.Toast(Application.Current.Resources["IconFileCannotExceed"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return false;
                }
            }

            IsEditError = false;
            EditErrorText = "";
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
            return false;
        }
    }

    private async Task EditAppCategoryAction()
    {
        try
        {
            if (!IsEditVerify()) return;
            
            var directoriesStr = JsonConvert.SerializeObject(EditDirectories.ToList());
            
            if (IsCreate)
            {
                // 检查重复分类名称
                if (Data.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase)))
                {
                    _mainVm.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                var res = await _categorys.CreateAsync(new Core.Models.CategoryModel
                {
                    Name = EditName,
                    IconFile = EditIconFile,
                    Color = EditColor,
                    IsDirectoryMath = EditIsDirectoryMath,
                    Directories = directoriesStr
                });
                
                if (res == null)
                {
                    _mainVm.Toast(Application.Current.Resources["CreationFailed"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                var item = new CategoryModel
                {
                    Data = res,
                    Count = 0
                };
                
                Data.Add(item);
                _mainVm.Toast(Application.Current.Resources["CreationCompleted"] as string, ToastType.Success);
            }
            else
            {
                // 编辑分类
                if (SelectedAppCategoryItem == null || SelectedAppCategoryItem.Data == null)
                {
                    _mainVm.Toast(Application.Current.Resources["SelectedItemNotFound"] as string, ToastType.Error);
                    return;
                }
                
                // 检查重复分类名称（排除当前编辑的项目）
                if (Data.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase) && 
                                  m.Data.ID != SelectedAppCategoryItem.Data.ID))
                {
                    _mainVm.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                var category = _categorys.GetCategory(SelectedAppCategoryItem.Data.ID);
                if (category == null)
                {
                    _mainVm.Toast(Application.Current.Resources["CategoryNotFound"] as string, ToastType.Error);
                    return;
                }
                
                category.Name = EditName;
                category.IconFile = EditIconFile;
                category.Color = EditColor;
                category.IsDirectoryMath = EditIsDirectoryMath;
                category.Directories = directoriesStr;
                
                await _categorys.UpdateAsync(category);
                
                // 更新UI
                var item = Data.FirstOrDefault(m => m.Data.ID == SelectedAppCategoryItem.Data.ID);
                if (item != null)
                {
                    var editItemIndex = Data.IndexOf(item);
                    if (editItemIndex != -1)
                    {
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
                        Data[editItemIndex] = changedItem;
                    }
                }
                
                _mainVm.Toast(Application.Current.Resources["Updated"] as string, ToastType.Success);
            }
            
            EditVisibility = false;
            SelectedAppCategoryItem = null;
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
            _mainVm.Toast(Application.Current.Resources["OperationFailedRetry"] as string, ToastType.Error);
        }
    }

    private async Task DelAppCategory()
    {
        if (SelectedAppCategoryItem == null) return;
        
        try
        {
            var isConfirm = await _uiServicer.ShowConfirmDialogAsync(
                Application.Current.Resources["DeleteCategory"] as string,
                Application.Current.Resources["WantDeleteCategory"] as string);
                
            if (!isConfirm) return;

            var category = _categorys.GetCategory(SelectedAppCategoryItem.Data.ID);
            if (category == null)
            {
                _mainVm.Toast(Application.Current.Resources["CategoryNotFound"] as string, ToastType.Error);
                return;
            }
            
            if (category.ID == 0) // 不能删除系统分类
            {
                _mainVm.Toast(Application.Current.Resources["CannotDeleteSystemCategory"] as string, ToastType.Error);
                return;
            }
            
            await _categorys.DeleteAsync(category);
            
            // 更新相关应用
            var apps = _appData.GetAppsByCategoryID(category.ID).ToList();
            foreach (var app in apps)
            {
                app.CategoryID = 0;
                app.Category = null;
                _appData.UpdateApp(app);
            }
            
            // 从界面移除
            Data.Remove(SelectedAppCategoryItem);
            SelectedAppCategoryItem = null;
            
            _mainVm.Toast(Application.Current.Resources["CategoryDeleted"] as string, ToastType.Success);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
            _mainVm.Toast(Application.Current.Resources["OperationFailedRetry"] as string, ToastType.Error);
        }
    }

    private async Task EditWebSiteCategoryAction()
    {
        try
        {
            if (!IsEditVerify()) return;
            
            if (IsCreate)
            {
                // 检查重复分类名称和颜色
                if (WebCategoryData.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase)))
                {
                    _mainVm.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                if (WebCategoryData.Any(m => m.Data.Color.Equals(EditColor, StringComparison.OrdinalIgnoreCase)))
                {
                    _mainVm.Toast(Application.Current.Resources["ColoreExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                var category = await _webData.CreateWebSiteCategoryAsync(new WebSiteCategoryModel
                {
                    Color = EditColor,
                    IconFile = EditIconFile,
                    Name = EditName
                });
                
                if (category == null)
                {
                    _mainVm.Toast(Application.Current.Resources["CreationFailed"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                var webCategory = new WebCategoryModel
                {
                    Count = 0,
                    Data = category
                };
                
                WebCategoryData.Add(webCategory);
                _mainVm.Toast(Application.Current.Resources["CreationCompleted"] as string, ToastType.Success);
            }
            else
            {
                // 编辑分类
                if (SelectedWebCategoryItem == null || SelectedWebCategoryItem.Data == null)
                {
                    _mainVm.Toast(Application.Current.Resources["SelectedItemNotFound"] as string, ToastType.Error);
                    return;
                }
                
                // 检查重复分类名称和颜色（排除当前编辑的项目）
                if (WebCategoryData.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase) && 
                                           m.Data.ID != SelectedWebCategoryItem.Data.ID))
                {
                    _mainVm.Toast(Application.Current.Resources["CategoryNameExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                if (WebCategoryData.Any(m => m.Data.Color.Equals(EditColor, StringComparison.OrdinalIgnoreCase) && 
                                           m.Data.ID != SelectedWebCategoryItem.Data.ID))
                {
                    _mainVm.Toast(Application.Current.Resources["ColoreExists"] as string, ToastType.Error,
                        IconTypes.ImportantBadge12);
                    return;
                }
                
                // 检查是否有变化
                if (EditName == SelectedWebCategoryItem.Data.Name &&
                    EditIconFile == SelectedWebCategoryItem.Data.IconFile &&
                    EditColor == SelectedWebCategoryItem.Data.Color)
                {
                    _mainVm.Toast(Application.Current.Resources["NoChangesMade"] as string, ToastType.Info);
                    EditVisibility = false;
                    SelectedWebCategoryItem = null;
                    return;
                }
                
                var category = SelectedWebCategoryItem.Data;
                category.Name = EditName;
                category.IconFile = EditIconFile;
                category.Color = EditColor;
                
                await _webData.UpdateWebSiteCategoryAsync(category);
                
                // 更新UI
                var item = WebCategoryData.FirstOrDefault(m => m.Data.ID == category.ID);
                if (item != null)
                {
                    var index = WebCategoryData.IndexOf(item);
                    if (index != -1)
                    {
                        WebCategoryData[index] = new WebCategoryModel
                        {
                            Count = item.Count,
                            Data = category
                        };
                    }
                }
                
                _mainVm.Toast(Application.Current.Resources["Updated"] as string, ToastType.Success);
            }
            
            EditVisibility = false;
            SelectedWebCategoryItem = null;
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
            _mainVm.Toast(Application.Current.Resources["OperationFailedRetry"] as string, ToastType.Error);
        }
    }

    private void OnEdit(object obj)
    {
        EditVisibility = true;
        IsCreate = obj == null;
        IsSysCategory = false;
        EditDirectories.Clear();
        EditSelectedDirectory = null;
        
        if (obj != null)
        {
            if (ShowType.Id == 0) // 应用分类
            {
                var appCategory = obj as CategoryModel;
                if (appCategory?.Data != null)
                {
                    IsSysCategory = appCategory.Data.ID == 0;
                    EditName = appCategory.Data.Name;
                    EditIconFile = appCategory.Data.IconFile;
                    EditColor = string.IsNullOrWhiteSpace(appCategory.Data.Color) 
                        ? "#00FFAB" 
                        : appCategory.Data.Color;
                    EditIsDirectoryMath = appCategory.Data.IsDirectoryMath;
                    
                    if (!string.IsNullOrWhiteSpace(appCategory.Data.Directories))
                    {
                        try
                        {
                            var directories = JsonConvert.DeserializeObject<List<string>>(appCategory.Data.Directories);
                            if (directories != null)
                            {
                                foreach (var dir in directories)
                                {
                                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                                    {
                                        EditDirectories.Add(dir);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to load directories: {ex.Message}");
                        }
                    }
                }
            }
            else if (ShowType.Id == 1) // 网站分类
            {
                var webCategory = obj as WebCategoryModel;
                if (webCategory?.Data != null)
                {
                    EditName = webCategory.Data.Name;
                    EditIconFile = webCategory.Data.IconFile;
                    EditColor = string.IsNullOrWhiteSpace(webCategory.Data.Color) 
                        ? "#00FFAB" 
                        : webCategory.Data.Color;
                    EditIsDirectoryMath = false;
                }
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
        try
        {
            // 加载应用分类
            Data.Clear();
            var categories = _categorys.GetCategories(true).ToList();
            foreach (var item in categories)
            {
                var appCount = _appData.GetAppsByCategoryID(item.ID).Count;
                Data.Add(new CategoryModel
                {
                    Count = appCount,
                    Data = item
                });
            }
            
            // 加载网站分类
            WebCategoryData.Clear();
            var webCategories = await _webData.GetWebSiteCategoriesAsync();
            foreach (var item in webCategories)
            {
                var siteCount = await _webData.GetWebSitesCountAsync(item.ID);
                WebCategoryData.Add(new WebCategoryModel
                {
                    Data = item,
                    Count = siteCount
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
    }

    private void GotoList()
    {
        try
        {
            if (ShowType.Id == 0 && SelectedAppCategoryItem != null)
            {
                _mainVm.Data = SelectedAppCategoryItem;
                _mainVm.Uri = nameof(CategoryAppListPage);
                SelectedAppCategoryItem = null;
            }
            else if (ShowType.Id == 1 && SelectedWebCategoryItem != null)
            {
                _mainVm.Data = SelectedWebCategoryItem.Data;
                _mainVm.Uri = nameof(CategoryWebSiteListPage);
                SelectedWebCategoryItem = null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Data?.Clear();
        WebCategoryData?.Clear();
        EditDirectories?.Clear();
        
        (GotoListCommand as IDisposable)?.Dispose();
        (EditCommand as IDisposable)?.Dispose();
        (EditDoneCommand as IDisposable)?.Dispose();
        (EditCloseCommand as IDisposable)?.Dispose();
        (DelCommand as IDisposable)?.Dispose();
        (RefreshCommand as IDisposable)?.Dispose();
        (AddDirectoryCommand as IDisposable)?.Dispose();
        (DirectoriesCommand as IDisposable)?.Dispose();
        (ListBoxContextRequestedCommand as IDisposable)?.Dispose();
        (RestoreSystemCategoryCommand as IDisposable)?.Dispose();
        
        Data = null;
        WebCategoryData = null;
        EditDirectories = null;
        SelectedAppCategoryItem = null;
        SelectedWebCategoryItem = null;
        
        GC.SuppressFinalize(this);
    }
}
