using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;
using UI.Librays.Image;

namespace UI.Controls.Charts
{
    public class ChartsItemTypeCard : TemplatedControl
    {
        public ChartsDataModel Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<ChartsDataModel> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypeCard, ChartsDataModel>(nameof(Data));

        public double MaxValue
        {
            get { return GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ChartsItemTypeCard, double>(nameof(MaxValue));

        public bool IsLoading
        {
            get { return GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly StyledProperty<bool> IsLoadingProperty =
           AvaloniaProperty.Register<ChartsItemTypeCard, bool>(nameof(IsLoading));

        private TextBlock NameTextObj, ValueTextObj;
        private Rectangle ValueBlockObj;
        private StackPanel ValueContainer;
        private Image IconObj;
        private bool isRendering = false;
        private bool IsAddEvent = false;


        protected override Type StyleKeyOverride => typeof(ChartsItemTypeCard);

        public ChartsItemTypeCard()
        {
            Unloaded += ChartsItemTypeCard_Unloaded;
        }
        private void ChartsItemTypeCard_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= ChartsItemTypeCard_Unloaded;
            Loaded -= ChartsItemTypeCard_Loaded;
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
                Loaded += ChartsItemTypeCard_Loaded;
                IsAddEvent = true;
            }
        }

        private void ChartsItemTypeCard_Loaded(object sender, RoutedEventArgs e)
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
            NameTextObj.SizeChanged += (e, c) =>
            {
                //  处理文字过长显示
                if (NameTextObj.Bounds.Width > 121 && NameTextObj.FontSize > 8)
                {
                    NameTextObj.FontSize = NameTextObj.FontSize - 1;
                }
            };
            ValueTextObj.Text = Data.Tag;
            IconObj.Source = Imager.Load(Data.Icon);

            ValueTextObj.SizeChanged += (e, c) =>
            {
                //if (MaxValue <= 0)
                //{
                //    return;
                //}
                double size = (Data.Value / MaxValue) * Bounds.Width / 3;
                ValueBlockObj.Width = ValueBlockObj.Height = size;


                ValueBlockObj.Effect = new BlurEffect()
                {
                    Radius = size
                };
            };
        }

    }
}
