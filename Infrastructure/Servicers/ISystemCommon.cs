using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Servicers
{
    public interface  ISystemInfrastructure
    {
        public  bool SetStartup(bool startup = true);

        public (string ostype, string version) GetOSVersionName();
    }
}
