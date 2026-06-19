using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 角色选择服务接口
/// 处理角色选择的核心业务逻辑，依赖动画服务执行视觉效果
/// </summary>
public interface ICharacterSelectionService
{
    /// <summary>
    /// 根据识别文本和阵营匹配角色字典中相似度最高的角色。
    /// </summary>
    /// <param name="text">待匹配的 OCR 或外部识别文本。</param>
    /// <param name="camp">角色阵营。</param>
    /// <returns>相似度不低于匹配阈值的最佳角色；没有有效候选时返回 <see langword="null"/>。</returns>
    Character? ResolveCharacter(string text, Camp camp);

    /// <summary>
    /// 选择求生者角色
    /// </summary>
    /// <param name="playerIndex">玩家索引 (0-3)</param>
    /// <param name="character">选择的角色</param>
    /// <param name="playAnimation">是否播放动画</param>
    /// <param name="isRecordGlobalBan">是否记录全局禁用</param>
    Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true);

    /// <summary>
    /// 选择监管者角色
    /// </summary>
    /// <param name="character">选择的角色</param>
    /// <param name="playAnimation">是否播放动画</param>
    /// <param name="isRecordGlobalBan">是否记录全局禁用</param>
    Task SelectHunterAsync(Character? character, bool playAnimation = true, bool isRecordGlobalBan = true);

    /// <summary>
    /// 禁用角色
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">禁用位索引</param>
    /// <param name="character">被禁用的角色</param>
    /// <param name="playAnimation">是否播放动画</param>
    Task BanCharacterAsync(Camp camp, int index, Character? character, bool playAnimation = true);

    /// <summary>
    /// 互换求生者角色
    /// </summary>
    /// <param name="sourceIndex">源玩家索引</param>
    /// <param name="targetIndex">目标玩家索引</param>
    /// <param name="playAnimation">是否播放动画</param>
    Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true);

    /// <summary>
    /// 角色选择事件
    /// </summary>
    [FrontedBehaviorEvent("Selection.CharacterSelected", DisplayNameKey = "Designer.Behaviors.Event.CharacterSelected", DescriptionKey = "Designer.Behaviors.Event.CharacterSelected.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.Camp", DisplayNameKey = "Designer.Behaviors.Payload.Camp", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(CharacterSelectedEventArgs.Camp), TypeName = "Camp")]
    [FrontedBehaviorEventPayload("Event.Index", DisplayNameKey = "Designer.Behaviors.Payload.Index", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(CharacterSelectedEventArgs.PlayerIndex), TypeName = "int")]
    event EventHandler<CharacterSelectedEventArgs> CharacterSelected;

    /// <summary>
    /// 角色禁用事件
    /// </summary>
    [FrontedBehaviorEvent("Selection.CharacterBanned", DisplayNameKey = "Designer.Behaviors.Event.CharacterBanned", DescriptionKey = "Designer.Behaviors.Event.CharacterBanned.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.Camp", DisplayNameKey = "Designer.Behaviors.Payload.Camp", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(CharacterBannedEventArgs.Camp), TypeName = "Camp")]
    [FrontedBehaviorEventPayload("Event.Index", DisplayNameKey = "Designer.Behaviors.Payload.Index", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(CharacterBannedEventArgs.Index), TypeName = "int")]
    event EventHandler<CharacterBannedEventArgs> CharacterBanned;
}
