using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 共享数据服务接口
/// </summary>
public interface ISharedDataService : INotifyPropertyChanged
{
    /// <summary>
    /// 主队
    /// 该属性对象全场始终不变，信息导入依靠 <see cref="Team.ImportTeamInfo(Team)"/> 方法
    /// </summary>
    Team HomeTeam { get; }

    /// <summary>
    /// 客队
    /// 该属性对象全场始终不变，信息导入依靠 <see cref="Team.ImportTeamInfo(Team)"/> 方法
    /// </summary>
    Team AwayTeam { get; }

    /// <summary>
    /// 当前对局
    /// </summary>
    Game CurrentGame { get; }

    /// <summary>
    /// 创建新对局
    /// </summary>
    void NewGame();

    /// <summary>
    /// 从文件导入对局
    /// </summary>
    /// <param name="filePath">对局文件路径</param>
    Task ImportGameAsync(string filePath);

    /// <summary>
    /// 求生者角色字典
    /// </summary>
    SortedDictionary<string, Character> SurCharaDict { get; set; }

    /// <summary>
    /// 监管者角色字典
    /// </summary>
    SortedDictionary<string, Character> HunCharaDict { get; set; }

    /// <summary>
    /// 求生者 (当局禁用) 是否可禁用
    /// </summary>
    ObservableCollection<bool> CanCurrentSurBannedList { get; }

    /// <summary>
    /// 监管者 (当局禁用) 是否可禁用
    /// </summary>
    ObservableCollection<bool> CanCurrentHunBannedList { get; }

    /// <summary>
    /// 求生者 (全局禁用) 是否可禁用
    /// </summary>
    ObservableCollection<bool> CanGlobalSurBannedList { get; }

    /// <summary>
    /// 监管者 (全局禁用) 是否可禁用
    /// </summary>
    ObservableCollection<bool> CanGlobalHunBannedList { get; }

    /// <summary>
    /// 辅助特质是否可见
    /// </summary>
    bool IsTraitVisible { get; set; }

    /// <summary>
    /// 倒计时剩余秒数
    /// </summary>
    string RemainingSeconds { get; set; }

    /// <summary>
    /// 是否是Bo3模式
    /// </summary>
    bool IsBo3Mode { get; set; }

    /// <summary>
    /// 地图V2呼吸灯是否开启
    /// </summary>
    bool IsMapV2Breathing { get; set; }

    /// <summary>
    /// 地图V2阵营是否可见
    /// </summary>
    bool IsMapV2CampVisible { get; set; }

    /// <summary>
    /// 设置Ban位数量
    /// </summary>
    /// <param name="listName">Ban位列表名称</param>
    /// <param name="count">Ban位数量</param>
    void SetBanCount(BanListName listName, int count);

    /// <summary>
    /// 开始倒计时
    /// </summary>
    /// <param name="seconds">倒计时秒数，传入 <c>null</c> 则使用默认值</param>
    void TimerStart(int? seconds);

    /// <summary>
    /// 停止倒计时
    /// </summary>
    void TimerStop();

    /// <summary>
    /// 当前对局改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.CurrentGameChanged", DisplayNameKey = "Designer.Behaviors.Event.CurrentGameChanged", DescriptionKey = "Designer.Behaviors.Event.CurrentGameChanged.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.Game", DisplayNameKey = "Designer.Behaviors.Payload.Game", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(CurrentGame), TypeName = "Game")]
    event EventHandler? CurrentGameChanged;

