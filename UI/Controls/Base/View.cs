using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
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
        public static readonly StyledProperty<string> ValueProperty =
           AvaloniaProperty.Register<View, string>(nameof(Value));

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
                        if (Value == null)
                        {
                            isShow = false;
                        }
                        else
                        {
                            var data = Value as IEnumerable<object>;
                            if (data != null)
                            {
                                isShow = data.Count() > 0;
                            }
                            else
                            {
                                isShow = !string.IsNullOrEmpty(Value.ToString());
                            }
                        }
                    }
                    else if (Condition.IndexOf("empty") != -1)
                    {
                        if (Value == null)
                        {
                            isShow = true;
                        }
                        else
                        {
                            var data = Value as IEnumerable<object>;
                            if (data != null)
                            {
                                isShow = data.Count() == 0;
                            }
                            else
                            {
                                isShow = string.IsNullOrEmpty(Value.ToString());
                            }
                        }
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
