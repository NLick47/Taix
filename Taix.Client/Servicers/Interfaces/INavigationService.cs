namespace Taix.Client.Servicers.Interfaces;

public interface INavigationService
{
    void NavigateTo(string pageName, object? data = null);
    void GoBack();
}
