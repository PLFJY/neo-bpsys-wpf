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

            // 加载角色数据
            if (!File.Exists("Resources\\data\\CharacterList.json")) return;

            var character = File.ReadAllText("Resources\\data\\CharacterList.json");
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var characters = JsonSerializer.Deserialize<Dictionary<string, Character>>(character, options);
            foreach (var i in characters)
            {
                CharacterList.Add(i.Value);

                if (i.Value.Type == Types.Sur)
                    SurNameList.Add(i.Value.Name);
                else
                    HunNameList.Add(i.Value.Name);
            }

            SurNameList.Sort();
            HunNameList.Sort();
        }

        public Team MainTeam { get; set; } = new();
        public Team AwayTeam { get; set; } = new();
        public Team CurrentSurTeam { get; set; }
        public Team CurrentHunTeam { get; set; }
        public Game CurrentGame { get; set; } = new();
        public GameProgresses GameProgress { get; set; } = GameProgresses.Game1FirstHalf;
        public List<Character> CharacterList { get; set; } = new();
        public List<string> SurNameList { get; set; } = new();
        public List<string> HunNameList { get; set; } = new();
    }
}
