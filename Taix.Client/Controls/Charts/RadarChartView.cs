using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Controls.Charts;

public class RadarChartView : TemplatedControl
{
    public static readonly DirectProperty<RadarChartView, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<RadarChartView, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    public static readonly DirectProperty<RadarChartView, double> DataMaximumProperty =
        AvaloniaProperty.RegisterDirect<RadarChartView, double>(
            nameof(DataMaximum), o => o.DataMaximum, (o, v) => o.DataMaximum = v);

    private IEnumerable<ChartsDataModel> _data = [];
    private double _dataMaximum;
    private Border _container;

    public IEnumerable<ChartsDataModel> Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public double DataMaximum
    {
        get => _dataMaximum;
        set => SetAndRaise(DataMaximumProperty, ref _dataMaximum, value);
    }

    protected override Type StyleKeyOverride => typeof(RadarChartView);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _container = e.NameScope.Get<Border>("RadarContainer");
        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == DataMaximumProperty)
            Render();
    }

    private void Render()
    {
        if (_container == null) return;
        _container.Child = null;

        var list = Data?.ToList();
        if (list == null || list.Count <= 2)
        {
            _container.Child = new EmptyData();
            return;
        }

        var maxValue = list.Max(m => m.Values.Sum());
        if (DataMaximum > 0) maxValue = DataMaximum;

        var radar = new RadarChart
        {
            Labels = list.Select(x => x.Name.Length > 4 ? x.Name[..4] : x.Name).ToList(),
            Values = list.Select(x => x.Values.Sum()).ToList(),
            MaxValue = maxValue
        };
        ToolTip.SetTip(radar, string.Join("\n", list.Select(x => x.Name + $" {Time.ToString((int)x.Values.Sum())}")));
        _container.Child = radar;
    }
}
