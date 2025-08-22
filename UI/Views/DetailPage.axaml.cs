using Avalonia.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class DetailPage : UserControl
{
    public DetailPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetService<DetailPageViewModel>();
    }
}