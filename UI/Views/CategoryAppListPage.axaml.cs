using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryAppListPage : TPage
{
    public CategoryAppListPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<CategoryAppListPageViewModel>();
    }
}