using Avalonia;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.DatePickerBar
{
    public class DatePickerBarItem : TemplatedControl
    {
        public string Title
        {
            get { return GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<DatePickerBarItem,string>(nameof(Title));

        public bool IsSelected
        {
            get { return GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }
        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<DatePickerBarItem,bool>(nameof(IsSelected));

        public bool IsDisabled
        {
            get { return GetValue(IsDisabledProperty); }
            set { SetValue(IsDisabledProperty, value); }
        }
        public static readonly StyledProperty<bool> IsDisabledProperty =
            AvaloniaProperty.Register<DatePickerBarItem, bool>(nameof(IsDisabled));

        public DateTime Date
        {
            get { return GetValue(DateProperty); }
            set { SetValue(DateProperty, value); }
        }
        public static readonly StyledProperty<DateTime> DateProperty =
            AvaloniaProperty.Register<DatePickerBarItem,DateTime>("Date");


        protected override Type StyleKeyOverride => typeof(DatePickerBarItem);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == DateProperty &&  change.NewValue != change.OldValue)
            {
                var control = change.Sender as DatePickerBarItem;
                control.IsDisabled = control.Date > DateTime.Now.Date;
            }
        }
    }
}
