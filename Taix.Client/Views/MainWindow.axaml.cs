using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Taix.Client.Controls.Window;
using Taix.Client.Platform;
using Taix.Client.Shared.Models.Search;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class MainWindow : DefaultWindow
{
    private IShortcutService? _shortcutService;
    private TextBox? _searchInput;
    private ListBox? _searchResultList;

    public MainWindow()
    {
        InitializeComponent();
        RequestClose += (_, _) => Close();

        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = -1;
            WindowDecorations = WindowDecorations.Full;
        }

        Loaded += OnLoadedAttachShortcuts;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyMacTitleBar();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && OperatingSystem.IsMacOS())
        {
            ApplyMacTitleBar();
        }
    }

    /// <summary>
    /// macOS：把原生标题条加厚（unified titlebar）并把页面内容让到实际标题条高度之下，
    /// 使自绘头部（左侧标题、右侧插件入口）与红绿灯处在同一设计行；
    /// 全屏时移除 toolbar（否则全屏顶部会留灰条），退出全屏后恢复
    /// </summary>
    private void ApplyMacTitleBar()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var fullScreen = WindowState == WindowState.FullScreen;
        // NativeAOT 下 Avalonia 的 ExtendClientArea 布局与 JIT 不同：
        // contentLayoutRect 会多算一个系统标题条（toolbar 38 + titlebar 28 = 66，
        var isAot = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
        var titleBarHeight = MacWindowChrome.SetThickTitleBar(this, !fullScreen && !isAot);
        MacWindowChrome.SetCornerRadius(this, fullScreen ? 0 : MacWindowChrome.WindowCornerRadius);
        if (!fullScreen) MacWindowChrome.SetTrafficLightScale(this, MacWindowChrome.TrafficLightScale);
        if (fullScreen || titleBarHeight <= 0) return;

        // 自绘头部与原生标题条同高（红绿灯居中于该高度），页面内容让位到同一条线之下
        SetTitleBarHeight(titleBarHeight);

        var nav = this.FindControl<Grid>("NavigationHost");
        if (nav != null) nav.Margin = new Thickness(0, titleBarHeight, 0, 15);

        var page = this.FindControl<Border>("PageHost");
        if (page != null) page.Margin = new Thickness(0, titleBarHeight, 10, 10);

        var overlay = this.FindControl<Grid>("SearchOverlay");
        if (overlay != null) overlay.Margin = new Thickness(0, titleBarHeight, 0, 0);
    }

    private void OnLoadedAttachShortcuts(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAttachShortcuts;
        _shortcutService = ServiceLocator.GetService<IShortcutService>();
        _shortcutService?.Attach(this);

        // 搜索覆盖层控件就绪后接线
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchResultList = this.FindControl<ListBox>("SearchResultList");

        if (_searchResultList != null)
        {
            _searchResultList.ContainerPrepared += OnSearchContainerPrepared;
            _searchResultList.ContainerClearing += OnSearchContainerClearing;
        }

        // 覆盖层可见时挂 Tunnel KeyDown，拦截 Esc/方向/Enter；隐藏时路由不到这里
        AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);

        // IsSearchOpen 变化时聚焦输入框
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnMainViewModelPropertyChanged;
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsSearchOpen)) return;
        if (DataContext is not MainViewModel { IsSearchOpen: true }) return;
        // 展开后聚焦输入框，延迟到布局完成
        Dispatcher.UIThread.Post(() => _searchInput?.Focus(), DispatcherPriority.Loaded);
    }

    // 点击遮罩关闭
    private void OnSearchMaskPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.IsSearchOpen = false;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel { IsSearchOpen: true } vm) return;
        var searchVm = vm.SearchVm;

        switch (e.Key)
        {
            case Key.Escape:
                vm.IsSearchOpen = false;
                e.Handled = true;
                break;
            case Key.Down:
                searchVm.MoveSelection(+1);
                e.Handled = true;
                break;
            case Key.Up:
                searchVm.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
                if (searchVm.SelectedItem?.IsCategoryCard == true)
                {
                    searchVm.TryExpand();
                    e.Handled = true;
                }
                break;
            case Key.Left:
                if (searchVm.SelectedItem != null && !searchVm.SelectedItem.IsCategoryCard
                    && !searchVm.SelectedItem.IsHeader && searchVm.IsInsideExpandedCard(searchVm.SelectedItem))
                {
                    searchVm.TryCollapse();
                    e.Handled = true;
                }
                break;
            case Key.Enter:
                searchVm.TriggerSelected();
                e.Handled = true;
                break;
        }
    }

    // 鼠标点击结果项
    private void OnSearchItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not ListBoxItem item) return;
        if (item.DataContext is not SearchResultItem r) return;
        if (DataContext is not MainViewModel vm) return;
        vm.SearchVm.Activate(r);
        e.Handled = true;
    }

    private void OnSearchContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item) return;
        item.Classes.Remove("section-header");
        item.Classes.Remove("category-card-row");
        if (e.Container.DataContext is SearchResultItem r)
        {
            switch (r.Kind)
            {
                case SearchRowKind.Header:
                    item.Classes.Add("section-header");
                    break;
                case SearchRowKind.CategoryCard:
                    item.Classes.Add("category-card-row");
                    break;
            }
            item.PointerReleased -= OnSearchItemPointerReleased;
            item.PointerReleased += OnSearchItemPointerReleased;
        }
    }

    private void OnSearchContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is ListBoxItem item)
        {
            item.Classes.Remove("section-header");
            item.Classes.Remove("category-card-row");
            item.PointerReleased -= OnSearchItemPointerReleased;
        }
    }

    protected override Type StyleKeyOverride =>
        OperatingSystem.IsMacOS() ? typeof(MacOSWindow) : typeof(DefaultWindow);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (App.IsShuttingDown) return;
        if (e.Cancel) return;

        e.Cancel = true;
        _ = App.ExitAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        RemoveHandler(KeyDownEvent, OnSearchKeyDown);
        if (_searchResultList != null)
        {
            _searchResultList.ContainerPrepared -= OnSearchContainerPrepared;
            _searchResultList.ContainerClearing -= OnSearchContainerClearing;
        }
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged -= OnMainViewModelPropertyChanged;
        }

        _shortcutService?.Detach();
        _shortcutService = null;
        base.OnClosed(e);
    }
}
