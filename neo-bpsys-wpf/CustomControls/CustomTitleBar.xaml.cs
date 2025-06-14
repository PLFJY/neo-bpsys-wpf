using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// CustomTitleBar.xaml 的交互逻辑
    /// </summary>
    public partial class CustomTitleBar : UserControl
    {
        public CustomTitleBar()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            WindowIcon.MouseDown += WindowIcon_MouseDown;
        }

        private void WindowIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 获取宿主窗口
            var window = Window.GetWindow(this);
            SystemCommands.ShowSystemMenu(window, window.PointToScreen(e.GetPosition(this)));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 获取宿主窗口
            var window = Window.GetWindow(this);
            if (window is FluentWindow fluentWindow)
            {
                // 绑定窗口状态变化
                window.StateChanged += (s, args) => UpdateMaximizeButtonIcon(window);
            }

            // 事件绑定
            TitleBar.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left && IsMaximizeVisible)
                    ToggleWindowState(window);

                if (e.LeftButton == MouseButtonState.Pressed)
                    DragMoveWindow(window);

                if (e.ChangedButton == MouseButton.Right)
                {
                    SystemCommands.ShowSystemMenu(window, window.PointToScreen(e.GetPosition(this)));
                }
            };

            MaximizeButton.Click += (s, e) => ToggleWindowState(window);
            MinimizeButton.Click += (s, e) => window.WindowState = WindowState.Minimized;
            ExitButton.Click += (s, e) => ConfirmExit(window);
        }

        private static void DragMoveWindow(Window window)
        {
            if (window is not null && window.WindowState != WindowState.Maximized)
                window.DragMove();
        }

        private void ToggleWindowState(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            UpdateMaximizeButtonIcon(window);
        }

        private void UpdateMaximizeButtonIcon(Window window)
        {
            MaximizeButton.Icon = window.WindowState == WindowState.Maximized
                ? new SymbolIcon { Symbol = SymbolRegular.SquareMultiple24 }
                : new SymbolIcon { Symbol = SymbolRegular.Maximize24 };
        }

        private static void ConfirmExit(Window window)
        {
            window.Close();
        }

        public bool IsThemeChangeChecked
        {
            get => (bool)GetValue(IsThemeChangeButtonCheckedProperty);
            set => SetValue(IsThemeChangeButtonCheckedProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsThemeChangeChecked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsThemeChangeButtonCheckedProperty =
            DependencyProperty.Register(nameof(IsThemeChangeChecked), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

        public bool IsThemeChangeVisible
        {
            get => (bool)GetValue(IsThemeChangeButtonVisibleProperty);
            set => SetValue(IsThemeChangeButtonVisibleProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsThemeChangeVisible.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsThemeChangeButtonVisibleProperty =
            DependencyProperty.Register(nameof(IsThemeChangeVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

        public ICommand ThemeChangeCommand
        {
            get => (ICommand)GetValue(ThemeChangeCommandProperty);
            set => SetValue(ThemeChangeCommandProperty, value);
        }

        // Using a DependencyProperty as the backing store for ThemeChangeCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ThemeChangeCommandProperty =
            DependencyProperty.Register(nameof(ThemeChangeCommand), typeof(ICommand), typeof(CustomTitleBar), new PropertyMetadata(null));

        public bool IsMaximizeVisible
        {
            get => (bool)GetValue(IsMaximizeVisibleProperty);
            set => SetValue(IsMaximizeVisibleProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsMaximizeVisible.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMaximizeVisibleProperty =
            DependencyProperty.Register(nameof(IsMaximizeVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

        public bool IsMinimizeVisible
        {
            get => (bool)GetValue(IsMinimizeVisibleProperty);
            set => SetValue(IsMinimizeVisibleProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsMinimizeVisible.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMinimizeVisibleProperty =
            DependencyProperty.Register(nameof(IsMinimizeVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

        public bool IsTopMostVisible
        {
            get => (bool)GetValue(IsTopMostVisibleProperty);
            set => SetValue(IsTopMostVisibleProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsTopMostVisible.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsTopMostVisibleProperty =
            DependencyProperty.Register(nameof(IsTopMostVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));


        public ImageSource Icon
        {
            get => (ImageSource)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        // Using a DependencyProperty as the backing store for Icon.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(CustomTitleBar), new PropertyMetadata(null));
    }
}
