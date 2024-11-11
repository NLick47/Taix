using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Window
{
    public class HideWindow : Avalonia.Controls.Window
    {
        public HideWindow()
        {
            
        }

        protected override Type StyleKeyOverride => typeof(HideWindow);
    }
}
