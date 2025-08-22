using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using UI.Controls.Button;

namespace UI.Controls.Expander;

public class Expander : Avalonia.Controls.Expander
{
    //  内容部分容器边框,用于设置下移动画
    private Border ContentBorder_;

    //  内容容器高度隐藏容器,用于辅助显示隐藏动画
    private Canvas ContentHeightCanvas_;
    private StackPanel ContentStackPanel_;
    private IconButton ExpBtn_;
    private Border HeaderBorder_;
    public ICommand ExpanderCommand { get; set; }


    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ContentHeightCanvas_.SetValue(HeightProperty, ContentStackPanel_.Bounds.Height);
        ContentStackPanel_.SizeChanged += ContentStackPanel__SizeChanged;
    }

    private void ContentStackPanel__SizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnExpand(false);
    }

    private void OnExpanderCommand(object obj)
    {
        IsExpanded = !IsExpanded;
        OnExpand();
    }

    private void OnExpand(bool isAnimation = true)
    {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        HeaderBorder_ = e.NameScope.Find<Border>("HeaderBorder");
        ExpBtn_ = e.NameScope.Find<IconButton>("ExpBtn");
        ContentHeightCanvas_ = e.NameScope.Find<Canvas>("ContentHeight");
        ContentBorder_ = e.NameScope.Find<Border>("Content");
        ContentStackPanel_ = e.NameScope.Find<StackPanel>("ContentStackPanel");
    }
}