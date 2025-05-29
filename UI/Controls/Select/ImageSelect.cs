using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.Templates;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using UI.Controls.Window;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace UI.Controls.Select
{
    public class ImageSelect : TemplatedControl
    {
        public ImageSelect() {
            SelectCommand = ReactiveCommand.CreateFromTask<object>(OnSelect);
        }

        private string _url = string.Empty;
        public static readonly DirectProperty<ImageSelect, string> URLProperty =
            AvaloniaProperty.RegisterDirect<ImageSelect, string>(
                nameof(URL),
                o => o.URL,
                (o, v) => o.URL = v);
        public string URL
        {
            get => _url;
            set => SetAndRaise(URLProperty, ref _url, value);
        }

        private bool _isSelected;
        public static readonly DirectProperty<ImageSelect, bool> IsSelectedProperty =
            AvaloniaProperty.RegisterDirect<ImageSelect, bool>(
                nameof(IsSelected),
                o => o.IsSelected,
                (o, v) => o.IsSelected = v);
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        }
        public double ImageWidth
        {
            get { return GetValue(ImageWidthProperty); }
            set { SetValue(ImageWidthProperty, value); }
        }

        public static readonly StyledProperty<double> ImageWidthProperty =
          AvaloniaProperty.Register<ImageSelect, double>(nameof(ImageWidth), 30);

        public double ImageHeight
        {
            get { return GetValue(ImageHeightProperty); }
            set { SetValue(ImageHeightProperty, value); }
        }


        public static readonly StyledProperty<double> ImageHeightProperty =
          AvaloniaProperty.Register<ImageSelect, double>(nameof(ImageHeight), 30);

        public ReactiveCommand<object, Unit> SelectCommand { get; }

        private async Task OnSelect(object obj)
        {
            var storage = TopLevel.GetTopLevel(this).StorageProvider;
            var results = await storage.OpenFilePickerAsync(new ()
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = [ "*.png", "*.jpg", "*.jpeg"]
                    }
                ]
            });

            var selectFile =  results.FirstOrDefault();
            if(selectFile is not null)
            {
                URL = selectFile.Path.ToString();
            }
        }



        protected override Type StyleKeyOverride => typeof(ImageSelect);
    }
}
