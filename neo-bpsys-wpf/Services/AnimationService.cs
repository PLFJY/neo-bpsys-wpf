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
    private const int TransitionDelayMs = 250;

    #region 角色选择动画 (Pick)

    /// <inheritdoc/>
    public async Task PlayPickTransitionAsync(Camp camp, int index, string? windowId = null)
    {
        PlayPickFadeOut(camp, index, windowId);
        await Task.Delay(TransitionDelayMs);
        PlayPickFadeIn(camp, index, windowId);
    }

    /// <inheritdoc/>
    public void PlayPickFadeIn(Camp camp, int index, string? windowId = null)
    {
        var controlNameHeader = GetPickControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

#pragma warning disable CS0618 // 内部调用，已知过时
        if (windowId != null)
            frontedWindowService.FadeInAnimation(windowId, controlNameHeader, controlIndex, string.Empty);
        else
            frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
#pragma warning restore CS0618
    }

    /// <inheritdoc/>
    public void PlayPickFadeOut(Camp camp, int index, string? windowId = null)
    {
        var controlNameHeader = GetPickControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

#pragma warning disable CS0618 // 内部调用，已知过时
        if (windowId != null)
            frontedWindowService.FadeOutAnimation(windowId, controlNameHeader, controlIndex, string.Empty);
        else
            frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
#pragma warning restore CS0618
    }

    private static string GetPickControlName(Camp camp) => camp == Camp.Sur ? "SurPick" : "HunPick";

    #endregion

    #region 角色禁用动画 (Ban)

    /// <inheritdoc/>
    public Task PlayBanAnimationAsync(Camp camp, int index, BanType banType, string? windowId = null)
    {
        // 当前实现中 Ban 操作没有专门的动画效果
        // 此方法作为扩展点，供插件实现自定义效果
        return Task.CompletedTask;
    }

    #endregion

    #region 待选框呼吸灯动画 (Picking Border)

    /// <inheritdoc/>
    public async Task StartPickingBorderBreathingAsync(Camp camp, int index, string? windowId = null)
    {
        var controlNameHeader = GetPickingBorderControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

#pragma warning disable CS0618 // 内部调用，已知过时
        if (windowId != null)
            await frontedWindowService.BreathingStart(windowId, controlNameHeader, controlIndex, string.Empty);
        else
            await frontedWindowService.BreathingStart(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
#pragma warning restore CS0618
    }

    /// <inheritdoc/>
    public async Task StopPickingBorderBreathingAsync(Camp camp, int index, string? windowId = null)
    {
        var controlNameHeader = GetPickingBorderControlName(camp);
        var controlIndex = camp == Camp.Hun ? -1 : index;

#pragma warning disable CS0618 // 内部调用，已知过时
        if (windowId != null)
            await frontedWindowService.BreathingStop(windowId, controlNameHeader, controlIndex, string.Empty);
        else
            await frontedWindowService.BreathingStop(FrontedWindowType.BpWindow, controlNameHeader, controlIndex, string.Empty);
#pragma warning restore CS0618
    }

    private static string GetPickingBorderControlName(Camp camp) =>
        camp == Camp.Sur ? "SurPickingBorder" : "HunPickingBorder";

    #endregion

    #region 角色互换动画

    /// <inheritdoc/>
    public async Task PlaySwapCharacterAnimationAsync(int sourceIndex, int targetIndex, string? windowId = null)
    {
#pragma warning disable CS0618 // 内部调用，已知过时
        // 同时淡出两个角色
        if (windowId != null)
        {
            frontedWindowService.FadeOutAnimation(windowId, "SurPick", sourceIndex, string.Empty);
            frontedWindowService.FadeOutAnimation(windowId, "SurPick", targetIndex, string.Empty);
        }
        else
        {
            frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", sourceIndex, string.Empty);
            frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", targetIndex, string.Empty);
        }

        await Task.Delay(TransitionDelayMs);

        // 同时淡入两个角色
        if (windowId != null)
        {
            frontedWindowService.FadeInAnimation(windowId, "SurPick", sourceIndex, string.Empty);
            frontedWindowService.FadeInAnimation(windowId, "SurPick", targetIndex, string.Empty);
        }
        else
        {
            frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", sourceIndex, string.Empty);
            frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", targetIndex, string.Empty);
        }
#pragma warning restore CS0618
    }

    #endregion
}
