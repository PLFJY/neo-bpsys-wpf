using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static neo_bpsys_wpf.Core.Helpers.FrontedWindowHelper;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 前台窗口服务, 实现了 <see cref="IFrontedWindowService"/> 接口，负责与前台窗口进行交互
/// </summary>
public class FrontedWindowService : IFrontedWindowService
{
    /// <summary>
    /// 前台窗口列表
    /// </summary>
    public Dictionary<string, Window> FrontedWindows { get; private set; } = [];

    /// <summary>
    /// 前台窗口状态列表
    /// </summary>
    public Dictionary<string, bool> FrontedWindowStates { get; private set; } = [];

    /// <summary>
    /// 前台画布列表
    /// </summary>
    public List<(string, string)> FrontedCanvas { get; private set; } = []; // 窗口ID, 画布名称

    private readonly ILogger<FrontedWindowService> _logger;

    public FrontedWindowService(
        BpWindow bpWindow,
        CutSceneWindow cutSceneWindow,
        GameDataWindow gameDataWindow,
        ScoreSurWindow scoreSurWindow,
        ScoreHunWindow scoreHunWindow,
        ScoreGlobalWindow scoreGlobalWindow,
        WidgetsWindow widgetsWindow,
        ISharedDataService sharedDataService,
        ISettingsHostService settingsHostService,
        ILogger<FrontedWindowService> logger
    )
    {
        _logger = logger;
        if (!Directory.Exists(AppConstants.AppDataPath)) Directory.CreateDirectory(AppConstants.AppDataPath);

        // 注册窗口和画布
        RegisterFrontedWindowAndCanvas();

        //加载后期注入的控件
        LoadInjectedControl();
    }

    /// <summary>
    /// 加载后期注入的控件
    /// </summary>
    private void LoadInjectedControl()
    {
        foreach (var info in FrontedWindowRegistryService.InjectedControls)
        {
            InjectControl(info.TargetWindow, info.TargetCanvas, info.Control, info.DefaultInfo);
        }
    }

    /// <summary>
    /// 注册窗口和画布
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="window">窗口</param>
    /// <param name="canvasNames">画布名称</param>
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
                FrontedCanvas.Add((windowId, canvasName));
        }
    }

    private void RegisterFrontedWindowAndCanvas()
    {
        var windowInfos = FrontedWindowRegistryService.RegisteredWindow;

        foreach (var info in windowInfos)
        {
            if (info.WindowType != null)
                RegisterFrontedWindowAndCanvas(info.Id,
                    IAppHost.Host?.Services.GetRequiredService(info.WindowType) as Window ??
                    throw new InvalidOperationException(),
                    info.Canvas.Select(x => x.Name).ToArray());
        }
    }


    public string? GetWindowName(FrontedWindowType windowType)
    {
        return GetWindowName(GetFrontedWindowGuid(windowType));
    }

    public string? GetWindowName(string windowId)
    {
        FrontedWindows.TryGetValue(windowId, out var window);
        return window?.GetType().Name;
    }

    public FrameworkElement GetInjectedControl(string guid)
    {
        var control = FrontedWindowRegistryService.InjectedControls
            .First(x => x.Id == guid).Control;

        return control;
    }

    /// <summary>
    /// 注入控件
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="canvasName">画布名称</param>
    /// <param name="control">控件</param>
    /// <param name="defaultInfo">默认位置信息</param>
    public void InjectControl(string windowId, string canvasName, FrameworkElement control, ElementInfo defaultInfo)
    {
        if (FrontedWindows.TryGetValue(windowId, out var window))
        {
            var canvas = window.FindName(canvasName) as Canvas;
            if (defaultInfo.Width != null)
                control.Width = (double)defaultInfo.Width;
            if (defaultInfo.Height != null)
                control.Height = (double)defaultInfo.Height;
            if (defaultInfo.Top != null)
                Canvas.SetTop(control, (double)defaultInfo.Top);
            if (defaultInfo.Left != null)
                Canvas.SetLeft(control, (double)defaultInfo.Left);

            canvas?.Children.Add(control);
        }
        else
        {
            _logger.LogError("Window {WindowId} not found.", windowId);
        }
    }

    #region 窗口显示/隐藏管理

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

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType"></param>
    public void HideWindow(FrontedWindowType windowType)
    {
        HideWindow(GetFrontedWindowGuid(windowType));
    }

    /// <inheritdoc/>
    public void HideWindow(string windowId)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowCloseError"));
            return;
        }

        if (!FrontedWindowStates[windowId]) return;
        window.Hide();
        FrontedWindowStates[windowId] = false;
    }

    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="windowType"></param>
    public void ShowWindow(FrontedWindowType windowType)
    {
        ShowWindow(GetFrontedWindowGuid(windowType));
    }

    /// <inheritdoc/>
    public void ShowWindow(string windowId)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowLaunchError"));
            _logger.LogError("Unregistered window type{WindowId}", windowId);
            return;
        }

        if (FrontedWindowStates[windowId])
        {
            window.Activate();
            return;
        }
        else
        {
            window.Show();
            FrontedWindowStates[windowId] = true;
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

    #endregion

    #region 分数统计

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void SetGlobalScore(TeamType team, GameProgress gameProgress, Camp camp, int score)
    {
        // Compatibility adapter: ScoreGlobalWindow is rendered from CurrentGame.MatchScore by v3 controls.
    }

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void SetGlobalScoreToBar(TeamType team, GameProgress gameProgress)
    {
        // Compatibility adapter: empty half display is derived from CurrentGame.MatchScore by v3 controls.
    }

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void ResetGlobalScore()
    {
        // Compatibility adapter: callers should clear CurrentGame.MatchScore through IMatchScoreService.
    }

    #endregion

    #region 动画

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在 3.0.0 中移除。")]
    public void FadeInAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        FadeInAnimation(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在 3.0.0 中移除。")]
    public void FadeInAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;

        if (window.FindName(ctrName) is FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5))));
        }
    }

    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    public void FadeOutAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        FadeOutAnimation(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    public void FadeOutAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        if (window.FindName(ctrName) is FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5))));
        }
    }

    /// <summary>
    /// 呼吸动画开始
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStart(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        await BreathingStart(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStart(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        if (window.FindName(ctrName) is not FrameworkElement element) return;

        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.25))));
        await Task.Delay(250);

        // 如果已有动画，先停止
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
        element.Tag = storyboard; // 用于后续停止动画
    }

    /// <summary>
    /// 停止呼吸动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStop(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        await BreathingStop(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStop(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        if (window.FindName(ctrName) is not FrameworkElement element) return;
        if (element.Tag is not Storyboard storyboard) return;

        storyboard.Stop();
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.25))));
        await Task.Delay(250);

        element.Opacity = 0; // 恢复初始状态
        element.Tag = null;
        element.Visibility = Visibility.Hidden;
    }

    #endregion
}
