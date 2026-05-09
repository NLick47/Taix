using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;
using Taix.Client.Controls;
using Taix.Client.Models;
using Taix.Client.Models.Category;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class CategoryPage : TPage
{
    private readonly IDisposable _editDirectoriesSubscription;
    private readonly IDisposable _editIsDirectoryMatchSubscription;
    private CategoryPageViewModel _model;

    public CategoryPage()
    {
        InitializeComponent();
        var model = ServiceLocator.GetRequiredService<CategoryPageViewModel>();
        _model = model;
        DataContext = model;
        _editIsDirectoryMatchSubscription = this.WhenAnyValue(x => x._model.EditIsDirectoryMatch)
            .Subscribe(HandleEditIsDirectoryMatchChange);

        _editDirectoriesSubscription = this.WhenAnyValue(x => x._model.EditDirectories).Subscribe(val =>
        {
            val.CollectionChanged += OnEditDirectoriesCollectionChanged;
        });
    }

    private void OnEditDirectoriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add) viewer.ScrollToEnd();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_model != null) _model.IsEditError = false;
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
            viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            viewer.ScrollToEnd();
        }
        else
        {
            viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private void OnAppCategoryListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        if (sender is not ListBox listBox) return;

        var point = e.GetPosition(listBox);
        var hit = listBox.InputHitTest(point) as Visual;

        Visual? visual = hit;
        while (visual != null && visual is not ListBoxItem)
        {
            visual = visual.GetVisualParent();
        }

        if (visual is ListBoxItem listBoxItem && listBoxItem.DataContext is CategoryModel category)
        {
            _model.SelectedAppCategoryItem = category;
            _model.IsSelectedSysCategory = category.Data?.IsSystem ?? false;
        }
    }

    private void OnWebCategoryListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        if (sender is not ListBox listBox) return;

        var point = e.GetPosition(listBox);
        var hit = listBox.InputHitTest(point) as Visual;

        Visual? visual = hit;
        while (visual != null && visual is not ListBoxItem)
        {
            visual = visual.GetVisualParent();
        }

        if (visual is ListBoxItem listBoxItem && listBoxItem.DataContext is CategoryPageModel.WebCategoryModel category)
        {
            _model.SelectedWebCategoryItem = category;
            _model.IsSelectedSysCategory = category.Data?.IsSystem ?? false;
        }
    }
}