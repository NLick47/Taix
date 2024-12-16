using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using DynamicData;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.List
{
    public class BaseList : TemplatedControl
    {
        public ObservableCollection<string> Items { get { return GetValue(ItemsProperty); } set { SetValue(ItemsProperty, value); } }

        public static readonly StyledProperty<ObservableCollection<string>> ItemsProperty =
            AvaloniaProperty.Register<BaseList, ObservableCollection<string>>(nameof(Items));

        public string SelectedItem { get { return GetValue(SelectedItemProperty); } set { SetValue(SelectedItemProperty, value); } }
        public static readonly StyledProperty<string> SelectedItemProperty =
            AvaloniaProperty.Register<BaseList, string>(nameof(SelectedItem));

        private StackPanel Container;

        private List<BaseListItem> ItemsMap;
        private ScrollViewer ScrollViewer;

        /// <summary>
        /// 选择项更改后发生
        /// </summary>
        public event EventHandler SelectedItemChanged;

        public BaseList()
        {
            Items = new ObservableCollection<string>();
            ItemsMap = new List<BaseListItem>();
            AddCollectionChangedHandler();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ItemsProperty)
            {
                (var oldVal, var newVal) = change.GetOldAndNewValue<ObservableCollection<string>>();
                if (oldVal != null)
                {
                    oldVal.CollectionChanged -= Items_CollectionChanged;
                }
                var control = change.Sender as BaseList;
                control.Render();
                control.AddCollectionChangedHandler();
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            UnsubscribeEvents();
            if (Items != null) Items.CollectionChanged -= Items_CollectionChanged;
        }

        public void UnsubscribeEvents()
        {
            if (ItemsMap != null)
            {
                foreach (var item in ItemsMap)
                {
                    if (item != null)
                    {
                        item.PointerPressed -= ItemClick;
                        item.Loaded -= HandleLoaded;
                    }
                }
                ItemsMap.Clear();
            }


            if (Container?.Children != null)
            {
                foreach (var item in Container.Children)
                {
                    if (item != null)
                    {
                        item.PointerPressed -= ItemClick;
                        item.Loaded -= HandleLoaded;
                    }
                }
                Container.Children.Clear();
            }

            ItemsMap?.Clear();
            Container?.Children?.Clear();
        }

        protected override Type StyleKeyOverride => typeof(BaseList);

        private void AddCollectionChangedHandler()
        {
            if (Items == null) return;

            Items.CollectionChanged -= Items_CollectionChanged;
            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    AddItem(item as string);
                }
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    RemoveItem(item as string);
                }
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                UnsubscribeEvents();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Container = e.NameScope.Get<StackPanel>("Container");
            ScrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
            Render();
        }

        private void Render()
        {
            if (Container != null)
            {
                UnsubscribeEvents();
                if (Items == null)
                {
                    return;
                }

                foreach (var item in Items)
                {
                    AddItem(item);
                }
            }
        }

        private void AddItem(string item)
        {
            if (Container == null)
            {
                return;
            }
            var itemControl = new BaseListItem();
            itemControl.Text = item;
            itemControl.PointerPressed += ItemClick;

            bool isHandleSelected = false;
            if (SelectedItem == item)
            {
                itemControl.IsSelected = true;

                itemControl.Loaded += HandleLoaded;
            }

            ItemsMap.Add(itemControl);
            Container.Children.Add(itemControl);
        }

        void HandleLoaded(object sender, RoutedEventArgs e)
        {
            var control = sender as BaseListItem;
            if (control != null)
            {
                var pointInScrollViewer = control.TranslatePoint(new Point(0, 0), ScrollViewer);
                if (pointInScrollViewer.HasValue)
                {
                    ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, pointInScrollViewer.Value.Y);
                }
            }
        }

        private void OnItemControlLoaded(object sender, RoutedEventArgs e)
        {
            var itemControl = sender as BaseListItem;
            if (itemControl != null)
            {
                bool isHandleSelected = false;
                var pointInScrollViewer = itemControl.TranslatePoint(new Point(0, 0), ScrollViewer);
                if (pointInScrollViewer.HasValue)
                {
                    ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, pointInScrollViewer.Value.Y);
                }
            }
        }
        private void RemoveItem(string item)
        {
            if (Container == null)
            {
                return;
            }
            var control = ItemsMap.Where(m => m.Text == item).FirstOrDefault();
            if (control != null)
            {
                Container.Children.Remove(control);
            }
        }

        private void ItemClick(object? sender, PointerPressedEventArgs e)
        {
            ClearSelectedItems();
            (sender as BaseListItem).IsSelected = true;
            SelectedItem = (sender as BaseListItem).Text;
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearSelectedItems()
        {
            foreach (var item in ItemsMap)
            {
                item.IsSelected = false;
            }
        }


        private void OnSelect()
        {
            ClearSelectedItems();
            var control = ItemsMap.Where(m => m.Text == SelectedItem).FirstOrDefault();
            if (control != null)
            {
                control.IsSelected = true;
            }
        }

    }
}
