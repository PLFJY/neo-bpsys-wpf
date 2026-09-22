namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 指定行为事件由哪一条运行时触发链路消费。
/// </summary>
[Flags]
public enum FrontedBehaviorEventUsage
{
    /// <summary>
    /// 不支持任何行为触发器。
    /// </summary>
    None = 0,

    /// <summary>
    /// 通过统一事件总线触发，可用于 OneShot、Loop StartTrigger 和 Loop StopTrigger。
    /// </summary>
    EventBus = 1,

    /// <summary>
    /// 可用于由转场编排器驱动的 Transition 行为触发器。
    /// </summary>
    Transition = 2,

    /// <summary>
    /// 全部已定义触发器位置。
    /// </summary>
    All = EventBus | Transition
}
