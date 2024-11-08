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
        public static readonly StyledProperty<IconTypes> IconTypeProperty =
       AvaloniaProperty.Register<Icon, IconTypes>(nameof(IconType), IconTypes.Back
         
         );


       

        public IconTypes IconType
        {
            get => GetValue(IconTypeProperty);
            set => SetValue(IconTypeProperty, value);
        }

        public static readonly StyledProperty<string> UnicodeProperty =
            AvaloniaProperty.Register<Icon, string>(nameof(Unicode),
                IconConverter.ToUnicode(IconTypes.Back));

        public string Unicode
        {
            get => GetValue(UnicodeProperty);
            set => SetValue(UnicodeProperty, value);
        }

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
