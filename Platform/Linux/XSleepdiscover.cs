using SharedLibrary.Event;
using SharedLibrary.Servicers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XSleepdiscover : ISleepdiscover
    {
        public event SleepdiscoverEventHandler SleepStatusChanged;

        public void Start()
        {
           
        }

        public void Stop()
        {
            
        }
    }
}
