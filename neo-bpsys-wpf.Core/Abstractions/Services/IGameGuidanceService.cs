using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 对局引导服务接口
/// </summary>
public interface IGameGuidanceService
{
    /// <summary>
    /// 是否启动引导
    /// </summary>
    bool IsGuidanceStarted { get; set; }

    /// <summary>
    /// 引导状态变化事件
    /// </summary>
    [FrontedBehaviorEvent("Guidance.StateChanged", DisplayNameKey = "Designer.Behaviors.Event.GuidanceStateChanged", DescriptionKey = "Designer.Behaviors.Event.GuidanceStateChanged.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.IsStarted", DisplayNameKey = "Designer.Behaviors.Payload.IsStarted", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.IsStarted), TypeName = "bool")]
    event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStateChanged;

    /// <summary>
    /// 引导启动事件。
    /// </summary>
    [FrontedBehaviorEvent("Guidance.Started", DisplayNameKey = "Designer.Behaviors.Event.GuidanceStarted", DescriptionKey = "Designer.Behaviors.Event.GuidanceStarted.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.IsStarted", DisplayNameKey = "Designer.Behaviors.Payload.IsStarted", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.IsStarted), TypeName = "bool")]
    event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStarted;

    /// <summary>
    /// 引导停止事件。
    /// </summary>
    [FrontedBehaviorEvent("Guidance.Stopped", DisplayNameKey = "Designer.Behaviors.Event.GuidanceStopped", DescriptionKey = "Designer.Behaviors.Event.GuidanceStopped.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.IsStarted", DisplayNameKey = "Designer.Behaviors.Payload.IsStarted", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.IsStarted), TypeName = "bool")]
    [FrontedBehaviorEventPayload("Event.Reason", DisplayNameKey = "Designer.Behaviors.Payload.Reason", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.Reason), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.Time", DisplayNameKey = "Designer.Behaviors.Payload.Time", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.Time), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousStepIndex", DisplayNameKey = "Designer.Behaviors.Payload.PreviousStepIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousStepIndex), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousAction", DisplayNameKey = "Designer.Behaviors.Payload.PreviousAction", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousAction), TypeName = "GameAction?")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexes", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexes", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousIndexes), TypeName = "int[]")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexesText", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexesText", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousIndexesText), TypeName = "string")]
    event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStopped;

    /// <summary>
    /// 引导取消事件。
    /// </summary>
    [FrontedBehaviorEvent("Guidance.Cancelled", DisplayNameKey = "Designer.Behaviors.Event.GuidanceCancelled", DescriptionKey = "Designer.Behaviors.Event.GuidanceCancelled.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.Reason", DisplayNameKey = "Designer.Behaviors.Payload.Reason", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.Reason), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.Time", DisplayNameKey = "Designer.Behaviors.Payload.Time", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.Time), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousStepIndex", DisplayNameKey = "Designer.Behaviors.Payload.PreviousStepIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousStepIndex), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousAction", DisplayNameKey = "Designer.Behaviors.Payload.PreviousAction", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousAction), TypeName = "GameAction?")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexes", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexes", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousIndexes), TypeName = "int[]")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexesText", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexesText", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStateChangedEventArgs.PreviousIndexesText), TypeName = "string")]
    event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceCancelled;

    /// <summary>
    /// 引导步骤变化事件
    /// </summary>
    [FrontedBehaviorEvent("Guidance.StepChanged", DisplayNameKey = "Designer.Behaviors.Event.GuidanceStepChanged", DescriptionKey = "Designer.Behaviors.Event.GuidanceStepChanged.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.StepIndex", DisplayNameKey = "Designer.Behaviors.Payload.StepIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.StepIndex), TypeName = "int")]
    [FrontedBehaviorEventPayload("Event.Action", DisplayNameKey = "Designer.Behaviors.Payload.Action", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.Action), TypeName = "GameAction")]
    [FrontedBehaviorEventPayload("Event.Indexes", DisplayNameKey = "Designer.Behaviors.Payload.Indexes", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.Indexes), TypeName = "int[]")]
    [FrontedBehaviorEventPayload("Event.IndexesText", DisplayNameKey = "Designer.Behaviors.Payload.IndexesText", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.IndexesText), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.Index", DisplayNameKey = "Designer.Behaviors.Payload.Index", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.FirstIndex), TypeName = "int")]
    [FrontedBehaviorEventPayload("Event.Time", DisplayNameKey = "Designer.Behaviors.Payload.Time", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.Time), TypeName = "int")]
    [FrontedBehaviorEventPayload("Event.PreviousStepIndex", DisplayNameKey = "Designer.Behaviors.Payload.PreviousStepIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousStepIndex), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousAction", DisplayNameKey = "Designer.Behaviors.Payload.PreviousAction", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousAction), TypeName = "GameAction?")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexes", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexes", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousIndexes), TypeName = "int[]")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexesText", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexesText", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousIndexesText), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.PreviousIndex", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousFirstIndex), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousTime", DisplayNameKey = "Designer.Behaviors.Payload.PreviousTime", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousTime), TypeName = "int?")]
    event EventHandler<GameGuidanceStepChangedEventArgs>? GuidanceStepChanged;

    /// <summary>
    /// 引导高亮变化事件
    /// </summary>
    event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightChanged;

    /// <summary>
    /// 引导高亮清除事件。
    /// </summary>
    event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightCleared;

    /// <summary>
    /// 启动对局引导
    /// </summary>
    /// <param name="isNavigatePageEnable">是否开启页面导航</param>
    /// <returns>错误信息，如果启动成功则返回 <c>null</c></returns>
    Task<string?> StartGuidance(bool isNavigatePageEnable = true);

    /// <summary>
    /// 下一步
    /// </summary>
    /// <param name="isNavigatePageEnable">是否开启页面导航</param>
    /// <returns>错误信息，如果执行成功则返回 <c>null</c></returns>
    Task<string?> NextStepAsync(bool isNavigatePageEnable = true);

    /// <summary>
    /// 上一步
    /// </summary>
    /// <returns>错误信息，如果执行成功则返回 <c>null</c></returns>
    Task<string?> PrevStepAsync(bool isNavigatePageEnable = true);

    /// <summary>Gets an immutable snapshot of the active workflow and current step.</summary>
    GameGuidanceRuntimeSnapshot GetRuntimeSnapshot() => new(false, -1, null, [], null, []);

    /// <summary>Moves to a validated workflow step through the normal guidance transition path.</summary>
    /// <param name="stepIndex">Target workflow step index.</param>
    /// <param name="isNavigatePageEnable">Is enable page switch</param>
    /// <returns>Error/display text following the existing guidance command convention.</returns>
    Task<string?> MoveToStepAsync(int stepIndex, bool isNavigatePageEnable = true) => Task.FromResult<string?>(null);

    /// <summary>
    /// 停止对局引导
    /// </summary>
    void StopGuidance();

    /// <summary>
    /// Completes the active game guidance workflow without treating it as a user cancellation.
    /// </summary>
    /// <param name="reason">Completion reason published with the stopped event.</param>
    void CompleteGuidance(string reason = "SmartBpCharacterBpEnded") => StopGuidance();
}
