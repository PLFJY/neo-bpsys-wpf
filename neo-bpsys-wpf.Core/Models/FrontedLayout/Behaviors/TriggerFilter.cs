namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 触发过滤器，用于对事件负载进行条件匹配。
/// </summary>
public sealed class TriggerFilter
{
    /// <summary>
    /// 左侧表达式（通常为事件负载路径）。
    /// </summary>
    public string Left { get; set; } = string.Empty;

    /// <summary>
    /// 比较运算符。
    /// </summary>
    public TriggerFilterOperator Operator { get; set; } = TriggerFilterOperator.Equals;

    /// <summary>
    /// 右侧值。
    /// </summary>
    public string? Right { get; set; }

    /// <summary>
    /// 右侧值的类型（字面量、事件路径或绑定路径）。
    /// </summary>
    public TriggerFilterValueKind RightValueKind { get; set; } = TriggerFilterValueKind.Literal;
}

