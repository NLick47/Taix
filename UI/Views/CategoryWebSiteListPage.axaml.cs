using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryWebSiteListPage : TPage
{
    public CategoryWebSiteListPage(CategoryWebSiteListPageViewModel model)
    {
        InitializeComponent();
        this.DataContext = model;
    }
}