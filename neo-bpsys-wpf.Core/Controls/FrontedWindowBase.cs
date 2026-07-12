using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Helpers;
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
    private ISettingsHostService? _settingsHostService;
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
    /// 获取 v3 内容控件是否已至少渲染过一次。
    /// </summary>
    public bool IsContentRendered { get; private set; }

    /// <summary>
    /// 获取已加载的 v3 布局在复用前是否需要重新渲染。
    /// </summary>
    public bool IsLayoutDirty { get; private set; }

    /// <summary>
    /// 获取行为运行时是否已附加到此窗口。
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
            ApplyFrontedRenderQualityOptions(viewbox);

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
    /// 将此窗口初始化为由配置驱动的 v3 布局宿主。
    /// </summary>
    /// <param name="descriptor">前台窗口描述符。</param>
    /// <param name="layoutService">布局服务。</param>
    /// <param name="renderer">前台渲染器。</param>
    /// <param name="sharedDataService">共享数据服务。</param>
    /// <param name="behaviorRuntime">可选的行为运行时。</param>
    /// <param name="logger">可选的日志记录器。</param>
    /// <param name="settingsHostService">可选的设置宿主服务，用于刷新本地化的窗口标题。</param>
    /// <exception cref="ArgumentNullException">当必选参数为 null 时抛出。</exception>
    public void InitializeV3LayoutHost(
        IFrontedWindowDescriptor descriptor,
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService,
        IFrontedBehaviorRuntime? behaviorRuntime,
        ILogger? logger,
        ISettingsHostService? settingsHostService = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(layoutService);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sharedDataService);

        _v3Descriptor = descriptor;
        _layoutService = layoutService;
        _renderer = renderer;
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        _behaviorRuntime = behaviorRuntime;
        _logger = logger;
        _isV3LayoutHost = true;

        RefreshV3WindowTitle();

        _baseCanvas = new Canvas
        {
            Name = FrontedLayoutConstants.BaseCanvasName
        };
        ApplyFrontedRenderQualityOptions(_baseCanvas);

        var viewbox = new Viewbox
        {
            Name = "InternalAutoViewbox",
            Stretch = Stretch.Fill,
            Child = _baseCanvas
        };
        ApplyFrontedRenderQualityOptions(viewbox);

        Content = viewbox;

        Loaded += OnV3HostLoaded;
        Unloaded += OnV3HostUnloaded;
        Closed += OnV3HostClosed;
        IsVisibleChanged += OnV3HostIsVisibleChanged;
        if (_settingsHostService is not null)
        {
            _settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
        }
    }

    /// <summary>
    /// 重新加载以窗口为中心的 v3 布局和行为运行时。
    /// </summary>
    /// <returns>布局重新加载完成后完成的任务。</returns>
    public async Task ReloadFrontedLayoutAsync()
    {
        await ReloadWindowSettingsAsync();
        await LoadOrReloadContentAsync(force: true);
    }

    /// <summary>
    /// 获取当前活动 v3 布局所请求的透明度值。
    /// </summary>
    /// <returns>请求的透明度值；当没有可用的 v3 布局时返回 <c>null</c>。</returns>
    public async Task<bool?> GetRequestedAllowsTransparencyAsync()
    {
        if (!_isV3LayoutHost || _v3Descriptor is null || _layoutService is null)
        {
            return null;
        }

        var config = await _layoutService.LoadWindowConfigAsync(_v3Descriptor.FullWindowType);
        return config?.WindowSettings.AllowsTransparency;
    }

    /// <summary>
    /// 重新加载在 WPF 源创建后可安全更改的窗口设置。
    /// </summary>
    /// <returns>设置应用完成后完成的任务。</returns>
    public async Task ReloadWindowSettingsAsync()
    {
        if (!_isV3LayoutHost || _v3Descriptor is null || _layoutService is null)
        {
            return;
        }

        var config = await _layoutService.LoadWindowConfigAsync(_v3Descriptor.FullWindowType);
        if (config is null)
        {
            return;
        }

        await RunOnDispatcherAsync(() =>
        {
            ApplyWindowSettings(config.WindowSettings, includeSourceAffectingSettings: false);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 仅应用在 WPF 源创建前所需的 v3 窗口设置。
    /// </summary>
    /// <returns>初始窗口设置应用完成后完成的任务。</returns>
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
                ApplyWindowSettings(config.WindowSettings, includeSourceAffectingSettings: true);
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
    /// 加载并渲染 v3 布局内容，或在已渲染控件仍然可用时复用它们。
    /// </summary>
    /// <param name="force">是否强制完整重新加载，即使已渲染内容仍然可用。</param>
    /// <returns>内容渲染或复用完成后完成的任务。</returns>
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
                await DetachBehaviorRuntimeAsync(FrontedBehaviorStopReason.LayoutReloaded);
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
    /// 将已渲染的 v3 布局内容标记为脏，以便下次加载时刷新控件。
    /// </summary>
    public void MarkLayoutDirty()
    {
        IsLayoutDirty = true;
    }

    /// <summary>
    /// 请求所属窗口服务执行真正的关闭，绕过正常的关闭即隐藏行为。
    /// </summary>
    public void RequestServiceClose()
    {
        _allowServiceClose = true;
        Close();
    }

    /// <summary>
    /// 将行为运行时附加到已渲染的 v3 内容。
    /// </summary>
    /// <returns>行为运行时附加完成后完成的任务。</returns>
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

    private void ApplyWindowSettings(FrontedWindowSettings settings, bool includeSourceAffectingSettings)
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

        if (includeSourceAffectingSettings && PresentationSource.FromVisual(this) is null)
        {
            AllowsTransparency = settings.AllowsTransparency;
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
        ApplyFrontedRenderQualityOptions(_baseCanvas);
    }

    private static void ApplyFrontedRenderQualityOptions(FrameworkElement element)
    {
        element.UseLayoutRounding = true;
        element.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(element, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(element, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(element, ClearTypeHint.Enabled);
        RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.HighQuality);
    }

    private void OnV3HostLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeBoModeChanged();
    }

    private void OnV3HostUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeBoModeChanged();
        DetachBehaviorRuntime(FrontedBehaviorStopReason.WindowHidden);
    }

    private void OnV3HostClosed(object? sender, EventArgs e)
    {
        UnsubscribeBoModeChanged();
        if (_settingsHostService is not null)
        {
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
        }

        DetachBehaviorRuntime(FrontedBehaviorStopReason.WindowHidden);
        IsVisibleChanged -= OnV3HostIsVisibleChanged;
    }

    private void OnLanguageSettingChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            RefreshV3WindowTitle(e.CultureInfo);
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() => RefreshV3WindowTitle(e.CultureInfo)));
    }

    private void RefreshV3WindowTitle(System.Globalization.CultureInfo? cultureInfo = null)
    {
        if (_v3Descriptor is null)
        {
            return;
        }

        var settings = _settingsHostService?.Settings;
        Title = FrontedWindowDisplayNameResolver.ResolveDisplayName(
            _v3Descriptor,
            settings?.Language ?? LanguageKey.System,
            cultureInfo ?? settings?.CultureInfo);
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
        DetachBehaviorRuntime(FrontedBehaviorStopReason.WindowHidden);
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

    private void DetachBehaviorRuntime(FrontedBehaviorStopReason reason = FrontedBehaviorStopReason.WindowHidden)
    {
        _ = DetachBehaviorRuntimeAsync(reason);
    }

    private async Task DetachBehaviorRuntimeAsync(FrontedBehaviorStopReason reason = FrontedBehaviorStopReason.WindowHidden)
    {
        if (_v3Descriptor is null || _behaviorRuntime is null || !IsBehaviorAttached)
        {
            return;
        }

        await _behaviorRuntime.StopLoopBehaviorsAsync(_v3Descriptor.WindowId, reason);
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
