using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Taix.Client.Servicers.Dialogs;

namespace Taix.Client.Views.Dialogs;

public partial class ColorPickerDialog : Window
{
    public ColorPickerDialog()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var ed = picker.Color;
        var pars = new Dictionary<object, object?> { { "pickColor", ed } };
        Close(new DialogResult(ButtonResult.OK, pars));
    }

    private void Close_Click(object? sender, PointerPressedEventArgs e)
    {
        Close(new DialogResult(ButtonResult.Cancel));
    }
}