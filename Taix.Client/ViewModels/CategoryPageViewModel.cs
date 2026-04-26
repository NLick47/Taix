using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Window;
using Taix.Client.Logging;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;
using CategoryModel = Taix.Client.Models.Category.CategoryModel;

namespace Taix.Client.ViewModels;

public class CategoryPageViewModel : CategoryPageModel
{
    private readonly IDialogService _dialogService;
    private readonly IWebData _webDataService;
    private readonly IAppData _appDataService;
    private readonly ICategorys _categoryService;
    private readonly IToastService _toastService;
    private readonly INavigationService _navigationService;

    public CategoryPageViewModel(
        ICategorys categorys,
        INavigationService navigationService,
        IAppData appData,
        IWebData webData,
        IDialogService dialogService,
        IToastService toastService)
    {
        _categoryService = categorys;
        _navigationService = navigationService;
        _appDataService = appData;
        _webDataService = webData;
        _dialogService = dialogService;
        _toastService = toastService;
        Data = new ObservableCollection<CategoryModel>();
        WebCategoryData = new ObservableCollection<WebCategoryModel>();
        EditDirectories = new ObservableCollection<string>();

        GotoListCommand = ReactiveCommand.Create<object>(OnGotoList).DisposeWith(Disposables);
        EditCommand = ReactiveCommand.Create<object>(OnEdit).DisposeWith(Disposables);
        EditDoneCommand = ReactiveCommand.CreateFromTask<object>(OnEditDoneAsync).DisposeWith(Disposables);
        EditCloseCommand = ReactiveCommand.Create<object>(OnEditClose).DisposeWith(Disposables);
        DelCommand = ReactiveCommand.CreateFromTask<object>(OnDelAsync).DisposeWith(Disposables);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync).DisposeWith(Disposables);
        AddDirectoryCommand = ReactiveCommand.CreateFromTask<object>(OnAddDirectoryAsync).DisposeWith(Disposables);
        DirectoriesCommand = ReactiveCommand.Create<object>(OnDirectoriesCommand).DisposeWith(Disposables);
        ListBoxContextRequestedCommand = ReactiveCommand.Create<object>(OnListBoxContextRequested).DisposeWith(Disposables);
        RestoreSystemCategoryCommand = ReactiveCommand.CreateFromTask<object>(OnRestoreSystemCategoryAsync).DisposeWith(Disposables);

