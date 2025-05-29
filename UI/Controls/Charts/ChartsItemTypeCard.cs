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
        private ChartsDataModel _data;
        public ChartsDataModel Data
        {
            get => _data;
            set => SetAndRaise(DataProperty, ref _data, value);
        }
        public static readonly DirectProperty<ChartsItemTypeCard, ChartsDataModel> DataProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeCard, ChartsDataModel>(
                nameof(Data),
                o => o.Data,
                (o, v) => o.Data = v);

        private double _maxValue;
        public double MaxValue
        {
            get => _maxValue;
            set => SetAndRaise(MaxValueProperty, ref _maxValue, value);
        }
        public static readonly DirectProperty<ChartsItemTypeCard, double> MaxValueProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeCard, double>(
                nameof(MaxValue),
                o => o.MaxValue,
                (o, v) => o.MaxValue = v);

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
        }
        public static readonly DirectProperty<ChartsItemTypeCard, bool> IsLoadingProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeCard, bool>(
                nameof(IsLoading),
                o => o.IsLoading,
                (o, v) => o.IsLoading = v);

        private TextBlock NameTextObj, ValueTextObj;
        private Rectangle ValueBlockObj;
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
