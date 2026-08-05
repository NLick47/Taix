using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Taix.Client.Controls;
using Taix.Client.Foundation;
using Taix.Client.Foundation.Rx;
using Taix.Client.Models;
using Taix.Client.Models.Category;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class CategoryPage : TPage
{
    private readonly IDisposable _editDirectoriesSubscription;
    private readonly IDisposable _editIsDirectoryMatchSubscription;
    private readonly IDisposable _editIsUrlMatchSubscription;
    private readonly IDisposable _editUrlPatternsSubscription;
    private ObservableCollection<string>? _observedEditDirectories;
    private ObservableCollection<string>? _observedEditUrlPatterns;
    private CategoryPageViewModel _model;

    public CategoryPage()
    {
        InitializeComponent();
        var model = ServiceLocator.GetRequiredService<CategoryPageViewModel>();
        _model = model;
        DataContext = model;
        _editIsDirectoryMatchSubscription = ObservablePropertyChangedExtensions.WhenPropertyChanged(model, x => x.EditIsDirectoryMatch)
            .Subscribe(HandleEditIsDirectoryMatchChange);

        _editDirectoriesSubscription = ObservablePropertyChangedExtensions.WhenPropertyChanged(model, x => x.EditDirectories).Subscribe(val =>
        {
            if (_observedEditDirectories != null)
                _observedEditDirectories.CollectionChanged -= OnEditDirectoriesCollectionChanged;
            _observedEditDirectories = val;
            val.CollectionChanged += OnEditDirectoriesCollectionChanged;
        });

        _editIsUrlMatchSubscription = ObservablePropertyChangedExtensions.WhenPropertyChanged(model, x => x.EditIsUrlMatch)
            .Subscribe(HandleEditIsUrlMatchChange);

        _editUrlPatternsSubscription = ObservablePropertyChangedExtensions.WhenPropertyChanged(model, x => x.EditUrlPatterns).Subscribe(val =>
        {
            if (_observedEditUrlPatterns != null)
                _observedEditUrlPatterns.CollectionChanged -= OnEditUrlPatternsCollectionChanged;
            _observedEditUrlPatterns = val;
            val.CollectionChanged += OnEditUrlPatternsCollectionChanged;
        });
    }

    private void OnEditDirectoriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add) viewer.ScrollToEnd();
    }

    private void OnEditUrlPatternsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
        _editIsUrlMatchSubscription.Dispose();
        _editUrlPatternsSubscription.Dispose();
        if (_observedEditDirectories != null)
            _observedEditDirectories.CollectionChanged -= OnEditDirectoriesCollectionChanged;
        if (_observedEditUrlPatterns != null)
            _observedEditUrlPatterns.CollectionChanged -= OnEditUrlPatternsCollectionChanged;
        _observedEditDirectories = null;
        _observedEditUrlPatterns = null;
        _model = null;
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

    private void HandleEditIsUrlMatchChange(bool isUrlMatch)
    {
        if (isUrlMatch)
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
        }
    }
}
