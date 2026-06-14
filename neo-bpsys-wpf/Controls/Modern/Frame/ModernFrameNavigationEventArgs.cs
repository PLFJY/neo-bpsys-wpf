#nullable enable

using System;
using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

/// <summary>
/// 为 <see cref="ModernFrame"/> 导航完成事件提供数据。
/// </summary>
public class ModernFrameNavigationEventArgs : EventArgs
{
    /// <summary>
    /// 初始化 <see cref="ModernFrameNavigationEventArgs"/> 的新实例。
    /// </summary>
    /// <param name="content">导航到的内容元素。</param>
    /// <param name="parameter">导航参数。</param>
    /// <param name="navigationMode">导航模式。</param>
    /// <param name="transitionInfo">过渡信息。</param>
    public ModernFrameNavigationEventArgs(
        FrameworkElement content,
        object? parameter,
        ModernFrameNavigationMode navigationMode,
        ModernNavigationTransitionInfo? transitionInfo)
    {
        Content = content;
        Parameter = parameter;
        NavigationMode = navigationMode;
        TransitionInfo = transitionInfo;
    }

    /// <summary>
    /// 获取导航到的内容元素。
    /// </summary>
    public FrameworkElement Content { get; }

    /// <summary>
    /// 获取导航参数。
    /// </summary>
    public object? Parameter { get; }

    /// <summary>
    /// 获取导航模式。
    /// </summary>
    public ModernFrameNavigationMode NavigationMode { get; }

    /// <summary>
    /// 获取过渡信息。
    /// </summary>
    public ModernNavigationTransitionInfo? TransitionInfo { get; }
}
