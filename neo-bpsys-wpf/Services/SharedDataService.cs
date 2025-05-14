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
    public partial class SharedDataService : ObservableObject, ISharedDataService
    {
        private DispatcherTimer _timer = new();

        public SharedDataService()
        {
            MainTeam = new Team(Camp.Sur);
            AwayTeam = new Team(Camp.Hun);

            CurrentGameProgress = GameProgress.Free;

            CurrentGame = new(MainTeam, AwayTeam, CurrentGameProgress);

            SurCharaList = new();
            HunCharaList = new();


            var charaListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\CharacterList.json");
            ReadCharaListFromFile(charaListFilePath);

            SurCharaList = SurCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;
            HunCharaList = HunCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;

            CanCurrentSurBanned.AddRange(Enumerable.Repeat(true, 4));
            CanCurrentHunBanned.AddRange(Enumerable.Repeat(true, 2));
            CanGlobalSurBanned.AddRange(Enumerable.Repeat(false, 9));
            CanGlobalHunBanned.AddRange(Enumerable.Repeat(false, 3));

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
        public Dictionary<string, Character> CharacterList { get; set; } = new();
        public Dictionary<string, Character> SurCharaList { get; set; }
        public Dictionary<string, Character> HunCharaList { get; set; }
        public ObservableCollection<bool> CanCurrentSurBanned { get; set; } = new();
        public ObservableCollection<bool> CanCurrentHunBanned { get; set; } = new();
        public ObservableCollection<bool> CanGlobalSurBanned { get; set; } = new();
        public ObservableCollection<bool> CanGlobalHunBanned { get; set; } = new();
        [ObservableProperty]
        private bool _isTraitVisible = true;

        private int _remainingSeconds = 0;

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

        public void TimerStart(int seconds)
        {
            _remainingSeconds = seconds;
            _timer.Start();
        }

        public void TimerStop()
        {
            _remainingSeconds = 0;
            _timer.Stop();
            OnPropertyChanged(nameof(RemainingSeconds));
        }

        private class CharacterMini
        {
            public Camp Camp { get; set; }
            public string ImageFileName { get; set; } = string.Empty;
        }
    }
}
