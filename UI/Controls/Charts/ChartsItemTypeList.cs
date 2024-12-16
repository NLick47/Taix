using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Org.BouncyCastle.Crmf;
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
        public ChartsDataModel Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<ChartsDataModel> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypeList, ChartsDataModel>(nameof(Data));

        public double MaxValue
        {
            get { return GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
           AvaloniaProperty.Register<ChartsItemTypeList, double>(nameof(MaxValue));

        /// <summary>
        /// 是否正在加载中
        /// </summary>
        public bool IsLoading
        {
            get { return GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<ChartsItemTypeList,bool>(nameof(IsLoading));

        /// <summary>
        /// 是否显示徽章
        /// </summary>
        public bool IsShowBadge
        {
            get { return GetValue(IsShowBadgeProperty); }
            set { SetValue(IsShowBadgeProperty, value); }
        }
        public static readonly StyledProperty<bool> IsShowBadgeProperty =
           AvaloniaProperty.Register<ChartsItemTypeList, bool>(nameof(IsShowBadge));


        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }
        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<ChartsItemTypeList,double>(nameof(IconSize));

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
