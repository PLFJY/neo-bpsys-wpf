#nullable enable

namespace neo_bpsys_wpf.Controls.Modern.Frame;

/// <summary>
/// 定义 <see cref="ModernFrame"/> 内容滚动宿主的行为模式。
/// </summary>
public enum ModernFrameContentScrollHostMode
{
    /// <summary>
    /// 启用内容滚动宿主。
    /// </summary>
    Enabled,

    /// <summary>
    /// 禁用内容滚动宿主。
    /// </summary>
    Disabled,

    /// <summary>
    /// 自动判断是否启用内容滚动宿主。
    /// </summary>
    Auto
}
