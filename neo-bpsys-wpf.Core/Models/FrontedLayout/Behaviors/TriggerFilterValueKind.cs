using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 触发过滤器右侧值的类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerFilterValueKind
{
    /// <summary>
    /// 字面量。
    /// </summary>
    Literal,

    /// <summary>
    /// 事件路径。
    /// </summary>
    EventPath,

    /// <summary>
    /// 绑定路径。
    /// </summary>
    BindingPath
}
