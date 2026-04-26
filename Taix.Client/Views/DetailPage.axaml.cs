using Avalonia.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class DetailPage : UserControl
{
    public DetailPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetService<DetailPageViewModel>();
    }
}