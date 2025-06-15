using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services
{
    public partial class SharedDataService : ISharedDataService
    {
        private readonly DispatcherTimer _timer = new();

        public SharedDataService()
        {
            MainTeam = new Team(Camp.Sur);
            AwayTeam = new Team(Camp.Hun);

            CurrentGame = new(MainTeam, AwayTeam, GameProgress.Free);

            var charaListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\CharacterList.json");
            ReadCharaListFromFile(charaListFilePath);

            SurCharaList = SurCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;
            HunCharaList = HunCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;

            CanCurrentSurBanned = [.. Enumerable.Repeat(true, 4)];
            CanCurrentHunBanned = [.. Enumerable.Repeat(true, 2)];
            CanGlobalSurBanned = [.. Enumerable.Repeat(false, 9)];
            CanGlobalHunBanned = [.. Enumerable.Repeat(false, 3)];

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        private readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// 从文件读取角色数据
        /// </summary>
        /// <param name="charaListFilePath"></param>
        private void ReadCharaListFromFile(string charaListFilePath)
        {
            if (!File.Exists(charaListFilePath))
                return;

            // 加载角色数据
            var characterFileContent = File.ReadAllText(charaListFilePath);
            var characters = JsonSerializer.Deserialize<Dictionary<string, CharacterMini>>(
                characterFileContent,
                jsonSerializerOptions
            );

            if (characters == null)
                return;

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
        }

        /// <summary>
        /// 主队
        /// </summary>
        public Team MainTeam { get; set; }
        /// <summary>
        /// 客队
        /// </summary>
        public Team AwayTeam { get; set; }
        /// <summary>
        /// 当前游戏
        /// </summary>
        public Game CurrentGame { get; set; }
        /// <summary>
        /// 所有角色
        /// </summary>
        public Dictionary<string, Character> CharacterList { get; set; } = [];
        /// <summary>
        /// 求生者角色列表
        /// </summary>
        public Dictionary<string, Character> SurCharaList { get; set; } = [];
        /// <summary>
        /// 监管者角色列表
        /// </summary>
        public Dictionary<string, Character> HunCharaList { get; set; } = [];
        /// <summary>
        /// 当局禁用是否可禁用
        /// </summary>
        public ObservableCollection<bool> CanCurrentSurBanned { get; set; } = [];
        public ObservableCollection<bool> CanCurrentHunBanned { get; set; } = [];
        /// <summary>
        /// 全局禁用是否可禁用
        /// </summary>
        public ObservableCollection<bool> CanGlobalSurBanned { get; set; } = [];
        public ObservableCollection<bool> CanGlobalHunBanned { get; set; } = [];

        /// <summary>
        /// 是否显示辅助特质
        /// </summary>
        private bool _isTraitVisible = true;
        public bool IsTraitVisible
        {
            get => _isTraitVisible;
            set
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsTraitVisible), _isTraitVisible, value));
                _isTraitVisible = value;
            }
        }

        /// <summary>
        /// 是否是BO3模式
        /// </summary>
        private bool _isBo3Mode = false;
        public bool IsBo3Mode
        {
            get => _isBo3Mode;
            set
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBo3Mode), _isBo3Mode, value));
                _isBo3Mode = value;
            }
        }

        private int _remainingSeconds = -1;
        /// <summary>
        /// 倒计时剩余时间
        /// </summary>
        public string RemainingSeconds
        {
            get => _remainingSeconds < 0 ? "VS" : _remainingSeconds.ToString();
            set
            {
                if (!int.TryParse(value, out _remainingSeconds))
                    _remainingSeconds = 0;

                WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(nameof(RemainingSeconds)));
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_remainingSeconds >= 0)
            {
                _remainingSeconds--;
                WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(nameof(RemainingSeconds)));
            }
            else
            {
                _timer.Stop();
            }
        }

        public void TimerStart(int? seconds)
        {
            if (seconds == null) return;
            _remainingSeconds = (int)seconds;
            _timer.Start();
        }

        public void TimerStop()
        {
            _remainingSeconds = 0;
            _timer.Stop();
            WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(nameof(RemainingSeconds)));
        }

        /// <summary>
        /// 设置Ban位数量，第一个参数传入列表名称<br/>
        /// </summary>
        /// <param name="listName">传入的列表名称</param>
        /// <param name="count">Ban位数量</param>
        /// <exception cref="ArgumentException"></exception>
        public void SetBanCount(BanListName listName, int count)
        {
            switch (listName)
            {
                case BanListName.CanCurrentSurBanned:
                    for (int i = 0; i < CanCurrentSurBanned.Count; i++)
                        CanCurrentSurBanned[i] = i < count;
                    break;
                case BanListName.CanCurrentHunBanned:
                    for (int i = 0; i < CanCurrentHunBanned.Count; i++)
                        CanCurrentHunBanned[i] = i < count;
                    break;
                case BanListName.CanGlobalSurBanned:
                    for (int i = 0; i < CanGlobalSurBanned.Count; i++)
                        CanGlobalSurBanned[i] = i < count;
                    break;
                case BanListName.CanGlobalHunBanned:
                    for (int i = 0; i < CanGlobalHunBanned.Count; i++)
                        CanGlobalHunBanned[i] = i < count;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(listName), listName, null);
            }
            WeakReferenceMessenger.Default.Send(new BanCountChangedMessage(listName));
        }

        /// <summary>
        /// 分数统计界面 BO3 和 BO5之间"Total"相差的距离
        /// </summary>
        private double _globalScoreTotalMargin = 390;
        public double GlobalScoreTotalMargin
        {
            get => _globalScoreTotalMargin;
            set
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<double>(this, nameof(GlobalScoreTotalMargin), _globalScoreTotalMargin, value));
                _globalScoreTotalMargin = value;
            }
        }

        private class CharacterMini
        {
            public Camp Camp { get; set; }
            public string ImageFileName { get; set; } = string.Empty;
        }
    }
}