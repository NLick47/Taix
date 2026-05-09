using System.Collections.ObjectModel;
using Taix.Client.Models.Category;
using Taix.Client.Shared.Models.Db;

namespace Taix.Client.Models;

public class CategoryPageModel : ModelBase
{
    private ObservableCollection<CategoryModel> _data = new();
    private string _editColor = string.Empty;
    private string _editErrorText = string.Empty;
    private string _editIconFile = string.Empty;
    private bool _editIsDirectoryMatch;
    private string _editName = string.Empty;
    private string _editSelectedDirectory = string.Empty;
    private bool _editVisibility;
    private bool _isCreate;
    private bool _isEditError;
    private bool _isSysCategory;
    private CategoryModel? _selectedAppCategoryItem;
    private WebCategoryModel? _selectedWebCategoryItem;
    private ObservableCollection<WebCategoryModel> _webCategoryData = new();

    public ObservableCollection<CategoryModel> Data
    {
        get => _data;
        set
        {
            _data = value;
            OnPropertyChanged();
        }
    }

    public CategoryModel? SelectedAppCategoryItem
    {
        get => _selectedAppCategoryItem;
        set
        {
            _selectedAppCategoryItem = value;
            OnPropertyChanged();
        }
    }

    public bool EditVisibility
    {
        get => _editVisibility;
        set
        {
            _editVisibility = value;
            OnPropertyChanged();
        }
    }

    public bool IsCreate
    {
        get => _isCreate;
        set
        {
            _isCreate = value;
            OnPropertyChanged();
        }
    }

    public string EditName
    {
        get => _editName;
        set
        {
            _editName = value;
            OnPropertyChanged();
        }
    }

    public string EditIconFile
    {
        get => _editIconFile;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                _editIconFile = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EditIsDirectoryMatch
    {
        get => _editIsDirectoryMatch;
        set
        {
            _editIsDirectoryMatch = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> EditDirectories { get; set; } = new();

    public string EditSelectedDirectory
    {
        get => _editSelectedDirectory;
        set
        {
            _editSelectedDirectory = value;
            OnPropertyChanged();
        }
    }

    public string EditErrorText
    {
        get => _editErrorText;
        set
        {
            _editErrorText = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditError
    {
        get => _isEditError;
        set
        {
            _isEditError = value;
            OnPropertyChanged();
        }
    }

    public string EditColor
    {
        get => _editColor;
        set
        {
            _editColor = value;
            OnPropertyChanged();
        }
    }

    public bool IsSysCategory
    {
        get => _isSysCategory;
        set
        {
            _isSysCategory = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<WebCategoryModel> WebCategoryData
    {
        get => _webCategoryData;
        set
        {
            _webCategoryData = value;
            OnPropertyChanged();
        }
    }

    public WebCategoryModel? SelectedWebCategoryItem
    {
        get => _selectedWebCategoryItem;
        set
        {
            _selectedWebCategoryItem = value;
            OnPropertyChanged();
        }
    }

    public class WebCategoryModel
    {
        public WebSiteCategoryModel? Data { get; set; }
        public int Count { get; set; }
    }
}
