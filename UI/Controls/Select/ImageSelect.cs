using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace UI.Controls.Select;

public class ImageSelect : TemplatedControl
{
    public static readonly DirectProperty<ImageSelect, string> URLProperty =
        AvaloniaProperty.RegisterDirect<ImageSelect, string>(
            nameof(URL),
            o => o.URL,
            (o, v) => o.URL = v);

    public static readonly DirectProperty<ImageSelect, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<ImageSelect, bool>(
            nameof(IsSelected),
            o => o.IsSelected,
            (o, v) => o.IsSelected = v);

    public static readonly StyledProperty<double> ImageWidthProperty =
        AvaloniaProperty.Register<ImageSelect, double>(nameof(ImageWidth), 30);


    public static readonly StyledProperty<double> ImageHeightProperty =
        AvaloniaProperty.Register<ImageSelect, double>(nameof(ImageHeight), 30);

    private bool _isSelected;

    private string _url = string.Empty;

    public ImageSelect()
    {
        SelectCommand = ReactiveCommand.CreateFromTask<object>(OnSelect);
    }

    public string URL
    {
        get => _url;
        set => SetAndRaise(URLProperty, ref _url, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
    }

    public double ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public double ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public ReactiveCommand<object, Unit> SelectCommand { get; }


    protected override Type StyleKeyOverride => typeof(ImageSelect);

    private async Task OnSelect(object obj)
    {
        var storage = TopLevel.GetTopLevel(this).StorageProvider;
        var results = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"]
                }
            ]
        });

        var selectFile = results.FirstOrDefault();
        if (selectFile is not null) URL = selectFile.Path.ToString();
    }
}