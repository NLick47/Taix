using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.Controls;
using UI.ViewModels;

namespace UI.Views;

public partial class DataPage : TPage
{
    public DataPage(DataPageViewModel dataPage)
    {
        InitializeComponent();
        DataContext = dataPage;
    }
}