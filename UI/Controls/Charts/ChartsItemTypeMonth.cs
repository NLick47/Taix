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

        public static readonly StyledProperty<string> ToolTipProperty =
        AvaloniaProperty.Register<ChartsItemTypeMonth, string>(nameof(ToolTip));

        public string ToolTip
        {
            get => GetValue(ToolTipProperty);
            set => SetValue(ToolTipProperty, value);
        }
        /// <summary>
        /// 数据
        /// </summary>
        public ChartsDataModel Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<ChartsDataModel> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypeMonth, ChartsDataModel>(nameof(Data));

        public double MaxValue
        {
            get { return GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ChartsItemTypeMonth, double>(nameof(MaxValue));

        /// <summary>
        /// 是否正在加载中
        /// </summary>
        public bool IsLoading
        {
            get { return GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<ChartsItemTypeMonth, bool>(nameof(IsLoading));

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get { return GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }
        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<ChartsItemTypeMonth, bool>(nameof(IsSelected));

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