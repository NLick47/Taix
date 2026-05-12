using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Controls.Charts;

public class PieChart : TemplatedControl
{
    public static readonly DirectProperty<PieChart, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<PieChart, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    private IEnumerable<ChartsDataModel> _data = [];

    public IEnumerable<ChartsDataModel> Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    protected override Type StyleKeyOverride => typeof(PieChart);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var canvas = e.NameScope.Get<Canvas>("PieCanvas");
        canvas.SizeChanged += (_, _) => Render(canvas);
        Render(canvas);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
        {
            var canvas = this.FindControl<Canvas>("PieCanvas");
            if (canvas != null) Render(canvas);
        }
    }

    private void Render(Canvas canvas)
    {
        canvas.Children.Clear();
        var list = Data?.ToList();
        if (list == null || list.Count == 0) return;

        if (canvas.Bounds.Width <= 0 || canvas.Bounds.Height <= 0) return;

        var item = new ChartsItemTypePie
        {
            Width = canvas.Bounds.Width,
            Height = canvas.Bounds.Height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Data = list.OrderBy(m => m.Value).ToList()
        };
        canvas.Children.Add(item);
    }
}
