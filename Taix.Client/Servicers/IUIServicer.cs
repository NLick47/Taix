using System;
using System.Threading.Tasks;

namespace Taix.Client.Servicers;

public interface IUIServicer
{
    Task<bool> ShowConfirmDialogAsync(string title, string message);

    Task<string?> ShowInputModalAsync(string title, string placeholder, string value = null,
        Func<string, bool> validate = null);
}