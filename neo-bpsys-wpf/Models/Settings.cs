using System.Net.Mime;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Models;

public partial class Settings : ObservableObject
{
    [ObservableProperty] private BpWindowSettings _bpWindowSettings = new();
    [ObservableProperty] private CutSceneWindowSettings _cutSceneWindowSettings = new();
    [ObservableProperty] private ScoreWindowSettings _scoreWindowSettings = new();
    [ObservableProperty] private GameDataWindowSettings _gameDataWindowSettings = new();
    [ObservableProperty] private WidgetsWindowSettings _widgetsWindowSettings = new();
}

/// <summary>
/// 文本设置
/// </summary>
public partial class TextSettings : ObservableObject
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Foreground))]
    private string? _color;

    /// <summary>
    /// 文本颜色Brush
    /// </summary>
    [JsonIgnore]
    public Brush Foreground => ColorHelper.HexToBrush(string.IsNullOrEmpty(Color) ? "#FFFFFFFF" : Color);

    public string? FontFamilySite { get; set; }

    private FontFamily? _fontFamily;

    /// <summary>
    /// 字体(字体对象)
    /// </summary>
    [JsonIgnore]
    public FontFamily FontFamily
    {
        get
        {
            if (string.IsNullOrEmpty(FontFamilySite)) return new FontFamily("Arial");

            return FontFamilySite.StartsWith("pack://application:,,,/")
                ? new FontFamily(new Uri(FontFamilySite[..FontFamilySite.IndexOf('#')]),
                    "./" + FontFamilySite[FontFamilySite.IndexOf('#')..])
                : new FontFamily(FontFamilySite);
        }
        set
        {
            _fontFamily = value;
            FontFamilySite = _fontFamily.Source;
        }
    }
    
    [ObservableProperty] private FontWeight _fontWeight;

    [ObservableProperty] private double _fontSize;
    
    [JsonConstructor]
    public TextSettings() { }

    /// <summary>
    /// 文本设置
    /// </summary>
    /// <param name="color">文本颜色</param>
    /// <param name="fontFamilySite">字体地址</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="fontWeight">字体粗细</param>
    public TextSettings(string color, string? fontFamilySite, double fontSize, FontWeight? fontWeight = null)
    {
        Color = color;
        FontFamilySite = fontFamilySite;
        FontWeight = fontWeight?? FontWeights.Normal;
        FontSize = fontSize;
    }
}

/// <summary>
/// BP窗口设置
/// </summary>
public class BpWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? BgImageUri { get; set; }
    public string? CurrentBanLockImageUri { get; set; }
    public string? GlobalBanLockImageUri { get; set; }
    public string? PickingBorderImageUri { get; set; }
    public BpWindowTextSettings TextSettings { get; set; } = new();
}

/// <summary>
/// BP窗口文本设置
/// </summary>
public class BpWindowTextSettings
{
    public TextSettings Timer { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 58, FontWeights.Bold);

    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 28);

    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 50, FontWeights.Bold);

    public TextSettings MajorPoints { get; set; } = new("#FFFFFFFF", "Arial", 28, FontWeights.Bold);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 16);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简", 20);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 16);
}

/// <summary>
/// 过场窗口设置
/// </summary>
public class CutSceneWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public bool IsBlackTalentAndTraitEnable { get; set; } = false;
    public string? BgUri { get; set; }
    public CutSceneWindowTextSettings TextSettings { get; set; } = new();
}

/// <summary>
/// 过场窗口文本设置
/// </summary>
public class CutSceneWindowTextSettings
{
    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 28);

    public TextSettings MajorPoints { get; set; } = new("#FFFFFFFF", "Arial", 28, FontWeights.Bold);

    public TextSettings SurPlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 18);
    
    public TextSettings HunPlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 24);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简", 28);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 24);
}

/// <summary>
/// 比分窗口设置
/// </summary>
public class ScoreWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? SurScoreBgImageUri { get; set; }
    public string? HunScoreBgImageUri { get; set; }
    public string? GlobalScoreBgImageUri { get; set; }
    public string? GlobalScoreBgImageUriBo3 { get; set; }
    public double GlobalScoreTotalMargin { get; set; } = 390;
    public ScoreWindowTextSettings TextSettings { get; set; } = new();
}

/// <summary>
/// 分数窗口文本设置
/// </summary>
public class ScoreWindowTextSettings
{
    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 100);

    public TextSettings MajorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 38);

    public TextSettings TeamName { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 32);

    public TextSettings ScoreGlobal_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 18);

    public TextSettings ScoreGlobal_Data { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 16);

    public TextSettings ScoreGlobal_Total { get; set; } =
        new TextSettings("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 48, FontWeights.Bold);
}

/// <summary>
/// 赛后数据窗口设置
/// </summary>
public class GameDataWindowSettings
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public string? BgImageUri { get; set; }
    public GameDataWindowTextSettings TextSettings { get; set; } = new();
}

/// <summary>
/// 赛后数据窗口文本设置
/// </summary>
public class GameDataWindowTextSettings
{
    public TextSettings TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 32);

    public TextSettings MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 80, FontWeights.Bold);

    public TextSettings MajorPoints { get; set; } = new("#FFFFFFFF", "Arial", 30, FontWeights.Bold);

    public TextSettings PlayerId { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 22);

    public TextSettings MapName { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简", 18);

    public TextSettings GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 16);

    public TextSettings SurData { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings HunData { get; set; } = new("#FFFFFFFF",
        "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 22);
}

/// <summary>
/// 小组件窗口设置
/// </summary>
public class WidgetsWindowSettings
{
    public string? MapBpBgUri { get; set; }
    public string? BpOverviewBgUri { get; set; }
    public WidgetsWindowTextSettings TextSettings { get; set; } = new();
}

/// <summary>
/// 小组件窗口文本设置
/// </summary>
public class WidgetsWindowTextSettings
{
    public TextSettings MapBp_MapName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简", 22);

    public TextSettings MapBp_PickWord { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings MapBp_BanWord { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings MapBp_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简", 22);

    public TextSettings BpOverview_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 22);

    public TextSettings BpOverview_GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings BpOverview_MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 50);
}