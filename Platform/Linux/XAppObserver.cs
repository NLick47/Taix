using SharedLibrary.Servicers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XAppObserver : IAppObserver
    {
        public event AppObserverEventHandler OnAppActiveChanged;

        public void Start()
        {
            
        }

        public void Stop()
        {
            
        }
    }
}
