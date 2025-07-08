using Avalonia.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class SettingPage : UserControl
{
    public SettingPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<SettingPageViewModel>();
    }
}