using System;
using System.Collections.Specialized;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class CategoryPage : TPage
{
    private CategoryPageViewModel _model;
    private IDisposable _editIsDirectoryMatchSubscription;
    private IDisposable _editDirectoriesSubscription;
    public CategoryPage()
    {
        InitializeComponent();
        var model = ServiceLocator.GetRequiredService<CategoryPageViewModel>();
        _model = model;
        DataContext = model;
        _editIsDirectoryMatchSubscription = this.WhenAnyValue(x => x._model.EditIsDirectoryMath).Subscribe(HandleEditIsDirectoryMatchChange);

        _editDirectoriesSubscription = this.WhenAnyValue(x => x._model.EditDirectories).Subscribe(val =>
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

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if(_model != null)
        {
            _model.IsEditError = false;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _editIsDirectoryMatchSubscription.Dispose();
        _editDirectoriesSubscription.Dispose();
        if (_model?.EditDirectories != null)
        {
            _model.EditDirectories.CollectionChanged -= OnEditDirectoriesCollectionChanged;
            _model = null;
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