using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UI.Controls.Base;

public class View : ContentControl
{
    public static readonly DirectProperty<View, string> ConditionProperty =
        AvaloniaProperty.RegisterDirect<View, string>(
            nameof(Condition),
            o => o.Condition,
            (o, v) => o.Condition = v);

    public static readonly DirectProperty<View, object> ValueProperty =
        AvaloniaProperty.RegisterDirect<View, object>(
            nameof(Value),
            o => o.Value,
            (o, v) => o.Value = v);

    private string _condition;

    private object _value;

    public View()
    {
        Loaded += View_Loaded;
        Unloaded += View_Unloaded;
    }

    public string Condition
    {
        get => _condition;
        set => SetAndRaise(ConditionProperty, ref _condition, value);
    }

    public object Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }

    protected override Type StyleKeyOverride => typeof(View);

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
            if (string.IsNullOrEmpty(Condition) && Value == null) return;

            var isShow = false;
            if (string.IsNullOrEmpty(Condition))
            {
                if ((bool)Value) isShow = true;
            }
            else
            {
                if (Condition.IndexOf("=") != -1)
                {
                    var conditionVal = Condition.Substring(Condition.IndexOf("=") + 1);
                    var value = Value == null ? string.Empty : Value.ToString();
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