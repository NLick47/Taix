using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Transformation;
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
using Avalonia.Collections;
using UI.Controls.Navigation.Models;

namespace UI.Controls.Navigation
{
    public class Navigation : TemplatedControl
    {
        private ContextMenu? _itemContextMenu;

        public static readonly DirectProperty<Navigation, ContextMenu?> ItemContextMenuProperty =
            AvaloniaProperty.RegisterDirect<Navigation, ContextMenu?>(
                nameof(ItemContextMenu),
                o => o.ItemContextMenu,
                (o, v) => o.ItemContextMenu = v);

        public ContextMenu? ItemContextMenu
        {
            get => _itemContextMenu;
            set => SetAndRaise(ItemContextMenuProperty, ref _itemContextMenu, value);
        }
        
        private NavigationItemModel? _selectedItem;

        public static readonly DirectProperty<Navigation, NavigationItemModel?> SelectedItemProperty =
            AvaloniaProperty.RegisterDirect<Navigation, NavigationItemModel?>(
                nameof(SelectedItem),
                o => o.SelectedItem,
                (o, v) => o.SelectedItem = v);

        public NavigationItemModel? SelectedItem
        {
            get => _selectedItem;
            set => SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
        }

        private object? _topExtContent;

        public static readonly DirectProperty<Navigation, object?> TopExtContentProperty =
            AvaloniaProperty.RegisterDirect<Navigation, object?>(
                nameof(TopExtContent),
                o => o.TopExtContent,
                (o, v) => o.TopExtContent = v);

        public object? TopExtContent
        {
            get => _topExtContent;
            set => SetAndRaise(TopExtContentProperty, ref _topExtContent, value);
        }

        private object? _bottomExtContent;

        public static readonly DirectProperty<Navigation, object?> BottomExtContentProperty =
            AvaloniaProperty.RegisterDirect<Navigation, object?>(
                nameof(BottomExtContent),
                o => o.BottomExtContent,
                (o, v) => o.BottomExtContent = v);

        public object? BottomExtContent
        {
            get => _bottomExtContent;
            set => SetAndRaise(BottomExtContentProperty, ref _bottomExtContent, value);
        }

        private ObservableCollection<NavigationItemModel> _data = new();

        public static readonly DirectProperty<Navigation, ObservableCollection<NavigationItemModel>> DataProperty =
            AvaloniaProperty.RegisterDirect<Navigation, ObservableCollection<NavigationItemModel>>(
                nameof(Data),
                o => o.Data,
                (o, v) => o.Data = v);

        public ObservableCollection<NavigationItemModel> Data
        {
            get => _data;
            set => SetAndRaise(DataProperty, ref _data, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SelectedItemProperty)
            {
                OnSelectedItemChanged(change);
            }

            if (change.Property == DataProperty)
            {
                OnDataChanged(change);
            }
        }

        private Dictionary<int, NavigationItem> ItemsDictionary;

        public event EventHandler OnSelected;

        public event EventHandler OnMouseRightButtonUP;

        //  选中标记块
        private Border ActiveBlock;

        private StackPanel ItemsPanel;

        private void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var control = e.Sender as Navigation;
            var oldItem = e.OldValue as NavigationItemModel;
            var newItem = e.NewValue as NavigationItemModel;
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

        private void OnDataChanged(AvaloniaPropertyChangedEventArgs e)
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
            ItemsPanel = e.NameScope.Get<StackPanel>("ItemsPanel")!;
            ActiveBlock = e.NameScope.Get<Border>("ActiveBlock")!;
            CreateTransitions();
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

                if (!string.IsNullOrEmpty(item.Title))
                {
                    navItem.MouseUp += NavItem_MouseUp;
                    navItem.Unloaded += (e, c) => { navItem.MouseUp -= NavItem_MouseUp; };
                }

                if (SelectedItem?.ID == id)
                {
                    navItem.IsSelected = true;
                }

                ItemsPanel.Children.Add(navItem);
                ItemsDictionary.Add(id, navItem);
            }
        }


        public void CreateTransitions()
        {
            ActiveBlock.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Border.RenderTransformProperty,
                    Duration = TimeSpan.FromSeconds(0.25),
                }
            };
        }


        private void NavItem_MouseUp(object sender, PointerPressedEventArgs e)
        {
            var navitem = sender as NavigationItem;
            if (navitem != null)
            {
                var properties = e.GetCurrentPoint(null).Properties;

                if (properties.IsLeftButtonPressed)
                {
                    // 左键选中
                    SelectedItem = Data.First(m => m.ID == navitem.ID);
                    OnSelected?.Invoke(this, EventArgs.Empty);

                    if (SelectedItem != null && SelectedItem.ID != navitem.ID)
                    {
                        ScrollToActive();
                    }
                }
                else if (properties.IsRightButtonPressed)
                {
                    // 右键
                    var args = new RoutedEventArgs();
                    args.RoutedEvent = e.RoutedEvent;
                    args.Source = Data.FirstOrDefault(m => m.ID == navitem.ID);
                    OnMouseRightButtonUP?.Invoke(this, args);
                }
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

            //  选中项的坐标
            var relativePoint = item.Bounds.Position;
            var activeBlockTTF = ActiveBlock.Bounds.Position;
            ActiveBlock.RenderTransform = TransformOperations.Parse($"translateY({relativePoint.Y + 16}px)");
        }

        private void UpdateActiveLocation()
        {
            if (SelectedItem == null || !IsLoaded)
            {
                return;
            }

            var item = ItemsDictionary[SelectedItem.ID];
            item.Loaded += (e, c) => { ScrollToActive(0); };
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