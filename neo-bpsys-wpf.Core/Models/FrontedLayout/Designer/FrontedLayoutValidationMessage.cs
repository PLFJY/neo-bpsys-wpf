namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// v3 前台布局设计期校验消息。
/// </summary>
public sealed class FrontedLayoutValidationMessage
{
    /// <summary>
    /// 严重级别。
    /// </summary>
    public FrontedLayoutValidationSeverity Severity { get; set; }

    /// <summary>
    /// 稳定消息代码，供 UI 分类和测试使用。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 面向维护者和编辑器 UI 的说明。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 关联控件名。Canvas 级消息为空。
    /// </summary>
    public string? ControlName { get; set; }

    /// <summary>
    /// 关联属性名。
    /// </summary>
    public string? PropertyName { get; set; }
}
