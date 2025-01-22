using SharedLibrary.Models.AppObserver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Servicers
{
    public interface IWindowManager
    {
        /// <summary>
        /// 获取窗口信息
        /// </summary>
        /// <param name="handle_">窗口句柄</param>
        /// <returns></returns>
        WindowInfo GetWindowInfo(IntPtr handle_);
    }
}
