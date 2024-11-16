using Infrastructure.Servicers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XSystemInfrastructure : ISystemInfrastructure
    {
        public (string ostype, string version) GetOSVersionName()
        {
            return (string.Empty,string.Empty);
        }

        public bool SetStartup(bool startup = true)
        {
            return false;
        }
    }
}
