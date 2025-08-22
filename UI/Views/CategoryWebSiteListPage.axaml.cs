using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryWebSiteListPage : TPage
{
    public CategoryWebSiteListPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<CategoryWebSiteListPageViewModel>();
    }
}