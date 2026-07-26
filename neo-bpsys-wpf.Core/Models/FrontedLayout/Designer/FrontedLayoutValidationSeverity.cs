namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// v3 前台布局设计期校验严重级别。
/// </summary>
public enum FrontedLayoutValidationSeverity
{
    /// <summary>
    /// 信息提示。
    /// </summary>
    Info,

    /// <summary>
    /// 警告，可由用户确认后继续。
    /// </summary>
    Warning,

    /// <summary>
    /// 错误，保存前必须修复。
    /// </summary>
    Error
}
