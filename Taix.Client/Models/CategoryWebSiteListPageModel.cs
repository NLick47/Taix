using System.Collections.Generic;
using System.Collections.ObjectModel;
using Taix.Client.Controls.Select;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Models;

public class CategoryWebSiteListPageModel : ModelBase
{
    private WebSiteCategoryModel? _category;
    private ObservableCollection<WebSiteModel> _categoryWebSiteList = new();
    private bool _chooseVisibility;
    private string _searchInput = string.Empty;
    private WebSiteModel? _selectedItem;
    private List<OptionModel> _webSiteOptionList = [];

    public bool ChooseVisibility
    {
        get => _chooseVisibility;
        set
        {
            _chooseVisibility = value;
            OnPropertyChanged();
        }
    }

    public WebSiteModel? SelectedItem
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

    public ObservableCollection<WebSiteModel> CategoryWebSiteList
    {
        get => _categoryWebSiteList;
        set
        {
            _categoryWebSiteList = value;
            OnPropertyChanged();
        }
    }

    public WebSiteCategoryModel? Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
        }
    }

    public List<OptionModel> WebSiteOptionList
    {
        get => _webSiteOptionList;
        set
        {
            _webSiteOptionList = value;
            OnPropertyChanged();
        }
    }

    public class OptionModel
    {
        public bool IsChecked { get; set; }
        public WebSiteModel? WebSite { get; set; }
        public SelectItemModel OptionValue { get; set; } = new();
    }
}
