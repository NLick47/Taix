using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Models.Db;
using UI.Controls.Select;

namespace UI.Models;

public class CategoryWebSiteListPageModel : ModelBase
{
    private WebSiteCategoryModel Category_;

    private ObservableCollection<WebSiteModel> CategoryWebSiteList_;
    private bool ChooseVisibility_;

    private string SearchInput_;

    private WebSiteModel SelectedItem_;

    private List<OptionModel> WebSiteOptionList_;

    public bool ChooseVisibility
    {
        get => ChooseVisibility_;
        set
        {
            ChooseVisibility_ = value;
            OnPropertyChanged();
        }
    }

    public WebSiteModel SelectedItem
    {
        get => SelectedItem_;
        set
        {
            SelectedItem_ = value;
            OnPropertyChanged();
        }
    }

    public string SearchInput
    {
        get => SearchInput_;
        set
        {
            SearchInput_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     分类站点列表
    /// </summary>
    public ObservableCollection<WebSiteModel> CategoryWebSiteList
    {
        get => CategoryWebSiteList_;
        set
        {
            CategoryWebSiteList_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     当前分类
    /// </summary>
    public WebSiteCategoryModel Category
    {
        get => Category_;
        set
        {
            Category_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     站点可选列表
    /// </summary>
    public List<OptionModel> WebSiteOptionList
    {
        get => WebSiteOptionList_;
        set
        {
            WebSiteOptionList_ = value;
            OnPropertyChanged();
        }
    }

    public class OptionModel
    {
        public bool IsChecked { get; set; }
        public WebSiteModel WebSite { get; set; }
        public SelectItemModel OptionValue { get; set; } = new();
    }
}