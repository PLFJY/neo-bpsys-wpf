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
            CurrentSurTeam = MainTeam;
            CurrentHunTeam = AwayTeam;

            var characterListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\CharacterList.json");

            if (!File.Exists(characterListFilePath)) return;

            // 加载角色数据
            var character = File.ReadAllText(characterListFilePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var characters = JsonSerializer.Deserialize<Dictionary<string, Character>>(character, options);

            if (characters == null) return;

            foreach (var i in characters)
            {
                CharacterList?.Add(i.Key, i.Value);

                if (i.Value.Camp == Camp.Sur)
                    SurCharList?.Add(i.Key, new Character(i.Value.Name, i.Value.Camp, i.Value.ImageFileName));
                else
                    HunCharList?.Add(i.Key, new Character(i.Value.Name, i.Value.Camp, i.Value.ImageFileName));
            }

            SurCharList?.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            HunCharList?.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public Team MainTeam { get; set; } = new();
        public Team AwayTeam { get; set; } = new();
        public Team CurrentSurTeam { get; set; }
        public Team CurrentHunTeam { get; set; }
        public Game CurrentGame { get; set; }
        public GameProgress CurrentGameProgress { get; set; } = GameProgress.Game1FirstHalf;
        public Dictionary<string, Character> CharacterList { get; set; } = new();
        public Dictionary<string, Character> SurCharList { get; set; } = new();
        public Dictionary<string, Character> HunCharList { get; set; } = new();
    }
}
