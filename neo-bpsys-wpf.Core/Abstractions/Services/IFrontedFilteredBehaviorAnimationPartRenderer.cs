using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System;
using System.Collections.Generic;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 可只重应用指定行为动画部件的前台动画部件渲染能力。
/// </summary>
/// <remarks>
/// 用于局部刷新单个控件时避免遍历并重建全部动画部件；不支持该能力时调用方回退
/// <see cref="IFrontedBehaviorAnimationPartRenderer.ApplyAnimationParts(FrameworkElement, FrontedBehaviorDocument)"/>。
/// 实现必须在拥有 <c>root</c> 的 UI Dispatcher 线程调用，并校验根元素与行为文档非空。
/// </remarks>
public interface IFrontedFilteredBehaviorAnimationPartRenderer
{
    /// <summary>
    /// 只应用指定行为 GUID 的动画部件；集合为 <see langword="null"/> 时应用全部行为。
    /// </summary>
    /// <param name="root">已渲染的前台根元素。</param>
    /// <param name="behaviorDocument">行为文档。</param>
    /// <param name="behaviorGuids">需要应用的行为 GUID 集合；为 <see langword="null"/> 时应用全部。</param>
    void ApplyAnimationParts(
        FrameworkElement root,
        FrontedBehaviorDocument behaviorDocument,
        IReadOnlySet<Guid>? behaviorGuids);
}