    /// <summary>
    /// 当前对局进度改变事件。
    /// </summary>
    [FrontedBehaviorEvent("SharedData.GameProgressChanged", DisplayNameKey = "Designer.Behaviors.Event.GameProgressChanged", DescriptionKey = "Designer.Behaviors.Event.GameProgressChanged", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.GameProgress", DisplayNameKey = "Designer.Behaviors.Payload.GameProgress", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = "CurrentGame.GameProgress", TypeName = "GameProgress")]
    event EventHandler? GameProgressChanged;

    /// <summary>
    /// Ban位数量改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.BanCountChanged", DisplayNameKey = "Designer.Behaviors.Event.BanCountChanged", DescriptionKey = "Designer.Behaviors.Event.BanCountChanged.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.ListName", DisplayNameKey = "Designer.Behaviors.Payload.ListName", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(BanCountChangedEventArgs.BanListName), TypeName = "BanListName")]
    [FrontedBehaviorEventPayload("Event.Count", DisplayNameKey = "Designer.Behaviors.Payload.Count", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(BanCountChangedEventArgs.Index), TypeName = "int")]
    event EventHandler<BanCountChangedEventArgs>? BanCountChanged;

    /// <summary>
    /// 辅助特质是否可见改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.IsTraitVisibleChanged", DisplayNameKey = "Designer.Behaviors.Event.IsTraitVisibleChanged", DescriptionKey = "Designer.Behaviors.Event.IsTraitVisibleChanged.Description", Category = "Display", CategoryKey = "Designer.Behaviors.Category.Display")]
    [FrontedBehaviorEventPayload("Event.IsTraitVisible", DisplayNameKey = "Designer.Behaviors.Payload.IsTraitVisible", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(IsTraitVisible), TypeName = "bool")]
    event EventHandler? IsTraitVisibleChanged;

    /// <summary>
    /// Bo3模式改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.IsBo3ModeChanged", DisplayNameKey = "Designer.Behaviors.Event.IsBo3ModeChanged", DescriptionKey = "Designer.Behaviors.Event.IsBo3ModeChanged.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.IsBo3Mode", DisplayNameKey = "Designer.Behaviors.Payload.IsBo3Mode", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(IsBo3Mode), TypeName = "bool")]
    event EventHandler? IsBo3ModeChanged;

    /// <summary>
    /// 倒计时剩余秒数改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.CountDownValueChanged", DisplayNameKey = "Designer.Behaviors.Event.CountDownValueChanged", DescriptionKey = "Designer.Behaviors.Event.CountDownValueChanged.Description", Category = "Timer", CategoryKey = "Designer.Behaviors.Category.Timer")]
    [FrontedBehaviorEventPayload("Event.RemainingSeconds", DisplayNameKey = "Designer.Behaviors.Payload.RemainingSeconds", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(RemainingSeconds), TypeName = "string")]
    event EventHandler? CountDownValueChanged;

    /// <summary>
    /// 队伍换边事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.TeamSwapped", DisplayNameKey = "Designer.Behaviors.Event.TeamSwapped", DescriptionKey = "Designer.Behaviors.Event.TeamSwapped.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    event EventHandler? TeamSwapped;

    /// <summary>
    /// 地图V2呼吸灯改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.IsMapV2BreathingChanged", DisplayNameKey = "Designer.Behaviors.Event.IsMapV2BreathingChanged", DescriptionKey = "Designer.Behaviors.Event.IsMapV2BreathingChanged.Description", Category = "Display", CategoryKey = "Designer.Behaviors.Category.Display")]
    [FrontedBehaviorEventPayload("Event.IsMapV2Breathing", DisplayNameKey = "Designer.Behaviors.Payload.IsMapV2Breathing", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(IsMapV2Breathing), TypeName = "bool")]
    event EventHandler? IsMapV2BreathingChanged;

    /// <summary>
    /// 地图 BP v2 选图边框状态改变事件。
    /// </summary>
    [FrontedBehaviorEvent("MapV2.PickingBorderStateChanged", DisplayNameKey = "Designer.Behaviors.Event.MapV2PickingBorderStateChanged", DescriptionKey = "Designer.Behaviors.Event.MapV2PickingBorderStateChanged.Description", Category = "Display", CategoryKey = "Designer.Behaviors.Category.Display")]
    [FrontedBehaviorEventPayload("Event.MapKey", DisplayNameKey = "Designer.Behaviors.Payload.MapKey", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(MapV2PickingBorderStateChangedEventArgs.MapKey), TypeName = "string")]
    [FrontedBehaviorEventPayload("Event.IsMapV2Breathing", DisplayNameKey = "Designer.Behaviors.Payload.IsMapV2Breathing", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(MapV2PickingBorderStateChangedEventArgs.IsMapV2Breathing), TypeName = "bool")]
    [FrontedBehaviorEventPayload("Event.IsMapBanned", DisplayNameKey = "Designer.Behaviors.Payload.IsMapBanned", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(MapV2PickingBorderStateChangedEventArgs.IsMapBanned), TypeName = "bool")]
    [FrontedBehaviorEventPayload("Event.IsPickingBorderVisible", DisplayNameKey = "Designer.Behaviors.Payload.IsPickingBorderVisible", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(MapV2PickingBorderStateChangedEventArgs.IsPickingBorderVisible), TypeName = "bool")]
    event EventHandler<MapV2PickingBorderStateChangedEventArgs>? MapV2PickingBorderStateChanged;

    /// <summary>
    /// 地图V2阵营是否可见改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.IsMapV2CampVisibleChanged", DisplayNameKey = "Designer.Behaviors.Event.IsMapV2CampVisibleChanged", DescriptionKey = "Designer.Behaviors.Event.IsMapV2CampVisibleChanged.Description", Category = "Display", CategoryKey = "Designer.Behaviors.Category.Display")]
    [FrontedBehaviorEventPayload("Event.IsMapV2CampVisible", DisplayNameKey = "Designer.Behaviors.Payload.IsMapV2CampVisible", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(IsMapV2CampVisible), TypeName = "bool")]
    event EventHandler? IsMapV2CampVisibleChanged;

    /// <summary>
    /// 选择地图改变事件
    /// </summary>
    [FrontedBehaviorEvent("SharedData.PickedMapChanged", DisplayNameKey = "Designer.Behaviors.Event.PickedMapChanged", DescriptionKey = "Designer.Behaviors.Event.PickedMapChanged.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.PickedMap", DisplayNameKey = "Designer.Behaviors.Payload.PickedMap", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = "CurrentGame.PickedMap", TypeName = "string")]
    event EventHandler? PickedMapChanged;

    /// <summary>
    /// 地图禁用状态改变事件（MapV2）
    /// </summary>
    [FrontedBehaviorEvent("SharedData.MapV2BannedChanged", DisplayNameKey = "Designer.Behaviors.Event.MapV2BannedChanged", DescriptionKey = "Designer.Behaviors.Event.MapV2BannedChanged.Description", Category = "Game", CategoryKey = "Designer.Behaviors.Category.Game")]
    [FrontedBehaviorEventPayload("Event.Game", DisplayNameKey = "Designer.Behaviors.Payload.Game", Source = FrontedBehaviorPayloadSource.ServiceProperty, SourcePath = nameof(CurrentGame), TypeName = "Game")]
    event EventHandler? MapV2BannedChanged;
}
