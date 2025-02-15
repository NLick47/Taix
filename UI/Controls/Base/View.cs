using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Base
{
    public class View : ContentControl
    {
        public string Condition
        {
            get { return GetValue(ConditionProperty); }
            set { SetValue(ConditionProperty, value); }
        }

        public static readonly StyledProperty<string> ConditionProperty =
          AvaloniaProperty.Register<View, string>(nameof(Condition));

        public object Value
        {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly StyledProperty<object> ValueProperty =
           AvaloniaProperty.Register<View, object>(nameof(Value));

        protected override Type StyleKeyOverride => typeof(View);

        public View()
        {
            Loaded += View_Loaded;
            Unloaded += View_Unloaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            Handle();
        }

        private void View_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= View_Loaded;
            Unloaded -= View_Unloaded;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ValueProperty)
            {
                var control = change.Sender as View;
                control.Handle();
            }
        }

        private void Handle()
        {
            try
            {
                if (string.IsNullOrEmpty(Condition) && Value == null)
                {
                    return;
                }

                bool isShow = false;
                if (string.IsNullOrEmpty(Condition))
                {
                    if ((bool)Value)
                    {
                        isShow = true;
                    }
                }
                else
                {
                    if (Condition.IndexOf("=") != -1)
                    {
                        string conditionVal = Condition.Substring(Condition.IndexOf("=") + 1);
                        string value = (Value == null ? string.Empty : Value.ToString());
                        isShow = Condition.Contains("!") ? conditionVal != value : conditionVal == value;
                    }
                    else if (Condition.IndexOf("not null") != -1)
                    {

                        isShow = Value != null;
                    }
                    else if (Condition.IndexOf("null") != -1)
                    {

                        isShow = Value == null;
                    }
                    else if (Condition.IndexOf("not empty") != -1)
                    {
                        isShow = Value switch
                        {
                            null => false,
                            IList<object> m => m.Count != 0,
                            IList n => n.Count != 0,
                            _ => !string.IsNullOrEmpty(Value.ToString())
                        };

                    }
                    else if (Condition.IndexOf("empty") != -1)
                    {
                        isShow = Value switch
                        {
                            null => true,
                            IList<object> m => m.Count == 0,
                            IList n => n.Count == 0,
                            _ => string.IsNullOrEmpty(Value.ToString())
                        };
                    }
                    else
                    {
                        isShow = Condition == (Value != null ? Value.ToString() : "");
                    }
                }
                IsVisible = isShow;
            }
            catch (Exception ex)
            {
                IsVisible = false;
            }
        }
    }
}
