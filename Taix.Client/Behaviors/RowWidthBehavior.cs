using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace Taix.Client.Behaviors;

public class RowWidthBehavior : Behavior<WrapPanel>
{
    public static readonly StyledProperty<double> RowWidthProperty =
        AvaloniaProperty.Register<RowWidthBehavior, double>(nameof(RowWidth));

    public double RowWidth
    {
        get => GetValue(RowWidthProperty);
        set => SetValue(RowWidthProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject!.SizeChanged += OnSizeChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject?.SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        => RowWidth = e.NewSize.Width;
}
