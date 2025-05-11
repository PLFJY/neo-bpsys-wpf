using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 共享数据服务接口，用于管理比赛相关数据和全局状态
    /// </summary>
    public interface ISharedDataService
    {
        /// <summary>
        /// 获取或设置当前比赛的主队信息
        /// </summary>
        Team MainTeam { get; set; }

        /// <summary>
        /// 获取或设置当前比赛的客队信息
        /// </summary>
        Team AwayTeam { get; set; }

        /// <summary>
        /// 获取或设置当前比赛的核心数据对象
        /// </summary>
        Game CurrentGame { get; set; }

        /// <summary>
        /// 获取或设置当前比赛的进度状态
        /// </summary>
        GameProgress CurrentGameProgress { get; set; }

        /// <summary>
        /// 获取或设置全角色字典，键为角色ID，值为角色对象
        /// </summary>
        Dictionary<string, Character> CharacterList { get; set; }

        /// <summary>
        /// 获取或设置求生者阵营角色字典，键为角色ID，值为角色对象
        /// </summary>
        Dictionary<string, Character> SurCharaList { get; set; }

        /// <summary>
        /// 获取或设置监管者阵营角色字典，键为角色ID，值为角色对象
        /// </summary>
        Dictionary<string, Character> HunCharaList { get; set; }

        /// <summary>
        /// 获取或设置当前局求生者阵营可禁用状态集合
        /// </summary>
        ObservableCollection<bool> CanCurrentSurBanned { get; set; }

        /// <summary>
        /// 获取或设置当前局监管者阵营可禁用状态集合
        /// </summary>
        ObservableCollection<bool> CanCurrentHunBanned { get; set; }

        /// <summary>
        /// 获取或设置全局求生者阵营全局禁用状态集合
        /// </summary>
        ObservableCollection<bool> CanGlobalSurBanned { get; set; }

        /// <summary>
        /// 获取或设置监管者阵营全局禁用状态集合
        /// </summary>
        ObservableCollection<bool> CanGlobalHunBanned { get; set; }

        /// <summary>
        /// 获取或设置监管者辅助特质特质可见性状态
        /// true表示显示监管者特质信息，false表示隐藏
        /// </summary>
        bool IsTraitVisible { get; set; }

        /// <summary>
        /// 获取或设置倒计时剩余时间字符串
        /// </summary>
        string RemainingSeconds { get; set; }

        /// <summary>
        /// 启动倒计时定时器
        /// </summary>
        /// <param name="seconds">倒计时总秒数</param>
        void TimerStart(int seconds);

        /// <summary>
        /// 停止当前运行的倒计时定时器
        /// </summary>
        void TimerStop();
    }
}