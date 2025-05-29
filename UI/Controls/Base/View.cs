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
        private string _condition;
        public string Condition
        {
            get => _condition;
            set => SetAndRaise(ConditionProperty, ref _condition, value);
        }
        public static readonly DirectProperty<View, string> ConditionProperty =
            AvaloniaProperty.RegisterDirect<View, string>(
                nameof(Condition),
                o => o.Condition,
                (o, v) => o.Condition = v);

        private object _value;
        public object Value
        {
            get => _value;
            set => SetAndRaise(ValueProperty, ref _value, value);
        }
        public static readonly DirectProperty<View, object> ValueProperty =
            AvaloniaProperty.RegisterDirect<View, object>(
                nameof(Value),
                o => o.Value,
                (o, v) => o.Value = v);

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
