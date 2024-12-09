using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryAppListPage : TPage
{
    public CategoryAppListPage(CategoryAppListPageViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}