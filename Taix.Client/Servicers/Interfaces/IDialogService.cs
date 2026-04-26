using System;
using System.Threading.Tasks;

namespace Taix.Client.Servicers.Interfaces;

public interface IDialogService
{
    Task<bool> ShowConfirmDialogAsync(string title, string message);
    Task<string?> ShowInputModalAsync(string title, string placeholder, string value = null, Func<string, bool>? validate = null);
    Task<string?> ShowFolderPickerAsync(string? title = null);
}
