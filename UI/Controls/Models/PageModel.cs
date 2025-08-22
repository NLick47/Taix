using Avalonia.Controls;

namespace UI.Controls.Models;

public class PageModel
{
    /// 页面实例
    /// </summary>
    public UserControl Instance { get; set; }

    /// <summary>
    ///     滚动条位置
    /// </summary>
    public double ScrollValue { get; set; }
}