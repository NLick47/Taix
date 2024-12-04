using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;


namespace UI.Controls.Base
{
    public class ColorSelect : TemplatedControl
    {
        public List<string> Colors
        {
            get { return (List<string>)GetValue(ColorsProperty); }
            set { SetValue(ColorsProperty, value); }
        }
        public static readonly AvaloniaProperty<List<string>> ColorsProperty =
            AvaloniaProperty.Register<ColorSelect,List<string>>(nameof(Colors) );

        private void OnWindowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            if (!this.Bounds.Contains(point))
            {
                IsOpen = false;
            }
        }

        public string Color
        {
            get { return (string)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }
        public static readonly AvaloniaProperty<string> ColorProperty =
               AvaloniaProperty.Register<ColorSelect, string>(nameof(ColorProperty),defaultBindingMode: BindingMode.TwoWay);

        public bool IsOpen
        {
            get { return (bool)GetValue(IsOpenProperty); }
            set { SetValue(IsOpenProperty, value); }
        }

        public static readonly AvaloniaProperty<bool> IsOpenProperty =
           AvaloniaProperty.Register<ColorSelect, bool>(nameof(IsOpen));

        private string HexConverter(System.Drawing.Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        private bool IsFirstClick = false;
        private Border SelectContainer;
        private void OnShowSelect(object obj)
        {
            IsOpen = !IsOpen;
            IsFirstClick = !IsFirstClick;
        }

        private ColorPicker _colorPicker;
        public ICommand ShowSelectCommand { get; set; }
        public ICommand ColorSelectCommand { get; set; }
        public event EventHandler OnSelected;


        protected override Type StyleKeyOverride => typeof(ColorSelect);
        public ColorSelect()
        {
            _colorPicker = new ColorPicker
            {
                Name = "ColorPicker"
            };

            this.GetObservable(IsOpenProperty).Subscribe(isOpen =>
            {
                HandleWindowEvents(isOpen);
            });

            ShowSelectCommand = ReactiveCommand.Create<object>(OnShowSelect);
            ColorSelectCommand = ReactiveCommand.Create<object>(OnColorSelect);
            LoadColors();
        }

        private void HandleWindowEvents(bool isOpen)
        {
            var window = this.GetVisualRoot() as Avalonia.Controls.Window;
            if (window != null)
            {
                if (isOpen)
                {
                    window.PointerPressed += OnWindowPointerPressed;
                    window.LostFocus += OnLostFocus;
                    window.Deactivated += OnDeactivated;
                }
                else
                {
                    window.PointerPressed -= OnWindowPointerPressed;
                    window.LostFocus -= OnLostFocus;
                    window.Deactivated -= OnDeactivated;
                }
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                HandleMouseEvent(e);
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            IsOpen = false;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            IsOpen = false;
        }

        private void OnColorSelect(object obj)
        {
            IsOpen = false;
            IsFirstClick = false;

            //System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
            //if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    Color = HexConverter(colorDialog.Color);
            //}
            
          
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            HandleMouseEvent(e);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SelectContainer = e.NameScope.Get<Border>("SelectContainer");
        }

        private void HandleMouseEvent(PointerEventArgs  e)
        {
            if (!IsFirstClick)
            {
                bool isInControl = IsInControl(e);

                if (!isInControl)
                {
                    IsOpen = false;
                }
            }
            IsFirstClick = false;
        }

        private bool IsInControl(PointerEventArgs e)
        {
            var p = e.GetPosition(SelectContainer);
            if (p.X < 0 || p.Y < 0 || p.X > SelectContainer.Bounds.Width || p.Y > SelectContainer.Bounds.Height)
            {
                return false;
            }
            return true;
        }

        private void LoadColors()
        {
            Colors = [
                "#00FFAB",
                "#017aff",
                "#6c47ff",
                "#39c0c8",
                "#f96300",
                "#f34971",
                "#ff9382",
                "#f5c900",
                "#cdad7a",
                "#aabb5d",
                "#000000",
                "#2B20D9"
            ];
            Color = Colors[0];
        }
    }
}
