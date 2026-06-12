using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Windows;
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

    public Dictionary<string, Window> FrontedWindows { get; private set; } = [];

    public Dictionary<string, bool> FrontedWindowStates { get; private set; } = [];

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

        if (!_windowRegistry.TryGetByWindowId(windowId, out var descriptor))
        {
            return null;
        }

        try
        {
            var window = CreateWindow(descriptor);
            if (window is null)
            {
                return null;
            }

            RegisterFrontedWindow(descriptor.WindowId, window);
            return window;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create fronted window {FullWindowType} ({WindowId}).",
                descriptor.FullWindowType,
                descriptor.WindowId);
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
    /// 根据窗口描述符的分派策略创建对应的前台窗口实例。
    /// </summary>
    /// <param name="descriptor">窗口描述符，携带来源（内置/插件）和布局模式信息。</param>
    /// <returns>创建的 <see cref="Window"/> 实例；无法识别时返回 <c>null</c>。</returns>
    /// <remarks>
    /// 分派优先级：
    /// <list type="number">
    ///   <item><description><see cref="IFrontedWindowDescriptor.IsV3LayoutWindow"/> 为 <c>true</c> —
    /// 任何 descriptor（内置或插件）只要声明使用 v3 layout host 渲染，都走 <see cref="CreateV3LayoutHostWindow"/>。</description></item>
    ///   <item><description><see cref="FrontedBuiltInWindowDescriptor"/> — 非 v3 的内置窗口，
    /// 直接通过 DI 创建 XAML 窗口，不绑定 ViewModel。</description></item>
    ///   <item><description><see cref="FrontedPluginWindowDescriptor"/> 且 <see cref="FrontedWindowKind.PluginXaml"/> —
    /// 插件提供的纯 XAML 窗口，通过 DI 创建窗口并设置 ViewModel 为 DataContext。</description></item>
    ///   <item><description>其他未知类型 — 返回 <c>null</c>，由调用方跳过注册。</description></item>
    /// </list>
    /// </remarks>
    private Window? CreateWindow(IFrontedWindowDescriptor descriptor)
    {
        return descriptor switch
        {
            // 模式 1：v3 layout host 窗口（含内置 v3 窗口和插件 PluginLayout 窗口）
            { IsV3LayoutWindow: true } => CreateV3LayoutHostWindow(descriptor),

            // 模式 2：非 v3 的内置窗口 — 创建 XAML 窗口，无 ViewModel
            FrontedBuiltInWindowDescriptor builtIn => CreateXamlWindow(builtIn.WindowType, null),

            // 模式 3：插件提供的纯 XAML 窗口 — 创建窗口并设置 ViewModel 为 DataContext
            FrontedPluginWindowDescriptor { Kind: FrontedWindowKind.PluginXaml } pluginXaml =>
                CreateXamlWindow(pluginXaml.WindowType, pluginXaml.ViewModelType),

            // 模式 4：无法识别的描述符，跳过
            _ => null
        };
    }

    private Window CreateV3LayoutHostWindow(IFrontedWindowDescriptor descriptor)
    {
        var window = new FrontedWindowBase();
        window.InitializeV3LayoutHost(
            descriptor,
            _services.GetRequiredService<IFrontedLayoutService>(),
            _services.GetRequiredService<IFrontedRenderer>(),
            _services.GetRequiredService<ISharedDataService>(),
            _services.GetService<IFrontedBehaviorRuntime>(),
            _services.GetService<ILogger<FrontedWindowBase>>());
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

    public string? GetWindowName(FrontedWindowType windowType)
    {
        return GetWindowName(FrontedWindowHelper.GetFrontedWindowGuid(windowType));
    }

    public string? GetWindowName(string windowId)
    {
        if (_windowRegistry.TryGetByWindowId(windowId, out var descriptor))
        {
            return descriptor.DisplayName;
        }

        FrontedWindows.TryGetValue(windowId, out var window);
        return window?.GetType().Name;
    }

    public async void AllWindowShow()
    {
        foreach (var descriptor in _windowRegistry.GetWindows())
        {
            await ShowWindowAsync(descriptor.WindowId);
        }
    }

    public void AllWindowHide()
    {
        foreach (var window in FrontedWindows.Where(pair => FrontedWindowStates[pair.Key]))
        {
            window.Value.Hide();
            FrontedWindowStates[window.Key] = false;
            PublishWindowHidden(window.Key);
        }
    }

    public void HideWindow(FrontedWindowType windowType)
    {
        HideWindow(FrontedWindowHelper.GetFrontedWindowGuid(windowType));
    }

    public void HideWindow(string windowId)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            if (!_windowRegistry.TryGetByWindowId(windowId, out _))
            {
                _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowCloseError"));
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

    public void ShowWindow(FrontedWindowType windowType)
    {
        ShowWindow(FrontedWindowHelper.GetFrontedWindowGuid(windowType));
    }

    public async void ShowWindow(string windowId)
    {
        await ShowWindowAsync(windowId);
    }

    private async Task ShowWindowAsync(string windowId)
    {
        var window = EnsureWindowCreated(windowId);
        if (window is null)
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowLaunchError"));
            _logger.LogError("Unregistered window type {WindowId}", windowId);
            return;
        }

        if (FrontedWindowStates.GetValueOrDefault(windowId))
        {
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
        if (!_windowRegistry.TryGetByWindowId(windowId, out var descriptor))
        {
            return;
        }

        if (descriptor.IsV3LayoutWindow)
        {
            return;
        }

        if (!File.Exists(_windowLayoutOptionsService.GetUserOptionsPath(descriptor.FullWindowType)))
        {
            return;
        }

        var options = _windowLayoutOptionsService.LoadOptions(descriptor.FullWindowType);
        try
        {
            window.AllowsTransparency = options.AllowTransparency;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(
                ex,
                "Fronted window transparency option could not be applied after source creation. Window: {FullWindowType}",
                descriptor.FullWindowType);
        }

        if (!TryCreateBackgroundBrush(options.BackgroundColor, out var brush))
        {
            return;
        }

        window.SetCurrentValue(Window.BackgroundProperty, brush);
    }

    public async Task<bool> ApplyWindowBackgroundColorAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGetByFullWindowType(fullWindowType, out var descriptor)
            || !FrontedWindows.TryGetValue(descriptor.WindowId, out var window))
        {
            return false;
        }

        var backgroundColor = descriptor.IsV3LayoutWindow
            ? (await LoadV3WindowSettingsAsync(descriptor.FullWindowType))?.BackgroundColor
            : _windowLayoutOptionsService.LoadOptions(descriptor.FullWindowType).BackgroundColor;
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

    public async Task<bool> ApplyWindowSizeAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGetByFullWindowType(fullWindowType, out var descriptor)
            || !FrontedWindows.TryGetValue(descriptor.WindowId, out var window))
        {
            return false;
        }

        var v3Settings = descriptor.IsV3LayoutWindow
            ? await LoadV3WindowSettingsAsync(descriptor.FullWindowType)
            : null;
        var options = descriptor.IsV3LayoutWindow
            ? null
            : _windowLayoutOptionsService.LoadOptions(descriptor.FullWindowType);
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
        if (!_windowRegistry.TryGetByFullWindowType(fullWindowType, out var descriptor)
            || !FrontedWindows.TryGetValue(descriptor.WindowId, out var oldWindow))
        {
            return false;
        }

        if (oldWindow.Dispatcher.CheckAccess())
        {
            return await RestartWindowForTransparencyChangeOnDispatcherAsync(descriptor, oldWindow);
        }

        return await oldWindow.Dispatcher
            .InvokeAsync(() => RestartWindowForTransparencyChangeOnDispatcherAsync(descriptor, oldWindow))
            .Task
            .Unwrap();
    }

    private async Task<bool> RestartWindowForTransparencyChangeOnDispatcherAsync(
        IFrontedWindowDescriptor descriptor,
        Window oldWindow)
    {
        var windowId = descriptor.WindowId;
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
                "Failed to recreate fronted window after transparency change. Window: {FullWindowType}, WindowId: {WindowId}",
                descriptor.FullWindowType,
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
        if (!_windowRegistry.TryGetByFullWindowType(fullWindowType, out var descriptor)
            || !FrontedWindows.TryGetValue(descriptor.WindowId, out var window))
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
                || !_windowRegistry.TryGetByWindowId(pair.Key, out var descriptor)
                || !descriptor.IsV3LayoutWindow)
            {
                continue;
            }

            try
            {
                var requestedTransparency = await frontedWindow.GetRequestedAllowsTransparencyAsync();
                if (requestedTransparency.HasValue
                    && requestedTransparency.Value != frontedWindow.AllowsTransparency)
                {
                    await RestartWindowForTransparencyChangeAsync(descriptor.FullWindowType);
                    continue;
                }

                await frontedWindow.ReloadFrontedLayoutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reload fronted v3 layout for {WindowType}.", descriptor.FullWindowType);
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
        if (_windowRegistry.TryGetByFullWindowType(windowIdOrFullWindowType, out var descriptor))
        {
            windowId = descriptor.WindowId;
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
