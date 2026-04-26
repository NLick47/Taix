using Avalonia.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class SettingPage : UserControl
{
    public SettingPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<SettingPageViewModel>();
    }
}