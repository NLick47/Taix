using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;
using UI.Librays.Image;

namespace UI.Controls.Charts
{
    public class ChartsItemTypeList : TemplatedControl
    {
        private ChartsDataModel _data;
        public ChartsDataModel Data
        {
            get => _data;
            set => SetAndRaise(DataProperty, ref _data, value);
        }
        public static readonly DirectProperty<ChartsItemTypeList, ChartsDataModel> DataProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeList, ChartsDataModel>(
                nameof(Data),
                o => o.Data,
                (o, v) => o.Data = v);

        private double _maxValue;
        public double MaxValue
        {
            get => _maxValue;
            set => SetAndRaise(MaxValueProperty, ref _maxValue, value);
        }
        public static readonly DirectProperty<ChartsItemTypeList, double> MaxValueProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeList, double>(
                nameof(MaxValue),
                o => o.MaxValue,
                (o, v) => o.MaxValue = v);

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
        }
        public static readonly DirectProperty<ChartsItemTypeList, bool> IsLoadingProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeList, bool>(
                nameof(IsLoading),
                o => o.IsLoading,
                (o, v) => o.IsLoading = v);

        private bool _isShowBadge;
        public bool IsShowBadge
        {
            get => _isShowBadge;
            set => SetAndRaise(IsShowBadgeProperty, ref _isShowBadge, value);
        }
        public static readonly DirectProperty<ChartsItemTypeList, bool> IsShowBadgeProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeList, bool>(
                nameof(IsShowBadge),
                o => o.IsShowBadge,
                (o, v) => o.IsShowBadge = v);

        private double _iconSize;
        public double IconSize
        {
            get => _iconSize;
            set => SetAndRaise(IconSizeProperty, ref _iconSize, value);
        }
        public static readonly DirectProperty<ChartsItemTypeList, double> IconSizeProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeList, double>(
                nameof(IconSize),
                o => o.IconSize,
                (o, v) => o.IconSize = v);

        private TextBlock NameTextObj, ValueTextObj;
        private Rectangle ValueBlockObj;
        private StackPanel ValueContainer;
        private Image IconObj;
        private bool isRendering = false;
        private bool IsAddEvent = false;

        protected override Type StyleKeyOverride => typeof(ChartsItemTypeList);


        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            Loaded -= ChartsItemTypeList_Loaded;
            var parent = this.Parent as Control;
            parent.SizeChanged -= Parent_SizeChanged;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            NameTextObj = e.NameScope.Get<TextBlock>("NameTextObj");
            ValueTextObj = e.NameScope.Get<TextBlock>("ValueTextObj");
            ValueBlockObj = e.NameScope.Get<Rectangle>("ValueBlockObj");
            ValueContainer = e.NameScope.Get<StackPanel>("ValueContainer");
            IconObj = e.NameScope.Get<Image>("IconObj");

            if (!IsAddEvent)
            {
                Loaded += ChartsItemTypeList_Loaded;
                IsAddEvent = true;
            }

            var parent = this.Parent as Control;
            parent.SizeChanged += Parent_SizeChanged;
        }

    

        private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateValueBlockWidth();
        }

        private void ChartsItemTypeList_Loaded(object sender, RoutedEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            if (isRendering || Data == null)
            {
                return;
            }
            isRendering = true;
            ValueTextObj.LayoutUpdated += (e, c) =>
            {
                if (MaxValue <= 0)
                {
                    return;
                }
                UpdateValueBlockWidth();
            };

            ValueTextObj.Text = Data.Tag;
            IconObj.Source = Imager.Load(Data.Icon);

          
        }

        public void UpdateValueBlockWidth()
        {
            if (Data == null || !IsLoaded)
            {
                return;
            }
            ValueBlockObj.Width = (Data.Value / MaxValue) * (ValueContainer.Bounds.Width * 0.95 - ValueTextObj.Bounds.Width);
        }




    }
}
