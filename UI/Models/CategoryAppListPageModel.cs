using System.Collections.Generic;
using Core.Models;
using UI.Models.CategoryAppList;
using CategoryModel = UI.Models.Category.CategoryModel;

namespace UI.Models;

public class CategoryAppListPageModel : ModelBase
{
    private List<ChooseAppModel> AppList_;
    private CategoryModel Category_;

    //private List<AppModel> ChooseAppList_;
    //public List<AppModel> ChooseAppList { get { return ChooseAppList_; } set { ChooseAppList_ = value; OnPropertyChanged(); } }

    private bool ChooseVisibility_;
    private List<AppModel> Data_;

    private string SearchInput_;

    private AppModel SelectedItem_;

    public bool IsSystemCategory => Category_.Data.ID == 0;

    public CategoryModel Category
    {
        get => Category_;
        set
        {
            Category_ = value;
            OnPropertyChanged();
        }
    }

    public List<AppModel> Data
    {
        get => Data_;
        set
        {
            Data_ = value;
            OnPropertyChanged();
        }
    }

    public List<ChooseAppModel> AppList
    {
        get => AppList_;
        set
        {
            AppList_ = value;
            OnPropertyChanged();
        }
    }

    public bool ChooseVisibility
    {
        get => ChooseVisibility_;
        set
        {
            ChooseVisibility_ = value;
            OnPropertyChanged();
        }
    }

    public AppModel SelectedItem
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
}