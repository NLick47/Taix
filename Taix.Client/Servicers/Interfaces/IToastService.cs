using Taix.Client.Controls.Base;
using Taix.Client.Controls.Window;

namespace Taix.Client.Servicers.Interfaces;

public interface IToastService
{
    void Toast(string content, ToastType type = ToastType.Info, IconTypes icon = IconTypes.Accept);
    void Error(string message);
    void Info(string message);
    void Success(string message);
}
