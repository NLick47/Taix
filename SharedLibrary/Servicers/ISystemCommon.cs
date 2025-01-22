﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Servicers
{
    public interface  ISystemInfrastructure
    {
        public  bool SetAutoStartInRegistry();

        public (string ostype, string version) GetOSVersionName();
    }
}
