using System.Windows;
using System.Windows.Media.Animation;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using static neo_bpsys_wpf.Core.Helpers.FrontedWindowHelper;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 角色选择动画服务的默认实现
/// 依赖 <see cref="IFrontedWindowService"/> 执行底层动画效果
/// </summary>
public class AnimationService(IFrontedWindowService frontedWindowService) : IAnimationService
{
    private readonly IFrontedWindowService _frontedWindowService = frontedWindowService;
    private const int TransitionDelayMs = 250;

    #region 角色选择动画 (Pick)

    /// <inheritdoc/>
    public async Task PlayPickTransitionAsync(Camp camp, int index)
    {
        PlayPickFadeOut(camp, index);
        await Task.Delay(TransitionDelayMs);
        PlayPickFadeIn(camp, index);
    }

    /// <inheritdoc/>
    public void PlayPickFadeIn(Camp camp, int index)
    {
        var controlNameHeader = GetPickControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

        FadeInAnimation(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
    }

    /// <inheritdoc/>
    public void PlayPickFadeOut(Camp camp, int index)
    {
        var controlNameHeader = GetPickControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

        FadeOutAnimation(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
    }

    private static string GetPickControlName(Camp camp) => camp == Camp.Sur ? "SurPick" : "HunPick";

    #endregion

    #region 角色禁用动画 (Ban)

    /// <inheritdoc/>
    public Task PlayBanAnimationAsync(Camp camp, int index)
    {
        // 当前实现中 Ban 操作没有专门的动画效果
        // 此方法作为扩展点，供插件实现自定义效果
        return Task.CompletedTask;
    }

    #endregion

    #region 待选框呼吸灯动画 (Picking Border)

    /// <inheritdoc/>
    public async Task StartPickingBorderBreathingAsync(Camp camp, int index)
    {
        var controlNameHeader = GetPickingBorderControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

        await BreathingStart(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
    }

    /// <inheritdoc/>
    public async Task StopPickingBorderBreathingAsync(Camp camp, int index)
    {
        var controlNameHeader = GetPickingBorderControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

        await BreathingStop(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
    }

    private static string GetPickingBorderControlName(Camp camp) =>
        camp == Camp.Sur ? "SurPickingBorder" : "HunPickingBorder";

    #endregion

    #region 角色互换动画

    /// <inheritdoc/>
    public async Task PlaySwapCharacterAnimationAsync(int sourceIndex, int targetIndex)
    {
        // 同时淡出两个角色
        FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", sourceIndex, string.Empty);
        FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", targetIndex, string.Empty);

        await Task.Delay(TransitionDelayMs);

        // 同时淡入两个角色
        FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", sourceIndex, string.Empty);
        FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", targetIndex, string.Empty);
    }

    #endregion

    #region 动画预设

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    public void FadeInAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        FadeInAnimation(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    /// <inheritdoc/>
    public void FadeInAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!_frontedWindowService.FrontedWindows.TryGetValue(windowId, out var window)) return;

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
    public void FadeOutAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        FadeOutAnimation(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    /// <inheritdoc/>
    public void FadeOutAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!_frontedWindowService.FrontedWindows.TryGetValue(windowId, out var window)) return;
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
    public async Task BreathingStart(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        await BreathingStart(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    /// <inheritdoc/>
    public async Task BreathingStart(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!_frontedWindowService.FrontedWindows.TryGetValue(windowId, out var window)) return;
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
    public async Task BreathingStop(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        await BreathingStop(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    /// <inheritdoc/>
    public async Task BreathingStop(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!_frontedWindowService.FrontedWindows.TryGetValue(windowId, out var window)) return;
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