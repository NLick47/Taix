using SharedLibrary.Models.AppObserver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Event
{
    public delegate void ObserverEventHandler(AppObserverEventArgs args);
}
