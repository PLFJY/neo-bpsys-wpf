using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.Legacy;

/// <summary>
/// Legacy Config.json frontend settings DTO. These types exist only for migration.
/// </summary>
public sealed class LegacySettings
{
    public int? Version { get; set; }

    public LegacyBpWindowSettings? BpWindowSettings { get; set; }

    public LegacyCutSceneWindowSettings? CutSceneWindowSettings { get; set; }

    public LegacyScoreWindowSettings? ScoreWindowSettings { get; set; }

    public LegacyGameDataWindowSettings? GameDataWindowSettings { get; set; }

    public LegacyWidgetsWindowSettings? WidgetsWindowSettings { get; set; }
}

public sealed class LegacyTextSettings
{
    private FontFamily? _fontFamily;

    /// <summary>
    /// Gets or sets whether this legacy text style was enabled by the user.
    /// </summary>
    public bool IsActive { get; set; }

    public string? Color { get; set; }

    public string? FontFamilySite { get; set; }

    public FontWeight FontWeight { get; set; }

    public double FontSize { get; set; }

    [JsonIgnore]
    public Brush Foreground => new BrushConverter().ConvertFromString(string.IsNullOrWhiteSpace(Color)
        ? "#FFFFFFFF"
        : Color) as Brush ?? Brushes.White;

    [JsonIgnore]
    public FontFamily FontFamily
    {
        get
        {
            if (string.IsNullOrEmpty(FontFamilySite))
            {
                return new FontFamily("Arial");
            }

            return FontFamilySite.StartsWith("pack://application:,,,/Assets/Fonts/", StringComparison.Ordinal)
                ? new FontFamily(
                    new Uri(FontFamilySite[..FontFamilySite.IndexOf('#', StringComparison.Ordinal)]),
                    "./" + FontFamilySite[FontFamilySite.IndexOf('#', StringComparison.Ordinal)..])
                : new FontFamily(FontFamilySite);
        }
        set
        {
            _fontFamily = value;
            FontFamilySite = _fontFamily.Source;
        }
    }
}

public sealed class LegacyBpWindowSettings
{
    public WindowSize? WindowSize { get; set; }

    public string? BgImageUri { get; set; }

    public string? CurrentBanLockImageUri { get; set; }

    public string? GlobalBanLockImageUri { get; set; }

    public string? PickingBorderImageUri { get; set; }

    public string? PickingBorderColor { get; set; }

    public string? BackgroundColor { get; set; }

    public bool AllowsWindowTransparency { get; set; }

    public LegacyBpWindowTextSettings? TextSettings { get; set; }
}

public sealed class LegacyBpWindowTextSettings
{
    public LegacyTextSettings? Timer { get; set; }

    public LegacyTextSettings? TeamName { get; set; }

    public LegacyTextSettings? GameScores { get; set; }

    public LegacyTextSettings? MajorPoints { get; set; }

    public LegacyTextSettings? PlayerId { get; set; }

    public LegacyTextSettings? MapName { get; set; }

    public LegacyTextSettings? GameProgress { get; set; }
}

public sealed class LegacyCutSceneWindowSettings
{
    public WindowSize? WindowSize { get; set; }

    public bool IsBlackTalentAndTraitEnable { get; set; }

    public string? BgUri { get; set; }

    public LegacyCutSceneWindowTextSettings? TextSettings { get; set; }
}

public sealed class LegacyCutSceneWindowTextSettings
{
    public LegacyTextSettings? TeamName { get; set; }

    public LegacyTextSettings? MajorPoints { get; set; }

    public LegacyTextSettings? SurPlayerId { get; set; }

    public LegacyTextSettings? HunPlayerId { get; set; }

    public LegacyTextSettings? MapName { get; set; }

    public LegacyTextSettings? GameProgress { get; set; }
}

public sealed class LegacyScoreWindowSettings
{
    public WindowSize? ScoreInGameWindowSize { get; set; }

    public WindowSize? ScoreGlobalWindowSize { get; set; }

    public string? SurScoreBgImageUri { get; set; }

    public string? HunScoreBgImageUri { get; set; }

    public string? GlobalScoreBgImageUri { get; set; }

    public string? GlobalScoreBgImageUriBo3 { get; set; }

    public bool IsCampIconBlackVerEnabled { get; set; }

    public double GlobalScoreTotalMargin { get; set; }

    public string? ScoreGlobalWindowBackgroundColor { get; set; }

    public bool AllowsScoreGlobalWindowTransparency { get; set; }

    public LegacyScoreWindowTextSettings? TextSettings { get; set; }
}

public sealed class LegacyScoreWindowTextSettings
{
    public LegacyTextSettings? GameScores { get; set; }

    public LegacyTextSettings? MajorPoints { get; set; }

    public LegacyTextSettings? TeamName { get; set; }

    public LegacyTextSettings? ScoreGlobal_TeamName { get; set; }

    public LegacyTextSettings? ScoreGlobal_Data { get; set; }

    public LegacyTextSettings? ScoreGlobal_Total { get; set; }
}

public sealed class LegacyGameDataWindowSettings
{
    public WindowSize? WindowSize { get; set; }

    public string? BgImageUri { get; set; }

    public LegacyGameDataWindowTextSettings? TextSettings { get; set; }
}

public sealed class LegacyGameDataWindowTextSettings
{
    public LegacyTextSettings? TeamName { get; set; }

    public LegacyTextSettings? GameScores { get; set; }

    public LegacyTextSettings? MajorPoints { get; set; }

    public LegacyTextSettings? PlayerId { get; set; }

    public LegacyTextSettings? MapName { get; set; }

    public LegacyTextSettings? GameProgress { get; set; }

    public LegacyTextSettings? SurDataHeader { get; set; }

    public LegacyTextSettings? HunDataHeader { get; set; }

    public LegacyTextSettings? SurData { get; set; }

    public LegacyTextSettings? HunData { get; set; }
}

public sealed class LegacyWidgetsWindowSettings
{
    public WindowSize? WindowSize { get; set; }

    public string? MapBpBgUri { get; set; }

    public string? MapBpV2BgUri { get; set; }

    public string? MapBpV2PickingBorderImageUri { get; set; }

    public bool IsCampIconBlackVerEnabled { get; set; }

    public string? BpOverviewBgUri { get; set; }

    public string? CurrentBanLockImageUri { get; set; }

    public string? GlobalBanLockImageUri { get; set; }

    [JsonPropertyName("MapBpV2_PickingBorderColor")]
    public string? MapBpV2_PickingBorderColor { get; set; }

    public string? BackgroundColor { get; set; }

    public bool AllowsWindowTransparency { get; set; }

    public LegacyWidgetsWindowTextSettings? TextSettings { get; set; }
}

public sealed class LegacyWidgetsWindowTextSettings
{
    public LegacyTextSettings? MapBp_MapName { get; set; }

    public LegacyTextSettings? MapBp_PickWord { get; set; }

    public LegacyTextSettings? MapBp_BanWord { get; set; }

    public LegacyTextSettings? MapBp_TeamName { get; set; }

    public LegacyTextSettings? MapBpV2_MapName { get; set; }

    public LegacyTextSettings? MapBpV2_TeamName { get; set; }

    public LegacyTextSettings? MapBpV2_CampWords { get; set; }

    public LegacyTextSettings? BpOverview_TeamName { get; set; }

    public LegacyTextSettings? BpOverview_GameProgress { get; set; }

    public LegacyTextSettings? BpOverview_GameScores { get; set; }
}
