using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryPage : UserControl
{
    public CategoryPage(CategoryPageViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}