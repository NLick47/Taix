﻿using Avalonia;
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

        public string URL
        {
            get { return GetValue(URLProperty); }
            set { SetValue(URLProperty, value); }
        }
        public static readonly StyledProperty<string> URLProperty =
            AvaloniaProperty.Register<ImageSelect, string>(nameof(URL));

        public bool IsSelected
        {
            get { return GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public static readonly StyledProperty<bool> IsSelectedProperty =
           AvaloniaProperty.Register<ImageSelect, bool>(nameof(IsSelected));

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
