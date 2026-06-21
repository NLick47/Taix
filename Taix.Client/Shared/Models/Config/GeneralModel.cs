using Taix.Client.Platform.Abstractions.Primitives;

namespace Taix.Client.Shared.Models.Config;

/// <summary>
/// 常规
/// </summary>
public class GeneralModel
{
    [Config(Options = "跟随系统|浅色|深色", Name = "主题模式", Description = "设置以浅色或深色模式显示", Group = "界面",
        CultureCode = CultureCode.ZhCn)]
    [Config(Options = "Follow System|Light|Dark", Name = "Theme Mode",
        Description = "Set the display mode to light or dark", Group = "Interface", CultureCode = CultureCode.EnUs)]
    public int Theme { get; set; } = 0;

   
    [Config(Name = "主题颜色", Description = "", Group = "界面", CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Theme Color", Description = "", Group = "Interface", CultureCode = CultureCode.EnUs)]
    public string ThemeColor { get; set; } = "#FFFF1BBC";

    [Config(Options = "自动|中|English", Name = "语言", Description = "设置程序首选的显示语言", Group = "界面",
        OptionsChangedRefresh = true, CultureCode = CultureCode.ZhCn)]
    [Config(Options = "Auto|中|English", Name = "Language",
        Description = "Set the preferred display language for the program", Group = "Interface",
        OptionsChangedRefresh = true, CultureCode = CultureCode.EnUs)]
    public int Language { get; set; }

   
    [Config(Options = "概览|统计|详细|分类", Name = "启动页", Description = "设置打开主界面时显示的页面", Group = "基础",
        CultureCode = CultureCode.ZhCn)]
    [Config(Options = "Overview|Charts|Details|Category", Name = "Start Page",
        Description = "Set the page displayed when the main interface opens", Group = "Basics",
        CultureCode = CultureCode.EnUs)]
    public int StartPage { get; set; } = 0;

    [Config(Name = "自动检测更新", Description = "", Group = "基础", CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Automatic Update Detection", Description = "", Group = "Basics", ToggleFalseText = "Off",
        ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
    
    public bool IsAutoUpdate { get; set; } = true;

   
    [Config(Options = "1|2|3|4|5|6|7|8|9|10", Name = "最为频繁显示条数", Description = "", Group = "概览页",
        CultureCode = CultureCode.ZhCn)]
    [Config(Options = "1|2|3|4|5|6|7|8|9|10", Name = "Frequent Use Number", Description = "", Group = "Overview",
        CultureCode = CultureCode.EnUs)]
    public int IndexPageFrequentUseNum { get; set; } = 2;

   
    [Config(Options = "1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20", Name = "更多显示条数", Description = "",
        Group = "概览页", CultureCode = CultureCode.ZhCn)]
    [Config(Options = "1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20", Name = "More Number", Description = "",
        Group = "Overview", CultureCode = CultureCode.EnUs)]
    public int IndexPageMoreNum { get; set; } = 11;

   
    [Config(Name = "网站浏览统计",
        Description = "统计浏览器的网站访问数据，支持：Google Chrome、MSEdge或任何能够安装Chrome拓展的浏览器。请点击 “关于 > 浏览器统计插件” 了解如何安装和使用此功能。",
        Group = "功能", CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Web Browsing Statistics",
        Description =
            "Statistics for website visits in the browser, supporting: Google Chrome, MSEdge, or any browser that can install Chrome extensions. Click \"About > Browser Statistics Plugin\" to learn how to install and use this feature.",
        Group = "Features", ToggleFalseText = "Off", ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
    public bool IsWebEnabled { get; set; } = false;

    [Config(Name = "系统托盘", Description = "修改后需重启 taix-shell 进程生效", Group = "界面", CultureCode = CultureCode.ZhCn)]
    [Config(Name = "System Tray", Description = "Requires restarting taix-shell process to take effect", Group = "Interface", ToggleFalseText = "Off", ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
    public bool IsEnableTray { get; set; } = true;

    [Config(Name = "保存窗口大小", Description = "重启软件时恢复上次窗口尺寸", Group = "界面", CultureCode = CultureCode.ZhCn)]
    [Config(Name = "Save Window Size", Description = "Restore the last window size when restarting", Group = "Interface", ToggleFalseText = "Off", ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
    public bool IsSaveWindowSize { get; set; } = false;

    public int DataRetentionDays { get; set; } = 31;

    public double WindowWidth { get; set; }

    public double WindowHeight { get; set; }

    [Config(Options = "禁用|渐变1|渐变2|渐变3", Name = "窗口渐变", Group = "界面", CultureCode = CultureCode.ZhCn)]
    [Config(Options = "Off|Gradient 1|Gradient 2|Gradient 3", Name = "Window Gradient", Group = "Interface", CultureCode = CultureCode.EnUs)]
    public int WindowGradientScheme { get; set; } = 3;

    public string SyncUrl { get; set; } = string.Empty;

    /// <summary>
    /// ChartPage 分类筛选器隐藏的分类 ID 列表（JSON 数组字符串）
    /// </summary>
    public string ChartHiddenCategoryIds { get; set; } = string.Empty;
}
