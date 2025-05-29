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
        private string _text = string.Empty;
        public static readonly DirectProperty<BaseListItem, string> TextProperty =
            AvaloniaProperty.RegisterDirect<BaseListItem, string>(
                nameof(Text),
                o => o.Text,
                (o, v) => o.Text = v);

        public string Text
        {
            get => _text;
            set => SetAndRaise(TextProperty, ref _text, value);
        }
        
        private bool _isSelected;
        public static readonly DirectProperty<BaseListItem, bool> IsSelectedProperty =
            AvaloniaProperty.RegisterDirect<BaseListItem, bool>(
                nameof(IsSelected),
                o => o.IsSelected,
                (o, v) => o.IsSelected = v);

        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        }

        protected override Type StyleKeyOverride => typeof(BaseListItem);


    }
}
