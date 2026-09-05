using System.Text.Json.Serialization;

namespace Taix.Client.Shared.Models.Config;

public enum WindowStateKind
{
    Normal = 0,
    Maximized = 1,
    FullScreen = 2,
}

public class WindowStateModel
{
    public bool HasValue { get; set; }

    public WindowStateKind State { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    // 以下三个是旧版本遗留字段：只在读取历史缓存时用于一次性迁移，写入时不再序列化

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public double WindowWidth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public double WindowHeight { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public bool? IsMaximized { get; set; }
}
