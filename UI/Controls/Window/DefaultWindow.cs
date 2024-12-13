using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Core;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Input;

namespace UI.Controls.Window
{
    public class DefaultWindow : Avalonia.Controls.Window
    {
        public static readonly StyledProperty<IImage?> IconSourceProperty =
    AvaloniaProperty.Register<DefaultWindow, IImage?>(nameof(IconSource));

        public static readonly StyledProperty<bool> MaximizeVisibleProperty =
            AvaloniaProperty.Register<DefaultWindow, bool>(nameof(MaximizeVisible));

        public static readonly StyledProperty<bool> RestoreVisibleProperty =
            AvaloniaProperty.Register<DefaultWindow, bool>(nameof(RestoreVisible));

        public bool MaximizeVisible { get => GetValue(MaximizeVisibleProperty); set => SetValue(MaximizeVisibleProperty, value); }


        public bool RestoreVisible { get => GetValue(RestoreVisibleProperty); set => SetValue(RestoreVisibleProperty, value); }

        public static readonly StyledProperty<PageContainer> PageContainerProperty =
            AvaloniaProperty.Register<DefaultWindow, PageContainer>(nameof(PageContainer));
        public PageContainer PageContainer { get { return GetValue(PageContainerProperty); } set { SetValue(PageContainerProperty, value); } }

        public static readonly StyledProperty<ToastType> ToastTypeProperty =
             AvaloniaProperty.Register<DefaultWindow, ToastType>(nameof(ToastType));
        /// <summary>
        /// toast type
        /// </summary>
        public ToastType ToastType
        {
            get { return GetValue(ToastTypeProperty); }
            set { SetValue(ToastTypeProperty, value); }
        }


        public static readonly StyledProperty<string> DialogTitleProperty =
            AvaloniaProperty.Register<DefaultWindow, string>(nameof(DialogTitle));

        public static readonly StyledProperty<Base.IconTypes> ToastIconProperty =
             AvaloniaProperty.Register<DefaultWindow, Base.IconTypes>(nameof(ToastIcon));

        public static readonly StyledProperty<string> ToastContentProperty =
              AvaloniaProperty.Register<DefaultWindow, string>(nameof(ToastContent));
        /// <summary>
        /// toast content
        /// </summary>
        public string ToastContent
        {
            get { return (string)GetValue(ToastContentProperty); }
            set { SetValue(ToastContentProperty, value); }
        }
        public Base.IconTypes ToastIcon
        {
            get
            {
                return (Base.IconTypes)GetValue(ToastIconProperty);
            }
            set { SetValue(ToastIconProperty, value); }
        }
        /// <summary>
        /// Dialog title
        /// </summary>
        public string DialogTitle
        {
            get { return (string)GetValue(DialogTitleProperty); }
            set { SetValue(DialogTitleProperty, value); }
        }


        public static readonly StyledProperty<string> DialogMessageProperty =
             AvaloniaProperty.Register<DefaultWindow, string>(nameof(DialogMessage));


        public static readonly StyledProperty<string> InputModalValueProperty =
             AvaloniaProperty.Register<DefaultWindow, string>(nameof(InputModalValue));
        /// <summary>
        /// Dialog title
        /// </summary>
        public string InputModalValue
        {
            get { return (string)GetValue(InputModalValueProperty); }
            set { SetValue(InputModalValueProperty, value); }
        }
        /// <summary>
        /// Dialog message
        /// </summary>
        public string DialogMessage
        {
            get { return (string)GetValue(DialogMessageProperty); }
            set { SetValue(DialogMessageProperty, value); }
        }

        public static readonly StyledProperty<bool> IsShowToastProperty =
             AvaloniaProperty.Register<DefaultWindow, bool>(nameof(IsShowToast));
        public bool IsShowToast { get { return (bool)GetValue(IsShowToastProperty); } set { SetValue(IsShowToastProperty, value); } }

