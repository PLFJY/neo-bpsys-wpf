using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedBehaviorKind
{
    /// <summary>
    /// 在匹配的事件已经发生之后运行。
    /// </summary>
    OneShot,

    /// <summary>
    /// 根据匹配的生命周期触发器启动和停止。
    /// </summary>
    Loop,

    /// <summary>
    /// 在业务状态变更前运行退出图，在变更提交后运行进入图。
    /// </summary>
    Transition
}
