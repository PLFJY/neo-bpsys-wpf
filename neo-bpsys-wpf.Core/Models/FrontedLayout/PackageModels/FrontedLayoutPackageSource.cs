namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 前台布局包的来源类型。
/// </summary>
public enum FrontedLayoutPackageSource
{
    /// <summary>
    /// 内置包。
    /// </summary>
    BuiltIn,

    /// <summary>
    /// 已安装的包。
    /// </summary>
    Installed,

    /// <summary>
    /// 本地包。
    /// </summary>
    Local
}
