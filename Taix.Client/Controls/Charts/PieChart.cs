using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Controls.Charts;

public class PieChart : TemplatedControl
{
    public static readonly DirectProperty<PieChart, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<PieChart, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    private EmptyData? _emptyDataView;
    private ChartsItemTypePie? _pieView;
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
        _emptyDataView = e.NameScope.Find<EmptyData>("EmptyDataView");
        _pieView = e.NameScope.Find<ChartsItemTypePie>("PieView");
        Update();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
        {
            Update();
        }
    }

    private void Update()
    {
        var list = Data?.ToList();
        var hasData = list != null && list.Count > 0 && list.Sum(d => d.Value) > 0;

        if (_emptyDataView != null)
            _emptyDataView.IsVisible = !hasData;

        if (_pieView != null)
        {
            _pieView.Data = hasData ? list!.OrderBy(m => m.Value).ToList() : null;
        }
    }
}
