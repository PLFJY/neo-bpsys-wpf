using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Services
{
    public partial class SharedDataService : ObservableObject, ISharedDataService
    {
        public SharedDataService()
        {
            MainTeam = new Team(Camp.Sur);
            AwayTeam = new Team(Camp.Hun);

            CurrentGameProgress = GameProgress.Free;

            CurrentGame = new(MainTeam, AwayTeam, CurrentGameProgress);

            SurCharaList = new();
            HunCharaList = new();

            var charaListFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources\\CharacterList.json"
            );

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

            SurCharaList = SurCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;
            HunCharaList = HunCharaList
                ?.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value)!;

            CanCurrentSurBanned.AddRange(Enumerable.Repeat(true, 4));
            CanCurrentHunBanned.AddRange(Enumerable.Repeat(true, 2));
            CanGlobalSurBanned.AddRange(Enumerable.Repeat(true, 9));
            CanGlobalHunBanned.AddRange(Enumerable.Repeat(true, 3));
        }

        public Team MainTeam { get; set; }
        public Team AwayTeam { get; set; }
        public Game CurrentGame { get; set; }
        public GameProgress CurrentGameProgress { get; set; }
        public Dictionary<string, Character> CharacterList { get; set; } = new();
        public Dictionary<string, Character> SurCharaList { get; set; }
        public Dictionary<string, Character> HunCharaList { get; set; }
        [ObservableProperty]
        private ObservableCollection<bool> _canCurrentSurBanned = new();
        [ObservableProperty]
        private ObservableCollection<bool> _canCurrentHunBanned = new();
        [ObservableProperty]
        private ObservableCollection<bool> _canGlobalSurBanned = new();
        [ObservableProperty]
        private ObservableCollection<bool> _canGlobalHunBanned = new();
        [ObservableProperty]
        private bool _isTraitVisible = true;

        private int _timer = 0;

        public string Timer
        {
            get => _timer == 0 ? "VS" : _timer.ToString();
            set
            {
                if (!int.TryParse(value, out _timer))
                    _timer = 0;

                OnPropertyChanged();
            }
        }

        private class CharacterMini
        {
            public Camp Camp { get; set; }
            public string ImageFileName { get; set; } = string.Empty;
        }
    }
}
