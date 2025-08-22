using System.Collections.Generic;

namespace UI.Servicers.Dialogs;

public class DialogResult : IDialogResult
{
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

    public IDictionary<object, object?> Parameters { get; } = new Dictionary<object, object?>();


    public ButtonResult Result { get; }
}