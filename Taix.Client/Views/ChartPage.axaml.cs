using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Taix.Client.ViewModels;

namespace Taix.Client.Views;

public partial class ChartPage : UserControl
{
    public ChartPage()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<ChartPageViewModel>();
    }
}
