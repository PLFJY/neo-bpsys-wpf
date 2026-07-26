using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为定义，包含触发条件、节点图和策略配置。
/// </summary>
public sealed class FrontedBehavior
{
    /// <summary>
    /// 行为唯一标识符。
    /// </summary>
    public Guid BehaviorId { get; set; } = FrontedBehaviorGuidHelper.NewGuid();

    /// <summary>
    /// 行为名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 行为类型（OneShot、Loop 或 Transition）。
    /// </summary>
    public FrontedBehaviorKind Kind { get; set; } = FrontedBehaviorKind.OneShot;

    /// <summary>
    /// 触发条件描述。
    /// </summary>
    public TriggerDescriptor? Trigger { get; set; }

    /// <summary>
    /// 行为执行的节点图。
    /// </summary>
    public FrontedNodeGraph Graph { get; set; } = new();

    /// <summary>
    /// Loop 行为的启动触发条件。
    /// </summary>
    public TriggerDescriptor? StartTrigger { get; set; }

    /// <summary>
    /// Loop 行为启动时执行的节点图。
    /// </summary>
    public FrontedNodeGraph StartGraph { get; set; } = new();

    /// <summary>
    /// Loop 行为循环执行的节点图。
    /// </summary>
    public FrontedNodeGraph LoopGraph { get; set; } = new();

    /// <summary>
    /// 获取或设置循环行为的停止触发条件。任一匹配的触发条件都会停止循环。
    /// </summary>
    public List<TriggerDescriptor> StopTriggers { get; set; } = [];

    /// <summary>
    /// Loop 行为停止时执行的节点图。
    /// </summary>
    public FrontedNodeGraph StopGraph { get; set; } = new();

    /// <summary>
    /// 获取或设置过渡行为匹配使用的触发条件描述符。
    /// </summary>
    public TriggerDescriptor? TransitionTrigger { get; set; }

    /// <summary>
    /// 获取或设置在业务状态变更提交前运行的节点图。
    /// </summary>
    public FrontedNodeGraph ExitGraph { get; set; } = new();

    /// <summary>
    /// 获取或设置在业务状态变更提交后运行的节点图。
    /// </summary>
    public FrontedNodeGraph EnterGraph { get; set; } = new();

    /// <summary>
    /// OneShot 行为的重入策略。Loop 行为的重入策略在 <see cref="LoopPolicy"/> 中配置。
    /// </summary>
    public FrontedReentryPolicy ReentryPolicy { get; set; } = FrontedReentryPolicy.InterruptPrevious;

    /// <summary>
    /// Loop 行为的循环策略配置。
    /// </summary>
    public FrontedLoopPolicy LoopPolicy { get; set; } = new();
}

