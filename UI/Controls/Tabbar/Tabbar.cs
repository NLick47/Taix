using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DynamicData;
using UI.Controls.Base;

namespace UI.Controls.Tabbar
{
    public class Tabbar : TemplatedControl
    {
        public Color SelectedTextColor
        {
            get => GetValue(SelectedTextColorProperty);
            set => SetValue(SelectedTextColorProperty, value);
        }

        public static readonly StyledProperty<Color> SelectedTextColorProperty =
            AvaloniaProperty.Register<Tabbar, Color>(nameof(SelectedTextColor));

        public int SelectedIndex
        {
            get => GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public static readonly StyledProperty<int> SelectedIndexProperty =
            AvaloniaProperty.Register<Tabbar, int>(nameof(SelectedIndex));

        public ObservableCollection<string> Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<ObservableCollection<string>> DataProperty =
            AvaloniaProperty.Register<Tabbar, ObservableCollection<string>>(nameof(Data));

        private List<TextBlock> ItemsDictionary;
        private Grid ItemsContainer;

        //  选中标记块
        private Border ActiveBlock;
        private Grid GridIcon;

        public Tabbar()
        {
            ItemsDictionary = new List<TextBlock>();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            if(Data != null) Data.CollectionChanged -= Data_CollectionChanged;
        }

        private void Data_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e == null || Data == null || ItemsDictionary == null)
                return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldStartingIndex >= 0 && e.OldStartingIndex < Data.Count && e.OldStartingIndex < ItemsDictionary.Count)
                    {
                        if (Data[e.OldStartingIndex] != null)
                        {
                            ItemsDictionary[e.OldStartingIndex].Text = Data[e.OldStartingIndex];
                        }
                    }
                    break;
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ActiveBlock = e.NameScope.Get<Border>("ActiveBlock");
            ItemsContainer = e.NameScope.Get<Grid>("ItemsContainer");
            if(Data != null) Data.CollectionChanged += Data_CollectionChanged;
            Render();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SelectedIndexProperty)
            {
                OnSelectedItemChanged(change);
            }
        }

        private static void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs change)
        {
            var control = change.Sender as Tabbar;
            if (change.NewValue != change.OldValue)
            {
                control.ScrollToActive(int.Parse(change.OldValue.ToString()));
            }
        }

        private void ScrollToActive(int oldSelectedIndex = 0)
        {
            if (oldSelectedIndex > ItemsDictionary.Count || ItemsDictionary.Count == 0 || !IsLoaded)
            {
                return;
            }
            //  获取选中项
            var item = ItemsDictionary[SelectedIndex];
            var oldSelectedItem = ItemsDictionary[oldSelectedIndex];

            var relativePoint = item.Bounds.Position;
            item.Foreground = new SolidColorBrush(SelectedTextColor);

            var itemsContainerBounds = ItemsContainer.Bounds;

            var textBlockBounds = item.Bounds;

            var relativeCenter = new Point(
                textBlockBounds.Center.X - itemsContainerBounds.X,
                textBlockBounds.Center.Y - itemsContainerBounds.Y
            );

            ActiveBlock.TranslatePoint(new Point(0, 0), ItemsContainer);
            ActiveBlock.Width = item.Bounds.Width;

            ActiveBlock.RenderTransform = TransformOperations.Parse(
                $"translateX({relativeCenter.X - (ActiveBlock.Width / 2)}px)"
            );
            ReOldSelectedStyle();
        }

        private void ReOldSelectedStyle() => Reset();


        private void Render()
        {
            if (Data != null)
            {
                ItemsContainer.Children.Clear();
                ItemsContainer.ColumnDefinitions.Clear();
                ItemsDictionary.Clear();

                for (int i = 0; i < Data.Count; i++)
                {
                    var item = Data[i];
                    ItemsContainer.ColumnDefinitions.Add(
                        new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }
                    );
                    AddItem(item, i);
                }
            }
        }

        private void AddItem(string item, int col)
        {
            if (ItemsContainer != null)
            {
                var control = new TextBlock();
                control.TextAlignment = TextAlignment.Center;
                control.Text = item;
                control.Margin = new Thickness(0, 0, 10, 0);
                control.FontSize = 16;
                control.Cursor = new Cursor(StandardCursorType.Hand);
                control.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#1F1F1F"));
                control.Tag = col;
                control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
                control.PointerPressed += (e, c) =>
                {
                    int index = int.Parse(((Control)e).Tag?.ToString());
                    if (SelectedIndex != index)
                    {
                        SelectedIndex = index;
                    }
                };
                if (Data.IndexOf(item) == Data.Count - 1)
                {
                    control.Loaded += (e, c) =>
                    {
                        ActiveBlock.Width = control.Bounds.Width;
                        ScrollToActive();
                        Reset();
                    };
                }
                Grid.SetColumn(control, col);
                ItemsContainer.Children.Add(control);
                ItemsDictionary.Add(control);
            }
        }

        private void Reset()
        {
            foreach (var item in ItemsContainer.Children)
            {
                if (item != ItemsContainer.Children[SelectedIndex])
                {
                    var text = item as TextBlock;
                    text.Foreground = UI.Base.Color.Colors.GetFromString("#ccc");
                }
            }
        }

        protected override Type StyleKeyOverride => typeof(Tabbar);
    }
}
