using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idv_bpsys_avalonia;

public static class StaticData
{
    public static Team MainTeam { get; set; } = new();

    public static Team AwayTeam { get; set; } = new();

    public static Team CurrentSurTeam { get; set; }

    public static Team CurrentHunTeam { get; set; }

    public static Game CurrentGame { get; set; } = new();

    public static GameProgress GameProgress { get; set; } = GameProgress.Game1FirstHalf;

    public static List<Character> CharacterList { get; set; } = new();

    public static List<string> SurSearchingNameList { get; set; } = new();

    public static List<string> HunSearchingNameList { get; set; } = new();

    static StaticData()
    {
        CurrentSurTeam = MainTeam;
        CurrentHunTeam = AwayTeam;

        //加载角色数据
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

            if (i.Value.CharCamp == Camp.Sur)
                SurSearchingNameList.Add(i.Value.SearchingName);
            else
                HunSearchingNameList.Add(i.Value.SearchingName);
        }
    }
}