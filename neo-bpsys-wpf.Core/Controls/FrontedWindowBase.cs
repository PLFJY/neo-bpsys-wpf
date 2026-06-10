using System.ComponentModel;
using System.Threading;
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
    private bool _hasInitialWindowSettingsApplied;
    private bool _allowServiceClose;
    private FrontedWindowConfig? _lastRenderedConfig;
    private readonly SemaphoreSlim _layoutLoadGate = new(1, 1);

    private bool _isInternalContentChange = false;

    /// <summary>
    /// Gets whether the v3 content controls have been rendered at least once.
    /// </summary>
    public bool IsContentRendered { get; private set; }

    /// <summary>
    /// Gets whether the loaded v3 layout should be re-rendered before reuse.
    /// </summary>
    public bool IsLayoutDirty { get; private set; }

    /// <summary>
    /// Gets whether the behavior runtime is currently attached to this window.
    /// </summary>
    public bool IsBehaviorAttached { get; private set; }

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
        await EnsureInitialWindowSettingsAppliedAsync();
        await LoadOrReloadContentAsync(force: true);
    }

    /// <summary>
    /// Applies only the v3 window settings required before a WPF source is created.
    /// </summary>
    /// <returns>A task that completes when the initial window settings are applied.</returns>
    public async Task EnsureInitialWindowSettingsAppliedAsync()
    {
        if (!_isV3LayoutHost
            || _v3Descriptor is null
            || _layoutService is null
            || _hasInitialWindowSettingsApplied)
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

            await RunOnDispatcherAsync(() =>
            {
                ApplyWindowSettings(config.WindowSettings);
                _hasInitialWindowSettingsApplied = true;
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to apply initial fronted v3 window settings. Window: {WindowTypeName}",
                _v3Descriptor.FullWindowType);
        }
    }

    /// <summary>
    /// Loads and renders the v3 layout content, or reuses rendered controls when they are still clean.
    /// </summary>
    /// <param name="force">Whether to force a full reload even when the rendered content is clean.</param>
    /// <returns>A task that completes when content is rendered or reused.</returns>
    public async Task LoadOrReloadContentAsync(bool force = false)
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

        await _layoutLoadGate.WaitAsync();
        try
        {
            if (!force && IsContentRendered && !IsLayoutDirty)
            {
                if (!IsBehaviorAttached)
                {
                    await AttachBehaviorRuntimeAsync();
                }

                return;
            }

            var config = await _layoutService.LoadWindowConfigAsync(_v3Descriptor.FullWindowType);
            if (config is null)
            {
                _logger?.LogWarning(
                    "Fronted v3 window layout config not found. Window: {WindowTypeName}",
                    _v3Descriptor.FullWindowType);
                return;
            }

            await RunOnDispatcherAsync(async () =>
            {
                ApplyCanvasSettings(config.CanvasSettings);
                await DetachBehaviorRuntimeAsync();
                _renderer.RenderToCanvas(_baseCanvas, config, new FrontedRenderContext
                {
                    WindowId = _v3Descriptor.WindowId,
                    WindowTypeName = _v3Descriptor.FullWindowType,
                    CanvasName = FrontedLayoutConstants.BaseCanvasName
                });

                _lastRenderedConfig = config;
                IsContentRendered = true;
                IsLayoutDirty = false;
                await AttachBehaviorRuntimeAsync();
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to reload fronted v3 window layout. Window: {WindowTypeName}",
                _v3Descriptor.FullWindowType);
        }
        finally
        {
            _layoutLoadGate.Release();
        }
    }

    /// <summary>
    /// Marks the rendered v3 layout content dirty so the next load refreshes controls.
    /// </summary>
    public void MarkLayoutDirty()
    {
        IsLayoutDirty = true;
    }

    /// <summary>
    /// Requests a real close from the owning window service, bypassing the normal close-to-hide behavior.
    /// </summary>
    public void RequestServiceClose()
    {
        _allowServiceClose = true;
        Close();
    }

    /// <summary>
    /// Attaches the behavior runtime to already rendered v3 content.
    /// </summary>
    /// <returns>A task that completes when the behavior runtime is attached.</returns>
    public async Task AttachBehaviorRuntimeAsync()
    {
        if (!_isV3LayoutHost
            || _v3Descriptor is null
            || _sharedDataService is null
            || _baseCanvas is null
            || _behaviorRuntime is null
            || _lastRenderedConfig is null
            || !IsVisible
            || IsBehaviorAttached)
        {
            return;
        }

        await RunOnDispatcherAsync(async () =>
        {
            await _behaviorRuntime.AttachAsync(new FrontedBehaviorRuntimeContext
            {
                WindowId = _v3Descriptor.WindowId,
                WindowType = _v3Descriptor.FullWindowType,
                RootCanvas = _baseCanvas,
                WindowConfig = _lastRenderedConfig,
                SharedDataService = _sharedDataService,
                IsDesignerPreview = false,
                Logger = _logger
            });
            IsBehaviorAttached = true;
        });
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
        MarkLayoutDirty();

        if (!IsVisible)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            _ = LoadOrReloadContentAsync();
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() => _ = LoadOrReloadContentAsync()));
    }

    private void DetachBehaviorRuntime()
    {
        _ = DetachBehaviorRuntimeAsync();
    }

    private async Task DetachBehaviorRuntimeAsync()
    {
        if (_v3Descriptor is null || _behaviorRuntime is null || !IsBehaviorAttached)
        {
            return;
        }

        await _behaviorRuntime.DetachAsync(_v3Descriptor.WindowId);
        IsBehaviorAttached = false;
    }

    private Task RunOnDispatcherAsync(Func<Task> action)
    {
        if (Dispatcher.CheckAccess())
        {
            return action();
        }

        return Dispatcher.InvokeAsync(action).Task.Unwrap();
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
        if (_allowServiceClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
