
using Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Event
{
    public delegate void SleepdiscoverEventHandler(SleepStatus sleepStatus);
    public delegate void DateTimeObserverEventHandler(object sender, DateTime e);
}