        #region sys command
        public static ReactiveCommand<Unit, Unit> MinimizeWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> RestoreWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> MaximizeWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> CloseWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> LogoButtonClickCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> BackCommand { get; private set; }
        #endregion




        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PageContainerProperty)
            {
                OnPageContainerChanged(change);
            }
            if (change.Property == IsCanBackProperty)
            {
                OnIsCanBackChanged(change);
            }
            if (change.Property == IsShowToastProperty)
            {
                OnIsShowToastChanged(change);
            }
        }

        private static void OnIsShowToastChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var that = e.Sender as DefaultWindow;
            if (that != null)
            {
                if (that.IsShowToast)
                {
                    that.ShowToast();
                }
                else
                {
                    that.HideToast();
                }
            }
        }

        private Border ToastBorder, Masklayer, DialogBorder, InputModalBorder;
        private Grid ToastGrid;
        private DispatcherTimer toastTimer;
        private bool IsDialogConfirm;
        private bool IsShowConfirmDialog, IsShowInputModal;
        private Button.Button CancelBtn, ConfirmBtn, InputModalCancelBtn, InputModalConfirmBtn;
        private InputBox InputModalInputBox;
        private string InputValue;
        private Func<string, bool> InputModalValidFnc;
        private CancellationTokenSource _toastCancellationTokenSource;
        private async void ShowToast()
        {
            if (_toastCancellationTokenSource != null)
            {
                if (!_toastCancellationTokenSource.TryReset())
                {
                    _toastCancellationTokenSource.Dispose();
                    _toastCancellationTokenSource = new CancellationTokenSource();
                }
            }
            else
            {
                _toastCancellationTokenSource = new CancellationTokenSource();
            }

            ToastGrid.IsVisible = true;
            DialogBorder.IsVisible = false;
            if (!IsShowInputModal)
            {
                InputModalBorder.IsVisible = false;
            }
            ToastBorder.IsVisible = true;
            ToastBorder.RenderTransform = TransformOperations.Parse("translateY(0px)");
            Masklayer.Opacity = 0.6;
            ToastGrid.PointerPressed += ToastGrid_MouseLeftButtonDown;

            try
            {
                await Task.Delay(3000, _toastCancellationTokenSource.Token);
            }
            catch (TaskCanceledException )
            {
            }
            IsShowToast = false;
        }

        public Task<bool> ShowConfirmDialogAsync(string title_, string message_)
        {
            IsShowConfirmDialog = true;
            ToastGrid.IsVisible = true;
            ToastBorder.IsVisible = false;
            DialogBorder.IsVisible = true;
            InputModalBorder.IsVisible = false;

            DialogMessage = message_;
            DialogTitle = title_;
            DialogBorder.RenderTransform = TransformOperations.Parse("translateY(0px)");
            Masklayer.Opacity = 0;

            return Task.Run(async () =>
            {
                while (IsShowConfirmDialog)
                {
                    await Task.Delay(10);
                }

                return IsDialogConfirm;
            });
        }

        private void ToastGrid_MouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            IsShowToast = false;
            _toastCancellationTokenSource?.Cancel();
            ToastGrid.PointerPressed -= ToastGrid_MouseLeftButtonDown;
        }

        public Task<string> ShowInputModalAsync(string title_, string message_, string value_, Func<string, bool> validFnc_)
        {
            IsShowInputModal = true;
            ToastGrid.IsVisible = true;
            ToastBorder.IsVisible = false;
            DialogBorder.IsVisible = false;
            InputModalBorder.IsVisible = true;

            DialogMessage = message_;
            DialogTitle = title_;
            InputModalValue = value_;
            InputModalValidFnc = validFnc_;

            InputModalInputBox.Text = InputModalValue;
            InputModalBorder.RenderTransform = TransformOperations.Parse("translateY(10px)");

            Masklayer.Opacity = 0.6;

            return Task.Run(async () =>
            {
                while (IsShowInputModal)
                {
                    await Task.Delay(10);
                }

                if (IsDialogConfirm)
                {
                    return InputValue;
                }
                else
                {
                    throw new Exception("Input cancel");
                }
            });
        }


        private void HideToast()
        {
            ToastBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
            Masklayer.Opacity = 0;
            ToastGrid.IsVisible = false;
        }


        private Grid titleBar;

        /// <summary>
        /// 是否可以返回
        /// </summary>
        public bool IsCanBack { get { return GetValue(IsCanBackProperty); } set { SetValue(IsCanBackProperty, value); } }

        public static readonly StyledProperty<bool> IsCanBackProperty =
            AvaloniaProperty.Register<DefaultWindow, bool>(nameof(IsCanBack));

        private static void OnIsCanBackChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var that = e.Sender as DefaultWindow;
            if (that != null)
            {
                //if (that.IsCanBack)
                //{
                //    VisualStateManager.GoToState(that, "CanBackState", true);
                //}
                //else
                //{
                //    VisualStateManager.GoToState(that, "Normal", true);
                //}
            }
        }

        private static void OnPageContainerChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var that = (DefaultWindow)e.Sender;
            if (that != null)
            {
                if (e.NewValue != null)
                {
                    that.IsCanBack = that.PageContainer.Index >= 1;

                    that.PageContainer.OnLoadPaged += (s, v) =>
                    {
                        var pc = s as PageContainer;
                        that.IsCanBack = pc?.Index >= 1;
                    };
                }

            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            titleBar = e.NameScope.Find<Grid>("TitleBar");
            ToastBorder = e.NameScope.Find<Border>("ToastBorder");
            Masklayer = e.NameScope.Find<Border>("Masklayer");
            ToastGrid = e.NameScope.Find<Grid>("ToastGrid");
            DialogBorder = e.NameScope.Find<Border>("DialogBorder");
            CancelBtn = e.NameScope.Find<Button.Button>("CancelBtn");
            ConfirmBtn = e.NameScope.Find<Button.Button>("ConfirmBtn");
            InputModalBorder = e.NameScope.Find<Border>("InputModalBorder");
            InputModalCancelBtn = e.NameScope.Find<Button.Button>("InputModalCancelBtn");
            InputModalConfirmBtn = e.NameScope.Find<Button.Button>("InputModalConfirmBtn");
            InputModalInputBox = e.NameScope.Find<InputBox>("InputModalInputBox");

            if (CancelBtn != null)
            {
                CancelBtn.Click += (e, c) =>
                {
                    IsDialogConfirm = false;
                    HideDialog();
                };
            }

            if (ConfirmBtn != null)
            {
                ConfirmBtn.Click += (e, c) =>
                {
                    IsDialogConfirm = true;
                    HideDialog();
                };
            }

            if (InputModalCancelBtn != null)
            {
                InputModalCancelBtn.Click += (e, c) =>
                {
                    IsDialogConfirm = false;
                    HideInputModal();
                };
            }

            if (InputModalConfirmBtn != null)
            {
                InputModalConfirmBtn.Click += (e, c) =>
                {
                    if (InputModalValidFnc != null && !InputModalValidFnc(InputValue))
                    {
                        return;
                    }
                    IsDialogConfirm = true;
                    HideInputModal();
                };
            }

            if (InputModalInputBox != null)
            {
                InputModalInputBox.TextChanged += (e, c) =>
                {
                    InputValue = InputModalInputBox.Text;
                };
            }
        }

        private void HideDialog()
        {
            InputModalBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
            Masklayer.Opacity = 0;
            ToastGrid.IsVisible = false;
            IsShowConfirmDialog = false;
        }

        private void HideInputModal()
        {

            InputModalBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
            Masklayer.Opacity = 0;
            ToastGrid.IsVisible = false;
            IsShowInputModal = false;
        }



        private bool IsWindowClosed_ = false;
        public bool IsWindowClosed { get { return IsWindowClosed_; } }
        public IImage? IconSource
        {
            get => GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }
        public DefaultWindow()
        {
            this.WhenAnyValue(x => x.MaximizeVisible, x => x.WindowState)
             .Subscribe(values =>
             {
                 var (maximizeVisible, windowState) = values;
                 switch (windowState)
                 {
                     case WindowState.Normal:
                         MaximizeVisible = true;
                         RestoreVisible = false;
                         break;
                     case WindowState.Maximized:
                         RestoreVisible = true;
                         MaximizeVisible = false;
                         break;
                 }
             });

            MinimizeWindowCommand = ReactiveCommand.Create(() =>
            {
                WindowState = WindowState.Minimized;
            });

            RestoreWindowCommand = ReactiveCommand.Create(() =>
            {
                WindowState = WindowState.Normal;
            });

            MaximizeWindowCommand = ReactiveCommand.Create(() =>
            {
                WindowState = WindowState.Maximized;
            });

            CloseWindowCommand = ReactiveCommand.Create(() =>
            {
                Close();
            });

            BackCommand = ReactiveCommand.Create(() =>
            {
                if (PageContainer != null)
                {
                    PageContainer.Back();
                    if (PageContainer.Index == 0)
                    {
                        IsCanBack = false;
                    }
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsWindowClosed_ = true;
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (titleBar.Bounds.Contains(e.GetCurrentPoint(null).Position)
                && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }


        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Icon = IconSource is Bitmap bitmap ? new WindowIcon(bitmap) : default;
        }

        protected override Type StyleKeyOverride => typeof(DefaultWindow);

    }
}
