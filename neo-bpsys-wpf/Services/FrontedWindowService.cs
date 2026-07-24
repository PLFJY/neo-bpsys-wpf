using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Helpers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 前台窗口服务，负责与前台窗口进行交互。
/// </summary>
public class FrontedWindowService : IFrontedWindowService
{
#if DEBUG
    static FrontedWindowService()
    {
        Debug.WriteLine($"[DIAG] FrontedWindowService: static ctor at {DateTimeOffset.Now:HH:mm:ss.fff}");
    }
#endif
    private readonly IServiceProvider _services;
    private readonly IFrontedWindowRegistry _windowRegistry;
    private readonly IFrontedWindowLayoutOptionsService _windowLayoutOptionsService;
    private readonly ILogger<FrontedWindowService> _logger;
    private readonly IFrontedEventBus? _eventBus;

    /// <summary>
    /// 前台窗口字典，键为窗口 ID。
    /// </summary>
    public Dictionary<string, Window> FrontedWindows { get; private set; } = [];

    /// <summary>
    /// 前台窗口状态字典，键为窗口 ID，值为窗口是否可见。
    /// </summary>
    public Dictionary<string, bool> FrontedWindowStates { get; private set; } = [];

    /// <summary>
    /// 初始化前台窗口服务。
    /// </summary>
    /// <param name="services">服务提供者。</param>
    /// <param name="windowRegistry">窗口注册表。</param>
    /// <param name="windowLayoutOptionsService">窗口布局选项服务。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="eventBus">前台事件总线（可选）。</param>
    public FrontedWindowService(
        IServiceProvider services,
        IFrontedWindowRegistry windowRegistry,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        ILogger<FrontedWindowService> logger,
        IFrontedEventBus? eventBus = null)
    {
        _services = services;
        _windowRegistry = windowRegistry;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _logger = logger;
        _eventBus = eventBus;
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }

#if DEBUG
        Debug.WriteLine($"[DIAG] FrontedWindowService: lazy fronted window creation enabled at {DateTimeOffset.Now:HH:mm:ss.fff}");
#endif
    }

    /// <inheritdoc/>
    public Window? EnsureWindowCreated(string windowId)
    {
        if (FrontedWindows.TryGetValue(windowId, out var existingWindow))
        {
            return existingWindow;
        }

        if (!_windowRegistry.TryGet(windowId, out var registration))
        {
            return null;
        }

        try
        {
            var window = CreateWindow(registration);
            if (window is null)
            {
                return null;
            }

            RegisterFrontedWindow(registration.Id, window);
            return window;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create fronted window {WindowId}.",
                registration.Id);
            return null;
        }
    }

    private void RegisterFrontedWindow(string windowId, Window window)
    {
        if (FrontedWindows.TryAdd(windowId, window))
        {
            FrontedWindowStates[windowId] = false;
        }
    }

    /// <summary>
    /// 根据窗口注册的承载方式创建对应的前台窗口实例。
    /// </summary>
    /// <param name="registration">窗口注册，携带承载方式（Xaml/V3Layout）和来源信息。</param>
    /// <returns>创建的 <see cref="Window"/> 实例；无法识别时返回 <c>null</c>。</returns>
    /// <remarks>
    /// 分派依据 <see cref="FrontedWindowRegistration.Kind"/>：
    /// <list type="number">
    ///   <item><description><see cref="FrontedWindowRegistrationKind.V3Layout"/> —
    /// v3 layout host 窗口（含内置 v3 窗口和插件 v3 窗口），走 <see cref="CreateV3LayoutHostWindow"/>。</description></item>
    ///   <item><description><see cref="FrontedWindowRegistrationKind.Xaml"/> —
    /// XAML 窗口（含内置与插件），通过 DI 创建窗口并设置 ViewModel 为 DataContext。</description></item>
    ///   <item><description>其他未知类型 — 返回 <c>null</c>，由调用方跳过注册。</description></item>
    /// </list>
    /// </remarks>
    private Window? CreateWindow(FrontedWindowRegistration registration)
    {
        return registration switch
        {
            // 模式 1：v3 layout host 窗口（含内置 v3 窗口和插件 v3 窗口）
            FrontedV3LayoutWindowRegistration v3 => CreateV3LayoutHostWindow(v3),

            // 模式 2：XAML 窗口（含内置与插件）— 创建窗口并设置 ViewModel 为 DataContext
            FrontedXamlWindowRegistration xaml => CreateXamlWindow(xaml.WindowType, xaml.ViewModelType),

            // 模式 3：无法识别的注册，跳过
            _ => null
        };
    }

    private Window CreateV3LayoutHostWindow(FrontedWindowRegistration registration)
    {
        var window = new FrontedWindowBase();
        window.InitializeV3LayoutHost(
            registration,
            _services.GetRequiredService<IFrontedLayoutService>(),
            _services.GetRequiredService<IFrontedRenderer>(),
            _services.GetRequiredService<ISharedDataService>(),
            _services.GetService<IFrontedBehaviorRuntime>(),
            _services.GetService<ILogger<FrontedWindowBase>>(),
            _services.GetService<ISettingsHostService>());
        return window;
    }

    private Window? CreateXamlWindow(Type? windowType, Type? viewModelType)
    {
        if (windowType is null || !typeof(Window).IsAssignableFrom(windowType))
        {
            return null;
        }

        var window = (_services.GetService(windowType)
                      ?? ActivatorUtilities.CreateInstance(_services, windowType)) as Window;
        if (window is null)
        {
            return null;
        }

        if (viewModelType is not null)
        {
            window.DataContext = _services.GetService(viewModelType)
                                 ?? ActivatorUtilities.CreateInstance(_services, viewModelType);
        }

        return window;
    }

    /// <summary>
    /// 获取指定前台窗口类型的显示名称。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    /// <returns>显示名称；未找到时返回 <see langword="null"/>。</returns>
    public string? GetWindowName(FrontedWindowType windowType)
    {
        return GetWindowName(FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType));
    }

    public string? GetWindowName(string windowId)
    {
        if (_windowRegistry.TryGet(windowId, out var registration))
        {
            var settings = _services.GetService<ISettingsHostService>()?.Settings;
            return FrontedWindowDisplayNameResolver.ResolveDisplayName(
                registration,
                settings?.Language ?? LanguageKey.System,
                settings?.CultureInfo);
        }

        FrontedWindows.TryGetValue(windowId, out var window);
        return window?.GetType().Name;
    }

    /// <summary>
    /// 显示所有前台窗口。
    /// </summary>
    public async void AllWindowShow()
    {
        foreach (var registration in _windowRegistry.GetWindows())
        {
            await ShowWindowAsync(registration.Id);
        }
    }

    /// <summary>
    /// 隐藏所有前台窗口。
    /// </summary>
    public void AllWindowHide()
    {
        foreach (var window in FrontedWindows.Where(pair => FrontedWindowStates[pair.Key]))
        {
            window.Value.Hide();
            FrontedWindowStates[window.Key] = false;
            PublishWindowHidden(window.Key);
        }
    }

    /// <summary>
    /// 隐藏指定类型的前台窗口。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    public void HideWindow(FrontedWindowType windowType)
    {
        HideWindow(FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType));
    }

    public void HideWindow(string windowId)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            if (!_windowRegistry.TryGet(windowId, out _))
            {
                _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowCloseError"));
            }

            return;
        }

        if (!FrontedWindowStates.GetValueOrDefault(windowId))
        {
            return;
        }

        window.Hide();
        FrontedWindowStates[windowId] = false;
        PublishWindowHidden(windowId);
    }

    /// <summary>
    /// 显示指定类型的前台窗口。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    public void ShowWindow(FrontedWindowType windowType)
    {
        ShowWindow(FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType));
    }

    /// <summary>
    /// 显示指定 ID 的前台窗口。
    /// </summary>
    /// <param name="windowId">窗口 ID。</param>
    public void ShowWindow(string windowId)
    {
        _ = ShowWindowAsync(windowId).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Failed to show window {WindowId}", windowId);
            }
        }, TaskScheduler.Default);
    }

    private async Task ShowWindowAsync(string windowId)
    {
        var window = EnsureWindowCreated(windowId);
        if (window is null)
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowLaunchError"));
            _logger.LogError("Unregistered window type {WindowId}", windowId);
            return;
        }

        if (FrontedWindowStates.GetValueOrDefault(windowId))
        {
            window.Show();
            window.Activate();
            return;
        }

        if (window is FrontedWindowBase frontedWindow)
        {
            await frontedWindow.EnsureInitialWindowSettingsAppliedAsync();
        }

        ApplyWindowLayoutOptions(windowId, window);
        window.Show();
        FrontedWindowStates[windowId] = true;
        PublishWindowShown(windowId);

        if (window is FrontedWindowBase shownFrontedWindow)
        {
            _ = LoadFrontedContentAfterShowAsync(windowId, shownFrontedWindow);
        }
    }

    private async Task LoadFrontedContentAfterShowAsync(string windowId, FrontedWindowBase frontedWindow)
    {
        try
        {
            await frontedWindow.LoadOrReloadContentAsync(force: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load v3 fronted window content after show. WindowId: {WindowId}",
                windowId);
        }
    }

    private void ApplyWindowLayoutOptions(string windowId, Window window)
    {
        if (!_windowRegistry.TryGet(windowId, out var registration))
        {
            return;
        }

        if (registration.Kind == FrontedWindowRegistrationKind.V3Layout)
        {
            return;
        }

        if (!File.Exists(_windowLayoutOptionsService.GetUserOptionsPath(registration.Id)))
        {
            return;
        }

        var options = _windowLayoutOptionsService.LoadOptions(registration.Id);
        try
        {
            window.AllowsTransparency = options.AllowTransparency;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(
                ex,
                "Fronted window transparency option could not be applied after source creation. Window: {WindowId}",
                registration.Id);
        }

        if (!TryCreateBackgroundBrush(options.BackgroundColor, out var brush))
        {
            return;
        }

        window.SetCurrentValue(Window.BackgroundProperty, brush);
    }

    /// <summary>
    /// 应用指定窗口的背景色。
    /// </summary>
    /// <param name="fullWindowType">完整窗口类型名。</param>
    /// <returns>成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public async Task<bool> ApplyWindowBackgroundColorAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !FrontedWindows.TryGetValue(registration.Id, out var window))
        {
            return false;
        }

        var isV3 = registration.Kind == FrontedWindowRegistrationKind.V3Layout;
        var backgroundColor = isV3
            ? (await LoadV3WindowSettingsAsync(registration.Id))?.BackgroundColor
            : _windowLayoutOptionsService.LoadOptions(registration.Id).BackgroundColor;
        if (!TryCreateBackgroundBrush(backgroundColor, out var brush))
        {
            brush = Brushes.Transparent;
        }

        void Apply() => window.SetCurrentValue(Window.BackgroundProperty, brush);

        if (window.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            await window.Dispatcher.InvokeAsync(Apply);
        }

        return true;
    }

    /// <summary>
    /// 应用指定窗口的尺寸。
    /// </summary>
    /// <param name="fullWindowType">完整窗口类型名。</param>
    /// <returns>成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public async Task<bool> ApplyWindowSizeAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !FrontedWindows.TryGetValue(registration.Id, out var window))
        {
            return false;
        }

        var isV3 = registration.Kind == FrontedWindowRegistrationKind.V3Layout;
        var v3Settings = isV3
            ? await LoadV3WindowSettingsAsync(registration.Id)
            : null;
        var options = isV3
            ? null
            : _windowLayoutOptionsService.LoadOptions(registration.Id);
        var width = v3Settings?.WindowWidth ?? options?.WindowWidth;
        var height = v3Settings?.WindowHeight ?? options?.WindowHeight;
        if (width is null && height is null)
        {
            return false;
        }

        void Apply()
        {
            if (width is { } w && w > 0 && double.IsFinite(w))
            {
                window.Width = w;
            }

            if (height is { } h && h > 0 && double.IsFinite(h))
            {
                window.Height = h;
            }
        }

        if (window.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            await window.Dispatcher.InvokeAsync(Apply);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RestartWindowForTransparencyChangeAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !FrontedWindows.TryGetValue(registration.Id, out var oldWindow))
        {
            return false;
        }

        if (oldWindow.Dispatcher.CheckAccess())
        {
            return await RestartWindowForTransparencyChangeOnDispatcherAsync(registration, oldWindow);
        }

        return await oldWindow.Dispatcher
            .InvokeAsync(() => RestartWindowForTransparencyChangeOnDispatcherAsync(registration, oldWindow))
            .Task
            .Unwrap();
    }

    private async Task<bool> RestartWindowForTransparencyChangeOnDispatcherAsync(
        FrontedWindowRegistration registration,
        Window oldWindow)
    {
        var windowId = registration.Id;
        if (!FrontedWindows.TryGetValue(windowId, out var currentWindow)
            || !ReferenceEquals(currentWindow, oldWindow))
        {
            return false;
        }

        var wasShown = FrontedWindowStates.GetValueOrDefault(windowId);

        FrontedWindows.Remove(windowId);
        FrontedWindowStates.Remove(windowId);

        if (wasShown)
        {
            PublishWindowHidden(windowId);
        }

        CloseFrontedWindowInstance(oldWindow);

        if (!wasShown)
        {
            return true;
        }

        var newWindow = EnsureWindowCreated(windowId);
        if (newWindow is null)
        {
            _logger.LogWarning(
                "Failed to recreate fronted window after transparency change. WindowId: {WindowId}",
                windowId);
            return true;
        }

        if (newWindow is FrontedWindowBase frontedWindow)
        {
            await frontedWindow.EnsureInitialWindowSettingsAppliedAsync();
        }

        ApplyWindowLayoutOptions(windowId, newWindow);
        newWindow.Show();
        FrontedWindowStates[windowId] = true;
        PublishWindowShown(windowId);

        if (newWindow is FrontedWindowBase shownFrontedWindow)
        {
            _ = LoadFrontedContentAfterShowAsync(windowId, shownFrontedWindow);
        }

        return true;
    }

    private static void CloseFrontedWindowInstance(Window window)
    {
        if (window is FrontedWindowBase frontedWindow)
        {
            frontedWindow.RequestServiceClose();
            return;
        }

        window.Close();
    }

    private async Task<FrontedWindowSettings?> LoadV3WindowSettingsAsync(string fullWindowType)
    {
        try
        {
            var config = await _services.GetRequiredService<IFrontedLayoutService>()
                .LoadWindowConfigAsync(fullWindowType);
            return config?.WindowSettings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load v3 fronted window settings. Window: {FullWindowType}", fullWindowType);
            return null;
        }
    }

    public (double Width, double Height)? GetWindowSize(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !FrontedWindows.TryGetValue(registration.Id, out var window))
        {
            return null;
        }

        var width = window.Width;
        var height = window.Height;

        if (double.IsNaN(width) || double.IsNaN(height)
            || width <= 0 || height <= 0)
        {
            return null;
        }

        return (width, height);
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

    public async Task ReloadFrontedLayoutsAsync()
    {
        _services.GetService<IFrontedResourceResolver>()?.ClearCache();

        foreach (var pair in FrontedWindows.ToArray())
        {
            if (pair.Value is not FrontedWindowBase frontedWindow
                || !_windowRegistry.TryGet(pair.Key, out var registration)
                || registration.Kind != FrontedWindowRegistrationKind.V3Layout)
            {
                continue;
            }

            try
            {
                var requestedTransparency = await frontedWindow.GetRequestedAllowsTransparencyAsync();
                if (requestedTransparency.HasValue
                    && requestedTransparency.Value != frontedWindow.AllowsTransparency)
                {
                    await RestartWindowForTransparencyChangeAsync(registration.Id);
                    continue;
                }

                await frontedWindow.ReloadFrontedLayoutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reload fronted v3 layout for {WindowId}.", registration.Id);
            }
        }
    }

    /// <inheritdoc/>
    public void MarkWindowLayoutDirty(string windowIdOrFullWindowType)
    {
        if (string.IsNullOrWhiteSpace(windowIdOrFullWindowType))
        {
            return;
        }

        var windowId = windowIdOrFullWindowType;
        if (_windowRegistry.TryGet(windowIdOrFullWindowType, out var registration))
        {
            windowId = registration.Id;
        }

        if (FrontedWindows.TryGetValue(windowId, out var window)
            && window is FrontedWindowBase frontedWindow)
        {
            frontedWindow.MarkLayoutDirty();
        }
    }

    private void PublishWindowShown(string windowId)
    {
        try
        {
            _eventBus?.Publish(new FrontedBehaviorEvent
            {
                EventType = "WindowShown",
                WindowId = windowId,
                Source = "WindowLifecycle",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish WindowShown event for {WindowId}.", windowId);
        }
    }

    private void PublishWindowHidden(string windowId)
    {
        try
        {
            _eventBus?.Publish(new FrontedBehaviorEvent
            {
                EventType = "WindowHidden",
                WindowId = windowId,
                Source = "WindowLifecycle",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish WindowHidden event for {WindowId}.", windowId);
        }
    }

}
