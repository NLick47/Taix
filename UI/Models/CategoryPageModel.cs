using System.Collections.ObjectModel;
using Core.Models.Db;
using UI.Models.Category;

namespace UI.Models;

public class CategoryPageModel : ModelBase
{
    private ObservableCollection<CategoryModel> Data_;

    private string EditColor_;
    private string EditErrorText_;

    private string EditIconFile_;
    private bool EditIsDirectoryMath_;

    private string EditName_;
    private string EditSelectedDirectory_;


    private bool EditVisibility_;

    private bool IsCreate_;
    private bool IsEditError_;


    private bool IsRightClickSelected_;

    private bool IsSysCategory_;

    private CategoryModel SelectedAppCategoryItem_;

    private WebCategoryModel SelectedWebCategoryItem_;

    private ObservableCollection<WebCategoryModel> WebCategoryData_;

    public ObservableCollection<CategoryModel> Data
    {
        get => Data_;
        set
        {
            Data_ = value;
            OnPropertyChanged();
        }
    }

    public CategoryModel SelectedAppCategoryItem
    {
        get => SelectedAppCategoryItem_;
        set
        {
            if (value != null && value.Data != null)
            {
                SelectedAppCategoryItem_ = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EditVisibility
    {
        get => EditVisibility_;
        set
        {
            EditVisibility_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     是否是创建分类
    /// </summary>
    public bool IsCreate
    {
        get => IsCreate_;
        set
        {
            IsCreate_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     编辑分类名称
    /// </summary>
    public string EditName
    {
        get => EditName_;
        set
        {
            EditName_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     编辑分类图标
    /// </summary>
    public string EditIconFile
    {
        get => EditIconFile_;
        set
        {
            EditIconFile_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     是否启用目录匹配
    /// </summary>
    public bool EditIsDirectoryMath
    {
        get => EditIsDirectoryMath_;
        set
        {
            EditIsDirectoryMath_ = value;
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
        get => EditSelectedDirectory_;
        set
        {
            EditSelectedDirectory_ = value;
            OnPropertyChanged();
        }
    }

    public string EditErrorText
    {
        get => EditErrorText_;
        set
        {
            EditErrorText_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditError
    {
        get => IsEditError_;
        set
        {
            IsEditError_ = value;
            OnPropertyChanged();
        }
    }

    public string EditColor
    {
        get => EditColor_;
        set
        {
            EditColor_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelectedSysCategory
    {
        get => IsRightClickSelected_;
        set
        {
            IsRightClickSelected_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsSysCategory
    {
        get => IsSysCategory_;
        set
        {
            IsSysCategory_ = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<WebCategoryModel> WebCategoryData
    {
        get => WebCategoryData_;
        set
        {
            WebCategoryData_ = value;
            OnPropertyChanged();
        }
    }

    public WebCategoryModel SelectedWebCategoryItem
    {
        get => SelectedWebCategoryItem_;
        set
        {
            SelectedWebCategoryItem_ = value;
            OnPropertyChanged();
        }
    }

    public class WebCategoryModel
    {
        public WebSiteCategoryModel Data { get; set; }
        public int Count { get; set; }
    }
}