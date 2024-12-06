using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryPage : ReactiveUserControl<CategoryPageViewModel>
{
    public CategoryPage(CategoryPageViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        this.WhenAnyValue(x => x.ViewModel.EditIsDirectoryMath).Subscribe(HandleEditIsDirectoryMatchChange);
    }

    private void HandleEditIsDirectoryMatchChange(bool isDirectoryMatch)
    {
        if(isDirectoryMatch)
        {
            this.viewer.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
            this.viewer.ScrollToEnd();
        }
        else
        {
            this.viewer.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        }

    }
}