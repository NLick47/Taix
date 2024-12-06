using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Collections.Specialized;
using System.Reactive.Linq;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryPage : ReactiveUserControl<CategoryPageViewModel>
{
    public CategoryPage(CategoryPageViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        this.WhenAnyValue(x => x.ViewModel.EditIsDirectoryMath).Subscribe(HandleEditIsDirectoryMatchChange);

        this.WhenAnyValue(x => x.ViewModel.EditDirectories).Subscribe(val =>
        {
            val.CollectionChanged += OnEditDirectoriesCollectionChanged;
        });
        Unloaded += OnUnloaded;
    }

    private void OnEditDirectoriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            this.viewer.ScrollToEnd();
        }
    }

    protected void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var editDirectories = this.ViewModel.EditDirectories;
        if (editDirectories != null)
        {
            editDirectories.CollectionChanged -= OnEditDirectoriesCollectionChanged;
        }
    }

    private void HandleEditIsDirectoryMatchChange(bool isDirectoryMatch)
    {
        if (isDirectoryMatch)
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