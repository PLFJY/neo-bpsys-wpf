using System.Collections.ObjectModel;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 共享数据服务接口
/// </summary>
public interface ISharedDataService
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
    /// 分数统计界面 BO3 和 BO5之间"Total"相差的距离
    /// </summary>
    double GlobalScoreTotalMargin { get; set; }

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
    /// <param name="seconds"></param>
    void TimerStart(int? seconds);

    /// <summary>
    /// 停止倒计时
    /// </summary>
    void TimerStop();

    /// <summary>
    /// 当前对局改变事件
    /// </summary>
    event EventHandler? CurrentGameChanged;

    /// <summary>
    /// 分数统计界面 BO3 和 BO5之间"Total"相差的距离改变事件
    /// </summary>
    event EventHandler? GlobalScoreTotalMarginChanged;

    /// <summary>
    /// Ban位数量改变事件
    /// </summary>
    event EventHandler<BanCountChangedEventArgs>? BanCountChanged;

    /// <summary>
    /// 辅助特质是否可见改变事件
    /// </summary>
    event EventHandler? IsTraitVisibleChanged;

    /// <summary>
    /// Bo3模式改变事件
    /// </summary>
    event EventHandler? IsBo3ModeChanged;

    /// <summary>
    /// 倒计时剩余秒数改变事件
    /// </summary>
    event EventHandler? CountDownValueChanged;

    /// <summary>
    /// 队伍换边事件
    /// </summary>
    event EventHandler? TeamSwapped;

    /// <summary>
    /// 地图V2呼吸灯改变事件
    /// </summary>
    event EventHandler? IsMapV2BreathingChanged;

    /// <summary>
    /// 地图V2阵营是否可见改变事件
    /// </summary>
    event EventHandler? IsMapV2CampVisibleChanged;
}