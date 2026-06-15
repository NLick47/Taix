using System.Collections.Generic;
using Taix.Client.Models.CategoryAppList;
using Taix.Client.Shared.Models;
using CategoryModel = Taix.Client.Models.Category.CategoryModel;

namespace Taix.Client.Models;

public class CategoryAppListPageModel : ModelBase
{
    private List<ChooseAppModel> _appList = [];
    private CategoryModel? _category;
    private bool _chooseVisibility;
    private List<AppModel> _data = [];
    private string _searchInput = string.Empty;
    private AppModel? _selectedItem;
    private string _mainSearchInput = string.Empty;
    private List<AppModel> _mainPageOriginalData = [];

    public bool IsSystemCategory => _category?.Data.IsSystem ?? false;

    public CategoryModel? Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSystemCategory));
        }
    }

    public List<AppModel> Data
    {
        get => _data;
        set
        {
            _data = value;
            OnPropertyChanged();
        }
    }

    public List<ChooseAppModel> AppList
    {
        get => _appList;
        set
        {
            _appList = value;
            OnPropertyChanged();
        }
    }

    public bool ChooseVisibility
    {
        get => _chooseVisibility;
        set
        {
            _chooseVisibility = value;
            OnPropertyChanged();
        }
    }

    public AppModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    public string SearchInput
    {
        get => _searchInput;
        set
        {
            _searchInput = value;
            OnPropertyChanged();
        }
    }

    public string MainSearchInput
    {
        get => _mainSearchInput;
        set
        {
            _mainSearchInput = value;
            OnPropertyChanged();
        }
    }

    public List<AppModel> MainPageOriginalData
    {
        get => _mainPageOriginalData;
        set
        {
            _mainPageOriginalData = value;
            OnPropertyChanged();
        }
    }
}
