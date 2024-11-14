using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Servicers
{
    public interface IStatusBarIconServicer
    {
        /// <summary>
        /// 初始化状态栏图标
        /// </summary>
        Task Init();
    }
}
