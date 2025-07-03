using System.Net.Mime;
using System.Text.Json.Serialization;
using System.Windows.Media;
using neo_bpsys_wpf.Helpers;

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
    public string? Color { get; set; } = color;

    [JsonIgnore] public Brush ColorBrush => ColorHelper.HexToBrush(string.IsNullOrEmpty(Color) ? "#FFFFFFFF" : Color);
    
    public string? FontFamilySite { get; set; } = fontFamilySite;

    [JsonIgnore]
    public FontFamily FontFamily
    {
        get
        {
            if (string.IsNullOrEmpty(FontFamilySite)) return new FontFamily("Arial");
            
            return FontFamilySite.StartsWith("pack://application:,,,/")
                ? new FontFamily(new Uri(FontFamilySite[..FontFamilySite.IndexOf('#')]),
                    FontFamilySite[(FontFamilySite.IndexOf('#'))..])
                : new FontFamily(FontFamilySite);
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
    public TextSettings Timer { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 58);

    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 28);

    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 50);

    public TextSettings MajorPoints { get; set; } = new("#FFFFFFFF", "Arial", 28);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 16);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#汉仪第五人格体简", 20);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 16);
}

public class CutSceneWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public bool IsBlackTalentAndTraitEnable { get; set; } = false;
    public string? BgUri { get; set; }
    public CutSceneWindowTextSettings TextSettings { get; set; } = new();
}

public class CutSceneWindowTextSettings
{
    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 28);

    public TextSettings MajorPoints { get; set; } = new("#FFFFFFFF", "Arial", 28);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 18);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#汉仪第五人格体简", 28);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 24);
}

public class ScoreWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? SurScoreBgImageUri { get; set; }
    public string? HunScoreBgImageUri { get; set; }
    public string? GlobalScoreBgImageUri { get; set; }
    public double GlobalScoreTotalMargin { get; set; } = 390;
    public ScoreWindowTextSettings TextSettings { get; set; } = new();
}

public class ScoreWindowTextSettings
{
    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 36);

    public TextSettings MajorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 24);

    public TextSettings TeamName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 20);

    public TextSettings ScoreGlobal_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 18);

    public TextSettings ScoreGlobal_Data { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 16);

    public TextSettings ScoreGlobal_Total { get; set; } =
        new TextSettings("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 48);
}

public class GameDataWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? BgImageUri { get; set; }
    public GameDataWindowTextSettings TextSettings { get; set; } = new();
}

public class GameDataWindowTextSettings
{
    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 32);

    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 80);

    public TextSettings MajorPoints { get; set; } = new("#FFFFFFFF", "Arial", 30);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 22);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#汉仪第五人格体简", 18);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 16);

    public TextSettings SurData { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 16);
    
    public TextSettings HunData { get; set; } = new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 22);
}

public class WidgetsWindowSettings
{
    public string? MapBpBgUri { get; set; }
    public string? BpOverviewBgUri { get; set; }
    public WidgetsWindowTextSettings TextSettings { get; set; } = new();
}

public class WidgetsWindowTextSettings
{
    public TextSettings MapBp_MapName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#汉仪第五人格体简", 22);

    public TextSettings MapBp_PickWord { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings MapBp_BanWord { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings MapBp_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#汉仪第五人格体简", 20);

    public TextSettings BpOverview_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#Source Han Sans HW SC VF", 22);

    public TextSettings BpOverview_GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings BpOverview_MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/neo-bpsys-wpf;Component/Assets/Fonts/#华康POP1体W5", 50);
}