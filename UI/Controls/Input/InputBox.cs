using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
        /// <summary>
        /// 图标
        /// </summary>
        public IconTypes Icon
        {
            get { return GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly StyledProperty<IconTypes> IconProperty =
            AvaloniaProperty.Register<InputBox, IconTypes>(nameof(Icon));

        public Thickness BoxPadding
        {
            get { return GetValue(BoxPaddingProperty); }
            set { SetValue(BoxPaddingProperty, value); }
        }

        public static readonly StyledProperty<Thickness> BoxPaddingProperty =
            AvaloniaProperty.Register<InputBox, Thickness>(nameof(BoxPadding));

        private string _title = string.Empty;
        public static readonly DirectProperty<InputBox, string> TitleProperty =
            AvaloniaProperty.RegisterDirect<InputBox, string>(
                nameof(Title),
                o => o.Title,
                (o, v) => o.Title = v);

        public string Title
        {
            get => _title;
            set => SetAndRaise(TitleProperty, ref _title, value);
        }

        public Thickness TitleMargin
        {
            get { return GetValue(TitleMarginProperty); }
            set { SetValue(TitleMarginProperty, value); }
        }

        public static readonly StyledProperty<Thickness> TitleMarginProperty =
            AvaloniaProperty.Register<InputBox, Thickness>(nameof(TitleMargin));

        public Thickness IconMargin
        {
            get { return GetValue(IconMarginProperty); }
            set { SetValue(IconMarginProperty, value); }
        }

        public static readonly StyledProperty<Thickness> IconMarginProperty =
            AvaloniaProperty.Register<InputBox, Thickness>(nameof(IconMargin));

        public IBrush IconColor
        {
            get { return GetValue(IconColorProperty); }
            set { SetValue(IconColorProperty, value); }
        }

        public static readonly StyledProperty<IBrush> IconColorProperty =
            AvaloniaProperty.Register<InputBox, IBrush>(nameof(IconColor));

        public IBrush TitleColor
        {
            get { return GetValue(TitleColorProperty); }
            set { SetValue(TitleColorProperty, value); }
        }

        public static readonly StyledProperty<IBrush> TitleColorProperty =
            AvaloniaProperty.Register<InputBox, IBrush>(nameof(TitleColor));

        // 数值类型属性
        private double _iconSize;

        public static readonly DirectProperty<InputBox, double> IconSizeProperty =
            AvaloniaProperty.RegisterDirect<InputBox, double>(
                nameof(IconSize),
                o => o.IconSize,
                (o, v) => o.IconSize = v);

        public double IconSize
        {
            get => _iconSize;
            set => SetAndRaise(IconSizeProperty, ref _iconSize, value);
        }

        private double _titleSize;

        public static readonly DirectProperty<InputBox, double> TitleSizeProperty =
            AvaloniaProperty.RegisterDirect<InputBox, double>(
                nameof(TitleSize),
                o => o.TitleSize,
                (o, v) => o.TitleSize = v);

        public double TitleSize
        {
            get => _titleSize;
            set => SetAndRaise(TitleSizeProperty, ref _titleSize, value);
        }

// 字符串类型属性
        private string _error = string.Empty;

        public static readonly DirectProperty<InputBox, string> ErrorProperty =
            AvaloniaProperty.RegisterDirect<InputBox, string>(
                nameof(Error),
                o => o.Error,
                (o, v) => o.Error = v);

        public string Error
        {
            get => _error;
            set => SetAndRaise(ErrorProperty, ref _error, value);
        }

        private string _placeholder = string.Empty;

        public static readonly DirectProperty<InputBox, string> PlaceholderProperty =
            AvaloniaProperty.RegisterDirect<InputBox, string>(
                nameof(Placeholder),
                o => o.Placeholder,
                (o, v) => o.Placeholder = v);

        public string Placeholder
        {
            get => _placeholder;
            set => SetAndRaise(PlaceholderProperty, ref _placeholder, value);
        }

// 布尔类型属性
        private bool _isError;

        public static readonly DirectProperty<InputBox, bool> IsErrorProperty =
            AvaloniaProperty.RegisterDirect<InputBox, bool>(
                nameof(IsError),
                o => o.IsError,
                (o, v) => o.IsError = v);

        public bool IsError
        {
            get => _isError;
            set => SetAndRaise(IsErrorProperty, ref _isError, value);
        }

        private bool _isShowError;

        public static readonly DirectProperty<InputBox, bool> IsShowErrorProperty =
            AvaloniaProperty.RegisterDirect<InputBox, bool>(
                nameof(IsShowError),
                o => o.IsShowError,
                (o, v) => o.IsShowError = v);

        public bool IsShowError
        {
            get => _isShowError;
            set => SetAndRaise(IsShowErrorProperty, ref _isShowError, value);
        }

        private Popup? _errorPopup;


        protected override Type StyleKeyOverride => typeof(InputBox);

        public InputBox()
        {
            Unloaded += InputBox_Unloaded;
        }

        private void InputBox_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= InputBox_Unloaded;
            if (_errorPopup != null)
            {
                _errorPopup.Closed -= ErrorPopup_Closed;
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _errorPopup = e.NameScope.Get<Popup>("ErrorPopup");
            _errorPopup.Closed += ErrorPopup_Closed;
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