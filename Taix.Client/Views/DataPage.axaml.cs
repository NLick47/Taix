using Taix.Client.Controls;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class DataPage : TPage
{
    public DataPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<DataPageViewModel>();
    }
}