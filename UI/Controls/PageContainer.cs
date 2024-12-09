using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using UI.Controls.Models;
using UI.Models;

namespace UI.Controls
{
    public class PageContainer : TemplatedControl
    {
        public List<string> IndexUriList
        {
            get { return GetValue(IndexUriListProperty); }
            set { SetValue(IndexUriListProperty, value); }
        }

        public static readonly StyledProperty<List<string>> IndexUriListProperty =
           AvaloniaProperty.Register<PageContainer, List<string>>(nameof(IndexUriList));


        public ICommand BackCommand
        {
            get { return GetValue(BackCommandProperty); }
            set { SetValue(BackCommandProperty, value); }
        }

        public static readonly StyledProperty<ICommand> BackCommandProperty =
         AvaloniaProperty.Register<PageContainer, ICommand>(nameof(BackCommand));
        public string Title
        {
            get { return GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly StyledProperty<string> TitleProperty =
          AvaloniaProperty.Register<PageContainer, string>(nameof(Title));

        public object Content
        {
            get { return GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly StyledProperty<object> ContentProperty =
         AvaloniaProperty.Register<PageContainer, object>(nameof(Content));

        public string Uri
        {
            get { return GetValue(UriProperty); }
            set { SetValue(UriProperty, value); }
        }

        public static readonly StyledProperty<string> UriProperty =
          AvaloniaProperty.Register<PageContainer, string>(nameof(Uri),string.Empty);


        public PageContainer Instance { get { return GetValue(InstanceProperty); } set { SetValue(InstanceProperty, value); } }

        public static readonly StyledProperty<PageContainer> InstanceProperty =
            AvaloniaProperty.Register<PageContainer, PageContainer>(nameof(Instance));

        public event EventHandler OnLoadPaged;

        private double VerticalOffset;

        private readonly string ProjectName;
        private List<string> Historys;
        public int Index = 0, OldIndex = 0;
        private Dictionary<string, PageModel> PageCache;
        private bool IsBack = false;
        private ScrollViewer ScrollViewer;

        private ContentControl ContentControl;

        public PageContainer()
        {
            ProjectName = "UI";
            Historys = new ();
            PageCache = new ();
            BackCommand = ReactiveCommand.Create<object>(OnBackCommand);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ScrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
            ContentControl = e.NameScope.Get<ContentControl>("Frame");
          
            ContentControl.Loaded += ContentControlLoaded;
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
                string uri = Historys[Index];

                int preIndex = Index + 1;

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


        private void ContentControlLoaded(object? sender, RoutedEventArgs e)
        {
           
        }

        protected override Type StyleKeyOverride => typeof(PageContainer);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == UriProperty)
            {
                OnUriChanged(change);
            }

        }

        private void OnUriChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var control = e.Sender as PageContainer;
            if (e.NewValue != e.OldValue)
            {
                var oldUri = e.OldValue?.ToString();
                if (control.PageCache.ContainsKey(oldUri))
                {
                    PageModel page = control.PageCache[oldUri];
                    page.ScrollValue = VerticalOffset;
                }
                control.LoadPage();
            }
        }


        private PageModel GetPage()
        {
            UserControl page = null;
            if (PageCache.ContainsKey(Uri))
            {
                return PageCache[Uri];
            }
            Type pageType = Type.GetType(ProjectName + ".Views." + Uri);
            if (pageType != null)
            {
                page = App.ServiceProvider.GetRequiredService(pageType) as UserControl;
            }
            var newPage = new PageModel()
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
                    if (!IsBack)
                    {
                        ClearCache();
                    }
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
                PageModel page = GetPage();


                if (page != null)
                {
                    Content = page.Instance;

                    //  加入缓存
                    if (!PageCache.ContainsKey(Uri))
                    {
                        PageCache.Add(Uri, page);
                    }

                    ////  滚动条位置处理
                    //if (IsBack)
                    //{
                    //    ScrollViewer.ScrollToVerticalOffset(page.ScrollValue);
                    //}
                    //else
                    //{
                    //    ScrollViewer?.ScrollToVerticalOffset(0);
                    //}

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
                }
                PageCache.Clear();
            }
        }
    }
}
