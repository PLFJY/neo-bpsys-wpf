using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 触发过滤器比较运算符。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerFilterOperator
{
    /// <summary>
    /// 等于。
    /// </summary>
    Equals,

    /// <summary>
    /// 不等于。
    /// </summary>
    NotEquals,

    /// <summary>
    /// 大于。
    /// </summary>
    GreaterThan,

    /// <summary>
    /// 大于等于。
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// 小于。
    /// </summary>
    LessThan,

    /// <summary>
    /// 小于等于。
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// 包含。
    /// </summary>
    Contains,

    /// <summary>
    /// 不包含。
    /// </summary>
    NotContains,

    /// <summary>
    /// 存在。
    /// </summary>
    Exists
}
