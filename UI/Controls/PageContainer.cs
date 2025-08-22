using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ReactiveUI;
using UI.Controls.Models;
using UI.Models;

namespace UI.Controls;

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
    private readonly Dictionary<string, PageModel> PageCache;

    private readonly string ProjectName;

    private ContentControl ContentControl;
    public int Index, OldIndex;
    private bool IsBack;
    private ScrollViewer ScrollViewer;

    private double VerticalOffset;

    public PageContainer()
    {
        ProjectName = "UI";
        Historys = new List<string>();
        PageCache = new Dictionary<string, PageModel>();
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
        ContentControl = e.NameScope.Get<ContentControl>("Frame");
        Loaded += PageContainer_Loaded;
    }

    private void PageContainer_Loaded(object? sender, RoutedEventArgs e)
    {
        Instance = this;
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

            //  从缓存中移除上一页

            var pageUri = Historys[preIndex];
            if (PageCache.ContainsKey(pageUri))
            {
                var page = PageCache[pageUri];
                var vm = page.Instance.DataContext as ModelBase;
                vm?.Dispose();
                page.Instance.Content = null;
                page.Instance.DataContext = null;
                PageCache.Remove(pageUri);
            }

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
        var oldUri = e.OldValue?.ToString();
        if (oldUri != null && control.PageCache.ContainsKey(oldUri))
        {
            var page = control.PageCache[oldUri];
            page.ScrollValue = control.ScrollViewer.Offset.Y;
        }

        control.LoadPage();
    }


    private PageModel GetPage()
    {
        UserControl page = null;
        if (PageCache.ContainsKey(Uri) && !IndexUriList.Contains(Uri)) return PageCache[Uri];
        var pageType = Type.GetType(ProjectName + ".Views." + Uri);
        if (pageType != null) page = ServiceLocator.GetRequiredService(pageType) as UserControl;
        var newPage = new PageModel
        {
            Instance = page,
            ScrollValue = 0
        };

        return newPage;
    }


    private void LoadPage()
    {
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


            if (page != null)
            {
                Content = page.Instance;

                if (!PageCache.ContainsKey(Uri)) PageCache.Add(Uri, page);

                //  滚动条位置处理
                if (IsBack)
                    ScrollViewer.Offset = new Vector(0, PageCache[Uri].ScrollValue);
                else
                    ScrollViewer?.ScrollToHome();

                OnLoadPaged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Debug.WriteLine("找不到Page：" + Uri + "，请确认已被注入");
            }
        }

        IsBack = false;
    }

    private void ClearCache()
    {
        if (PageCache != null)
        {
            foreach (var key in PageCache.Keys)
            {
                var page = PageCache[key];
                var vm = page.Instance.DataContext as ModelBase;
                vm?.Dispose();
                page.Instance.Content = null;
                page.Instance.DataContext = null;
                page.Instance = null;
            }

            PageCache.Clear();
        }
    }
}