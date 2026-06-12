using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

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
    event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStopped;

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
    [FrontedBehaviorEventPayload("Event.ActionName", DisplayNameKey = "Designer.Behaviors.Payload.ActionName", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.ActionName), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.PreviousStepIndex", DisplayNameKey = "Designer.Behaviors.Payload.PreviousStepIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousStepIndex), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousAction", DisplayNameKey = "Designer.Behaviors.Payload.PreviousAction", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousAction), TypeName = "GameAction?")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexes", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexes", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousIndexes), TypeName = "int[]")]
    [FrontedBehaviorEventPayload("Event.PreviousIndexesText", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndexesText", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousIndexesText), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.PreviousIndex", DisplayNameKey = "Designer.Behaviors.Payload.PreviousIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousFirstIndex), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousTime", DisplayNameKey = "Designer.Behaviors.Payload.PreviousTime", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousTime), TypeName = "int?")]
    [FrontedBehaviorEventPayload("Event.PreviousActionName", DisplayNameKey = "Designer.Behaviors.Payload.PreviousActionName", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.PreviousActionName), TypeName = "string")]
    event EventHandler<GameGuidanceStepChangedEventArgs>? GuidanceStepChanged;

    /// <summary>
    /// 引导高亮变化事件
    /// </summary>
    [FrontedBehaviorEvent("Guidance.HighlightChanged", DisplayNameKey = "Designer.Behaviors.Event.GuidanceHighlightChanged", DescriptionKey = "Designer.Behaviors.Event.GuidanceHighlightChanged.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.GameAction", DisplayNameKey = "Designer.Behaviors.Payload.GameAction", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceHighlightChangedEventArgs.GameAction), TypeName = "GameAction")]
    [FrontedBehaviorEventPayload("Event.Action", DisplayNameKey = "Designer.Behaviors.Payload.Action", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceHighlightChangedEventArgs.Action), TypeName = "GameAction")]
    [FrontedBehaviorEventPayload("Event.Indexes", DisplayNameKey = "Designer.Behaviors.Payload.Indexes", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceHighlightChangedEventArgs.Indexes), TypeName = "int[]")]
    [FrontedBehaviorEventPayload("Event.IndexesText", DisplayNameKey = "Designer.Behaviors.Payload.IndexesText", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceHighlightChangedEventArgs.IndexesText), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.Index", DisplayNameKey = "Designer.Behaviors.Payload.Index", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceHighlightChangedEventArgs.FirstIndex), TypeName = "int")]
    event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightChanged;

    /// <summary>
    /// 引导高亮清除事件。
    /// </summary>
    [FrontedBehaviorEvent("Guidance.HighlightCleared", DisplayNameKey = "Designer.Behaviors.Event.GuidanceHighlightCleared", DescriptionKey = "Designer.Behaviors.Event.GuidanceHighlightCleared.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightCleared;

    /// <summary>
    /// 启动对局引导
    /// </summary>
    /// <returns></returns>
    Task<string?> StartGuidance();

    /// <summary>
    /// 下一步·
    /// </summary>
    /// <returns></returns>
    Task<string?> NextStepAsync();

    /// <summary>
    /// 上一步
    /// </summary>
    /// <returns></returns>
    Task<string?> PrevStepAsync();

    /// <summary>
    /// 停止对局引导
    /// </summary>
    void StopGuidance();
}
