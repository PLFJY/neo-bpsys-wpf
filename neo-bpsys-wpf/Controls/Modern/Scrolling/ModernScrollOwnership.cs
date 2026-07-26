namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 定义 <see cref="ModernScroll"/> 的所有权模式。
/// </summary>
public enum ModernScrollOwnership
{
    /// <summary>
    /// 自动确定滚动所有权。
    /// </summary>
    Auto,

    /// <summary>
    /// 由 <see cref="ModernFrame"/> 管理滚动。
    /// </summary>
    Frame,

    /// <summary>
    /// 由控件自身管理滚动。
    /// </summary>
    Self
}
