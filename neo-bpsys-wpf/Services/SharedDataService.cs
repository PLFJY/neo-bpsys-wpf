using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Services
{
    public class SharedDataService : ISharedDataService
    {
        public SharedDataService()
        {
            MainTeam = new Team();
            AwayTeam = new Team();

            CurrentSurTeam = MainTeam;
            CurrentHunTeam = AwayTeam;

            CurrentGameProgress = GameProgress.Free;

            CurrentGame = new(CurrentSurTeam, CurrentHunTeam, CurrentGameProgress);

            CharacterList = new();
            SurCharaList = new();
            HunCharaList = new();

            var charaListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\CharacterList.json");

            if (!File.Exists(charaListFilePath)) return;

            // 加载角色数据
            var character = File.ReadAllText(charaListFilePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var characters = JsonSerializer.Deserialize<Dictionary<string, CharacterMini>>(character, options);

            if (character == null) return;

            foreach (var i in characters)
            {
                CharacterList?.Add(i.Key, new Character(i.Key, i.Value.Camp, i.Value.ImageFileName));

                if (i.Value.Camp == Camp.Sur)
                    SurCharaList?.Add(i.Key, new Character(i.Key, Camp.Sur, i.Value.ImageFileName));
                else
                    HunCharaList?.Add(i.Key, new Character(i.Key, Camp.Hun, i.Value.ImageFileName));
            }

            SurCharaList = SurCharaList?.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value)!;
            HunCharaList = HunCharaList?.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value)!;
        }

        public Team MainTeam { get; set; }
        public Team AwayTeam { get; set; }
        public Team CurrentSurTeam { get; set; }
        public Team CurrentHunTeam { get; set; }
        public Game CurrentGame { get; set; }
        public GameProgress CurrentGameProgress { get; set; }
        public Dictionary<string, Character> CharacterList { get; set; }
        public Dictionary<string, Character> SurCharaList { get; set; }
        public Dictionary<string, Character> HunCharaList { get; set; }
    }
    public class CharacterMini
    {
        public Camp Camp { get; set; }
        public string ImageFileName { get; set; } = string.Empty;
    }
}
