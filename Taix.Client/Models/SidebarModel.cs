using Taix.Client.ViewModels;

namespace Taix.Client.Models;

public class SidebarModel
{
    public string Title { get; set; }

    public string IconPath { get; set; }

    public ViewModelBase NavigationViewModel { get; set; }
}