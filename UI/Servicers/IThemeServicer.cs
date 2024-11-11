using Avalonia.Controls;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Views;

namespace UI.Servicers
{
    public interface IThemeServicer
    {
        void Init();
        void LoadTheme(string themeName, bool isRefresh = false);
        void SetMainWindow(MainWindow mainWindow);

        /// <summary>
        /// 切换主题时发生
        /// </summary>
        event EventHandler OnThemeChanged;
    }
}
