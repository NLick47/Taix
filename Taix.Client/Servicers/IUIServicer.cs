using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Taix.Client.Servicers;

/// <summary>
/// 分类类型枚举
/// </summary>
public enum CategoryType
{
    App,
    Website
}

/// <summary>
/// 新建分类结果
/// </summary>
/// <param name="Name">分类名称</param>
/// <param name="IconFile">图标路径</param>
/// <param name="Color">颜色</param>
public record CategoryResult(string Name, string? IconFile, string? Color);

/// <summary>
/// 输入验证结果
/// </summary>
/// <param name="IsValid">是否有效</param>
/// <param name="ErrorMessage">错误消息（无效时）</param>
public record ValidationResult(bool IsValid, string? ErrorMessage = null);

public interface IUIServicer
{
    Task<bool> ShowConfirmDialogAsync(string title, string message);

    Task<string?> ShowInputModalAsync(string title, string placeholder, string value = null,
        Func<string, bool> validate = null);

    Task<int> ShowActionDialogAsync(string title, string message, string[] buttons);

    /// <summary>
    /// 显示新建分类对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="defaultName">默认分类名称</param>
    /// <param name="existingNames">已存在的分类名称列表（用于验证唯一性）</param>
    /// <param name="existingColors">已存在的颜色列表（用于验证唯一性）</param>
    /// <returns>分类结果，取消返回 null</returns>
    Task<CategoryResult?> ShowCreateCategoryDialogAsync(
        string title,
        string defaultName = null,
        IEnumerable<string>? existingNames = null,
        IEnumerable<string>? existingColors = null);
}