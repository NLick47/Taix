using System;
using Taix.Client.Platform.Abstractions.Primitives;

namespace Taix.Client.Shared.Models.Config;

[AttributeUsage(AttributeTargets.Class |
                AttributeTargets.Constructor |
                AttributeTargets.Field |
                AttributeTargets.Method |
                AttributeTargets.Property, AllowMultiple = true)]
public class ConfigAttribute : Attribute
{
    public CultureCode CultureCode = CultureCode.ZhCn;
    public string Description;
    public string Group;
    public int Index;
    public bool IsBeta;
    public bool IsCanImportExport;
    public bool IsCanRepeat;
    public bool IsName;

    public string Name;
    public string Options;
    public bool OptionsChangedRefresh = false;
    public string Placeholder;
    public string ToggleFalseText;
    public string ToggleTrueText;

    /// <summary>NumericUpDown 最小值（仅 int 类型 Options 为空时生效）</summary>
    public int Min;
    /// <summary>NumericUpDown 最大值</summary>
    public int Max;
    /// <summary>NumericUpDown 步进值</summary>
    public int Step = 1;

    public ConfigAttribute()
    {
        ToggleTrueText ??= "开";
        ToggleFalseText ??= "关";
        IsCanRepeat = true;
        Index = 0;
        IsName = false;
        IsCanImportExport = false;
        Options = string.Empty;
        IsBeta = false;
    }
}
