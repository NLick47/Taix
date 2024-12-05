using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace UI.Controls.Base
{
    public class IconSelect : TemplatedControl
    {
        public List<string> Icons
        {
            get { return GetValue(DaysProperty); }
            set { SetValue(DaysProperty, value); }
        }
        public static readonly StyledProperty<List<string>> DaysProperty =
            AvaloniaProperty.Register<IconSelect, List<string>>(nameof(Icons));

        public string URL
        {
            get { return GetValue(URLProperty); }
            set { SetValue(URLProperty, value); }
        }
        public static readonly StyledProperty<string> URLProperty =
                AvaloniaProperty.Register<IconSelect, string>(nameof(URL));

        public bool IsOpen
        {
            get { return GetValue(IsOpenProperty); }
            set { SetValue(IsOpenProperty, value); }
        }

        public static readonly StyledProperty<bool> IsOpenProperty =
           AvaloniaProperty.Register<IconSelect, bool>(nameof(IsOpen));

        private bool IsFirstClick = false;
        private Border SelectContainer;

        private void LoadIcons()
        {
            var list = new List<string>();
            for (int i = 1; i < 45; i++)
            {
                list.Add($"avares://UI/Resources/Emoji/({i}).png");
            }
            Icons = list;
        }

        private void Reset()
        {
            URL = Icons[0];
        }
        private void OnShowSelect(object obj)
        {
            IsOpen = true;
            IsFirstClick = true;
        }

        protected override Type StyleKeyOverride => typeof(IconSelect);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SelectContainer = e.NameScope.Get<Border>("SelectContainer");
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                HandleMouseEvent(e);
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            HandleMouseEvent(e);
        }

        private void HandleMouseEvent(PointerEventArgs e)
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

        public ICommand ShowSelectCommand { get; private set; }
        public ICommand FileSelectCommand { get; private set; }
    }
}
