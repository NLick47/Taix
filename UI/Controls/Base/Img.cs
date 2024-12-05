using Avalonia;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Base
{
    public class Img : TemplatedControl
    {
        public CornerRadius Radius
        {
            get { return GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly StyledProperty<CornerRadius> RadiusProperty =
          AvaloniaProperty.Register<Img, CornerRadius>(nameof(Radius));

        public string Src
        {
            get { return GetValue(SrcProperty); }
            set { SetValue(SrcProperty, value); }
        }

        public static readonly StyledProperty<string> SrcProperty =
         AvaloniaProperty.Register<Img,string>(nameof(Src), "avares://UI/Resources/Icons/defaultIcon.png");

        /// <summary>
        /// 图片链接
        /// </summary>
        public string URL
        {
            get { return GetValue(URLProperty); }
            set { SetValue(URLProperty, value); }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == URLProperty && e.OldValue != e.NewValue && e.NewValue != null)
            {
                var control = (e.Sender as Img);
                control.Handle(e.NewValue.ToString());
            }
        }

        public static readonly StyledProperty<string> URLProperty =
           AvaloniaProperty.Register<Img,string>(nameof(URL));


        private void Handle(string path)
        {
            string defaultIconFile = "avares://UI/Resources/Icons/defaultIcon.png";
            if (string.IsNullOrEmpty(path))
            {
                Src = defaultIconFile;
                return;
            }
            if (path.IndexOf("avares:") != -1)
            {
                Src = path;
                return;
            }

            string src = path.IndexOf(":") != -1 ? path : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            Src = File.Exists(src) ? src : defaultIconFile;
        }

        protected override Type StyleKeyOverride => typeof(Img);
    }
}
