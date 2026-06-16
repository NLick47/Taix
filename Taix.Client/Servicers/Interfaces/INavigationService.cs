namespace Taix.Client.Servicers.Interfaces;

/// <summary>
/// 导航服务接口
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// 导航到指定页面
    /// </summary>
    /// <param name="pageName">页面名称</param>
    /// <param name="data">导航数据</param>
    void NavigateTo(string pageName, object? data = null);

    /// <summary>
    /// 返回上一页
    /// </summary>
    void GoBack();

    /// <summary>
    /// 当前是否为返回导航
    /// 用于区分新进入页面和返回页面，控制是否需要重新加载数据
    /// </summary>
    bool IsNavigatingBack { get; }

    /// <summary>
    /// 重置导航状态（将 IsNavigatingBack 设置为 false）
    /// </summary>
    void ResetNavigationState();
}
