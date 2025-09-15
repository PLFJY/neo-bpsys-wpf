using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 设置
/// </summary>
public partial class Settings : ViewModelBase
{
    public bool ShowTip { get; set; } = true;
    [ObservableProperty] private BpWindowSettings _bpWindowSettings = new();
    [ObservableProperty] private CutSceneWindowSettings _cutSceneWindowSettings = new();
    [ObservableProperty] private ScoreWindowSettings _scoreWindowSettings = new();
    [ObservableProperty] private GameDataWindowSettings _gameDataWindowSettings = new();
    [ObservableProperty] private WidgetsWindowSettings _widgetsWindowSettings = new();
}

/// <summary>
/// 文本设置
/// </summary>
public partial class TextSettings : ViewModelBase
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Foreground))]
    private string? _color;

    /// <summary>
    /// 文本颜色Brush
    /// </summary>
    [JsonIgnore]
    public Brush Foreground => ColorHelper.HexToBrush(string.IsNullOrEmpty(Color) ? "#FFFFFFFF" : Color);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FontFamily))]
    private string? _fontFamilySite;

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
    public TextSettings()
    {
    }

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
        FontWeight = fontWeight ?? FontWeights.Normal;
        FontSize = fontSize;
    }
}

/// <summary>
/// BP窗口设置
/// </summary>
public partial class BpWindowSettings : ViewModelBase
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BgImage))]
    private string? _bgImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CurrentBanLockImage))]
    private string? _currentBanLockImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(GlobalBanLockImage))]
    private string? _globalBanLockImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PickingBorderImage))]
    private string? _pickingBorderImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PickingBorderBrush))]
    private string? _pickingBorderColor = Colors.White.ToString();

    /// <summary>
    /// 文本颜色Brush
    /// </summary>
    [JsonIgnore]
    public Brush PickingBorderBrush => ColorHelper.HexToBrush(string.IsNullOrEmpty(PickingBorderColor)
        ? Colors.White.ToString()
        : PickingBorderColor);

    [ObservableProperty] private BpWindowTextSettings _textSettings = new();

    [JsonIgnore] public ImageSource? BgImage => ImageHelper.GetUiImageFromSetting(BgImageUri, "bp");

    [JsonIgnore]
    public ImageSource? CurrentBanLockImage => ImageHelper.GetUiImageFromSetting(CurrentBanLockImageUri, "CurrentBanLock");

    [JsonIgnore]
    public ImageSource? GlobalBanLockImage => ImageHelper.GetUiImageFromSetting(GlobalBanLockImageUri, "GlobalBanLock");

    [JsonIgnore]
    public ImageSource? PickingBorderImage => ImageHelper.GetUiImageFromSetting(PickingBorderImageUri, "pickingBorder");
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
public partial class CutSceneWindowSettings : ViewModelBase
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);
    public bool IsBlackTalentAndTraitEnable { get; set; } = false;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BgImage))]
    private string? _bgUri;

    [ObservableProperty] private CutSceneWindowTextSettings _textSettings = new();

    [JsonIgnore] public ImageSource? BgImage => ImageHelper.GetUiImageFromSetting(BgUri, "cutScene");
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
public partial class ScoreWindowSettings : ViewModelBase
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SurScoreBgImage))]
    private string? _surScoreBgImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HunScoreBgImage))]
    private string? _hunScoreBgImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(GlobalScoreBgImage))]
    private string? _globalScoreBgImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(GlobalScoreBgImageBo3))]
    private string? _globalScoreBgImageUriBo3;

    public bool IsCampIconBlackVerEnabled { get; set; }
    public double GlobalScoreTotalMargin { get; set; } = 390;
    [ObservableProperty] private ScoreWindowTextSettings _textSettings = new();

    [JsonIgnore]
    public ImageSource? SurScoreBgImage => ImageHelper.GetUiImageFromSetting(SurScoreBgImageUri, "scoreSur");

    [JsonIgnore]
    public ImageSource? HunScoreBgImage => ImageHelper.GetUiImageFromSetting(HunScoreBgImageUri, "scoreHun");

    [JsonIgnore]
    public ImageSource? GlobalScoreBgImage => ImageHelper.GetUiImageFromSetting(GlobalScoreBgImageUri, "scoreGlobal");

    [JsonIgnore]
    public ImageSource? GlobalScoreBgImageBo3 =>
        ImageHelper.GetUiImageFromSetting(GlobalScoreBgImageUriBo3, "scoreGlobal_Bo3");
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
        new("#FFFFFFFF", "Arial", 24, FontWeights.Bold);

    public TextSettings ScoreGlobal_Total { get; set; } =
        new TextSettings("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 48, FontWeights.Bold);
}

