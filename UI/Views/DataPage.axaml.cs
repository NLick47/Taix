using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class DataPage : UserControl
{
    public DataPage(DataPageViewModel dataPage)
    {
        InitializeComponent();
        DataContext = dataPage;
    }
}