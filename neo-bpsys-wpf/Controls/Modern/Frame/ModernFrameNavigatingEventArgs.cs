#nullable enable

using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

/// <summary>
/// 为 <see cref="ModernFrame"/> 正在导航事件提供数据，支持取消导航。
/// </summary>
public sealed class ModernFrameNavigatingEventArgs : ModernFrameNavigationEventArgs
{
    /// <summary>
    /// 初始化 <see cref="ModernFrameNavigatingEventArgs"/> 的新实例。
    /// </summary>
    /// <param name="content">将要导航到的内容元素。</param>
    /// <param name="parameter">导航参数。</param>
    /// <param name="navigationMode">导航模式。</param>
    /// <param name="transitionInfo">过渡信息。</param>
    public ModernFrameNavigatingEventArgs(
        FrameworkElement content,
        object? parameter,
        ModernFrameNavigationMode navigationMode,
        ModernNavigationTransitionInfo? transitionInfo)
        : base(content, parameter, navigationMode, transitionInfo)
    {
    }

    /// <summary>
    /// 获取或设置一个值，指示是否应取消导航。
    /// </summary>
    public bool Cancel { get; set; }
}
