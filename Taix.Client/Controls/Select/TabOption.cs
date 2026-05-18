using System;
using Avalonia.Input;

namespace Taix.Client.Controls.Select;

public class TabOption : Option
{
    protected override Type StyleKeyOverride => typeof(TabOption);

    protected override void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
    }
}
