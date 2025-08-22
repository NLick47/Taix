using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace UI.Controls;

public class TPage : UserControl
{
    public static readonly StyledProperty<bool> IsFillPageProperty =
        AvaloniaProperty.Register<TPage, bool>(nameof(IsFillPage));

    private PageContainer pageContainer;

    /// <summary>
    ///     是否填充页面而不使用自适应滚动
    /// </summary>
    public bool IsFillPage
    {
        get => GetValue(IsFillPageProperty);
        set => SetValue(IsFillPageProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TPage);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdatePageSize();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        HandleEvent();
    }

    private void HandleEvent()
    {
        if (pageContainer != null) pageContainer.SizeChanged -= Pc_SizeChanged;

        if (IsFillPage)
        {
            //  查找父容器
            var parent = this.GetVisualParent();

            if (parent != null)
                while (!(parent is PageContainer))
                    parent = parent.GetVisualParent();

            pageContainer = parent as PageContainer;
            if (pageContainer != null) pageContainer.SizeChanged += Pc_SizeChanged;
        }
    }

    private void Pc_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePageSize();
    }

    private void UpdatePageSize()
    {
        Width = pageContainer.Bounds.Width;
        Height = pageContainer.Bounds.Height;

        Debug.WriteLine("UpdatePageSize");
    }
}