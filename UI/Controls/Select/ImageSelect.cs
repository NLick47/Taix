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
using UI.Controls.Window;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace UI.Controls.Select
{
    public class ImageSelect : TemplatedControl
    {
        public ImageSelect() {
            SelectCommand = ReactiveCommand.CreateFromTask<object>(OnSelect);
        }

        public string URL
        {
            get { return (string)GetValue(URLProperty); }
            set { SetValue(URLProperty, value); }
        }
        public static readonly StyledProperty<string> URLProperty =
            AvaloniaProperty.Register<ImageSelect, string>(nameof(URL));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public static readonly StyledProperty<bool> IsSelectedProperty =
           AvaloniaProperty.Register<ImageSelect, bool>(nameof(IsSelected));

        public double ImageWidth
        {
            get { return (double)GetValue(ImageWidthProperty); }
            set { SetValue(ImageWidthProperty, value); }
        }

        public static readonly StyledProperty<double> ImageWidthProperty =
          AvaloniaProperty.Register<ImageSelect, double>(nameof(ImageWidth), 30);

        public double ImageHeight
        {
            get { return (double)GetValue(ImageHeightProperty); }
            set { SetValue(ImageHeightProperty, value); }
        }


        public static readonly StyledProperty<double> ImageHeightProperty =
          AvaloniaProperty.Register<ImageSelect, double>(nameof(ImageHeight), 30);

        public ReactiveCommand<object, Unit> SelectCommand { get; }

        private async Task OnSelect(object obj)
        {
            var win = App.ServiceProvider.GetRequiredService<DefaultWindow>();
            var storage = win.StorageProvider;
            var results = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [new("*.png"), new("*.jpg")]
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
