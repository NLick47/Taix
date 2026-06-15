using Taix.Client.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class CategoryAppListPage : TPage
{
    public CategoryAppListPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<CategoryAppListPageViewModel>();
    }
}