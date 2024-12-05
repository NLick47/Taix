using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Servicers.Dialogs
{
    public interface IDialogResult
    {
        IDictionary<object,object?> Parameters { get; }

        ButtonResult Result { get; }
    }
}
