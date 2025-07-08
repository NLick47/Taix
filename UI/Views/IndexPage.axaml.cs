using Avalonia.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class IndexPage : UserControl
{
    public IndexPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<IndexPageViewModel>();
    }
}