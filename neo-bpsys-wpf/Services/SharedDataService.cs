using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 共享数据服务，管理游戏核心数据和定时器功能
    /// 实现ObservableObject支持属性变更通知，实现ISharedDataService接口
    /// </summary>
    public partial class SharedDataService : ObservableObject, ISharedDataService
    {
        private DispatcherTimer _timer = new();

        /// <summary>
        /// 构造函数初始化游戏数据和加载角色配置
        /// </summary>
        public SharedDataService()
        {
            // 初始化基础游戏对象
            MainTeam = new Team(Camp.Sur);
            AwayTeam = new Team(Camp.Hun);
            CurrentGameProgress = GameProgress.Free;
            CurrentGame = new(MainTeam, AwayTeam, CurrentGameProgress);

            // 初始化角色集合
            SurCharaList = new();
            HunCharaList = new();

            // 加载角色配置文件
            var charaListFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources\\CharacterList.json"
            );

            if (!File.Exists(charaListFilePath))
                return;

            // 反序列化角色配置数据
            var characterFileContent = File.ReadAllText(charaListFilePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
            };
            var characters = JsonSerializer.Deserialize<Dictionary<string, CharacterMini>>(
                characterFileContent,
                options
            );

            if (characters == null)
                return;

            // 构建角色字典并分类阵营
            foreach (var i in characters)
            {
                CharacterList.Add(
                    i.Key,
                    new Character(i.Key, i.Value.Camp, i.Value.ImageFileName)
                );

                if (i.Value.Camp == Camp.Sur)
                    SurCharaList?.Add(i.Key, CharacterList[i.Key]);
                else
                    HunCharaList?.Add(i.Key, CharacterList[i.Key]);
            }

            // 按角色名称排序
            SurCharaList = SurCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;
            HunCharaList = HunCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;

            // 初始化禁用状态集合
            CanCurrentSurBanned.AddRange(Enumerable.Repeat(true, 4));
            CanCurrentHunBanned.AddRange(Enumerable.Repeat(true, 2));
            CanGlobalSurBanned.AddRange(Enumerable.Repeat(false, 9));
            CanGlobalHunBanned.AddRange(Enumerable.Repeat(false, 3));

            // 配置定时器（每秒触发）
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        // 以下属性注释已通过ObservableProperty特性自动生成
        public Team MainTeam { get; set; }
        public Team AwayTeam { get; set; }
        public Game CurrentGame { get; set; }
        public GameProgress CurrentGameProgress { get; set; }

        /// <summary>
        /// 全部角色字典（键：角色名称）
        /// </summary>
        public Dictionary<string, Character> CharacterList { get; set; } = new();

        /// <summary>
        /// 求生者阵营角色字典（按名称排序）
        /// </summary>
        public Dictionary<string, Character> SurCharaList { get; set; }

        /// <summary>
        /// 监管者阵营角色字典（按名称排序）
        /// </summary>
        public Dictionary<string, Character> HunCharaList { get; set; }

        /// <summary>
        /// 当前求生者禁用状态集合（容量4）
        /// </summary>
        public ObservableCollection<bool> CanCurrentSurBanned { get; set; } = new();

        /// <summary>
        /// 当前监管者禁用状态集合（容量2）
        /// </summary>
        public ObservableCollection<bool> CanCurrentHunBanned { get; set; } = new();

        /// <summary>
        /// 求生者全局禁用状态集合（容量9）
        /// </summary>
        public ObservableCollection<bool> CanGlobalSurBanned { get; set; } = new();

        /// <summary>
        /// 监管者全局禁用状态集合（容量3）
        /// </summary>
        public ObservableCollection<bool> CanGlobalHunBanned { get; set; } = new();

        [ObservableProperty]
        private bool _isTraitVisible = true;

        private int _remainingSeconds = 0;

        /// <summary>
        /// 剩余时间显示（0时显示"VS"）
        /// </summary>
        public string RemainingSeconds
        {
            get => _remainingSeconds == 0 ? "VS" : _remainingSeconds.ToString();
            set
            {
                if (!int.TryParse(value, out _remainingSeconds))
                    _remainingSeconds = 0;

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 定时器触发事件处理（每秒减少剩余时间）
        /// </summary>
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;
                OnPropertyChanged(nameof(RemainingSeconds));
            }
            else
            {
                _timer.Stop();
            }
        }

        /// <summary>
        /// 启动倒计时定时器
        /// </summary>
        /// <param name="seconds">初始倒计时秒数</param>
        public void TimerStart(int seconds)
        {
            _remainingSeconds = seconds;
            _timer.Start();
        }

        /// <summary>
        /// 停止定时器并重置显示
        /// </summary>
        public void TimerStop()
        {
            _remainingSeconds = 0;
            _timer.Stop();
            OnPropertyChanged(nameof(RemainingSeconds));
        }

        /// <summary>
        /// 角色配置反序列化辅助类
        /// </summary>
        private class CharacterMini
        {
            public Camp Camp { get; set; }
            public string ImageFileName { get; set; } = string.Empty;
        }
    }
}