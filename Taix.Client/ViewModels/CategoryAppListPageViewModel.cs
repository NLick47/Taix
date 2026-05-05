using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using ReactiveUI;
using Taix.Client.Models;
using Taix.Client.Models.CategoryAppList;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;
using CategoryModel = Taix.Client.Models.Category.CategoryModel;

namespace Taix.Client.ViewModels;

public class CategoryAppListPageViewModel : CategoryAppListPageModel
{
    private readonly IAppData _appDataService;
    private readonly INavigationDataService _navigationData;
    private readonly INavigationService _navigationService;
    private List<ChooseAppModel> _appList = [];

    public CategoryAppListPageViewModel(
        IAppData appData,
        INavigationDataService navigationData,
        INavigationService navigationService)
    {
        _appDataService = appData;
        _navigationData = navigationData;
        _navigationService = navigationService;

        ShowChooseCommand = ReactiveCommand.CreateFromTask<object>(OnShowChooseAsync).DisposeWith(Disposables);
        ChoosedCommand = ReactiveCommand.CreateFromTask<object>(OnChoosed).DisposeWith(Disposables);
        GotoDetailCommand = ReactiveCommand.Create<object>(OnGotoDetail).DisposeWith(Disposables);
        SearchCommand = ReactiveCommand.CreateFromTask<object>(OnSearchAsync).DisposeWith(Disposables);
        ChooseCloseCommand = ReactiveCommand.Create<object>(OnChooseClose).DisposeWith(Disposables);
        DelCommand = ReactiveCommand.CreateFromTask<object>(OnDel).DisposeWith(Disposables);
    }

    public ReactiveCommand<object, Unit> ShowChooseCommand { get; }
    public ReactiveCommand<object, Unit> ChoosedCommand { get; }
    public ReactiveCommand<object, Unit> GotoDetailCommand { get; }
    public ReactiveCommand<object, Unit> SearchCommand { get; }
    public ReactiveCommand<object, Unit> ChooseCloseCommand { get; }
    public ReactiveCommand<object, Unit> DelCommand { get; }

    public override void Dispose()
    {
        Data = [];
        AppList = [];
        base.Dispose();
    }

    private async Task OnDel(object obj)
    {
        if (SelectedItem != null)
        {
            var list = Data.ToList();
            list.Remove(SelectedItem);

            var app = await _appDataService.GetAppAsync(SelectedItem.ID);
            if (app != null)
            {
                app = app with { CategoryID = 0, Category = null };
                await _appDataService.UpdateAppAsync(app);
            }

            Data = list;
        }
    }

    private void OnChooseClose(object obj)
    {
        ChooseVisibility = false;
        SearchInput = "";
    }

    private Task OnSearchAsync(object? obj)
    {
        if (string.IsNullOrEmpty(SearchInput)) return Task.CompletedTask;
        var keyword = obj?.ToString() ?? SearchInput;

        if (keyword == "vscode")
            keyword = "Visual Studio Code";
        else if (keyword == "ps")
            keyword = "Photoshop";

        return SearchAsync(keyword);
    }

    private void OnGotoDetail(object obj)
    {
        if (SelectedItem != null)
        {
            _navigationService.NavigateTo(nameof(DetailPage), SelectedItem);
        }
    }

    private async Task OnChoosed(object obj)
    {
        ChooseVisibility = false;
        SearchInput = "";
        var data = new List<AppModel>();

        foreach (var item in AppList)
        {
            var app = await _appDataService.GetAppAsync(item.App.ID);
            if (app == null) continue;

            if (item.IsChoosed)
            {
                if (app.CategoryID != Category.Data.ID)
                {
                    app = app with { CategoryID = Category.Data.ID, Category = Category.Data };
                    await _appDataService.UpdateAppAsync(app);
                }
                data.Add(app);
            }
            else
            {
                var isHas = Data.Any(m => m.ID == item.App.ID);
                if (isHas)
                {
                    app = app with { CategoryID = 0, Category = null };
                    await _appDataService.UpdateAppAsync(app);
                }
            }
        }

        Data = data;
    }

    private async Task OnShowChooseAsync(object obj)
    {
        ChooseVisibility = true;
        await LoadAppsAsync();
    }

    public override async Task OnNavigatedToAsync()
    {
        Category = _navigationData.Data as CategoryModel ?? new CategoryModel();
        Data = Category.Data != null
            ? (await _appDataService.GetAppsByCategoryIDAsync(Category.Data.ID)).ToList()
            : [];
    }

    private async Task LoadAppsAsync()
    {
        _appList = [];

        if (Category == null) return;
        var allApps = await _appDataService.GetAllAppsAsync();
        foreach (var item in allApps)
        {
            var app = new ChooseAppModel
            {
                App = item,
                IsChoosed = item.CategoryID == Category.Data.ID,
                Value =
                {
                    Name = string.IsNullOrEmpty(item.Description) ? item.Name : item.Description,
                    Img = item.IconFile
                }
            };

            if (app.IsChoosed || item.Category?.IsSystem == true) _appList.Add(app);
        }

        _appList = _appList.OrderBy(m => m.App.Description).ToList();
        AppList = _appList;
    }

    private Task SearchAsync(string keyword)
    {
        return Task.Run(() =>
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();
                var list = AppList.ToList();
                foreach (var item in list)
                {
                    item.Visibility = (item.App.Description != null &&
                                       item.App.Description.ToLower().Contains(keyword)) ||
                                      (item.App.Name != null && item.App.Name.ToLower().Contains(keyword));
                }
            }
            else
            {
                foreach (var item in AppList) item.Visibility = true;
            }
        });
    }
}
