using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class WebSiteDetailPage : UserControl
{
    public WebSiteDetailPage(WebSiteDetailPageViewModel model)
    {
        InitializeComponent();
        this.DataContext = model;
    }
}