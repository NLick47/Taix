using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Servicers.Dialogs
{
    public class DialogResult : IDialogResult
    {
        public IDictionary<object, object?> Parameters { get; private set; } = new Dictionary<object,object?>();


        public ButtonResult Result { get; private set; }

        public DialogResult()
        {
        }

        public DialogResult(ButtonResult result)
        {
            Result = result;
        }

        public DialogResult(ButtonResult result, IDictionary<object, object?> parameters)
        {
            Result = result;
            Parameters = parameters;
        }
    }
}
