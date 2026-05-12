using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ReactiveUI;
using Taix.Client.Controls.Models;
using Taix.Client.Models;

namespace Taix.Client.Controls;

public class PageContainer : TemplatedControl
{
    public static readonly StyledProperty<List<string>> IndexUriListProperty =
        AvaloniaProperty.Register<PageContainer, List<string>>(nameof(IndexUriList));

    public static readonly StyledProperty<ICommand> BackCommandProperty =
        AvaloniaProperty.Register<PageContainer, ICommand>(nameof(BackCommand));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PageContainer, string>(nameof(Title));

    public static readonly StyledProperty<object> ContentProperty =
        AvaloniaProperty.Register<PageContainer, object>(nameof(Content));

    public static readonly StyledProperty<string> UriProperty =
        AvaloniaProperty.Register<PageContainer, string>(nameof(Uri), string.Empty);

    public static readonly StyledProperty<PageContainer> InstanceProperty =
        AvaloniaProperty.Register<PageContainer, PageContainer>(nameof(Instance));

    private readonly List<string> Historys;
    private readonly Dictionary<string, double> ScrollPositions;

    private static readonly Dictionary<string, Type> PageTypeMap = new()
    {
        ["IndexPage"] = typeof(Views.IndexPage),
        ["DataPage"] = typeof(Views.DataPage),
        ["SettingPage"] = typeof(Views.SettingPage),
        ["DetailPage"] = typeof(Views.DetailPage),
        ["CategoryPage"] = typeof(Views.CategoryPage),
        ["CategoryAppListPage"] = typeof(Views.CategoryAppListPage),
        ["CategoryWebSiteListPage"] = typeof(Views.CategoryWebSiteListPage),
        ["ChartPage"] = typeof(Views.ChartPage),
        ["WebSiteDetailPage"] = typeof(Views.WebSiteDetailPage),
    };

    private ContentPresenter ContentPresenter;
    public int Index, OldIndex;
    private bool IsBack;
    private ScrollViewer ScrollViewer;
    private string? PendingUri;

    public PageContainer()
    {
        Historys = new List<string>();
        ScrollPositions = new Dictionary<string, double>();
        BackCommand = ReactiveCommand.Create<object>(OnBackCommand);
    }

    public List<string> IndexUriList
    {
        get => GetValue(IndexUriListProperty);
        set => SetValue(IndexUriListProperty, value);
    }


    public ICommand BackCommand
    {
        get => GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public string Uri
    {
        get => GetValue(UriProperty);
        set => SetValue(UriProperty, value);
    }


    public PageContainer Instance
    {
        get => GetValue(InstanceProperty);
        set => SetValue(InstanceProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(PageContainer);

    public event EventHandler OnLoadPaged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ScrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
        ContentPresenter = e.NameScope.Get<ContentPresenter>("Frame");
        Loaded += PageContainer_Loaded;
        Unloaded += PageContainer_Unloaded;
    }

    private void PageContainer_Loaded(object? sender, RoutedEventArgs e)
    {
        Instance = this;
    }

    private void PageContainer_Unloaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= PageContainer_Loaded;
        Unloaded -= PageContainer_Unloaded;
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (PendingUri != null)
        {
            var uri = PendingUri;
            PendingUri = null;
            Uri = uri;
        }
    }


    private void OnBackCommand(object obj)
    {
        Back();
    }

    public void Back()
    {
        if (Index - 1 >= 0)
        {
            OldIndex = Index;
            Index--;
            var uri = Historys[Index];

            var preIndex = Index + 1;

            //  dispose the page being navigated away from
            var pageUri = Historys[preIndex];
            if (Content is UserControl oldPage && oldPage.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
                oldPage.DataContext = null;
            }

            ScrollPositions.Remove(pageUri);
            Historys.RemoveRange(preIndex, 1);

            IsBack = true;

            Uri = uri;
        }
    }

    public void ClearHistorys()
    {
        Historys.Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UriProperty) OnUriChanged(change);
    }

    private void OnUriChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var control = e.Sender as PageContainer;
        if (string.IsNullOrEmpty(e.NewValue?.ToString())) return;

        // 如果模板尚未应用，挂起本次导航，等附加到逻辑树后再执行
        if (control.ContentPresenter == null || control.ScrollViewer == null)
        {
            control.PendingUri = e.NewValue?.ToString();
            return;
        }

        var oldUri = e.OldValue?.ToString();
        if (oldUri != null)
        {
            control.ScrollPositions[oldUri] = control.ScrollViewer?.Offset.Y ?? 0;
        }

        control.LoadPage();
    }


    private PageModel GetPage()
    {
        UserControl? page = null;
        if (PageTypeMap.TryGetValue(Uri, out var pageType))
            page = ServiceLocator.GetRequiredService(pageType) as UserControl;
        return new PageModel
        {
            Instance = page,
            ScrollValue = ScrollPositions.TryGetValue(Uri, out var scroll) ? scroll : 0
        };
    }


    private void LoadPage()
    {
        try
        {
            // 模板元素未就绪时不执行加载，挂起等待逻辑树就绪
            if (ContentPresenter == null || ScrollViewer == null)
            {
                PendingUri = Uri;
                return;
            }

            if (Content is UserControl oldPage && oldPage.DataContext is ModelBase oldVm)
            {
                oldVm.OnNavigatedFrom();
                if (oldVm is IDisposable disposable)
                    disposable.Dispose();
                oldPage.DataContext = null;
            }

            if (Uri != string.Empty)
            {
                if (IndexUriList != null && IndexUriList.Contains(Uri))
                {
                    Historys.Clear();
                    Index = 0;
                    OldIndex = 0;
                    Historys.Add(Uri);
                    if (!IsBack) ClearCache();
                }
                else
                {
                    //处理历史记录
                    if (OldIndex == Index)
                    {
                        //新开
                        Historys.Add(Uri);
                        Index++;
                    }

                    OldIndex = Index;
                }

                var page = GetPage();

                if (page?.Instance != null)
                {
                    // 安全序列：先分离旧内容逻辑树，再附加新内容，避免 Avalonia 12 逻辑树竞态
                    ContentPresenter.Content = null;
                    ContentPresenter.Content = page.Instance;

                    // 同步 Content 属性供外部 Binding 观察（不再驱动模板）
                    SetValue(ContentProperty, page.Instance);

                    //  滚动条位置处理
                    if (IsBack)
                        ScrollViewer.Offset = new Vector(0, page.ScrollValue);
                    else
                        ScrollViewer?.ScrollToHome();

                    OnLoadPaged?.Invoke(this, EventArgs.Empty);

                    if (page.Instance.DataContext is ModelBase newVm)
                        _ = newVm.OnNavigatedToAsync();
                }
                else
                {
                    Debug.WriteLine("找不到Page：" + Uri + "，请确认已被注入");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"页面加载失败 [{Uri}]: {ex}");
            throw;
        }
        finally
        {
            IsBack = false;
        }
    }

    private void ClearCache()
    {
        ScrollPositions.Clear();
    }
}