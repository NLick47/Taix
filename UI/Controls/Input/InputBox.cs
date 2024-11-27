using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Base;

namespace UI.Controls.Input
{
    public class InputBox : TextBox
    {
        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }
        public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            AvaloniaProperty.Register<InputBox, CornerRadius>(nameof(CornerRadius));

        /// <summary>
        /// 图标
        /// </summary>
        public IconTypes Icon
        {
            get { return (IconTypes)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }
        public static readonly StyledProperty<IconTypes> IconProperty =
        AvaloniaProperty.Register<InputBox, IconTypes>(nameof(Icon));

        public Thickness BoxPadding { get { return (Thickness)GetValue(BoxPaddingProperty); } set { SetValue(BoxPaddingProperty, value); } 
        }
        public static readonly StyledProperty<Thickness> BoxPaddingProperty =
             AvaloniaProperty.Register<InputBox, Thickness>(nameof(Icon));

        public string Title { get { return (string)GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
        public static readonly StyledProperty<string> TitleProperty =
             AvaloniaProperty.Register<InputBox, string>(nameof(Title));

        public Thickness TitleMargin { get { return (Thickness)GetValue(TitleMarginProperty); } set { SetValue(TitleMarginProperty, value); } }
        public static readonly StyledProperty<Thickness> TitleMarginProperty = AvaloniaProperty.Register<InputBox, Thickness>(nameof(TitleMargin));

        public Thickness IconMargin { get { return (Thickness)GetValue(IconMarginProperty); } set { SetValue(IconMarginProperty, value); } }
        public static readonly StyledProperty<Thickness> IconMarginProperty = AvaloniaProperty.Register<InputBox, Thickness>(nameof(IconMargin));

        public IBrush IconColor { get { return (SolidColorBrush)GetValue(IconColorProperty); } set { SetValue(IconColorProperty, value); } }
        public static readonly StyledProperty<IBrush> IconColorProperty = AvaloniaProperty.Register<InputBox, IBrush>(nameof(IconColor));

        public IBrush TitleColor { get { return (SolidColorBrush)GetValue(TitleColorProperty); } set { SetValue(TitleColorProperty, value); } }
        public static readonly StyledProperty<IBrush> TitleColorProperty = AvaloniaProperty.Register<InputBox, IBrush>(nameof(TitleColor));

        public double IconSize { get { return (double)GetValue(IconSizeProperty); } set { SetValue(IconSizeProperty, value); } }
        public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<InputBox, double>(nameof(IconSize));

        public double TitleSize { get { return (double)GetValue(TitleSizeProperty); } set { SetValue(TitleSizeProperty, value); } }
        public static readonly StyledProperty<double> TitleSizeProperty = AvaloniaProperty.Register<InputBox, double>(nameof(TitleSize));

        public string Error { get { return (string)GetValue(ErrorProperty); } set { SetValue(ErrorProperty, value); } }
        public static readonly StyledProperty<string> ErrorProperty = AvaloniaProperty.Register<InputBox, string>(nameof(Error));

        public bool IsError { get { return (bool)GetValue(IsErrorProperty); } set { SetValue(IsErrorProperty, value); } }
        public static readonly StyledProperty<bool> IsErrorProperty = AvaloniaProperty.Register<InputBox, bool>(nameof(IsError));

        public bool IsShowError { get { return (bool)GetValue(IsShowErrorProperty); } set { SetValue(IsShowErrorProperty, value); } }
        public static readonly StyledProperty<bool> IsShowErrorProperty = AvaloniaProperty.Register<InputBox, bool>(nameof(IsShowError));


        public string Placeholder { get { return (string)GetValue(PlaceholderProperty); } set { SetValue(PlaceholderProperty, value); } }
        public static readonly StyledProperty<string> PlaceholderProperty = AvaloniaProperty.Register<InputBox, string>(nameof(Placeholder));
        private Popup ErrorPopup;

        protected override Type StyleKeyOverride => typeof(InputBox);

        public InputBox()
        {
            Unloaded += InputBox_Unloaded;
        }

        private void InputBox_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= InputBox_Unloaded;
            if (ErrorPopup != null)
            {
                ErrorPopup.Closed -= ErrorPopup_Closed;
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ErrorPopup = e.NameScope.Find("ErrorPopup") as Popup;
            ErrorPopup.Closed += ErrorPopup_Closed;
        }

        private void ErrorPopup_Closed(object sender, EventArgs e)
        {
            IsShowError = false;
        }

        public void ShowError()
        {
            IsShowError = true;
        }

        public void HideError()
        {

            IsShowError = false;
        }
    }

}
