using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
namespace UI.Controls.Base
{
    public class Icon : TemplatedControl
    {
        private IconTypes _iconType = IconTypes.Back;
        public IconTypes IconType
        {
            get => _iconType;
            set => SetAndRaise(IconTypeProperty, ref _iconType, value);
        }
        public static readonly DirectProperty<Icon, IconTypes> IconTypeProperty =
            AvaloniaProperty.RegisterDirect<Icon, IconTypes>(
                nameof(IconType),
                o => o.IconType,
                (o, v) => o.IconType = v);

        private string _unicode = IconConverter.ToUnicode(IconTypes.Back);
        public string Unicode
        {
            get => _unicode;
            set => SetAndRaise(UnicodeProperty, ref _unicode, value);
        }
        public static readonly DirectProperty<Icon, string> UnicodeProperty =
            AvaloniaProperty.RegisterDirect<Icon, string>(
                nameof(Unicode),
                o => o.Unicode,
                (o, v) => o.Unicode = v);

        private  void OnIconTypeChanged(IconTypes icon)
        {
           
           Unicode = IconConverter.ToUnicode(icon);

        }

        public Icon()
        {
            this.GetObservable(IconTypeProperty).Subscribe(OnIconTypeChanged);
        }
    }
}
