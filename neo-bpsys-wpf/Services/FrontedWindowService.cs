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
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 前台窗口服务，负责与前台窗口进行交互。
/// </summary>
public class FrontedWindowService : IFrontedWindowService
{
    private readonly IServiceProvider _services;
    private readonly IFrontedWindowRegistry _windowRegistry;
    private readonly ILogger<FrontedWindowService> _logger;

    public Dictionary<string, Window> FrontedWindows { get; private set; } = [];

    public Dictionary<string, bool> FrontedWindowStates { get; private set; } = [];

    public List<(string, string)> FrontedCanvas { get; private set; } = [];

    public FrontedWindowService(
        IServiceProvider services,
        IFrontedWindowRegistry windowRegistry,
        ILogger<FrontedWindowService> logger)
    {
        _services = services;
        _windowRegistry = windowRegistry;
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

        window.Show();
        FrontedWindowStates[windowId] = true;
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

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void SetGlobalScore(TeamType team, GameProgress gameProgress, Camp camp, int score)
    {
    }

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void SetGlobalScoreToBar(TeamType team, GameProgress gameProgress)
    {
    }

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void ResetGlobalScore()
    {
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在 3.0.0 中移除。")]
    public void FadeInAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        FadeInAnimation(FrontedWindowHelper.GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在 3.0.0 中移除。")]
    public void FadeInAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var controlName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            return;
        }

        if (window.FindName(controlName) is FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)));
        }
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在 3.0.0 中移除。")]
    public void FadeOutAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        FadeOutAnimation(FrontedWindowHelper.GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    public void FadeOutAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var controlName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            return;
        }

        if (window.FindName(controlName) is FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5)));
        }
    }

    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStart(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        await BreathingStart(FrontedWindowHelper.GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStart(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var controlName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)
            || window.FindName(controlName) is not FrameworkElement element)
        {
            return;
        }

        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25)));
        await Task.Delay(250);

        await BreathingStop(windowId, controlNameHeader, controlIndex, controlNameFooter);

        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.25,
            Duration = TimeSpan.FromSeconds(1),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(element);
        element.Tag = storyboard;
    }

    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStop(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        await BreathingStop(FrontedWindowHelper.GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStop(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var controlName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)
            || window.FindName(controlName) is not FrameworkElement element
            || element.Tag is not Storyboard storyboard)
        {
            return;
        }

        storyboard.Stop();
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.25)));
        await Task.Delay(250);

        element.Opacity = 0;
        element.Tag = null;
        element.Visibility = Visibility.Hidden;
    }
}
