using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Controls;

/// <summary>
/// 前台窗口基类
/// </summary>
public class FrontedWindowBase : Window
{
    private IFrontedWindowDescriptor? _v3Descriptor;
    private IFrontedLayoutService? _layoutService;
    private IFrontedRenderer? _renderer;
    private ISharedDataService? _sharedDataService;
    private IFrontedBehaviorRuntime? _behaviorRuntime;
    private ILogger? _logger;
    private Canvas? _baseCanvas;
    private bool _isV3LayoutHost;
    private bool _isBoModeSubscribed;
    private bool _hasLoadedV3Layout;

    private bool _isInternalContentChange = false;
    /// <summary>
    /// 前台窗口基类构造
    /// </summary>
    public FrontedWindowBase()
    {
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        if (_isV3LayoutHost)
        {
            base.OnContentChanged(oldContent, newContent);
            return;
        }

        // 防止递归调用：如果是我们自己包装 Viewbox 导致的 Content 改变，直接返回
        if (_isInternalContentChange || newContent is Viewbox vb && vb.Name == "InternalAutoViewbox")
        {
            base.OnContentChanged(oldContent, newContent);
            return;
        }

        _isInternalContentChange = true;

        try
        {
            // 1. 创建 Viewbox 并配置属性
            var viewbox = new Viewbox
            {
                Name = "InternalAutoViewbox",
                Stretch = Stretch.Fill
            };

            // 2. 创建 Binding (等价于你 XAML 里的 RelativeSource Binding)
            Binding widthBinding = new("Width")
            {
                Source = this, // 直接指向当前 Window
                Mode = BindingMode.OneWay
            };
            Binding heightBinding = new("Height")
            {
                Source = this,
                Mode = BindingMode.OneWay
            };

            viewbox.SetBinding(Viewbox.WidthProperty, widthBinding);
            viewbox.SetBinding(Viewbox.HeightProperty, heightBinding);

            // 3. 将原本的内容移交给 Viewbox
            // 注意：需要先将 Content 置空，否则 newContent 仍然属于 Window 的 Logical Tree，
            // 直接赋值给 Viewbox.Child 会报错
            this.Content = null;
            viewbox.Child = newContent as UIElement;

            // 4. 重新将 Viewbox 设为 Window 的 Content
            this.Content = viewbox;
        }
        finally
        {
            _isInternalContentChange = false;
        }

        base.OnContentChanged(oldContent, this.Content);
    }

