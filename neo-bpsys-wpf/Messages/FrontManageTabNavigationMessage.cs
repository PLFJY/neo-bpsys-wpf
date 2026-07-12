namespace neo_bpsys_wpf.Messages;

/// <summary>
/// 请求前台管理页切换其本地选项卡。
/// </summary>
/// <param name="TabKey">目标本地选项卡键。</param>
public sealed record FrontManageTabNavigationMessage(string TabKey)
{
    /// <summary>
    /// 布局包管理选项卡的键。
    /// </summary>
    public const string LayoutPackagesTabKey = "LayoutPackages";
}
