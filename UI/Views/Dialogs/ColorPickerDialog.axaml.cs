using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using UI.Servicers.Dialogs;

namespace UI.Views.Dialogs;

public partial class ColorPickerDialog : Window
{
    public ColorPickerDialog()
    {
        InitializeComponent();
    }
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var ed = this.picker.Color;
        var pars = new Dictionary<object, object?>() { { "pickColor", ed } };
        Close(new DialogResult(ButtonResult.OK, pars));
    }

    private void Close_Click(object? sender, PointerPressedEventArgs e)
    {
        Close(new DialogResult(ButtonResult.Cancel));
    }
}