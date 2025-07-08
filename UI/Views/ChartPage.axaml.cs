using Avalonia.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class ChartPage : UserControl
{
    public ChartPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<ChartPageViewModel>();
    }
}