using Avalonia.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class IndexPage : UserControl
{
    public IndexPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<IndexPageViewModel>();
    }
}
