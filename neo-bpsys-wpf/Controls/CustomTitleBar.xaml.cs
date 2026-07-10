using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// CustomTitleBar.xaml 的交互逻辑
/// </summary>
public partial class CustomTitleBar : UserControl
{
    private Window? _hostWindow;

    public CustomTitleBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        WindowIcon.MouseDown += WindowIcon_MouseDown;
        TitleBar.MouseDown += TitleBar_MouseDown;
        MaximizeButton.Click += MaximizeButton_OnClick;
        MinimizeButton.Click += MinimizeButton_OnClick;
        ExitButton.Click += ExitButton_OnClick;
    }

    private void WindowIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 获取宿主窗口
        if (_hostWindow is { } window)
        {
            SystemCommands.ShowSystemMenu(window, window.PointToScreen(e.GetPosition(this)));
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 获取宿主窗口
        AttachHostWindow(Window.GetWindow(this));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AttachHostWindow(null);
    }

    private void AttachHostWindow(Window? window)
    {
        if (ReferenceEquals(_hostWindow, window))
        {
            return;
        }

        if (_hostWindow is not null)
        {
            _hostWindow.StateChanged -= HostWindow_OnStateChanged;
        }

        _hostWindow = window;
        if (_hostWindow is not null)
        {
            _hostWindow.StateChanged += HostWindow_OnStateChanged;
            UpdateMaximizeButtonIcon(_hostWindow);
        }
    }

    private void HostWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            UpdateMaximizeButtonIcon(window);
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_hostWindow is not { } window)
        {
            return;
        }

        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left && IsMaximizeVisible)
        {
            ToggleWindowState(window);
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMoveWindow(window);
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            SystemCommands.ShowSystemMenu(window, window.PointToScreen(e.GetPosition(this)));
        }
    }

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is { } window)
        {
            ToggleWindowState(window);
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is { } window)
        {
            ConfirmExit(window);
        }
    }

    private static void DragMoveWindow(Window window)
    {
        if (window is not null && window.WindowState != WindowState.Maximized)
            window.DragMove();
    }

    private void ToggleWindowState(Window window)
    {
        window.WindowState = ToggleWindowStateOnce(window.WindowState);
        UpdateMaximizeButtonIcon(window);
    }

    internal static WindowState ToggleWindowStateOnce(WindowState currentState) =>
        currentState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

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

    /// <summary>
    /// 获取或设置一个值，指示主题切换按钮是否已选中。
    /// </summary>
    public bool IsThemeChangeChecked
    {
        get => (bool)GetValue(IsThemeChangeCheckedProperty);
        set => SetValue(IsThemeChangeCheckedProperty, value);
    }

    /// <summary>
    /// <see cref="IsThemeChangeChecked"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsThemeChangeCheckedProperty =
        DependencyProperty.Register(nameof(IsThemeChangeChecked), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置一个值，指示主题切换按钮是否可见。
    /// </summary>
    public bool IsThemeChangeVisible
    {
        get => (bool)GetValue(IsThemeChangeButtonVisibleProperty);
        set => SetValue(IsThemeChangeButtonVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsThemeChangeVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsThemeChangeButtonVisibleProperty =
        DependencyProperty.Register(nameof(IsThemeChangeVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置主题切换按钮点击时执行的命令。
    /// </summary>
    public ICommand ThemeChangeCommand
    {
        get => (ICommand)GetValue(ThemeChangeCommandProperty);
        set => SetValue(ThemeChangeCommandProperty, value);
    }

    /// <summary>
    /// <see cref="ThemeChangeCommand"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ThemeChangeCommandProperty =
        DependencyProperty.Register(nameof(ThemeChangeCommand), typeof(ICommand), typeof(CustomTitleBar), new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置一个值，指示最大化按钮是否可见。
    /// </summary>
    public bool IsMaximizeVisible
    {
        get => (bool)GetValue(IsMaximizeVisibleProperty);
        set => SetValue(IsMaximizeVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsMaximizeVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsMaximizeVisibleProperty =
        DependencyProperty.Register(nameof(IsMaximizeVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置一个值，指示最小化按钮是否可见。
    /// </summary>
    public bool IsMinimizeVisible
    {
        get => (bool)GetValue(IsMinimizeVisibleProperty);
        set => SetValue(IsMinimizeVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsMinimizeVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsMinimizeVisibleProperty =
        DependencyProperty.Register(nameof(IsMinimizeVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

    /// <summary>
    /// 获取或设置一个值，指示置顶按钮是否可见。
    /// </summary>
    public bool IsTopMostVisible
    {
        get => (bool)GetValue(IsTopMostVisibleProperty);
        set => SetValue(IsTopMostVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsTopMostVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsTopMostVisibleProperty =
        DependencyProperty.Register(nameof(IsTopMostVisible), typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));


    /// <summary>
    /// 获取或设置标题栏的图标。
    /// </summary>
    public ImageSource Icon
    {
        get => (ImageSource)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// <see cref="Icon"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(CustomTitleBar), new PropertyMetadata(null));
}
