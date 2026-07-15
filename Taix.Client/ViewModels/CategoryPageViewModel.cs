using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Window;
using Taix.Client.Logging;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models.Web;
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
    private readonly IStateService _stateService;

    private List<string> _originalDirectories = new();
    private List<string> _originalUrlPatterns = new();

    public CategoryPageViewModel(
        ICategorys categorys,
        INavigationService navigationService,
        IAppData appData,
        IWebData webData,
        IDialogService dialogService,
        IToastService toastService,
        IStateService stateService)
    {
        _categoryService = categorys;
        _navigationService = navigationService;
        _appDataService = appData;
        _webDataService = webData;
        _dialogService = dialogService;
        _toastService = toastService;
        _stateService = stateService;
        Data = new ObservableCollection<CategoryModel>();
        WebCategoryData = new ObservableCollection<WebCategoryModel>();
        EditDirectories = new ObservableCollection<string>();
        EditUrlPatterns = new ObservableCollection<string>();

        GotoListCommand = ReactiveCommand.Create<object>(OnGotoList).DisposeWith(Disposables);
        EditCommand = ReactiveCommand.Create<object>(OnEdit).DisposeWith(Disposables);
        EditDoneCommand = ReactiveCommand.CreateFromTask<object>(OnEditDoneAsync).DisposeWith(Disposables);
        EditCloseCommand = ReactiveCommand.Create<object>(OnEditClose).DisposeWith(Disposables);
        DelCommand = ReactiveCommand.CreateFromTask<object>(OnDelAsync).DisposeWith(Disposables);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync).DisposeWith(Disposables);
        AddDirectoryCommand = ReactiveCommand.CreateFromTask<object>(OnAddDirectoryAsync).DisposeWith(Disposables);
        DirectoriesCommand = ReactiveCommand.Create<object>(OnDirectoriesCommand).DisposeWith(Disposables);
        AddUrlPatternCommand = ReactiveCommand.CreateFromTask<object>(OnAddUrlPatternAsync).DisposeWith(Disposables);
        UrlPatternsCommand = ReactiveCommand.Create<object>(OnUrlPatternsCommand).DisposeWith(Disposables);
        RestoreSystemCategoryCommand = ReactiveCommand.CreateFromTask<object>(OnRestoreSystemCategoryAsync).DisposeWith(Disposables);
        ApplyDirectoryMatchCommand = ReactiveCommand.CreateFromTask<object>(OnApplyDirectoryMatchAsync).DisposeWith(Disposables);
        ApplyUrlMatchCommand = ReactiveCommand.CreateFromTask<object>(OnApplyUrlMatchAsync).DisposeWith(Disposables);
        WhenPropertyChanged(this, x => x.ShowType, _ => ExecuteAsync(LoadDataCoreAsync)).DisposeWith(Disposables);
    }

    public override async Task OnNavigatedToAsync()
    {
        TryRestoreState(_navigationService, _stateService);
        await ExecuteAsync(LoadDataCoreAsync);
    }

    public override void OnNavigatedFrom()
    {
        SaveState(_stateService);
        base.OnNavigatedFrom();
    }

    public ReactiveCommand<object, Unit> GotoListCommand { get; }
    public ReactiveCommand<object, Unit> EditCommand { get; }
    public ReactiveCommand<object, Unit> EditDoneCommand { get; }
    public ReactiveCommand<object, Unit> EditCloseCommand { get; }
    public ReactiveCommand<object, Unit> DelCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }
    public ReactiveCommand<object, Unit> AddDirectoryCommand { get; }
    public ReactiveCommand<object, Unit> DirectoriesCommand { get; }
    public ReactiveCommand<object, Unit> AddUrlPatternCommand { get; }
    public ReactiveCommand<object, Unit> UrlPatternsCommand { get; }
    public ReactiveCommand<object, Unit> RestoreSystemCategoryCommand { get; }
    public ReactiveCommand<object, Unit> ApplyDirectoryMatchCommand { get; }
    public ReactiveCommand<object, Unit> ApplyUrlMatchCommand { get; }


    private bool _hasDirectoryMatchCategory;
    public bool HasDirectoryMatchCategory
    {
        get => _hasDirectoryMatchCategory;
        set
        {
            _hasDirectoryMatchCategory = value;
            OnPropertyChanged();
        }
    }

    private Task OnRefreshAsync(object _) => ExecuteAsync(LoadDataCoreAsync);

    public override Task RefreshAsync() => ExecuteAsync(LoadDataCoreAsync);

    private async Task OnApplyDirectoryMatchAsync(object _)
    {
        try
        {
            var count = await _categoryService.ApplyDirectoryMatchAsync();
            _toastService.Success(string.Format(ResourceStrings.ApplyDirectoryMatchSuccess, count));
            await LoadDataCoreAsync(default);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            _toastService.Error(ResourceStrings.OperationFailedRetry);
        }
    }

    private async Task OnApplyUrlMatchAsync(object _)
    {
        try
        {
            var count = await _webDataService.ApplyUrlMatchAsync();
            _toastService.Success(string.Format(ResourceStrings.ApplyUrlMatchSuccess, count));
            await LoadDataCoreAsync(default);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            _toastService.Error(ResourceStrings.OperationFailedRetry);
        }
    }

    private async Task OnRestoreSystemCategoryAsync(object arg)
    {
        try
        {
            if (arg is not CategoryModel selected || selected.Data == null || !selected.Data.IsSystem)
                return;

            var restored = await _categoryService.RestoreSystemCategoryAsync(selected.Data.ID);

            var editItemIndex = Data.IndexOf(selected);
            if (editItemIndex == -1)
                return;

            var originalCount = selected.Count;
            Data[editItemIndex] = new CategoryModel
            {
                Count = originalCount,
                Data = restored
            };
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
        EditIsDirectoryMatch = false;
        EditIsUrlMatch = false;
        EditDirectories.Clear();
        EditUrlPatterns.Clear();
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
            var category = SelectedWebCategoryItem.Data;
            if (category.IsSystem) return;

            var message = SelectedWebCategoryItem.Count > 0
                ? string.Format(ResourceStrings.CategoryHasSites, SelectedWebCategoryItem.Count)
                : ResourceStrings.WantDeleteCategory;

            var isConfirm = await _dialogService.ShowConfirmDialogAsync(
                ResourceStrings.DeleteCategory,
                message);

            if (!isConfirm) return;

            await _webDataService.DeleteWebSiteCategoryAsync(category);

            if (SelectedWebCategoryItem.Count > 0)
            {
                var sysCategory = WebCategoryData.FirstOrDefault(m => m.Data.IsSystem);
                if (sysCategory != null)
                {
                    var index = WebCategoryData.IndexOf(sysCategory);
                    if (index != -1)
                    {
                        WebCategoryData[index] = new WebCategoryModel
                        {
                            Data = sysCategory.Data,
                            Count = sysCategory.Count + SelectedWebCategoryItem.Count
                        };
                    }
                }
            }

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

    private async Task OnAddUrlPatternAsync(object obj)
    {
        try
        {
            var pattern = await _dialogService.ShowInputModalAsync(ResourceStrings.AddUrlPattern, ResourceStrings.UrlPatternPlaceholder);
            if (string.IsNullOrEmpty(pattern)) return;

            if (pattern.Length > 256)
            {
                _toastService.Toast(ResourceStrings.UrlPatternTooLong, ToastType.Warning);
                return;
            }

            if (EditUrlPatterns.Count >= 20)
            {
                _toastService.Toast(ResourceStrings.UrlPatternLimitReached, ToastType.Warning);
                return;
            }

            if (EditUrlPatterns.Contains(pattern))
            {
                _toastService.Toast(ResourceStrings.UrlPatternExists, ToastType.Warning);
                return;
            }

            EditUrlPatterns.Add(pattern);
            _toastService.Success(ResourceStrings.Added);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }
    }

    private void OnUrlPatternsCommand(object obj)
    {
        try
        {
            var action = obj?.ToString();
            if (string.IsNullOrEmpty(action) || EditSelectedUrlPattern == null)
                return;

            switch (action.ToLower())
            {
                case "remove":
                    if (EditUrlPatterns.Contains(EditSelectedUrlPattern))
                    {
                        EditUrlPatterns.Remove(EditSelectedUrlPattern);
                        EditSelectedUrlPattern = null;
                    }
                    break;
                default:
                    Logger.Warn($"Unknown URL pattern command: {action}");
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

    private void ComputeHasDirectoryMatchCategory()
    {
        HasDirectoryMatchCategory = Data.Any(m => m.Data.IsDirectoryMatch);
    }

    private async Task EditAppCategoryActionAsync()
    {
        try
        {
            if (!IsEditVerify()) return;

            var directoriesStr = JsonSerializer.Serialize(EditDirectories.ToList(), ClientJsonContext.Default.ListString);

            if (IsCreate)
            {
                if (Data.Any(m => EditName.Equals(m.Data.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var res = await _categoryService.CreateAsync(new Shared.Models.CategoryModel
                {
                    Name = EditName,
                    IconFile = EditIconFile,
                    Color = EditColor,
                    IsDirectoryMatch = EditIsDirectoryMatch,
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

                if (Data.Any(m => EditName.Equals(m.Data.Name, StringComparison.OrdinalIgnoreCase) &&
                                  m.Data.ID != SelectedAppCategoryItem.Data.ID))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var category = await _categoryService.GetCategoryAsync(SelectedAppCategoryItem.Data.ID);
                if (category == null)
                {
                    _toastService.Error(ResourceStrings.CategoryNotFound);
                    return;
                }

                category = category with
                {
                    Name = EditName,
                    IconFile = EditIconFile,
                    Color = EditColor,
                    IsDirectoryMatch = EditIsDirectoryMatch,
                    Directories = directoriesStr
                };

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
                                IsDirectoryMatch = EditIsDirectoryMatch,
                                IsSystem = item.Data.IsSystem,
                                Directories = directoriesStr
                            }
                        };
                    }
                }

                _toastService.Success(ResourceStrings.Updated);
            }

            if (EditIsDirectoryMatch && EditDirectories.Count > 0)
            {
                try
                {
                    var newDirectories = EditDirectories
                        .Where(d => !_originalDirectories.Contains(d))
                        .ToArray();
                    if (newDirectories.Length > 0)
                    {
                        var matchCount = await _categoryService.ApplyDirectoryMatchAsync(newDirectories);
                        if (matchCount > 0)
                        {
                            await LoadDataCoreAsync(default);
                            _toastService.Success(string.Format(ResourceStrings.ApplyDirectoryMatchSuccess, matchCount));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message, ex);
                }
            }

            ComputeHasDirectoryMatchCategory();
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
            var category = await _categoryService.GetCategoryAsync(SelectedAppCategoryItem.Data.ID);
            if (category == null || category.IsSystem)
                return;

            var message = SelectedAppCategoryItem.Count > 0
                ? string.Format(ResourceStrings.CategoryHasApps, SelectedAppCategoryItem.Count)
                : ResourceStrings.WantDeleteCategory;

            var isConfirm = await _dialogService.ShowConfirmDialogAsync(
                ResourceStrings.DeleteCategory,
                message);

            if (!isConfirm) return;

            await _categoryService.DeleteAsync(category);

            if (SelectedAppCategoryItem.Count > 0)
            {
                var sysCategory = Data.FirstOrDefault(m => m.Data.IsSystem);
                if (sysCategory != null)
                {
                    var index = Data.IndexOf(sysCategory);
                    if (index != -1)
                    {
                        Data[index] = new CategoryModel
                        {
                            Data = sysCategory.Data,
                            Count = sysCategory.Count + SelectedAppCategoryItem.Count
                        };
                    }
                }
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

            var urlPatternsStr = JsonSerializer.Serialize(EditUrlPatterns.ToList(), ClientJsonContext.Default.ListString);

            if (IsCreate)
            {
                if (WebCategoryData.Any(m => EditName.Equals(m.Data.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var category = await _webDataService.CreateWebSiteCategoryAsync(new WebSiteCategoryModel
                {
                    Color = EditColor,
                    IconFile = EditIconFile,
                    Name = EditName,
                    IsUrlMatch = EditIsUrlMatch,
                    UrlPatterns = urlPatternsStr
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

                if (WebCategoryData.Any(m => EditName.Equals(m.Data.Name, StringComparison.OrdinalIgnoreCase) &&
                                           m.Data.ID != SelectedWebCategoryItem.Data.ID))
                {
                    _toastService.Toast(ResourceStrings.CategoryNameExists, ToastType.Error, IconTypes.ImportantBadge12);
                    return;
                }

                var existingIconFile = SelectedWebCategoryItem.Data.IconFile ?? "";
                var existingColor = string.IsNullOrWhiteSpace(SelectedWebCategoryItem.Data.Color) ? "#00FFAB" : SelectedWebCategoryItem.Data.Color;
                var existingUrlPatterns = SelectedWebCategoryItem.Data.UrlPatterns ?? "[]";

                var category = SelectedWebCategoryItem.Data with
                {
                    Name = EditName,
                    IconFile = EditIconFile,
                    Color = EditColor,
                    IsUrlMatch = EditIsUrlMatch,
                    UrlPatterns = urlPatternsStr
                };

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

            if (EditIsUrlMatch && EditUrlPatterns.Count > 0)
            {
                try
                {
                    var newPatterns = EditUrlPatterns
                        .Where(p => !_originalUrlPatterns.Contains(p))
                        .ToArray();
                    if (newPatterns.Length > 0)
                    {
                        var matchCount = await _webDataService.ApplyUrlMatchAsync(newPatterns);
                        if (matchCount > 0)
                        {
                            await LoadDataCoreAsync(default);
                            _toastService.Success(string.Format(ResourceStrings.ApplyUrlMatchSuccess, matchCount));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message, ex);
                }
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
        EditUrlPatterns.Clear();
        EditSelectedUrlPattern = null;
        _originalDirectories.Clear();
        _originalUrlPatterns.Clear();

        if (obj != null)
        {
            if (ShowType.Id == 0 && obj is CategoryModel appCategory && appCategory.Data != null)
            {
                IsSysCategory = appCategory.Data.IsSystem;
                EditName = appCategory.Data.Name;
                EditIconFile = appCategory.Data.IconFile;
                EditColor = string.IsNullOrWhiteSpace(appCategory.Data.Color) ? "#00FFAB" : appCategory.Data.Color;
                EditIsDirectoryMatch = appCategory.Data.IsDirectoryMatch;

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
                                {
                                    EditDirectories.Add(dir);
                                    _originalDirectories.Add(dir);
                                }
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
                IsSysCategory = webCategory.Data.IsSystem;
                EditName = webCategory.Data.Name;
                EditIconFile = webCategory.Data.IconFile;
                EditColor = string.IsNullOrWhiteSpace(webCategory.Data.Color) ? "#00FFAB" : webCategory.Data.Color;
                EditIsUrlMatch = webCategory.Data.IsUrlMatch;

                if (!string.IsNullOrWhiteSpace(webCategory.Data.UrlPatterns))
                {
                    try
                    {
                        var patterns = JsonSerializer.Deserialize<List<string>>(webCategory.Data.UrlPatterns, ClientJsonContext.Default.ListString);
                        if (patterns != null)
                        {
                            foreach (var pattern in patterns)
                            {
                                if (!string.IsNullOrWhiteSpace(pattern))
                                {
                                    EditUrlPatterns.Add(pattern);
                                    _originalUrlPatterns.Add(pattern);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load URL patterns: {ex.Message}", ex);
                    }
                }
            }
        }
        else
        {
            EditName = "";
            EditIconFile = "avares://Taix/Resources/Emoji/(1).png";
            EditColor = "#00FFAB";
            EditIsDirectoryMatch = false;
            EditIsUrlMatch = false;
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
        if (ShowType.Id == 0)
        {
            Data.Clear();
            var categories = await _categoryService.GetCategoriesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in categories)
            {
                var appCount = await _appDataService.GetAppCountByCategoryIDAsync(item.ID);
                cancellationToken.ThrowIfCancellationRequested();
                Data.Add(new CategoryModel
                {
                    Count = appCount,
                    Data = item
                });
            }
            ComputeHasDirectoryMatchCategory();
        }
        else
        {
            HasDirectoryMatchCategory = false;
            WebCategoryData.Clear();
            var webCategories = await _webDataService.GetWebSiteCategoriesAsync(cancellationToken);
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
    }

    public override void Dispose()
    {
        Data?.Clear();
        WebCategoryData?.Clear();
        EditDirectories?.Clear();
        base.Dispose();
    }
}
