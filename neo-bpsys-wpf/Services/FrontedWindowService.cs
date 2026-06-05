using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
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

    public Dictionary<string, Window> FrontedWindows { get; private set; } = [];

    public Dictionary<string, bool> FrontedWindowStates { get; private set; } = [];

    public List<(string, string)> FrontedCanvas { get; private set; } = [];

    public FrontedWindowService(
        IServiceProvider services,
        IFrontedWindowRegistry windowRegistry,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        ILogger<FrontedWindowService> logger)
    {
        _services = services;
        _windowRegistry = windowRegistry;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _logger = logger;
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }

        RegisterFrontedWindowAndCanvas();
    }

    public void RegisterFrontedWindowAndCanvas(string windowId, Window window, string[]? canvasNames = null)
    {
        canvasNames ??= ["BaseCanvas"];

        if (FrontedWindows.TryAdd(windowId, window))
        {
            FrontedWindowStates[windowId] = false;
        }

        foreach (var canvasName in canvasNames)
        {
            if (!FrontedCanvas.Contains((windowId, canvasName)))
            {
                FrontedCanvas.Add((windowId, canvasName));
            }
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

                RegisterFrontedWindowAndCanvas(
                    descriptor.WindowId,
                    window,
                    descriptor.Canvases.Select(canvas => canvas.CanvasName).ToArray());
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

    private Window? CreateWindow(IFrontedWindowDescriptor descriptor)
    {
        return descriptor switch
        {
            FrontedBuiltInWindowDescriptor builtIn => CreateXamlWindow(builtIn.WindowType, null),
            FrontedPluginWindowDescriptor { Kind: FrontedWindowKind.PluginXaml } pluginXaml =>
                CreateXamlWindow(pluginXaml.WindowType, pluginXaml.ViewModelType),
            FrontedPluginWindowDescriptor { Kind: FrontedWindowKind.PluginLayout } pluginLayout =>
                ActivatorUtilities.CreateInstance<FrontedPluginLayoutWindow>(_services, pluginLayout),
            _ => null
        };
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

    public void AllWindowShow()
    {
        foreach (var window in FrontedWindows.Where(pair => !FrontedWindowStates[pair.Key]))
        {
            ApplyWindowLayoutOptions(window.Key, window.Value);
            window.Value.Show();
            FrontedWindowStates[window.Key] = true;
        }
    }

    public void AllWindowHide()
    {
        foreach (var window in FrontedWindows.Where(pair => FrontedWindowStates[pair.Key]))
        {
            window.Value.Hide();
            FrontedWindowStates[window.Key] = false;
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
    }

    public void ShowWindow(FrontedWindowType windowType)
    {
        ShowWindow(FrontedWindowHelper.GetFrontedWindowGuid(windowType));
    }

    public void ShowWindow(string windowId)
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
        window.Show();
        FrontedWindowStates[windowId] = true;
    }

    private void ApplyWindowLayoutOptions(string windowId, Window window)
    {
        if (!_windowRegistry.TryGetByWindowId(windowId, out var descriptor))
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

}
