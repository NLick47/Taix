using System.Collections.ObjectModel;
using Core.Models.Db;
using UI.Models.Category;

namespace UI.Models;

#nullable disable
public class CategoryPageModel : ModelBase
{
    private ObservableCollection<CategoryModel> _data;

    private string _editColor;
    private string _editErrorText;

    private string _editIconFile;
    private bool _editIsDirectoryMath;

    private string _editName;
    private string _editSelectedDirectory;


    private bool _editVisibility;

    private bool _isCreate;
    private bool _isEditError;


    private bool _isRightClickSelected;

    private bool _isSysCategory;

    private CategoryModel _selectedAppCategoryItem;

    private WebCategoryModel _selectedWebCategoryItem;

    private ObservableCollection<WebCategoryModel> _webCategoryData;

    public ObservableCollection<CategoryModel> Data
    {
        get => _data;
        set
        {
            _data = value;
            OnPropertyChanged();
        }
    }

    public CategoryModel SelectedAppCategoryItem
    {
        get => _selectedAppCategoryItem;
        set
        {
            if (value != null && value.Data != null)
            {
                _selectedAppCategoryItem = value;
                OnPropertyChanged();
            }
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

    /// <summary>
    ///     是否是创建分类
    /// </summary>
    public bool IsCreate
    {
        get => _isCreate;
        set
        {
            _isCreate = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     编辑分类名称
    /// </summary>
    public string EditName
    {
        get => _editName;
        set
        {
            _editName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     编辑分类图标
    /// </summary>
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

    /// <summary>
    ///     是否启用目录匹配
    /// </summary>
    public bool EditIsDirectoryMath
    {
        get => _editIsDirectoryMath;
        set
        {
            _editIsDirectoryMath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     匹配目录
    /// </summary>
    public ObservableCollection<string> EditDirectories { get; set; } = new();

    /// <summary>
    ///     当前列表选择目录
    /// </summary>
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

    public bool IsSelectedSysCategory
    {
        get => _isRightClickSelected;
        set
        {
            _isRightClickSelected = value;
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

    public WebCategoryModel SelectedWebCategoryItem
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
        public WebSiteCategoryModel Data { get; set; }
        public int Count { get; set; }
    }
}