using Avalonia.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class DetailPage : UserControl
{
    public DetailPage()
    {
        InitializeComponent();
        this.DataContext = ServiceLocator.GetService<DetailPageViewModel>();
    }
}