        _ = ExecuteAsync(LoadDataCoreAsync);
    }

    public ReactiveCommand<object, Unit> GotoListCommand { get; }
    public ReactiveCommand<object, Unit> EditCommand { get; }
    public ReactiveCommand<object, Unit> EditDoneCommand { get; }
    public ReactiveCommand<object, Unit> EditCloseCommand { get; }
    public ReactiveCommand<object, Unit> DelCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }
    public ReactiveCommand<object, Unit> AddDirectoryCommand { get; }
    public ReactiveCommand<object, Unit> DirectoriesCommand { get; }
    public ReactiveCommand<object, Unit> ListBoxContextRequestedCommand { get; }
    public ReactiveCommand<object, Unit> RestoreSystemCategoryCommand { get; }

    private Task OnRefreshAsync(object _) => ExecuteAsync(LoadDataCoreAsync);

    private async Task OnRestoreSystemCategoryAsync(object arg)
    {
        try
        {
            if (arg is not CategoryModel selected || selected.Data == null || !selected.Data.IsSystem)
                return;

            var defaultSys = Shared.Models.CategoryModel.DefaultSystemCategory();
            var editItemIndex = Data.IndexOf(selected);
            if (editItemIndex == -1)
                return;

            var originalCount = selected.Count;
            Data[editItemIndex] = new CategoryModel
            {
                Count = originalCount,
                Data = defaultSys
            };

            await _categoryService.UpdateAsync(defaultSys);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }
    }

    private void OnListBoxContextRequested(object arg)
    {
        try
        {
            if (arg is CategoryModel appCategory && appCategory.Data != null)
            {
                IsSelectedSysCategory = appCategory.Data.IsSystem;
                return;
            }

            if (arg is WebCategoryModel)
            {
                IsSelectedSysCategory = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }
    }

    private async Task OnDelAsync(object obj)
    {
        if (ShowType.Id == 0)
            await DelAppCategoryAsync();
        else if (ShowType.Id == 1)
            await DelWebSiteCategoryAsync();
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

    private Task OnEditDoneAsync(object obj)
    {
        if (ShowType.Id == 0) return EditAppCategoryActionAsync();
        if (ShowType.Id == 1) return EditWebSiteCategoryActionAsync();
        return Task.CompletedTask;
    }

    private async Task DelWebSiteCategoryAsync()
    {
        if (SelectedWebCategoryItem == null) return;

        try
        {
            var isConfirm = await _dialogService.ShowConfirmDialogAsync(
                ResourceStrings.DeleteCategory,
                ResourceStrings.WantDeleteCategory);

            if (!isConfirm) return;

            await _webDataService.DeleteWebSiteCategoryAsync(SelectedWebCategoryItem.Data);
            WebCategoryData.Remove(SelectedWebCategoryItem);
            SelectedWebCategoryItem = null;

            _toastService.Success(ResourceStrings.CategoryDeleted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }
    }

    private async Task OnAddDirectoryAsync(object obj)
    {
        try
        {
            var path = await _dialogService.ShowFolderPickerAsync(ResourceStrings.SelectDirectory);
            if (string.IsNullOrEmpty(path)) return;

            if (EditDirectories.Contains(path))
            {
                _toastService.Toast(ResourceStrings.DirectoryExists, ToastType.Warning);
                return;
            }

            EditDirectories.Add(path);
            _toastService.Success(ResourceStrings.Added);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
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
            Logger.Error(ex.Message, ex);
            _toastService.Toast(ResourceStrings.OperationFailedRetry);
        }
    }

    private bool IsEditVerify()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EditName))
            {
                IsEditError = true;
                EditErrorText = ResourceStrings.CategoryCannotEmpty;
                return false;
            }

            if (string.IsNullOrWhiteSpace(EditIconFile))
            {
                _toastService.Toast(ResourceStrings.SelectCategoryIcon, ToastType.Error, IconTypes.ImportantBadge12);
                return false;
            }

            if (string.IsNullOrWhiteSpace(EditColor))
            {
                _toastService.Toast(ResourceStrings.CategoryColor, ToastType.Error, IconTypes.ImportantBadge12);
                return false;
            }

            if (!EditIconFile.StartsWith("avares://") && File.Exists(EditIconFile))
            {
                var fileInfo = new FileInfo(EditIconFile);
                if (fileInfo.Length > 1000000)
                {
                    _toastService.Toast(ResourceStrings.IconFileCannotExceed, ToastType.Error, IconTypes.ImportantBadge12);
                    return false;
                }
            }

            IsEditError = false;
            EditErrorText = "";
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            return false;
        }
    }

    private async Task EditAppCategoryActionAsync()
    {
        try
        {
            if (!IsEditVerify()) return;

            var directoriesStr = JsonSerializer.Serialize(EditDirectories.ToList(), ClientJsonContext.Default.ListString);

            if (IsCreate)
            {
                if (Data.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase)))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var res = await _categoryService.CreateAsync(new Shared.Models.CategoryModel
                {
                    Name = EditName,
                    IconFile = EditIconFile,
                    Color = EditColor,
                    IsDirectoryMath = EditIsDirectoryMath,
                    Directories = directoriesStr
                });

                if (res == null)
                {
                    _toastService.Toast(ResourceStrings.CreationFailed, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                Data.Add(new CategoryModel
                {
                    Data = res,
                    Count = 0
                });
                _toastService.Success(ResourceStrings.CreationCompleted);
            }
            else
            {
                if (SelectedAppCategoryItem?.Data == null)
                {
                    _toastService.Error(ResourceStrings.SelectedItemNotFound);
                    return;
                }

                if (Data.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase) &&
                                  m.Data.ID != SelectedAppCategoryItem.Data.ID))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var category = _categoryService.GetCategory(SelectedAppCategoryItem.Data.ID);
                if (category == null)
                {
                    _toastService.Error(ResourceStrings.CategoryNotFound);
                    return;
                }

                category.Name = EditName;
                category.IconFile = EditIconFile;
                category.Color = EditColor;
                category.IsDirectoryMath = EditIsDirectoryMath;
                category.Directories = directoriesStr;

                await _categoryService.UpdateAsync(category);

                var item = Data.FirstOrDefault(m => m.Data.ID == SelectedAppCategoryItem.Data.ID);
                if (item != null)
                {
                    var editItemIndex = Data.IndexOf(item);
                    if (editItemIndex != -1)
                    {
                        Data[editItemIndex] = new CategoryModel
                        {
                            Count = item.Count,
                            Data = new Shared.Models.CategoryModel
                            {
                                ID = item.Data.ID,
                                Name = EditName,
                                IconFile = EditIconFile,
                                Color = EditColor,
                                IsDirectoryMath = EditIsDirectoryMath,
                                Directories = directoriesStr
                            }
                        };
                    }
                }

                _toastService.Success(ResourceStrings.Updated);
            }

            EditVisibility = false;
            SelectedAppCategoryItem = null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            _toastService.Error(ResourceStrings.OperationFailedRetry);
        }
    }

    private async Task DelAppCategoryAsync()
    {
        if (SelectedAppCategoryItem == null) return;

        try
        {
            var isConfirm = await _dialogService.ShowConfirmDialogAsync(
                ResourceStrings.DeleteCategory,
                ResourceStrings.WantDeleteCategory);

            if (!isConfirm) return;

            var category = _categoryService.GetCategory(SelectedAppCategoryItem.Data.ID);
            if (category == null || category.IsSystem)
                return;

            await _categoryService.DeleteAsync(category);

            var apps = _appDataService.GetAppsByCategoryID(category.ID).ToList();
            foreach (var app in apps)
            {
                app.CategoryID = 0;
                app.Category = null;
                _appDataService.UpdateApp(app);
            }

            Data.Remove(SelectedAppCategoryItem);
            SelectedAppCategoryItem = null;

            _toastService.Success(ResourceStrings.CategoryDeleted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            _toastService.Error(ResourceStrings.OperationFailedRetry);
        }
    }

    private async Task EditWebSiteCategoryActionAsync()
    {
        try
        {
            if (!IsEditVerify()) return;

            if (IsCreate)
            {
                if (WebCategoryData.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase)))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                if (WebCategoryData.Any(m => m.Data.Color.Equals(EditColor, StringComparison.OrdinalIgnoreCase)))
                {
                    _toastService.Toast(ResourceStrings.ColoreExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var category = await _webDataService.CreateWebSiteCategoryAsync(new WebSiteCategoryModel
                {
                    Color = EditColor,
                    IconFile = EditIconFile,
                    Name = EditName
                });

                if (category == null)
                {
                    _toastService.Toast(ResourceStrings.CreationFailed, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                WebCategoryData.Add(new WebCategoryModel
                {
                    Count = 0,
                    Data = category
                });
                _toastService.Success(ResourceStrings.CreationCompleted);
            }
            else
            {
                if (SelectedWebCategoryItem?.Data == null)
                {
                    _toastService.Error(ResourceStrings.SelectedItemNotFound);
                    return;
                }

                if (WebCategoryData.Any(m => m.Data.Name.Equals(EditName, StringComparison.OrdinalIgnoreCase) &&
                                           m.Data.ID != SelectedWebCategoryItem.Data.ID))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                if (WebCategoryData.Any(m => m.Data.Color.Equals(EditColor, StringComparison.OrdinalIgnoreCase) &&
                                           m.Data.ID != SelectedWebCategoryItem.Data.ID))
                {
                    _toastService.Toast(ResourceStrings.ColoreExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                if (EditName == SelectedWebCategoryItem.Data.Name &&
                    EditIconFile == SelectedWebCategoryItem.Data.IconFile &&
                    EditColor == SelectedWebCategoryItem.Data.Color)
                {
                    _toastService.Info(ResourceStrings.NoChangesMade);
                    EditVisibility = false;
                    SelectedWebCategoryItem = null;
                    return;
                }

                var category = SelectedWebCategoryItem.Data;
                category.Name = EditName;
                category.IconFile = EditIconFile;
                category.Color = EditColor;

                await _webDataService.UpdateWebSiteCategoryAsync(category);

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

                _toastService.Success(ResourceStrings.Updated);
            }

            EditVisibility = false;
            SelectedWebCategoryItem = null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            _toastService.Error(ResourceStrings.OperationFailedRetry);
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
            if (ShowType.Id == 0 && obj is CategoryModel appCategory && appCategory.Data != null)
            {
                IsSysCategory = appCategory.Data.IsSystem;
                EditName = appCategory.Data.Name;
                EditIconFile = appCategory.Data.IconFile;
                EditColor = string.IsNullOrWhiteSpace(appCategory.Data.Color) ? "#00FFAB" : appCategory.Data.Color;
                EditIsDirectoryMath = appCategory.Data.IsDirectoryMath;

                if (!string.IsNullOrWhiteSpace(appCategory.Data.Directories))
                {
                    try
                    {
                        var directories = JsonSerializer.Deserialize<List<string>>(appCategory.Data.Directories, ClientJsonContext.Default.ListString);
                        if (directories != null)
                        {
                            foreach (var dir in directories)
                            {
                                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                                    EditDirectories.Add(dir);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load directories: {ex.Message}", ex);
                    }
                }
            }
            else if (ShowType.Id == 1 && obj is WebCategoryModel webCategory && webCategory.Data != null)
            {
                EditName = webCategory.Data.Name;
                EditIconFile = webCategory.Data.IconFile;
                EditColor = string.IsNullOrWhiteSpace(webCategory.Data.Color) ? "#00FFAB" : webCategory.Data.Color;
                EditIsDirectoryMath = false;
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
        try
        {
            if (ShowType.Id == 0 && SelectedAppCategoryItem != null)
            {
                _navigationService.NavigateTo(nameof(CategoryAppListPage), SelectedAppCategoryItem);
                SelectedAppCategoryItem = null;
            }
            else if (ShowType.Id == 1 && SelectedWebCategoryItem != null)
            {
                _navigationService.NavigateTo(nameof(CategoryWebSiteListPage), SelectedWebCategoryItem.Data);
                SelectedWebCategoryItem = null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }
    }

    private async Task LoadDataCoreAsync(CancellationToken cancellationToken)
    {
        Data.Clear();
        var categories = _categoryService.GetCategories(true).ToList();
        foreach (var item in categories)
        {
            var appCount = _appDataService.GetAppsByCategoryID(item.ID).Count;
            Data.Add(new CategoryModel
            {
                Count = appCount,
                Data = item
            });
        }

        WebCategoryData.Clear();
        var webCategories = await _webDataService.GetWebSiteCategoriesAsync(true);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in webCategories)
        {
            var siteCount = await _webDataService.GetWebSitesCountAsync(item.ID);
            cancellationToken.ThrowIfCancellationRequested();
            WebCategoryData.Add(new WebCategoryModel
            {
                Data = item,
                Count = siteCount
            });
        }
    }

    public override void Dispose()
    {
        Data?.Clear();
        WebCategoryData?.Clear();
        EditDirectories?.Clear();
        base.Dispose();
    }
}
