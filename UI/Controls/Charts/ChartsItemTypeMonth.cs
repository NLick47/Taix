using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;

namespace UI.Controls.Charts
{
    public class ChartsItemTypeMonth : TemplatedControl
    {
        private string _toolTip;

        public static readonly DirectProperty<ChartsItemTypeMonth, string> ToolTipProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeMonth, string>(
                nameof(ToolTip),
                o => o.ToolTip,
                (o, v) => o.ToolTip = v);

        public string ToolTip
        {
            get => _toolTip;
            set => SetAndRaise(ToolTipProperty, ref _toolTip, value);
        }

        private ChartsDataModel _data;

        public static readonly DirectProperty<ChartsItemTypeMonth, ChartsDataModel> DataProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeMonth, ChartsDataModel>(
                nameof(Data),
                o => o.Data,
                (o, v) => o.Data = v);

        /// <summary>
        /// 数据
        /// </summary>
        public ChartsDataModel Data
        {
            get => _data;
            set => SetAndRaise(DataProperty, ref _data, value);
        }

        private double _maxValue;

        public static readonly DirectProperty<ChartsItemTypeMonth, double> MaxValueProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeMonth, double>(
                nameof(MaxValue),
                o => o.MaxValue,
                (o, v) => o.MaxValue = v);

        public double MaxValue
        {
            get => _maxValue;
            set => SetAndRaise(MaxValueProperty, ref _maxValue, value);
        }

        private bool _isLoading;

        public static readonly DirectProperty<ChartsItemTypeMonth, bool> IsLoadingProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeMonth, bool>(
                nameof(IsLoading),
                o => o.IsLoading,
                (o, v) => o.IsLoading = v);

        /// <summary>
        /// 是否正在加载中
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
        }

        private bool _isSelected;

        public static readonly DirectProperty<ChartsItemTypeMonth, bool> IsSelectedProperty =
            AvaloniaProperty.RegisterDirect<ChartsItemTypeMonth, bool>(
                nameof(IsSelected),
                o => o.IsSelected,
                (o, v) => o.IsSelected = v);

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        }

        private Rectangle ValueBlockObj;

        //private StackPanel ValueContainer;
        //private Image IconObj;
        private bool isRendering = false;
        private bool IsAddEvent = false;

        protected override Type StyleKeyOverride => typeof(ChartsItemTypeMonth);

        public ChartsItemTypeMonth()
        {
            Unloaded += ChartsItemTypeMonth_Unloaded;
        }

        private void ChartsItemTypeMonth_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= ChartsItemTypeMonth_Unloaded;
            Loaded -= ChartsItemTypeMonth_Loaded;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ValueBlockObj = e.NameScope.Get<Rectangle>("ValueBlockObj");
            if (!IsAddEvent)
            {
                IsAddEvent = true;
                Loaded += ChartsItemTypeMonth_Loaded;
            }
        }

        private void ChartsItemTypeMonth_Loaded(object sender, RoutedEventArgs e)
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
            //Loaded += (s, e) =>
            //{
            //    ValueBlockObj.Width = ValueBlockObj.Height = (Data.Value / MaxValue) * ActualWidth;
            //};

            double size = (Data.Value / MaxValue) * Bounds.Width;
            if (size > 0 && size < 8) //防止历史数值太小界面无显示效果
                size = 8;
            ValueBlockObj.Width = ValueBlockObj.Height = size;
            ToolTip = Data.DateTime.ToString("yyyy年MM月dd日") + " " + (string.IsNullOrEmpty(Data.Tag) ? "无数据" : Data.Tag);

            if (Data.DateTime.Date == DateTime.Now.Date)
            {
                IsSelected = true;
                ToolTip = "[今日] " + ToolTip;
            }
            //ValueTextObj.Text = Data.DateTime.Day.ToString();
            //NameTextObj.Text = Data.Name;
            //NameTextObj.SizeChanged += (e, c) =>
            //{
            //    //  处理文字过长显示
            //    if (NameTextObj.ActualWidth > 121 && NameTextObj.FontSize > 8)
            //    {
            //        NameTextObj.FontSize = NameTextObj.FontSize - 1;
            //    }
            //};
            //ValueTextObj.Text = Data.Tag;
            //IconObj.Source = Imager.Load(Data.Icon);

            //ValueTextObj.SizeChanged += (e, c) =>
            //{
            //    //if (MaxValue <= 0)
            //    //{
            //    //    return;
            //    //}
            //    ValueBlockObj.Width = ValueBlockObj.Height = (Data.Value / MaxValue) * ActualWidth;
            //};
        }
    }
}