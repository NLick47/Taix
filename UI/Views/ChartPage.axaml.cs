using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class ChartPage : UserControl
{
    public ChartPage(ChartPageViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}