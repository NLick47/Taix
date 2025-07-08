using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Servicers
{
    public interface IMainServicer
    {
        Task Start(bool isSelfStart);
        
        void DesignStart();
    }
}
