using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 共享数据服务接口
    /// </summary>
    public interface ISharedDataService
    {
        /// <summary>
        /// 主队
        /// </summary>
        Team MainTeam { get; set; }
        /// <summary>
        /// 客队
        /// </summary>
        Team AwayTeam { get; set; }
        /// <summary>
        /// 当前对局
        /// </summary>
        Game CurrentGame { get; set; }
        /// <summary>
        /// 角色列表总表
        /// </summary>
        Dictionary<string, Character> CharacterList { get; set; }
        /// <summary>
        /// 求生者列表
        /// </summary>
        Dictionary<string, Character> SurCharaList { get; set; }
        /// <summary>
        /// 监管者列表
        /// </summary>
        Dictionary<string, Character> HunCharaList { get; set; }
        /// <summary>
        /// 求生者 (当局禁用) 是否可禁用
        /// </summary>
        ObservableCollection<bool> CanCurrentSurBanned { get; set; }
        /// <summary>
        /// 监管者 (当局禁用) 是否可禁用
        /// </summary>
        ObservableCollection<bool> CanCurrentHunBanned { get; set; }
        /// <summary>
        /// 求生者 (全局禁用) 是否可禁用
        /// </summary>
        ObservableCollection<bool> CanGlobalSurBanned { get; set; }
        /// <summary>
        /// 监管者 (全局禁用) 是否可禁用
        /// </summary>
        ObservableCollection<bool> CanGlobalHunBanned { get; set; }
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

        void SetBanCount(BanListName listName, int count);

        /// <summary>
        /// 倒计时开始
        /// </summary>
        /// <param name="seconds"></param>
        void TimerStart(int? seconds);
        /// <summary>
        /// 倒计时停止
        /// </summary>
        void TimerStop();
    }
}