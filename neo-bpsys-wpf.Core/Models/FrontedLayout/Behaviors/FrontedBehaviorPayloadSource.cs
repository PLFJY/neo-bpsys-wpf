namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为负载数据来源。
/// </summary>
public enum FrontedBehaviorPayloadSource
{
    /// <summary>
    /// 无来源。
    /// </summary>
    None,

    /// <summary>
    /// 发送者属性。
    /// </summary>
    SenderProperty,

    /// <summary>
    /// 服务属性。
    /// </summary>
    ServiceProperty,

    /// <summary>
    /// 事件参数属性。
    /// </summary>
    EventArgsProperty,

    /// <summary>
    /// 常量值。
    /// </summary>
    Constant
}
