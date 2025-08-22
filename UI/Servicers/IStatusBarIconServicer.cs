using System.Threading.Tasks;

namespace UI.Servicers;

public interface IStatusBarIconServicer
{
    /// <summary>
    ///     初始化状态栏图标
    /// </summary>
    Task Init();

    /// <summary>
    ///     显示主窗口
    /// </summary>
    void ShowMainWindow();
}