using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class DataPage : TPage
{
    public DataPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<DataPageViewModel>();
    }
}