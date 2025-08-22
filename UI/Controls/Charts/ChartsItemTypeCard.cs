using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using UI.Controls.Charts.Model;
using UI.Librays.Image;

namespace UI.Controls.Charts;

public class ChartsItemTypeCard : TemplatedControl
{
    public static readonly DirectProperty<ChartsItemTypeCard, ChartsDataModel> DataProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeCard, ChartsDataModel>(
            nameof(Data),
            o => o.Data,
            (o, v) => o.Data = v);

    public static readonly DirectProperty<ChartsItemTypeCard, double> MaxValueProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeCard, double>(
            nameof(MaxValue),
            o => o.MaxValue,
            (o, v) => o.MaxValue = v);

    public static readonly DirectProperty<ChartsItemTypeCard, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeCard, bool>(
            nameof(IsLoading),
            o => o.IsLoading,
            (o, v) => o.IsLoading = v);

    private ChartsDataModel _data;

    private bool _isLoading;

    private double _maxValue;
    private Image IconObj;
    private bool IsAddEvent;
    private bool isRendering;

    private TextBlock NameTextObj, ValueTextObj;
    private Rectangle ValueBlockObj;

    public ChartsItemTypeCard()
    {
        Unloaded += ChartsItemTypeCard_Unloaded;
    }

    public ChartsDataModel Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public double MaxValue
    {
        get => _maxValue;
        set => SetAndRaise(MaxValueProperty, ref _maxValue, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }


    protected override Type StyleKeyOverride => typeof(ChartsItemTypeCard);

    private void ChartsItemTypeCard_Unloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= ChartsItemTypeCard_Unloaded;
        Loaded -= ChartsItemTypeCard_Loaded;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        NameTextObj = e.NameScope.Get<TextBlock>("NameTextObj");
        ValueTextObj = e.NameScope.Get<TextBlock>("ValueTextObj");
        ValueBlockObj = e.NameScope.Get<Rectangle>("ValueBlockObj");
        IconObj = e.NameScope.Get<Image>("IconObj");
        if (!IsAddEvent)
        {
            Loaded += ChartsItemTypeCard_Loaded;
            IsAddEvent = true;
        }
    }

    private void ChartsItemTypeCard_Loaded(object sender, RoutedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        if (isRendering || Data == null) return;
        isRendering = true;
        NameTextObj.SizeChanged += (e, c) =>
        {
            //  处理文字过长显示
            if (NameTextObj.Bounds.Width > 121 && NameTextObj.FontSize > 8)
                NameTextObj.FontSize = NameTextObj.FontSize - 1;
        };
        ValueTextObj.Text = Data.Tag;
        IconObj.Source = Imager.Load(Data.Icon);

        ValueTextObj.SizeChanged += (e, c) =>
        {
            //if (MaxValue <= 0)
            //{
            //    return;
            //}
            var size = Data.Value / MaxValue * Bounds.Width / 3;
            ValueBlockObj.Width = ValueBlockObj.Height = size;


            ValueBlockObj.Effect = new BlurEffect
            {
                Radius = size
            };
        };
    }
}