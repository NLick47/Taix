using System.Collections.Generic;
using Taix.Client.Platform.Abstractions.Primitives;

namespace Taix.Client.Shared.Models.Config;

/// <summary>
/// 行为
/// </summary>
public class BehaviorModel
{
    [Config(Name = "睡眠监测", Description = "离开电脑时自动停止统计，重启软件生效。（一般情况不建议关闭）", Group = "偏好",
        CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Sleep Monitoring",
        Description =
            "Automatically stops statistics when you leave the computer, takes effect after restarting the software. (Not recommended to disable in general cases)",
        Group = "Preference", ToggleFalseText = "Off", ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
    public bool IsSleepWatch { get; set; } = true;

    [Config(IsCanImportExport = true, Name = "忽略应用", Description = "可以通过进程名称或者通配符进行匹配。* 匹配任意字符，? 匹配单个字符",
        Group = "忽略应用", Placeholder = "进程名称，不需要输入.exe。支持通配符", CultureCode = CultureCode.ZhCn)]
    [Config(IsCanImportExport = true, Name = "Ignore Application",
        Description =
            "Can be matched by process name or wildcard. * matches any characters, ? matches single character",
        Group = "Ignore Application", Placeholder = "Process name, no need to enter .exe. Supports wildcard",
        CultureCode = CultureCode.EnUs)]

    public List<string> IgnoreProcessList { get; set; } = new();

    [Config(IsCanImportExport = true, Name = "忽略URL", Description = "过滤不需要统计的网站或链接", Group = "忽略URL",
        Placeholder = "URL 支持通配符，如 *.bilibili.com", CultureCode = CultureCode.ZhCn)]
    [Config(IsCanImportExport = true, Name = "Ignore URL",
        Description = "Filter websites or links that do not need to be statistics", Group = "Ignore URL",
        Placeholder = "URL supports wildcard, e.g. *.bilibili.com", CultureCode = CultureCode.EnUs)]

    public List<string> IgnoreUrlList { get; set; } = new();

    [Config(Name = "应用白名单", Description = "仅统计白名单内的应用", Group = "应用白名单", CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Application Whitelist", Description = "Only statistics applications within the whitelist",
        Group = "Application Whitelist", ToggleFalseText = "Off", ToggleTrueText = "On",
        CultureCode = CultureCode.EnUs)]
    public bool IsWhiteList { get; set; } = false;

    [Config(IsCanImportExport = true, Name = "应用白名单", Description = "可以通过进程名称或者通配符进行匹配。* 匹配任意字符，? 匹配单个字符",
        Group = "应用白名单", Placeholder = "进程名称，不需要输入.exe。支持通配符", CultureCode = CultureCode.ZhCn)]
    [Config(IsCanImportExport = true, Name = "Application Whitelist",
        Description =
            "Can be matched by process name or wildcard. * matches any characters, ? matches single character",
        Group = "Application Whitelist",
        Placeholder = "Process name, no need to enter .exe. Supports wildcard",
        CultureCode = CultureCode.EnUs)]
    public List<string> ProcessWhiteList { get; set; } = new();

    [Config(Name = "空闲阈值", Description = "系统无操作多久后自动暂停统计，单位：分钟。重启 taix-shell 生效", Group = "偏好",
        Min = 1, Max = 60, Step = 1, CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Inactive Threshold",
        Description = "How long before idle system pauses tracking, unit: minutes. Restart taix-shell to apply",
        Group = "Preference", Min = 1, Max = 60, Step = 1, CultureCode = CultureCode.EnUs)]
    public int InactiveThreshold { get; set; } = 15;

    [Config(Name = "声音持续时间", Description = "播放声音时保持活跃的最长时间，超过后进入休眠，单位：分钟。重启 taix-shell 生效", Group = "偏好",
        Min = 15, Max = 480, Step = 15, CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Sound Duration",
        Description = "Max time to stay active while sound is playing, then sleep, unit: minutes. Restart taix-shell to apply",
        Group = "Preference", Min = 15, Max = 480, Step = 15, CultureCode = CultureCode.EnUs)]
    public int MaxSoundDuration { get; set; } = 120;
}
