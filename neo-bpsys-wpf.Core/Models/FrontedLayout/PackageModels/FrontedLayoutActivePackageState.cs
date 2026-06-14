namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 当前激活的前台布局包状态。
/// </summary>
public sealed class FrontedLayoutActivePackageState
{
    /// <summary>
    /// 包标识符，默认为 "builtin"。
    /// </summary>
    public string PackageId { get; set; } = "builtin";

    /// <summary>
    /// 激活时间。
    /// </summary>
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
}
