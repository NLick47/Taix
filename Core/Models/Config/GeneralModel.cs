﻿using SharedLibrary.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Config
{
    /// <summary>
    /// 常规
    /// </summary>
    public class GeneralModel
    {
        [Config(Name = "开机自启动", Description = "在电脑启动时自动运行 Taix", Group = "基础",CultureCode = CultureCode.ZhCn)]
        [Config(Name = "StartAtBoot", Description = "Automatically run Taix when the computer starts", Group = "Basics", ToggleFalseText = "Off", ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
        /// <summary>
        /// 是否启用开机自启动
        /// </summary>
        public bool IsStartatboot { get; set; }



        /// <summary>
        /// 主题模式
        /// </summary>
        [Config(Options = "跟随系统|浅色|深色", Name = "主题模式", Description = "设置以浅色或深色模式显示", Group = "界面", CultureCode = CultureCode.ZhCn)]
        [Config(Options = "Follow System|Light|Dark", Name = "Theme Mode", Description = "Set the display mode to light or dark", Group = "Interface", CultureCode = CultureCode.EnUs)]
        public int Theme { get; set; } = 0;
        /// <summary>
        /// 主题颜色
        /// </summary>
        /// 
        [Config(Name = "主题颜色", Description = "", Group = "界面",CultureCode = CultureCode.ZhCn)]
        [Config(Name = "Theme Color", Description = "", Group = "Interface",CultureCode = CultureCode.EnUs)]
        public string ThemeColor { get; set; } = "#FFFF1BBC";
        /// <summary>
        /// 是否保留界面大小
        /// </summary>
        [Config(Name = "保留窗口大小", Description = "保留当前的窗口大小，而非使用系统默认尺寸", Group = "界面", CultureCode = CultureCode.ZhCn)]
        [Config(Name = "Save Window Size", Description = "Retain the current window size instead of using the system default dimensions", ToggleFalseText = "Off", ToggleTrueText = "On",Group = "Interface", CultureCode = CultureCode.EnUs)]
        public bool IsSaveWindowSize { get; set; } = false;

        [Config(Options = "自动|中|English",Name = "语言",Description = "设置程序首选的显示语言",Group = "界面",OptionsChangedRefresh = true,CultureCode = CultureCode.ZhCn)]
        [Config(Options = "Auto|中|English", Name = "Language",Description = "Set the preferred display language for the program", Group = "Interface",OptionsChangedRefresh = true, CultureCode = CultureCode.EnUs)]
        public int Language { get; set; }
        
        /// <summary>
        /// 窗口宽度
        /// </summary>
        public double WindowWidth { get; set; } = 815;
        /// <summary>
        /// 窗口高度
        /// </summary>
        public double WindowHeight { get; set; } = 585;

        /// <summary>
        /// 启动页选择
        /// </summary>
        [Config(Options = "概览|统计|详细|分类", Name = "启动页", Description = "设置打开主界面时显示的页面", Group = "基础", CultureCode = CultureCode.ZhCn)]
        [Config(Options = "Overview|Charts|Details|Category", Name = "Start Page", Description = "Set the page displayed when the main interface opens", Group = "Basics", CultureCode = CultureCode.EnUs)]
        public int StartPage { get; set; } = 0;
        [Config(Name = "启动软件时显示主界面", Description = "在开机自启动时此选项无效", Group = "基础", CultureCode = CultureCode.ZhCn)]
        [Config(Name = "This option is invalid when starting automatically at boot", Description = "Show Main Window at Startup", Group = "Basics",ToggleFalseText = "Off",ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
        /// <summary>
        /// 启动软件时显示主界面
        /// </summary>
        public bool IsStartupShowMainWindow { get; set; } = true;




        [Config(Name = "自动检测更新", Description = "", Group = "基础", CultureCode = CultureCode.ZhCn)]
        [Config(Name = "Automatic Update Detection", Description = "", Group = "Basics", ToggleFalseText = "Off", ToggleTrueText = "On", CultureCode = CultureCode.EnUs)]
        /// <summary>
        /// 是否启用开机自启动
        /// </summary>
        public bool IsAutoUpdate { get; set; } = true;
        /// <summary>
        /// 概览页最为频繁显示条数
        /// </summary>
        [Config(Options = "1|2|3|4|5|6|7|8|9|10", Name = "最为频繁显示条数", Description = "", Group = "概览页", CultureCode = CultureCode.ZhCn)]
        [Config(Options = "1|2|3|4|5|6|7|8|9|10", Name = "Frequent Use Number", Description = "", Group = "Overview", CultureCode = CultureCode.EnUs)]
        public int IndexPageFrequentUseNum { get; set; } = 2;
        /// <summary>
        /// 概览页最为频繁显示条数
        /// </summary>
        [Config(Options = "1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20", Name = "更多显示条数", Description = "", Group = "概览页",CultureCode = CultureCode.ZhCn)]
        [Config(Options = "1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20", Name = "More Number", Description = "", Group = "Overview",CultureCode = CultureCode.EnUs)]
        public int IndexPageMoreNum { get; set; } = 11;

        /// <summary>
        /// 是否启用网站记录功能
        /// </summary>
        /// 
        [Config(Name = "网站浏览统计", Description = "统计浏览器的网站访问数据，支持：Google Chrome、MSEdge或任何能够安装Chrome拓展的浏览器。请点击 “关于 > 浏览器统计插件” 了解如何安装和使用此功能。", Group = "功能", CultureCode = CultureCode.ZhCn)]
        [Config(Name = "Web Browsing Statistics", Description = "Statistics for website visits in the browser, supporting: Google Chrome, MSEdge, or any browser that can install Chrome extensions. Click \"About > Browser Statistics Plugin\" to learn how to install and use this feature.", Group = "Features", ToggleFalseText = "Off", ToggleTrueText = "On",CultureCode = CultureCode.EnUs)]
        public bool IsWebEnabled { get; set; } = false;
    }
}