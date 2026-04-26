using Taix.Client.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class CategoryWebSiteListPage : TPage
{
    public CategoryWebSiteListPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<CategoryWebSiteListPageViewModel>();
    }
}