using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 分数页面视图模型，负责处理游戏得分逻辑和界面交互
    /// </summary>
    public partial class ScorePageViewModel : ObservableObject
    {
        #region 构造函数
        /// <summary>
        /// 设计时构造函数，用于XAML设计器实例化
        /// </summary>
        /// <remarks>
        /// 该构造函数仅用于设计时创建可实例化对象
        /// 实际运行时使用依赖注入构造函数
        /// </remarks>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ScorePageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            // Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 运行时构造函数，通过依赖注入初始化共享数据服务
        /// </summary>
        /// <param name="sharedDataService">共享数据服务接口实例</param>
        public ScorePageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }
        #endregion

        #region 命令方法
        /// <summary>
        /// 处理4跑事件：求生者方队伍获得5分 minor points
        /// </summary>
        /// <remarks>
        /// 触发后更新队伍分数并通知界面刷新
        /// </remarks>
        [RelayCommand]
        private void Escape4()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 5;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// 处理3跑事件：求生者方队伍+3分，监管者放队伍+1分
        /// </summary>
        /// <remarks>
        /// 触发后更新双方分数并通知界面刷新
        /// </remarks>
        [RelayCommand]
        private void Escape3()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 3;
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 1;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// 处理平局事件：双方各+2分
        /// </summary>
        [RelayCommand]
        private void Tie()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 2;
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 2;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// 处理3抓事件：求生者方队伍+1分，监管者方队+3分
        /// </summary>
        [RelayCommand]
        private void Out3()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 1;
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 3;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// 处理被4抓事件：监管者方队伍获得5分
        /// </summary>
        [RelayCommand]
        private void Out4()
        {
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 5;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// 重置当前比赛分数数据
        /// </summary>
        /// <remarks>
        /// 将主队和客队的分数对象重置为新实例
        /// 用于开始新比赛或重置当前比赛数据
        /// </remarks>
        [RelayCommand]
        private void Reset()
        {
            SharedDataService.MainTeam.Score = new();
            SharedDataService.AwayTeam.Score = new();
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// 计算双方 胜/平/负 场次
        /// </summary>
        [RelayCommand]
        private void CaculateMajorPoint()
        {
            if (SharedDataService.MainTeam.Score.MinorPoints == SharedDataService.AwayTeam.Score.MinorPoints)
            {
                SharedDataService.MainTeam.Score.Tie++;
                SharedDataService.AwayTeam.Score.Tie++;
            }
            else if (SharedDataService.MainTeam.Score.MinorPoints > SharedDataService.AwayTeam.Score.MinorPoints)
            {
                SharedDataService.MainTeam.Score.Win++;
                SharedDataService.AwayTeam.Score.Lose++;
            }
            else
            {
                SharedDataService.MainTeam.Score.Lose++;
                SharedDataService.AwayTeam.Score.Win++;
            }
            SharedDataService.MainTeam.Score.MinorPoints = 0;
            SharedDataService.AwayTeam.Score.MinorPoints = 0;
            OnPropertyChanged(string.Empty);
        }
        #endregion

        #region 属性
        /// <summary>
        /// 共享数据服务接口实例
        /// </summary>
        public ISharedDataService SharedDataService { get; }
        #endregion

        #region 数据绑定
        /// <summary>
        /// 游戏阶段枚举与显示名称的映射字典
        /// </summary>
        /// <remarks>
        /// 包含BO1到BO5全阶段及加赛阶段的本地化显示名称
        /// 用于界面下拉框等控件的数据绑定
        /// </remarks>
        public Dictionary<GameProgress, string> GameList { get; } =
            new Dictionary<GameProgress, string>()
            {
                { GameProgress.Game1FirstHalf, "BO1上半" },
                { GameProgress.Game1SecondHalf, "BO1下半" },
                { GameProgress.Game2FirstHalf, "BO2上半" },
                { GameProgress.Game2SecondHalf, "BO2下半" },
                { GameProgress.Game3FirstHalf, "BO3上半" },
                { GameProgress.Game3SecondHalf, "BO3下半" },
                { GameProgress.Game3ExtraFirstHalf, "BO3加赛上半" },
                { GameProgress.Game3ExtraSecondHalf, "BO3加赛下半" },
                { GameProgress.Game4FirstHalf, "BO4上半" },
                { GameProgress.Game4SecondHalf, "BO4下半" },
                { GameProgress.Game5FirstHalf, "BO5上半" },
                { GameProgress.Game5SecondHalf, "BO5下半" },
                { GameProgress.Game5ExtraFirstHalf, "BO5加赛上半" },
                { GameProgress.Game5ExtraSecondHalf, "BO5加赛下半" },
            };
        #endregion
    }
}