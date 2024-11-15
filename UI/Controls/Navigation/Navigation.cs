using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Core.Servicers.Instances;
using DynamicData.Binding;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using UI.Controls.Navigation.Models;

namespace UI.Controls.Navigation
{
    public class Navigation : TemplatedControl
    {
        public ContextMenu ItemContextMenu
        {
            get { return (ContextMenu)GetValue(ItemContextMenuProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly StyledProperty<ContextMenu> ItemContextMenuProperty =
            AvaloniaProperty.Register<Navigation, ContextMenu>(nameof(ItemContextMenu));

        public static readonly StyledProperty<NavigationItemModel> SelectedItemProperty =
          AvaloniaProperty.Register<Navigation,NavigationItemModel>(nameof(SelectedItem));

        public NavigationItemModel SelectedItem
        {
            get { return (NavigationItemModel)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SelectedItemProperty)
            {
                SelectedItemProperty.Changed.Subscribe(OnSelectedItemChanged);
            }
            if (change.Property == DataProperty)
            {
                OnDataChanged(change);
            }

        }

        public object TopExtContent
        {
            get { return (object)GetValue(TopExtContentProperty); }
            set { SetValue(TopExtContentProperty, value); }
        }
        public object BottomExtContent
        {
            get { return (object)GetValue(BottomExtContentProperty); }
            set { SetValue(BottomExtContentProperty, value); }
        }

        public ObservableCollection<NavigationItemModel> Data
        {
            get
            {
                return (ObservableCollection<NavigationItemModel>)GetValue(DataProperty);
            }
            set
            {
                SetValue(DataProperty, value);
            }
        }

        public static readonly StyledProperty<ObservableCollection<NavigationItemModel>> DataProperty =
           AvaloniaProperty.Register<Navigation,ObservableCollection<NavigationItemModel>>(nameof(Data));


        public static readonly StyledProperty<object> BottomExtContentProperty =
        AvaloniaProperty.Register<Navigation, object>(nameof(BottomExtContent));

        public static readonly StyledProperty<object> TopExtContentProperty =
          AvaloniaProperty.Register<Navigation,object>(nameof(TopExtContent));


        private Dictionary<int, NavigationItem> ItemsDictionary;

        //  选中标记块
        private Border ActiveBlock;
        //  滚动动画
        Animation scrollAnimation;
        //  伸缩动画
        Animation stretchAnimation;

        private StackPanel ItemsPanel;

        private void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs<NavigationItemModel> change)
        {
            var control = this;
            var oldItem = change.OldValue.Value;
            var newItem = change.NewValue.Value;
            if (newItem != oldItem)
            {
                if (newItem != null && control.ItemsDictionary.ContainsKey(newItem.ID))
                {
                    control.ItemsDictionary[newItem.ID].IsSelected = true;
                }
                if (oldItem != null && control.ItemsDictionary.ContainsKey(oldItem.ID))
                {
                    control.ItemsDictionary[oldItem.ID].IsSelected = false;
                }
                control.ScrollToActive();
            }
        }

        private  void OnDataChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var control = e.Sender as Navigation;
            if (e.OldValue != e.NewValue)
            {
                if (control.Data != null)
                {
                    control.Data.CollectionChanged -= control.Data_CollectionChanged;
                    control.Data.CollectionChanged += control.Data_CollectionChanged;

                    foreach (var item in control.Data)
                    {
                        control.AddItem(item);
                    }
                }
            }
        }

        private void Data_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    AddItem(item as NavigationItemModel);
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    RemoveItem(item as NavigationItemModel);
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (var ritem in e.NewItems)
                {
                    var item = ritem as NavigationItemModel;
                    var id = item.ID;
                    ItemsDictionary[id].Icon = item.UnSelectedIcon;
                    ItemsDictionary[id].Title = item.Title;
                    ItemsDictionary[id].SelectedIcon = item.SelectedIcon;

                }
            }
        }

        public Navigation()
        {
            ItemsDictionary = new Dictionary<int, NavigationItem>();
            Loaded += Navigation_Loaded;
        }

