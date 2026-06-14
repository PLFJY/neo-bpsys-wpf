#nullable enable

namespace neo_bpsys_wpf.Controls.Modern.Navigation;

/// <summary>
/// 定义 <see cref="ModernNavigationView"/> 的导航行为模式。
/// </summary>
public enum ModernNavigationBehavior
{
    /// <summary>
    /// 页面导航模式，使用 Frame 进行页面跳转。
    /// </summary>
    PageNavigation = 0,

    /// <summary>
    /// 本地标签模式，内容在同一页面内切换。
    /// </summary>
    LocalTabs = 1
}
