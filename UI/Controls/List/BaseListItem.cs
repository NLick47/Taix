using Avalonia;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.List
{
    public class BaseListItem : TemplatedControl
    {
        public string Text { get { return GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<BaseListItem, string>(nameof(Text));

        public bool IsSelected { get { return GetValue(IsSelectedProperty); } set { SetValue(IsSelectedProperty, value); } }
        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<BaseListItem, bool>(nameof(IsSelected));

        protected override Type StyleKeyOverride => typeof(BaseListItem);


    }
}
