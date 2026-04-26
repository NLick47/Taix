using Avalonia.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class ChartPage : UserControl
{
    public ChartPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<ChartPageViewModel>();
    }
}