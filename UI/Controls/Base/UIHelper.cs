using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace UI.Controls.Base;

public static class UIHelper
{
    /// <summary>
    ///     测量字符串控件尺寸
    /// </summary>
    /// <param name="textBlock"></param>
    /// <returns></returns>
    public static Size MeasureString(TextBlock textBlock)
    {
        var formattedText = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
            textBlock.FontSize,
            Brushes.Black);

        return new Size(formattedText.Width, formattedText.Height);
    }
}