namespace Taix.Client.Shared.Models.Config;

public class ShortcutModel
{
    public string Refresh { get; set; } = "F5";
    public string Search { get; set; } = "Ctrl+K";

    // 鼠标后退键始终额外触发，与此配置无关
    public string NavigateBack { get; set; } = "Alt+Left";
}
