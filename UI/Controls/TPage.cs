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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 清理事件订阅，防止内存泄漏
        if (pageContainer != null)
        {
            pageContainer.SizeChanged -= Pc_SizeChanged;
            pageContainer = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void HandleEvent()
    {
        // 清理之前的事件订阅
        if (pageContainer != null)
        {
            pageContainer.SizeChanged -= Pc_SizeChanged;
            pageContainer = null;
        }

        if (!IsFillPage) return;

        // 查找 PageContainer 父级
        var current = this.GetVisualParent();
        while (current != null && current is not PageContainer)
        {
            current = current.GetVisualParent();
        }

        pageContainer = current as PageContainer;
        if (pageContainer != null)
        {
            pageContainer.SizeChanged += Pc_SizeChanged;
            UpdatePageSize(); // 立即更新一次尺寸
        }
    }

    private void Pc_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePageSize();
    }

    private void UpdatePageSize()
    {
        if (pageContainer == null) return;
        
        Width = pageContainer.Bounds.Width;
        Height = pageContainer.Bounds.Height;

        Debug.WriteLine("UpdatePageSize");
    }
}