namespace Taix.Client.Servicers;

public interface IStatusBarIconServicer
{
    /// <summary>
    /// 初始化状态栏图标
    /// </summary>
    void Init();

    /// <summary>
    /// 显示主窗口
    /// </summary>
    void ShowMainWindow();
}