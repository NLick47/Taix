using System.Collections.Generic;

namespace UI.Servicers.Dialogs;

public interface IDialogResult
{
    IDictionary<object, object?> Parameters { get; }

    ButtonResult Result { get; }
}