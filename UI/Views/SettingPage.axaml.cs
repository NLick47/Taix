using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class SettingPage : UserControl
{
    public SettingPage(SettingPageViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}