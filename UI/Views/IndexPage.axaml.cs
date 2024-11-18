using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UI.ViewModels;

namespace UI.Views;

public partial class IndexPage : UserControl
{
    public IndexPage(IndexPageViewModel view)
    {
        InitializeComponent();
        DataContext = view;
    }
}