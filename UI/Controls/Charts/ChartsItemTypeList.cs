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
using UI.Librays.Image;

namespace UI.Controls.Charts
{
    public class ChartsItemTypeList : TemplatedControl
    {
        public ChartsDataModel Data
        {
            get { return (ChartsDataModel)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<ChartsDataModel> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypeList, ChartsDataModel>(nameof(Data));

        public double MaxValue
        {
            get { return (double)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
           AvaloniaProperty.Register<ChartsItemTypeList, double>(nameof(MaxValue));

        /// <summary>
        /// 是否正在加载中
        /// </summary>
        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<ChartsItemTypeList,bool>(nameof(IsLoading));

        /// <summary>
        /// 是否显示徽章
        /// </summary>
        public bool IsShowBadge
        {
            get { return (bool)GetValue(IsShowBadgeProperty); }
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
        //private Grid ValueContainer;
        private Image IconObj;
        private bool isRendering = false;
        private bool IsAddEvent = false;

        protected override Type StyleKeyOverride => typeof(ChartsItemTypeList);

        public ChartsItemTypeList()
        {
            Unloaded += ChartsItemTypeList_Unloaded;
        }

        private void ChartsItemTypeList_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= ChartsItemTypeList_Unloaded;
            Loaded -= ChartsItemTypeList_Loaded;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            NameTextObj = e.NameScope.Find("NameTextObj") as TextBlock;
            ValueTextObj = e.NameScope.Find("ValueTextObj") as TextBlock;
            ValueBlockObj = e.NameScope.Find("ValueBlockObj") as Rectangle;
            //ValueContainer = GetTemplateChild("ValueContainer") as Grid;
            ValueContainer = e.NameScope.Find("ValueContainer") as StackPanel;

            IconObj = e.NameScope.Find("IconObj") as Image;

            if (!IsAddEvent)
            {
                Loaded += ChartsItemTypeList_Loaded;
                IsAddEvent = true;
            }

            var parent = Parent as Control;
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
            //NameTextObj.Text = Data.Name;
            //对部分程序未获取程序名的程序使用路径中名字作为程序名
            //if (Data.Name.Trim() == "" && Data.PopupText.Trim() != "")
            //{
            //    FileInfo fi = new FileInfo(Data.PopupText);
            //    NameTextObj.Text = fi.Name.Replace(fi.Extension, "");
            //}

            ValueTextObj.Text = Data.Tag;
            IconObj.Source = Imager.Load(Data.Icon);

            ValueTextObj.SizeChanged += (e, c) =>
            {
                if (MaxValue <= 0)
                {
                    return;
                }
                UpdateValueBlockWidth();
            };
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
