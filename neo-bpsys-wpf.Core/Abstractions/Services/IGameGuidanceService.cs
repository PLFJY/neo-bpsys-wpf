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
    /// 引导步骤变化事件
    /// </summary>
    [FrontedBehaviorEvent("Guidance.StepChanged", DisplayNameKey = "Designer.Behaviors.Event.GuidanceStepChanged", DescriptionKey = "Designer.Behaviors.Event.GuidanceStepChanged.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.StepIndex", DisplayNameKey = "Designer.Behaviors.Payload.StepIndex", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.StepIndex), TypeName = "int")]
    [FrontedBehaviorEventPayload("Event.Action", DisplayNameKey = "Designer.Behaviors.Payload.Action", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.Action), TypeName = "GameAction")]
    [FrontedBehaviorEventPayload("Event.ActionName", DisplayNameKey = "Designer.Behaviors.Payload.ActionName", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceStepChangedEventArgs.ActionName), TypeName = "string")]
    event EventHandler<GameGuidanceStepChangedEventArgs>? GuidanceStepChanged;

    /// <summary>
    /// 引导高亮变化事件
    /// </summary>
    [FrontedBehaviorEvent("Guidance.HighlightChanged", DisplayNameKey = "Designer.Behaviors.Event.GuidanceHighlightChanged", DescriptionKey = "Designer.Behaviors.Event.GuidanceHighlightChanged.Description", Category = "Guidance", CategoryKey = "Designer.Behaviors.Category.Guidance")]
    [FrontedBehaviorEventPayload("Event.GameAction", DisplayNameKey = "Designer.Behaviors.Payload.GameAction", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(GameGuidanceHighlightChangedEventArgs.GameAction), TypeName = "GameAction")]
    event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightChanged;

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