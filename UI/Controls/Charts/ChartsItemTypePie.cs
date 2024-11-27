using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;

namespace UI.Controls.Charts
{
    public class ChartsItemTypePie : Canvas
    {
        /// <summary>
        /// Data
        /// </summary>
        public List<ChartsDataModel> Data
        {
            get { return (List<ChartsDataModel>)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<List<ChartsDataModel>> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypePie, List<ChartsDataModel>>(nameof(Data));


        /// <summary>
        /// 最大值
        /// </summary>
        public double MaxValue
        {
            get { return (double)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ChartsItemTypePie,double>(nameof(MaxValue));

        private double _lastAngle = -Math.PI / 2;
        private int _zIndex = 1;
        private List<Path> _paths = new List<Path>();

        protected override Type StyleKeyOverride => typeof(ChartsItemTypePie);

        public ChartsItemTypePie()
        {
            Loaded += ChartsItemTypePie_Loaded;
        }

        private void ChartsItemTypePie_Loaded(object sender, RoutedEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            _paths.Clear();
            Children.Clear();
            MaxValue = Data.Sum(m => m.Value);

            int i = 0;
            foreach (var item in Data)
            {
                var angle = item.Value / MaxValue * 360;
                var path = CreatePath(angle, UI.Base.Color.Colors.GetFromString(item.Color));
                path.ToolTip = item.PopupText;
                path.MouseEnter += Path_MouseEnter;
                path.MouseLeave += Path_MouseLeave;
                _paths.Add(path);
                Children.Add(path);
                i++;
            }
        }

       
    }
}
