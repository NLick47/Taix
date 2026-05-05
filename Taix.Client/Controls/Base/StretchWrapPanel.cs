using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Taix.Client.Controls.Base;

/// <summary>
/// 一种特殊的 WrapPanel：第一个可见子元素（或被标记为 IsStretchChild 的元素）会拉伸填满剩余空间（但不超过 MaxWidth），
/// 其余子元素按 WrapPanel 方式排列。当剩余空间足以容纳时，侧边元素会与主元素并排；否则自动换行。
/// </summary>
public class StretchWrapPanel : Panel
{
    public static readonly AttachedProperty<bool> IsStretchChildProperty =
        AvaloniaProperty.RegisterAttached<StretchWrapPanel, Control, bool>("IsStretchChild", false);

    public static bool GetIsStretchChild(Control element) => element.GetValue(IsStretchChildProperty);

    public static void SetIsStretchChild(Control element, bool value) => element.SetValue(IsStretchChildProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var visibleChildren = GetVisibleChildren();
        if (visibleChildren.Count == 0)
            return new Size(0, 0);

        var stretchChild = GetStretchChild(visibleChildren);
        var sideChildren = GetSideChildren(visibleChildren, stretchChild);

        double availableWidth = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;
        double availableHeight = double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height;

        // 先测量所有 sideChildren
        foreach (var child in sideChildren)
            child.Measure(new Size(availableWidth, availableHeight));

        var stretchMargin = stretchChild.Margin;
        double stretchMinWidth = stretchChild.MinWidth;
        double stretchMaxWidth = stretchChild.MaxWidth;

        if (double.IsInfinity(stretchMaxWidth) || double.IsNaN(stretchMaxWidth))
            stretchMaxWidth = availableWidth;
        if (double.IsInfinity(stretchMinWidth) || double.IsNaN(stretchMinWidth))
            stretchMinWidth = 0;

        // 计算 sideChildren 在完整宽度下的行分布
        var sideLinesFull = MeasureSideLines(sideChildren, availableWidth);
        double sideFirstLineWidthFull = sideLinesFull.Count > 0 ? sideLinesFull[0].Width : 0;

        bool sameRow = false;
        List<LineInfo> sideLines = sideLinesFull;

        // 尝试主元素和第一行 sideChildren 并排
        if (availableWidth >= stretchMinWidth + stretchMargin.Left + stretchMargin.Right + sideFirstLineWidthFull)
        {
            double candidateWidth = Math.Min(availableWidth - sideFirstLineWidthFull, stretchMaxWidth);
            candidateWidth = Math.Max(candidateWidth, stretchMinWidth);
            stretchChild.Measure(new Size(Math.Max(0, candidateWidth), availableHeight));

            double candidateTotalWidth = stretchChild.DesiredSize.Width + stretchMargin.Left + stretchMargin.Right;
            double remainingWidth = availableWidth - candidateTotalWidth;
            var sideLinesRemaining = MeasureSideLines(sideChildren, remainingWidth);
            double actualSideFirstLineWidth = sideLinesRemaining.Count > 0 ? sideLinesRemaining[0].Width : 0;

            sameRow = candidateTotalWidth + actualSideFirstLineWidth <= availableWidth;
            if (sameRow)
                sideLines = sideLinesRemaining;
        }

        if (!sameRow)
        {
            // 独占一行：重新测量主元素
            double stretchWidth = Math.Min(availableWidth, stretchMaxWidth);
            stretchWidth = Math.Max(stretchWidth, stretchMinWidth);
            stretchChild.Measure(new Size(Math.Max(0, stretchWidth), availableHeight));
        }

        double stretchTotalWidth = stretchChild.DesiredSize.Width + stretchMargin.Left + stretchMargin.Right;
        double stretchTotalHeight = stretchChild.DesiredSize.Height + stretchMargin.Top + stretchMargin.Bottom;

        if (sameRow)
        {
            double totalWidth = stretchTotalWidth + (sideLines.Count > 0 ? sideLines[0].Width : 0);
            double remainingSideHeight = sideLines.Count > 1 ? GetTotalLineHeight(sideLines, 1) : 0;
            double totalHeight = Math.Max(stretchTotalHeight, sideLines.Count > 0 ? sideLines[0].Height : 0) + remainingSideHeight;
            return new Size(totalWidth, totalHeight);
        }
        else
        {
            double sideTotalWidth = sideLines.Count > 0 ? GetMaxLineWidth(sideLines) : 0;
            double sideTotalHeight = GetTotalLineHeight(sideLines);
            double totalWidth = Math.Max(stretchTotalWidth, sideTotalWidth);
            double totalHeight = stretchTotalHeight + sideTotalHeight;
            return new Size(totalWidth, totalHeight);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visibleChildren = GetVisibleChildren();
        if (visibleChildren.Count == 0)
            return finalSize;

        var stretchChild = GetStretchChild(visibleChildren);
        var sideChildren = GetSideChildren(visibleChildren, stretchChild);

        var stretchMargin = stretchChild.Margin;
        double stretchMinWidth = stretchChild.MinWidth;
        double stretchMaxWidth = stretchChild.MaxWidth;

        if (double.IsInfinity(stretchMaxWidth) || double.IsNaN(stretchMaxWidth))
            stretchMaxWidth = finalSize.Width;
        if (double.IsInfinity(stretchMinWidth) || double.IsNaN(stretchMinWidth))
            stretchMinWidth = 0;

        // 计算 sideChildren 在完整宽度及剩余空间下的行分布（与 MeasureOverride 保持一致的 sameRow 判断逻辑）
        double candidateTotalWidth = stretchChild.DesiredSize.Width + stretchMargin.Left + stretchMargin.Right;

        // sideChildren 在完整宽度下的行分布（用于最小宽度预筛选）
        var sideLinesFull = MeasureSideLines(sideChildren, finalSize.Width);
        double sideFirstLineWidthFull = sideLinesFull.Count > 0 ? sideLinesFull[0].Width : 0;

        // sideChildren 在剩余空间下的行分布（用于 sameRow 最终判断）
        double remainingWidth = Math.Max(0, finalSize.Width - candidateTotalWidth);
        var sideLinesRemaining = MeasureSideLines(sideChildren, remainingWidth);
        double sideFirstLineWidthRemaining = sideLinesRemaining.Count > 0 ? sideLinesRemaining[0].Width : 0;

        // 只有当总宽度足以容纳 stretchMinWidth 的主元素和第一行 sideChildren 时才尝试并排
        bool sameRow = false;
        if (finalSize.Width >= stretchMinWidth + stretchMargin.Left + stretchMargin.Right + sideFirstLineWidthFull)
        {
            sameRow = candidateTotalWidth + sideFirstLineWidthRemaining <= finalSize.Width;
        }

        // 计算主元素的最终宽度
        double stretchFinalWidth;
        if (sameRow)
        {
            stretchFinalWidth = finalSize.Width - sideFirstLineWidthRemaining - stretchMargin.Left - stretchMargin.Right;
            stretchFinalWidth = Math.Min(stretchFinalWidth, stretchMaxWidth);
            stretchFinalWidth = Math.Max(stretchFinalWidth, stretchMinWidth);
        }
        else
        {
            stretchFinalWidth = finalSize.Width - stretchMargin.Left - stretchMargin.Right;
            stretchFinalWidth = Math.Min(stretchFinalWidth, stretchMaxWidth);
            stretchFinalWidth = Math.Max(stretchFinalWidth, stretchMinWidth);
        }
        stretchFinalWidth = Math.Max(stretchFinalWidth, 0);

        // 排列主元素
        stretchChild.Arrange(new Rect(
            stretchMargin.Left,
            stretchMargin.Top,
            stretchFinalWidth,
            stretchChild.DesiredSize.Height));

        double stretchChildTotalWidth = stretchFinalWidth + stretchMargin.Left + stretchMargin.Right;
        double stretchChildTotalHeight = stretchChild.DesiredSize.Height + stretchMargin.Top + stretchMargin.Bottom;

        // 在剩余空间下重新计算 sideChildren 的行分布
        double sideAvailableWidth = sameRow ? Math.Max(0, finalSize.Width - stretchChildTotalWidth) : finalSize.Width;
        var sideLines = MeasureSideLines(sideChildren, sideAvailableWidth);

        // 排列 sideChildren
        double x = sameRow ? stretchChildTotalWidth : 0;
        double y = sameRow ? 0 : stretchChildTotalHeight;
        bool isFirstWrapLine = sameRow;
        bool isFirstSideLine = true;

        foreach (var line in sideLines)
        {
            // 首次换行时（从主元素旁边换到下方），y 必须跳到主元素底部
            if (isFirstWrapLine && x == 0)
            {
                y = Math.Max(y, stretchChildTotalHeight);
                isFirstWrapLine = false;
            }

            double lineHeight = line.Height;
            // sameRow 时第一行 sideChildren 要与主元素底部对齐
            if (sameRow && isFirstSideLine)
            {
                lineHeight = Math.Max(lineHeight, stretchChildTotalHeight);
                isFirstSideLine = false;
            }

            foreach (var child in line.Children)
            {
                var margin = child.Margin;
                double arrangeHeight = lineHeight - margin.Top - margin.Bottom;
                arrangeHeight = Math.Max(arrangeHeight, 0);

                child.Arrange(new Rect(
                    x + margin.Left,
                    y + margin.Top,
                    child.DesiredSize.Width,
                    arrangeHeight));

                x += child.DesiredSize.Width + margin.Left + margin.Right;
            }

            x = 0;
            y += lineHeight;
        }

        return finalSize;
    }

    private List<Control> GetVisibleChildren()
    {
        var result = new List<Control>();
        foreach (var child in Children)
        {
            // 只使用 IsVisible 判断，避免与 View 控件的 Opacity 动画产生竞态条件
            // （View 显示时 IsVisible=true 后 Opacity 从 0 开始动画，
            //  若在动画初期检查 Opacity > 0.01，会导致该元素被错误地排除在布局外）
            if (child.IsVisible)
                result.Add(child);
        }
        return result;
    }

    private static Control GetStretchChild(List<Control> visibleChildren)
    {
        foreach (var child in visibleChildren)
        {
            if (GetIsStretchChild(child))
                return child;
        }
        return visibleChildren[0];
    }

    private static List<Control> GetSideChildren(List<Control> visibleChildren, Control stretchChild)
    {
        var result = new List<Control>(Math.Max(0, visibleChildren.Count - 1));
        foreach (var child in visibleChildren)
        {
            if (child != stretchChild)
                result.Add(child);
        }
        return result;
    }

    private readonly struct LineInfo
    {
        public IReadOnlyList<Control> Children { get; }
        public double Width { get; }
        public double Height { get; }

        public LineInfo(IReadOnlyList<Control> children, double width, double height)
        {
            Children = children;
            Width = width;
            Height = height;
        }
    }

    private static List<LineInfo> MeasureSideLines(List<Control> sideChildren, double availableWidth)
    {
        var lines = new List<LineInfo>();
        var currentLine = new List<Control>();
        double currentLineWidth = 0;
        double currentLineHeight = 0;

        foreach (var child in sideChildren)
        {
            var margin = child.Margin;
            double childWidth = child.DesiredSize.Width + margin.Left + margin.Right;
            double childHeight = child.DesiredSize.Height + margin.Top + margin.Bottom;

            if (currentLineWidth + childWidth > availableWidth && currentLineWidth > 0)
            {
                lines.Add(new LineInfo(currentLine, currentLineWidth, currentLineHeight));
                currentLine = new List<Control>();
                currentLineWidth = 0;
                currentLineHeight = 0;
            }

            currentLine.Add(child);
            currentLineWidth += childWidth;
            currentLineHeight = Math.Max(currentLineHeight, childHeight);
        }

        if (currentLine.Count > 0 || lines.Count == 0)
        {
            lines.Add(new LineInfo(currentLine, currentLineWidth, currentLineHeight));
        }

        return lines;
    }

    private static double GetMaxLineWidth(List<LineInfo> lines)
    {
        double max = 0;
        foreach (var line in lines)
        {
            if (line.Width > max)
                max = line.Width;
        }
        return max;
    }

    private static double GetTotalLineHeight(List<LineInfo> lines, int startIndex = 0)
    {
        double total = 0;
        for (int i = startIndex; i < lines.Count; i++)
            total += lines[i].Height;
        return total;
    }
}
