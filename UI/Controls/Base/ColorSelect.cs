using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Controls.Window;
using UI.Servicers.Dialogs;
using UI.Views;
using UI.Views.Dialogs;


namespace UI.Controls.Base
{
    public class ColorSelect : TemplatedControl
    {
        public List<string> Colors
        {
            get { return GetValue(ColorsProperty); }
            set { SetValue(ColorsProperty, value); }
        }
        public static readonly StyledProperty<List<string>> ColorsProperty =
            AvaloniaProperty.Register<ColorSelect,List<string>>(nameof(Colors) );

        private void OnWindowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            IsOpen = false;
        }   

        public string Color
        {
            get { return GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }
        public static readonly StyledProperty<string> ColorProperty =
               AvaloniaProperty.Register<ColorSelect, string>(nameof(Color), "#00FFAB");

        public bool IsOpen
        {
            get { return GetValue(IsOpenProperty); }
            set { SetValue(IsOpenProperty, value); }
        }

        public static readonly StyledProperty<bool> IsOpenProperty =
           AvaloniaProperty.Register<ColorSelect, bool>(nameof(IsOpen));

        private Border SelectContainer;
        private void OnShowSelect(object obj)
        {
            IsOpen = !IsOpen;
          
        }

       
        public ICommand ShowSelectCommand { get; private set; }
        public ICommand ColorSelectCommand { get; private set; }

        public ICommand SelectionChangedCommand { get; private set; }
        public event EventHandler OnSelected;


        protected override Type StyleKeyOverride => typeof(ColorSelect);
        public ColorSelect()
        {
            this.GetObservable(IsOpenProperty).Subscribe(isOpen =>
            {
                HandleWindowEvents(isOpen);
            });
            ShowSelectCommand = ReactiveCommand.Create<object>(OnShowSelect);
            ColorSelectCommand = ReactiveCommand.Create<object>(OnColorSelect);
            SelectionChangedCommand = ReactiveCommand.Create<string>(OnSelectionChanged);

            LoadColors();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if(change.Property == ColorProperty)
            {
                var control = change.Sender as ColorSelect;
                if (string.IsNullOrEmpty(control.Color))
                {
                    control.Color = control.Colors[0];
                }   
                control.OnSelected?.Invoke(control, EventArgs.Empty);
            }
        }

        private void HandleWindowEvents(bool isOpen)
        {
            var window = this.GetVisualRoot() as Avalonia.Controls.Window;
            if (window != null)
            {
                if (isOpen)
                {
                    window.PointerPressed += OnWindowPointerPressed;
                    window.Deactivated += OnDeactivated;
                    window.PointerWheelChanged += OnPointerWheelChanged;
                }
                else
                {
                    window.PointerPressed -= OnWindowPointerPressed;
                    window.Deactivated -= OnDeactivated;
                    window.PointerWheelChanged -= OnPointerWheelChanged;
                }
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            IsOpen = false;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            IsOpen = false;
        }

        private async void OnColorSelect(object obj)
        {
            IsOpen = false;
            var picker = new ColorPickerDialog();
            var desk = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var result = await picker.ShowDialog<IDialogResult?>(desk.MainWindow);
            if(result?.Result == ButtonResult.OK)
            {
                result.Parameters.TryGetValue("pickColor",out var pickColor);
                Color = pickColor.ToString();
            }
        }

        private void OnSelectionChanged(string e)
        {
            IsOpen = false;
            Color = e;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SelectContainer = e.NameScope.Get<Border>("SelectContainer");
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
