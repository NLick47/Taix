using Avalonia.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class WebSiteDetailPage : UserControl
{
    public WebSiteDetailPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<WebSiteDetailPageViewModel>();
    }
}