        private void Navigation_Loaded(object sender, RoutedEventArgs e)
        {
            ScrollToActive();
        }

        private void RemoveItem(NavigationItemModel item)
        {
            var navItem = ItemsDictionary[item.ID];
            ItemsPanel.Children.Remove(navItem);
            ItemsDictionary.Remove(item.ID);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ItemsPanel = e.NameScope.Find<StackPanel>("ItemsPanel")!;
            ActiveBlock = e.NameScope.Find<Border>("ActiveBlock")!;
            Render();
        }

        protected override Type StyleKeyOverride => typeof(Navigation);

        private void AddItem(NavigationItemModel item)
        {
            if (ItemsPanel != null)
            {
                var navItem = new NavigationItem();
                int id = item.ID < -1 ? CreateID() : item.ID;
                if (ItemsDictionary.ContainsKey(id))
                {
                    return;
                }
                item.ID = id;
                navItem.ID = id;
                navItem.Title = item.Title;
                navItem.Icon = item.UnSelectedIcon;
                navItem.SelectedIcon = item.SelectedIcon;
                navItem.IconColor = item.IconColor;
                navItem.BadgeText = item.BadgeText;
                navItem.Uri = item.Uri;

                //if (!string.IsNullOrEmpty(item.Title))
                //{
                //    navItem.MouseUp += NavItem_MouseUp;
                //    navItem.Unloaded += (e, c) =>
                //    {
                //        navItem.MouseUp -= NavItem_MouseUp;
                //    };
                //}
                ItemsPanel.Children.Add(navItem);
                ItemsDictionary.Add(id, navItem);
            }
        }



        private int CreateID()
        {
            if (ItemsDictionary.Count == 0)
            {
                return 1;
            }
            return ItemsDictionary.Max(m => m.Key) + 1;
        }

        private void ScrollToActive(double animationDuration = 0.35)
        {
            //  获取选中项
            if (SelectedItem == null || ItemsDictionary.Count == 0 || !ItemsDictionary.ContainsKey(SelectedItem.ID))
            {
                return;
            }
            var item = ItemsDictionary[SelectedItem.ID];
            item.IsSelected = true;

            scrollAnimation.Duration = TimeSpan.FromSeconds(animationDuration);
            stretchAnimation.Duration = TimeSpan.FromSeconds(animationDuration);


            ////  选中项的坐标
            //Point relativePoint = item.TransformToAncestor(this).Transform(new Point(0, 0));

            ////  设定动画方向
            //var activeBlockTTF = (ActiveBlock.RenderTransform as TransformGroup).Children[0] as TranslateTransform;
            //scrollAnimation.to = relativePoint.Y + 8;

            ////  伸缩动画

            //stretchAnimation.To = 1.6;

            //if (relativePoint.Y > activeBlockTTF.Y)
            //{
            //    //  向下移动
            //    var transformGroup = new TransformGroup();
            //    transformGroup.Children.Add(new TranslateTransform(0, activeBlockTTF.Y));
            //    transformGroup.Children.Add(new ScaleTransform(1, 1, 0, 200));

            //    ActiveBlock.RenderTransform = transformGroup;
            //}
            //else
            //{
            //    var transformGroup = new TransformGroup();
            //    transformGroup.Children.Add(new TranslateTransform(0, activeBlockTTF.Y));
            //    transformGroup.Children.Add(new ScaleTransform(1, 1, 0, 0));

            //    ActiveBlock.RenderTransform = transformGroup;

            //}
        }
        private void UpdateActiveLocation()
        {
            if (SelectedItem == null || !IsLoaded)
            {
                return;
            }
            var item = ItemsDictionary[SelectedItem.ID];
            item.Loaded += (e, c) =>
            {
                ScrollToActive(0);
            };
        }

        private void Render()
        {
            ItemsPanel.Children.Clear();
            ItemsDictionary.Clear();

            if (Data != null)
            {
                foreach (var item in Data)
                {
                    AddItem(item);
                }
            }

            UpdateActiveLocation();
        }
    }
}
