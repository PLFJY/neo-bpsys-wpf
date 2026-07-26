namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 前台布局包验证状态。
/// </summary>
public enum FrontedLayoutPackageValidationStatus
{
    /// <summary>
    /// 验证通过。
    /// </summary>
    Valid,

    /// <summary>
    /// 存在警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 存在错误。
    /// </summary>
    Error
}
