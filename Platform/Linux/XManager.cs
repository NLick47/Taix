﻿using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XManager : IWindowManager
    {
        public WindowInfo GetWindowInfo(nint handle_)
        {
            return WindowInfo.Empty;
        }
    }
}
