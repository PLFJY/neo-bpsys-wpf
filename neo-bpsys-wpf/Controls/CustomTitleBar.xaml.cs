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
            window.StateChanged += (_, _) => UpdateMaximizeButtonIcon(window);
        }

        // 事件绑定
        TitleBar.MouseDown += (_, e) =>
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

        MaximizeButton.Click += (_, _) => ToggleWindowState(window);
        MinimizeButton.Click += (_, _) => window.WindowState = WindowState.Minimized;
        ExitButton.Click += (_, _) => ConfirmExit(window);
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