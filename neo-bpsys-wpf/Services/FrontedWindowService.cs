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

        RegisterFrontedWindowAndCanvas();
    }

    public void RegisterFrontedWindowAndCanvas(string windowId, Window window, string[]? canvasNames = null)
    {
        if (FrontedWindows.TryAdd(windowId, window))
        {
            FrontedWindowStates[windowId] = false;
        }
    }

    private void RegisterFrontedWindowAndCanvas()
    {
        foreach (var descriptor in _windowRegistry.GetWindows())
        {
            try
            {
                var window = CreateWindow(descriptor);
                if (window is null)
                {
                    continue;
                }

                RegisterFrontedWindowAndCanvas(descriptor.WindowId, window);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to register fronted window {FullWindowType} ({WindowId}).",
                    descriptor.FullWindowType,
                    descriptor.WindowId);
            }
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
        foreach (var window in FrontedWindows.Where(pair => !FrontedWindowStates[pair.Key]).ToArray())
        {
            await PrepareWindowForShowAsync(window.Key, window.Value);
            if (FrontedWindowStates[window.Key])
            {
                continue;
            }

            ApplyWindowLayoutOptions(window.Key, window.Value);
            window.Value.Show();
            FrontedWindowStates[window.Key] = true;
            PublishWindowShown(window.Key);
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
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowCloseError"));
            return;
        }

        if (!FrontedWindowStates[windowId])
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
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowLaunchError"));
            _logger.LogError("Unregistered window type {WindowId}", windowId);
            return;
        }

        if (FrontedWindowStates[windowId])
        {
            window.Activate();
            return;
        }

        ApplyWindowLayoutOptions(windowId, window);
        await PrepareWindowForShowAsync(windowId, window);
        if (FrontedWindowStates[windowId])
        {
            return;
        }

        window.Show();
        FrontedWindowStates[windowId] = true;
        PublishWindowShown(windowId);
    }

    private async Task PrepareWindowForShowAsync(string windowId, Window window)
    {
        if (!_windowRegistry.TryGetByWindowId(windowId, out var descriptor)
            || !descriptor.IsV3LayoutWindow
            || window is not FrontedWindowBase frontedWindow)
        {
            return;
        }

        try
        {
            await frontedWindow.ReloadFrontedLayoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to prepare v3 fronted window before show. Window: {FullWindowType}",
                descriptor.FullWindowType);
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

    public bool ApplyWindowBackgroundColor(string fullWindowType)
    {
        if (!_windowRegistry.TryGetByFullWindowType(fullWindowType, out var descriptor)
            || !FrontedWindows.TryGetValue(descriptor.WindowId, out var window))
        {
            return false;
        }

        var options = _windowLayoutOptionsService.LoadOptions(descriptor.FullWindowType);
        if (!TryCreateBackgroundBrush(options.BackgroundColor, out var brush))
        {
            return false;
        }

        void Apply() => window.SetCurrentValue(Window.BackgroundProperty, brush);

        if (window.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            window.Dispatcher.Invoke(Apply);
        }

        return true;
    }

    public bool ApplyWindowSize(string fullWindowType)
    {
        if (!_windowRegistry.TryGetByFullWindowType(fullWindowType, out var descriptor)
            || !FrontedWindows.TryGetValue(descriptor.WindowId, out var window))
        {
            return false;
        }

        var options = _windowLayoutOptionsService.LoadOptions(descriptor.FullWindowType);
        if (options.WindowWidth is null && options.WindowHeight is null)
        {
            return false;
        }

        void Apply()
        {
            if (options.WindowWidth is { } w && w > 0 && double.IsFinite(w))
            {
                window.Width = w;
            }

            if (options.WindowHeight is { } h && h > 0 && double.IsFinite(h))
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
            window.Dispatcher.Invoke(Apply);
        }

        return true;
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
        foreach (var window in FrontedWindows.Values)
        {
            var method = window.GetType().GetMethod("ReloadFrontedLayoutAsync");
            if (method is null)
            {
                continue;
            }

            try
            {
                if (method.Invoke(window, null) is Task task)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reload fronted v3 layout for {WindowType}.", window.GetType().Name);
            }
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
