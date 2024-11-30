using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Base
{
    public class Placeholder : TemplatedControl
    {
        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }
        public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            AvaloniaProperty.Register<Placeholder, CornerRadius>(nameof(CornerRadius));

        private Border Flash;
        private bool IsAddEvent = false;

        protected override Type StyleKeyOverride => typeof(Placeholder);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Flash = e.NameScope.Get<Border>("Flash");
            if (!IsAddEvent)
            {
                Loaded += Placeholder_Loaded;
            }
        }

        private void Placeholder_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= Placeholder_Loaded;
            IsAddEvent = true;
        }
    }
}
