using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Select;
using Taix.Client.Controls.Window;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.ViewModels;

public class CategoryWebSiteListPageViewModel : CategoryWebSiteListPageModel
{
    private readonly INavigationDataService _navigationData;
    private readonly INavigationService _navigationService;
    private readonly IToastService _toastService;
    private readonly IWebData _webDataService;

    private bool _isRegexSearch;
    private List<OptionModel> _webSiteOptionsTemp = [];

    public CategoryWebSiteListPageViewModel(
        INavigationDataService navigationData,
        INavigationService navigationService,
        IToastService toastService,
        IWebData webData)
    {
        _navigationData = navigationData;
        _navigationService = navigationService;
        _toastService = toastService;
        _webDataService = webData;
        Category = _navigationData.Data as WebSiteCategoryModel ?? new WebSiteCategoryModel();

        ShowChooseCommand = ReactiveCommand.CreateFromTask<object>(OnShowChooseAsync).DisposeWith(Disposables);
        ChoosedCommand = ReactiveCommand.Create<object>(OnChoosed).DisposeWith(Disposables);
        GotoDetailCommand = ReactiveCommand.Create<object>(OnGotoDetail).DisposeWith(Disposables);
        SearchCommand = ReactiveCommand.Create<object>(OnSearch).DisposeWith(Disposables);
        ChooseCloseCommand = ReactiveCommand.Create<object>(OnChooseClose).DisposeWith(Disposables);
        DelCommand = ReactiveCommand.CreateFromTask<object>(OnDelAsync).DisposeWith(Disposables);
    }

    public override Task OnNavigatedToAsync()
    {
        _ = ExecuteAsync(LoadDataCoreAsync);
        return Task.CompletedTask;
    }

    public bool IsRegexSearch
    {
        get => _isRegexSearch;
        set
        {
            if (_isRegexSearch != value)
            {
                _isRegexSearch = value;
                OnPropertyChanged();
                OnSearch(SearchInput);
            }
        }
    }

    public ReactiveCommand<object, Unit> ShowChooseCommand { get; }
    public ReactiveCommand<object, Unit> ChoosedCommand { get; }
    public ReactiveCommand<object, Unit> GotoDetailCommand { get; }
    public ReactiveCommand<object, Unit> SearchCommand { get; }
    public ReactiveCommand<object, Unit> ChooseCloseCommand { get; }
    public ReactiveCommand<object, Unit> DelCommand { get; }

    private async Task LoadDataCoreAsync(CancellationToken cancellationToken)
    {
        if (Category == null)
        {
            _toastService.Toast(ResourceStrings.InvalidParameter, ToastType.Error, IconTypes.Error);
            return;
        }

        var list = await _webDataService.GetWebSitesAsync(Category.ID);
        cancellationToken.ThrowIfCancellationRequested();
        CategoryWebSiteList = new ObservableCollection<WebSiteModel>(list);
    }

    private async Task LoadChooserDataAsync(CancellationToken cancellationToken = default)
    {
        var unsetList = await _webDataService.GetUnSetCategoryWebSitesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var combinedList = unsetList.Concat(CategoryWebSiteList).OrderBy(m => m.Title).ToList();

        var optionList = new List<OptionModel>();
        foreach (var site in combinedList)
        {
            optionList.Add(new OptionModel
            {
                IsChecked = site.CategoryID == Category.ID,
                OptionValue = new SelectItemModel
                {
                    Name = $"{site.Title} - {site.Domain}",
                    Img = site.IconFile
                },
                WebSite = site
            });
        }
        WebSiteOptionList = optionList;
        _webSiteOptionsTemp = new List<OptionModel>(WebSiteOptionList);
    }

    private async Task OnDelAsync(object obj)
    {
        if (SelectedItem != null)
        {
            await _webDataService.UpdateWebSitesCategoryAsync(new[] { SelectedItem.ID }, 0);
            CategoryWebSiteList.Remove(SelectedItem);
            if (CategoryWebSiteList.Count == 0) CategoryWebSiteList = new ObservableCollection<WebSiteModel>();

            await ExecuteAsync(LoadDataCoreAsync);
        }
    }

    private void OnChooseClose(object obj)
    {
        ChooseVisibility = false;
        SearchInput = "";
    }

    private void OnSearch(object obj)
    {
        var keyword = obj?.ToString();
        if (string.IsNullOrEmpty(keyword))
        {
            WebSiteOptionList = new List<OptionModel>(_webSiteOptionsTemp);
        }
        else
        {
            if (IsRegexSearch)
            {
                try
                {
                    var regex = new Regex(keyword, RegexOptions.IgnoreCase);
                    WebSiteOptionList = _webSiteOptionsTemp
                        .Where(m => regex.IsMatch(m.WebSite.Title) || regex.IsMatch(m.WebSite.Domain)).ToList();
                }
                catch (ArgumentException)
                {
                    WebSiteOptionList = new List<OptionModel>(_webSiteOptionsTemp);
                }
            }
            else
            {
                WebSiteOptionList = _webSiteOptionsTemp
                    .Where(m => m.WebSite.Title.Contains(keyword) || m.WebSite.Domain.Contains(keyword)).ToList();
            }
        }
    }

    private void OnGotoDetail(object obj)
    {
        if (SelectedItem != null)
        {
            _navigationService.NavigateTo(nameof(WebSiteDetailPage), SelectedItem);
        }
    }

    private void OnChoosed(object obj)
    {
        ChooseVisibility = false;
        SearchInput = "";
        _webSiteOptionsTemp = new List<OptionModel>(WebSiteOptionList);
        _ = UpdateCategoryAsync();
    }

    private async Task UpdateCategoryAsync()
    {
        var removeSiteList = WebSiteOptionList
            .Where(m => !m.IsChecked && CategoryWebSiteList.Any(s => s.ID == m.WebSite.ID))
            .Select(m => m.WebSite.ID)
            .ToList();
        var addSiteList = WebSiteOptionList
            .Where(m => m.IsChecked && !CategoryWebSiteList.Any(s => s.ID == m.WebSite.ID))
            .Select(m => m.WebSite)
            .ToList();

        foreach (var id in removeSiteList)
        {
            var item = CategoryWebSiteList.FirstOrDefault(m => m.ID == id);
            if (item != null) CategoryWebSiteList.Remove(item);
        }

        if (CategoryWebSiteList.Count == 0)
            CategoryWebSiteList = new ObservableCollection<WebSiteModel>(addSiteList);
        else
            foreach (var item in addSiteList)
                CategoryWebSiteList.Add(item);

        if (removeSiteList.Count > 0)
            await _webDataService.UpdateWebSitesCategoryAsync(removeSiteList.ToArray(), 0);

        if (addSiteList.Count > 0)
            await _webDataService.UpdateWebSitesCategoryAsync(addSiteList.Select(m => m.ID).ToArray(), Category.ID);
    }

    private async Task OnShowChooseAsync(object obj)
    {
        if (WebSiteOptionList == null || WebSiteOptionList.Count == 0)
        {
            await LoadChooserDataAsync();
        }
        ChooseVisibility = true;
    }
}
