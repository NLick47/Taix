using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.VisualTree;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class SettingPage : UserControl
{
    private IDisposable? _heightBinding;
    private ScrollViewer? _outerScrollViewer;

    public SettingPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<SettingPageViewModel>();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _outerScrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (_outerScrollViewer != null)
        {
            _outerScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            var binding = new Binding
            {
                Source = _outerScrollViewer,
                Path = "Viewport.Height"
            };
            _heightBinding = this.Bind(HeightProperty, binding);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _heightBinding?.Dispose();
        _heightBinding = null;

        if (_outerScrollViewer != null)
        {
            _outerScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _outerScrollViewer = null;
        }
    }
}
