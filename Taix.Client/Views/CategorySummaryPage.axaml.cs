using Avalonia.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class CategorySummaryPage : UserControl
{
    public CategorySummaryPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetService<CategorySummaryPageViewModel>();
    }
}