/// <summary>
/// 赛后数据窗口设置
/// </summary>
public partial class GameDataWindowSettings : ViewModelBase
{
    public WindowResolution Resolution { get; set; } = new(1440, 810);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BgImage))]
    private string? _bgImageUri;

    [ObservableProperty] private GameDataWindowTextSettings _textSettings = new();

    [JsonIgnore] public ImageSource? BgImage => ImageHelper.GetUiImageFromSetting(BgImageUri, "gameData");
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
public partial class WidgetsWindowSettings : ViewModelBase
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MapBpBgImage))]
    private string? _mapBpBgUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MapBpV2BgImage))]
    private string? _mapBpV2BgUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MapBpV2PickBorderImage))]
    private string? _mapBpV2PickingBorderImageUri;

    public bool IsCampIconBlackVerEnabled { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BpOverviewBgImage))]
    private string? _bpOverviewBgUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CurrentBanLockImage))]
    private string? _currentBanLockImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(GlobalBanLockImage))]
    private string? _globalBanLockImageUri;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MapBpV2_PickingBorderBrush))]
    private string? _mapBpV2_PickingBorderColor = Colors.DarkGreen.ToString();

    /// <summary>
    /// MapBpV2文本颜色Brush
    /// </summary>
    [JsonIgnore]
    public Brush MapBpV2_PickingBorderBrush => ColorHelper.HexToBrush(string.IsNullOrEmpty(MapBpV2_PickingBorderColor)
        ? Colors.DarkGreen.ToString()
        : MapBpV2_PickingBorderColor);

    [ObservableProperty] private WidgetsWindowTextSettings _textSettings = new();

    [JsonIgnore] public ImageSource? MapBpBgImage => ImageHelper.GetUiImageFromSetting(MapBpBgUri, "mapBp");
    [JsonIgnore] public ImageSource? MapBpV2BgImage => ImageHelper.GetUiImageFromSetting(MapBpV2BgUri, "mapBpV2");

    [JsonIgnore]
    public ImageSource? MapBpV2PickBorderImage =>
        ImageHelper.GetUiImageFromSetting(MapBpV2PickingBorderImageUri, "pickingBorder");

    [JsonIgnore]
    public ImageSource? BpOverviewBgImage => ImageHelper.GetUiImageFromSetting(BpOverviewBgUri, "bpOverview");

    [JsonIgnore]
    public ImageSource? CurrentBanLockImage =>
        ImageHelper.GetUiImageFromSetting(CurrentBanLockImageUri, "CurrentBanLock");

    [JsonIgnore]
    public ImageSource? GlobalBanLockImage => ImageHelper.GetUiImageFromSetting(GlobalBanLockImageUri, "GlobalBanLock");
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

    public TextSettings MapBpV2_MapName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简", 16);

    public TextSettings MapBpV2_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 18);

    public TextSettings MapBpV2_CampWords { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 20);

    public TextSettings BpOverview_TeamName { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#Source Han Sans HW SC VF", 22);

    public TextSettings BpOverview_GameProgress { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 22);

    public TextSettings BpOverview_MinorPoints { get; set; } =
        new("#FFFFFFFF", "pack://application:,,,/Assets/Fonts/#华康POP1体W5", 50);
}