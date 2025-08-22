using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace UI.Controls.Base;

public class Placeholder : TemplatedControl
{
    private Border Flash;
    private bool IsAddEvent;

    protected override Type StyleKeyOverride => typeof(Placeholder);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Flash = e.NameScope.Get<Border>("Flash");
        if (!IsAddEvent) Loaded += Placeholder_Loaded;
    }

    private void Placeholder_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= Placeholder_Loaded;
        IsAddEvent = true;
    }
}