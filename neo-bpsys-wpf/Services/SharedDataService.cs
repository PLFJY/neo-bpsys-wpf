using CommunityToolkit.Mvvm.ComponentModel;
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

            CurrentGameProgress = GameProgress.Free;

            CurrentGame = new(MainTeam, AwayTeam, CurrentGameProgress);

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

        public Team MainTeam { get; set; }
        public Team AwayTeam { get; set; }
        public Game CurrentGame { get; set; }
        public GameProgress CurrentGameProgress { get; set; }
        public Dictionary<string, Character> CharacterList { get; set; } = [];
        public Dictionary<string, Character> SurCharaList { get; set; } = [];
        public Dictionary<string, Character> HunCharaList { get; set; } = [];
        public ObservableCollection<bool> CanCurrentSurBanned { get; set; } = [];
        public ObservableCollection<bool> CanCurrentHunBanned { get; set; } = [];
        public ObservableCollection<bool> CanGlobalSurBanned { get; set; } = [];
        public ObservableCollection<bool> CanGlobalHunBanned { get; set; } = [];

        public bool IsTraitVisible { get; set; } = true;

        private int _remainingSeconds = 0;

        public string RemainingSeconds
        {
            get => _remainingSeconds == 0 ? "VS" : _remainingSeconds.ToString();
            set
            {
                if (!int.TryParse(value, out _remainingSeconds))
                    _remainingSeconds = 0;

                WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(nameof(RemainingSeconds)));
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;
                WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string>(nameof(RemainingSeconds)));
            }
            else
            {
                _timer.Stop();
            }
        }

        public void TimerStart(int seconds)
        {
            _remainingSeconds = seconds;
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
        /// 如 nameof(CanCurrentSurBanned)
        /// </summary>
        /// <param name="listName">传入的列表名称</param>
        /// <param name="count">Ban位数量</param>
        /// <exception cref="ArgumentException"></exception>
        public void SetBanCount(string listName, int count)
        {
            switch (listName)
            {
                case nameof(CanCurrentSurBanned):
                    for (int i = 0; i < CanCurrentSurBanned.Count; i++) 
                        CanCurrentSurBanned[i] = i < count;
                    break;
                case nameof(CanCurrentHunBanned):
                    for (int i = 0; i < CanCurrentHunBanned.Count; i++)
                        CanCurrentHunBanned[i] = i < count;
                    break;
                case nameof(CanGlobalSurBanned):
                    for (int i = 0; i < CanGlobalSurBanned.Count; i++)
                        CanGlobalSurBanned[i] = i < count;
                    break;
                case nameof(CanGlobalHunBanned):
                    for (int i = 0; i < CanGlobalHunBanned.Count; i++)
                        CanGlobalHunBanned[i] = i < count;
                    break;
                default:
                    throw new ArgumentException("Illegal ListName", nameof(listName));
            }
            WeakReferenceMessenger.Default.Send(new BanCountChangedMessage(listName));
        }

        private class CharacterMini
        {
            public Camp Camp { get; set; }
            public string ImageFileName { get; set; } = string.Empty;
        }
    }
}
