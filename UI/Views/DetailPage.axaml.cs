using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class DetailPage : UserControl
{
    public DetailPage(DetailPageViewModel model)
    {
        InitializeComponent();
        this.DataContext = model;
    }
}