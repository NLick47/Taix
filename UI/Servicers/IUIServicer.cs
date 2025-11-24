using System;
using System.Threading.Tasks;

namespace UI.Servicers;

public interface IUIServicer
{
    Task<bool> ShowConfirmDialogAsync(string title_, string message_);

    Task<string?> ShowInputModalAsync(string title_, string placeholder_, string value_ = null,
        Func<string, bool> validFnc_ = null);
}