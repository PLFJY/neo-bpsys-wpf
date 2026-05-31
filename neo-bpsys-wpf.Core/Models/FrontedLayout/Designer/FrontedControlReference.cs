namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 一个前台控件配置字段对另一个控件名的引用。
/// </summary>
public sealed class FrontedControlReference
{
    /// <summary>
    /// 发起引用的控件名。
    /// </summary>
    public string SourceControlName { get; set; } = string.Empty;

    /// <summary>
    /// 引用字段名。
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 被引用的目标控件名。
    /// </summary>
    public string TargetControlName { get; set; } = string.Empty;
}
