using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Collections.Specialized;
using System.Reactive.Linq;
using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryPage : TPage
{
    private CategoryPageViewModel _model;
    public CategoryPage(CategoryPageViewModel model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        this.WhenAnyValue(x => x._model.EditIsDirectoryMath).Subscribe(HandleEditIsDirectoryMatchChange);

        this.WhenAnyValue(x => x._model.EditDirectories).Subscribe(val =>
        {
            val.CollectionChanged += OnEditDirectoriesCollectionChanged;
        });
    }

    private void OnEditDirectoriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            this.viewer.ScrollToEnd();
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