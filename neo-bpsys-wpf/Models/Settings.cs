using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

public class Settings
{
    public BpWindowSettings BpWindowSettings { get; set; } = new();
    public CutSceneWindowSettings CutSceneWindowSettings { get; set; } = new();
    public ScoreWindowSettings ScoreWindowSettings { get; set; } = new();
    public GameDataWindowSettings GameDataWindowSettings { get; set; } = new();
    public WidgetsWindowSettings WidgetsWindowSettings { get; set; } = new();
}

public class TextSettings(string color, string fontFamilySite, double fontSize)
{
    public string Color { get; set; } = color;
    public string FontFamilySite { get; set; } = fontFamilySite;

    private FontFamily? _fontFamily;
    
    [JsonIgnore]
    public FontFamily FontFamily
    {
        get
        {
            if (_fontFamily == null) return _fontFamily = new FontFamily("Arial");
            
            _fontFamily = FontFamilySite.StartsWith("pack://application:,,,/")
                ? new FontFamily(new Uri("pack://application:,,,/"),
                    FontFamilySite[("pack://application:,,,/".Length - 1)..])
                : new FontFamily(FontFamilySite);

            return _fontFamily;
        }
    }

    public double FontSize { get; set; } = fontSize;
}

public class BpWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? BgImageUri { get; set; }
    public string? CurrentBanLockImageUri { get; set; }
    public string? GlobalBanLockImageUri { get; set; }
    public string? PickingBorderImageUri { get; set; }
    public BpWindowTextSettings TextSettings { get; set; } = new();
}

public class BpWindowTextSettings
{
    public TextSettings Timer { get; set; } = new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 58);

    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 28);

    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 50);

    public TextSettings MajorScore { get; set; } = new("#FFFFFFFF", "Arial", 28);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 16);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#汉仪第五人格体简", 20);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 16);
}

public class CutSceneWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? BgUri { get; set; }
    public CutSceneWindowTextSettings TextSettings { get; set; } = new();
}

public class CutSceneWindowTextSettings
{
    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 28);

    public TextSettings MajorScore { get; set; } = new("#FFFFFFFF", "Arial", 28);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 18);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#汉仪第五人格体简", 28);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 24);
}

public class ScoreWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? SurScoreBgImageUri { get; set; }
    public string? HunScoreBgImageUri { get; set; }
    public string? GlobalScoreBgImageUri { get; set; }
    public ScoreWindowTextSettings TextSettings { get; set; } = new();
}

public class ScoreWindowTextSettings
{
    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 36);

    public TextSettings MajorScore { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 24);

    public TextSettings TeamName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 20);

    public TextSettings ScoreGlobal_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 18);

    public TextSettings ScoreGlobal_Data { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 16);
}

public class GameDataWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? BgImageUri { get; set; }
    public GameDataWindowTextSettings BpWindowTextSettings { get; set; } = new();
}

public class GameDataWindowTextSettings
{
    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 32);

    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 80);

    public TextSettings MajorScore { get; set; } = new("#FFFFFFFF", "Arial", 30);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 22);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#汉仪第五人格体简", 18);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 16);

    public TextSettings GameData { get; set; } = new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 16);
}

public class WidgetsWindowSettings
{
    public string? MapBpBgUri { get; set; }
    public string? BpOverviewBgUri { get; set; }
    public WidgetsWindowTextSettings BpWindowTextSettings { get; set; } = new();
}

public class WidgetsWindowTextSettings
{
    public TextSettings MapBp_MapName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#汉仪第五人格体简", 22);

    public TextSettings MapBp_PickWord { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 22);

    public TextSettings MapBp_BanWord { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 22);

    public TextSettings MapBp_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#汉仪第五人格体简", 20);

    public TextSettings BpOverview_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#Source Han Sans HW SC VF", 22);

    public TextSettings BpOverview_GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 22);

    public TextSettings BpOverview_MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts#华康POP1体W5", 50);
}