    /// <summary>
    /// Initializes this window as a configuration-driven v3 layout host.
    /// </summary>
    /// <param name="descriptor">The fronted window descriptor.</param>
    /// <param name="layoutService">The layout service.</param>
    /// <param name="renderer">The fronted renderer.</param>
    /// <param name="sharedDataService">The shared data service.</param>
    /// <param name="behaviorRuntime">The optional behavior runtime.</param>
    /// <param name="logger">The optional logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public void InitializeV3LayoutHost(
        IFrontedWindowDescriptor descriptor,
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        IFrontedBehaviorRuntime? behaviorRuntime,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(layoutService);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sharedDataService);

        _v3Descriptor = descriptor;
        _layoutService = layoutService;
        _renderer = renderer;
        _sharedDataService = sharedDataService;
        _behaviorRuntime = behaviorRuntime;
        _logger = logger;
        _isV3LayoutHost = true;

        Title = string.IsNullOrWhiteSpace(descriptor.DisplayName)
            ? descriptor.WindowTypeName
            : descriptor.DisplayName;

        _baseCanvas = new Canvas
        {
            Name = FrontedLayoutConstants.BaseCanvasName
        };

        Content = new Viewbox
        {
            Name = "InternalAutoViewbox",
            Stretch = Stretch.Fill,
            Child = _baseCanvas
        };

        Loaded += OnV3HostLoaded;
        Unloaded += OnV3HostUnloaded;
        Closed += OnV3HostClosed;
        IsVisibleChanged += OnV3HostIsVisibleChanged;
    }

    /// <summary>
    /// Reloads the window-centric v3 layout and behavior runtime.
    /// </summary>
    /// <returns>A task that completes when the layout has reloaded.</returns>
    public async Task ReloadFrontedLayoutAsync()
    {
        if (!_isV3LayoutHost
            || _v3Descriptor is null
            || _layoutService is null
            || _renderer is null
            || _sharedDataService is null
            || _baseCanvas is null)
        {
            return;
        }

        try
        {
            var config = await _layoutService.LoadWindowConfigAsync(_v3Descriptor.FullWindowType);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 window layout config not found. Window: {WindowTypeName}",
                    _v3Descriptor.FullWindowType);
                return;
            }

            ApplyWindowSettings(config.WindowSettings);
            ApplyCanvasSettings(config.CanvasSettings);
            await (_behaviorRuntime?.DetachAsync(_v3Descriptor.WindowId) ?? Task.CompletedTask);
            _renderer.RenderToCanvas(_baseCanvas, config, new FrontedRenderContext
            {
                WindowId = _v3Descriptor.WindowId,
                WindowTypeName = _v3Descriptor.FullWindowType,
                CanvasName = FrontedLayoutConstants.BaseCanvasName
            });

            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.AttachAsync(new FrontedBehaviorRuntimeContext
                {
                    WindowId = _v3Descriptor.WindowId,
                    WindowType = _v3Descriptor.FullWindowType,
                    RootCanvas = _baseCanvas,
                    WindowConfig = config,
                    SharedDataService = _sharedDataService,
                    IsDesignerPreview = false,
                    Logger = _logger
                });
            }

            _hasLoadedV3Layout = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to reload fronted v3 window layout. Window: {WindowTypeName}",
                _v3Descriptor.FullWindowType);
        }
    }

    private void ApplyWindowSettings(FrontedWindowSettings settings)
    {
        if (IsPositiveFinite(settings.WindowWidth))
        {
            Width = settings.WindowWidth;
        }

        if (IsPositiveFinite(settings.WindowHeight))
        {
            Height = settings.WindowHeight;
        }

        Topmost = settings.Topmost;

        if (settings.WindowLeft.HasValue)
        {
            Left = settings.WindowLeft.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (settings.WindowTop.HasValue)
        {
            Top = settings.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (PresentationSource.FromVisual(this) is null)
        {
            AllowsTransparency = settings.AllowsTransparency;
        }
        else if (AllowsTransparency != settings.AllowsTransparency)
        {
            _logger?.LogInformation(
                "Fronted window transparency change will apply the next time the window is recreated. Window: {WindowTypeName}",
                _v3Descriptor?.FullWindowType);
        }

        if (!TryCreateBackgroundBrush(settings.BackgroundColor, out var brush))
        {
            _logger?.LogWarning(
                "Fronted window background color is empty or invalid; falling back to Transparent. Window: {WindowTypeName}, BackgroundColor: {BackgroundColor}",
                _v3Descriptor?.FullWindowType,
                settings.BackgroundColor);
        }

        SetCurrentValue(BackgroundProperty, brush);

        if (Content is Viewbox viewbox)
        {
            viewbox.Stretch = settings.ViewboxStretch;
        }
    }

    private void ApplyCanvasSettings(FrontedCanvasSettings settings)
    {
        if (_baseCanvas is null)
        {
            return;
        }

        _baseCanvas.Width = settings.CanvasWidth;
        _baseCanvas.Height = settings.CanvasHeight;
    }

    private void OnV3HostLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeBoModeChanged();
        if (!_hasLoadedV3Layout)
        {
            _ = ReloadFrontedLayoutAsync();
        }
    }

    private void OnV3HostUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeBoModeChanged();
        DetachBehaviorRuntime();
    }

    private void OnV3HostClosed(object? sender, EventArgs e)
    {
        UnsubscribeBoModeChanged();
        DetachBehaviorRuntime();
        IsVisibleChanged -= OnV3HostIsVisibleChanged;
    }

    private void OnV3HostIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_isV3LayoutHost)
        {
            return;
        }

        if (IsVisible)
        {
            SubscribeBoModeChanged();
            return;
        }

        UnsubscribeBoModeChanged();
        DetachBehaviorRuntime();
    }

    private void SubscribeBoModeChanged()
    {
        if (_sharedDataService is null || _isBoModeSubscribed)
        {
            return;
        }

        _sharedDataService.IsBo3ModeChanged += OnBoModeChanged;
        _isBoModeSubscribed = true;
    }

    private void UnsubscribeBoModeChanged()
    {
        if (_sharedDataService is null || !_isBoModeSubscribed)
        {
            return;
        }

        _sharedDataService.IsBo3ModeChanged -= OnBoModeChanged;
        _isBoModeSubscribed = false;
    }

    private void OnBoModeChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.CheckAccess())
        {
            _ = ReloadFrontedLayoutAsync();
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() => _ = ReloadFrontedLayoutAsync()));
    }

    private void DetachBehaviorRuntime()
    {
        if (_v3Descriptor is null || _behaviorRuntime is null)
        {
            return;
        }

        _ = _behaviorRuntime.DetachAsync(_v3Descriptor.WindowId);
    }

    private static bool TryCreateBackgroundBrush(string? colorText, out Brush brush)
    {
        brush = Brushes.Transparent;
        if (string.IsNullOrWhiteSpace(colorText))
        {
            return false;
        }

        try
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorText)!);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0D;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <inheritdoc/>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
