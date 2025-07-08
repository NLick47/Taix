using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryWebSiteListPage : TPage
{
    public CategoryWebSiteListPage()
    {
        InitializeComponent();
        this.DataContext = ServiceLocator.GetRequiredService<CategoryWebSiteListPageViewModel>();
    }
}