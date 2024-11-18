using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Models
{
    public class PageModel
    {
        /// 页面实例
        /// </summary>
        public UserControl Instance { get; set; }
        /// <summary>
        /// 滚动条位置
        /// </summary>
        public double ScrollValue { get; set; }
    }
}
