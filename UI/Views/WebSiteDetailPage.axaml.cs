using Avalonia.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class WebSiteDetailPage : UserControl
{
    public WebSiteDetailPage()
    {
        InitializeComponent();
        this.DataContext = ServiceLocator.GetRequiredService<WebSiteDetailPageViewModel>();
    }
}