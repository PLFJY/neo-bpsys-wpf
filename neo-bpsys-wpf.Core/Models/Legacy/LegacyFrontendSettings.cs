using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.Legacy;

/// <summary>
/// 旧版 Config.json 前台窗口设置 DTO。这些类型仅用于迁移。
/// </summary>
public sealed class LegacySettings
{
    /// <summary>
    /// 配置版本号。
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// BP 窗口设置。
    /// </summary>
    public LegacyBpWindowSettings? BpWindowSettings { get; set; }

    /// <summary>
    /// 过场窗口设置。
    /// </summary>
    public LegacyCutSceneWindowSettings? CutSceneWindowSettings { get; set; }

    /// <summary>
    /// 比分窗口设置。
    /// </summary>
    public LegacyScoreWindowSettings? ScoreWindowSettings { get; set; }

    /// <summary>
    /// 比赛数据窗口设置。
    /// </summary>
    public LegacyGameDataWindowSettings? GameDataWindowSettings { get; set; }

    /// <summary>
    /// 小部件窗口设置。
    /// </summary>
    public LegacyWidgetsWindowSettings? WidgetsWindowSettings { get; set; }
}

/// <summary>
/// 旧版文本样式设置，迁移后由 v3 布局文本属性替代。
/// </summary>
public sealed class LegacyTextSettings
{
    private FontFamily? _fontFamily;

    /// <summary>
    /// 文本颜色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 字体资源路径。
    /// </summary>
    public string? FontFamilySite { get; set; }

    /// <summary>
    /// 字体粗细。
    /// </summary>
    public FontWeight FontWeight { get; set; }

    /// <summary>
    /// 字体大小。
    /// </summary>
    public double FontSize { get; set; }

    /// <summary>
    /// 从 <see cref="Color"/> 派生的前景画刷。
    /// </summary>
    [JsonIgnore]
    public Brush Foreground => new BrushConverter().ConvertFromString(string.IsNullOrWhiteSpace(Color)
        ? "#FFFFFFFF"
        : Color) as Brush ?? Brushes.White;

    /// <summary>
    /// 字体族，从 <see cref="FontFamilySite"/> 解析。
    /// </summary>
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

/// <summary>
/// 旧版 BP 窗口设置。
/// </summary>
public sealed class LegacyBpWindowSettings
{
    /// <summary>
    /// 窗口大小。
    /// </summary>
    public WindowSize? WindowSize { get; set; }

    /// <summary>
    /// 背景图片 URI。
    /// </summary>
    public string? BgImageUri { get; set; }

    /// <summary>
    /// 当前 Ban 锁定图片 URI。
    /// </summary>
    public string? CurrentBanLockImageUri { get; set; }

    /// <summary>
    /// 全局 Ban 锁定图片 URI。
    /// </summary>
    public string? GlobalBanLockImageUri { get; set; }

    /// <summary>
    /// 选角边框图片 URI。
    /// </summary>
    public string? PickingBorderImageUri { get; set; }

    /// <summary>
    /// 选角边框颜色。
    /// </summary>
    public string? PickingBorderColor { get; set; }

    /// <summary>
    /// 背景颜色。
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// 是否允许窗口透明。
    /// </summary>
    public bool AllowsWindowTransparency { get; set; }

    /// <summary>
    /// BP 窗口文本样式设置。
    /// </summary>
    public LegacyBpWindowTextSettings? TextSettings { get; set; }
}

/// <summary>
/// 旧版 BP 窗口文本样式设置。
/// </summary>
public sealed class LegacyBpWindowTextSettings
{
    /// <summary>
    /// 计时器文本样式。
    /// </summary>
    public LegacyTextSettings? Timer { get; set; }

    /// <summary>
    /// 队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? TeamName { get; set; }

    /// <summary>
    /// 小比分文本样式。
    /// </summary>
    public LegacyTextSettings? GameScores { get; set; }

    /// <summary>
    /// 大比分文本样式。
    /// </summary>
    public LegacyTextSettings? MajorPoints { get; set; }

    /// <summary>
    /// 选手 ID 文本样式。
    /// </summary>
    public LegacyTextSettings? PlayerId { get; set; }

    /// <summary>
    /// 地图名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapName { get; set; }

    /// <summary>
    /// 对局进度文本样式。
    /// </summary>
    public LegacyTextSettings? GameProgress { get; set; }
}

/// <summary>
/// 旧版过场窗口设置。
/// </summary>
public sealed class LegacyCutSceneWindowSettings
{
    /// <summary>
    /// 窗口大小。
    /// </summary>
    public WindowSize? WindowSize { get; set; }

    /// <summary>
    /// 是否启用天赋/特质黑化显示。
    /// </summary>
    public bool IsBlackTalentAndTraitEnable { get; set; }

    /// <summary>
    /// 背景图片 URI。
    /// </summary>
    public string? BgUri { get; set; }

    /// <summary>
    /// 过场窗口文本样式设置。
    /// </summary>
    public LegacyCutSceneWindowTextSettings? TextSettings { get; set; }
}

/// <summary>
/// 旧版过场窗口文本样式设置。
/// </summary>
public sealed class LegacyCutSceneWindowTextSettings
{
    /// <summary>
    /// 队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? TeamName { get; set; }

    /// <summary>
    /// 大比分文本样式。
    /// </summary>
    public LegacyTextSettings? MajorPoints { get; set; }

    /// <summary>
    /// 求生者选手 ID 文本样式。
    /// </summary>
    public LegacyTextSettings? SurPlayerId { get; set; }

    /// <summary>
    /// 监管者选手 ID 文本样式。
    /// </summary>
    public LegacyTextSettings? HunPlayerId { get; set; }

    /// <summary>
    /// 地图名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapName { get; set; }

    /// <summary>
    /// 对局进度文本样式。
    /// </summary>
    public LegacyTextSettings? GameProgress { get; set; }
}

/// <summary>
/// 旧版比分窗口设置。
/// </summary>
public sealed class LegacyScoreWindowSettings
{
    /// <summary>
    /// 局内比分窗口大小。
    /// </summary>
    public WindowSize? ScoreInGameWindowSize { get; set; }

    /// <summary>
    /// 全局比分窗口大小。
    /// </summary>
    public WindowSize? ScoreGlobalWindowSize { get; set; }

    /// <summary>
    /// 求生者比分背景图片 URI。
    /// </summary>
    public string? SurScoreBgImageUri { get; set; }

    /// <summary>
    /// 监管者比分背景图片 URI。
    /// </summary>
    public string? HunScoreBgImageUri { get; set; }

    /// <summary>
    /// 全局比分背景图片 URI。
    /// </summary>
    public string? GlobalScoreBgImageUri { get; set; }

    /// <summary>
    /// BO3 全局比分背景图片 URI。
    /// </summary>
    public string? GlobalScoreBgImageUriBo3 { get; set; }

    /// <summary>
    /// 是否启用阵营图标黑色版本。
    /// </summary>
    public bool IsCampIconBlackVerEnabled { get; set; }

    /// <summary>
    /// 全局比分总分边距。
    /// </summary>
    public double GlobalScoreTotalMargin { get; set; }

    /// <summary>
    /// 全局比分窗口背景颜色。
    /// </summary>
    public string? ScoreGlobalWindowBackgroundColor { get; set; }

    /// <summary>
    /// 是否允许全局比分窗口透明。
    /// </summary>
    public bool AllowsScoreGlobalWindowTransparency { get; set; }

    /// <summary>
    /// 比分窗口文本样式设置。
    /// </summary>
    public LegacyScoreWindowTextSettings? TextSettings { get; set; }
}

/// <summary>
/// 旧版比分窗口文本样式设置。
/// </summary>
public sealed class LegacyScoreWindowTextSettings
{
    /// <summary>
    /// 小比分文本样式。
    /// </summary>
    public LegacyTextSettings? GameScores { get; set; }

    /// <summary>
    /// 大比分文本样式。
    /// </summary>
    public LegacyTextSettings? MajorPoints { get; set; }

    /// <summary>
    /// 队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? TeamName { get; set; }

    /// <summary>
    /// 全局比分队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? ScoreGlobal_TeamName { get; set; }

    /// <summary>
    /// 全局比分数据文本样式。
    /// </summary>
    public LegacyTextSettings? ScoreGlobal_Data { get; set; }

    /// <summary>
    /// 全局比分总分文本样式。
    /// </summary>
    public LegacyTextSettings? ScoreGlobal_Total { get; set; }
}

/// <summary>
/// 旧版比赛数据窗口设置。
/// </summary>
public sealed class LegacyGameDataWindowSettings
{
    /// <summary>
    /// 窗口大小。
    /// </summary>
    public WindowSize? WindowSize { get; set; }

    /// <summary>
    /// 背景图片 URI。
    /// </summary>
    public string? BgImageUri { get; set; }

    /// <summary>
    /// 比赛数据窗口文本样式设置。
    /// </summary>
    public LegacyGameDataWindowTextSettings? TextSettings { get; set; }
}

/// <summary>
/// 旧版比赛数据窗口文本样式设置。
/// </summary>
public sealed class LegacyGameDataWindowTextSettings
{
    /// <summary>
    /// 队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? TeamName { get; set; }

    /// <summary>
    /// 小比分文本样式。
    /// </summary>
    public LegacyTextSettings? GameScores { get; set; }

    /// <summary>
    /// 大比分文本样式。
    /// </summary>
    public LegacyTextSettings? MajorPoints { get; set; }

    /// <summary>
    /// 选手 ID 文本样式。
    /// </summary>
    public LegacyTextSettings? PlayerId { get; set; }

    /// <summary>
    /// 地图名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapName { get; set; }

    /// <summary>
    /// 对局进度文本样式。
    /// </summary>
    public LegacyTextSettings? GameProgress { get; set; }

    /// <summary>
    /// 求生者数据表头文本样式。
    /// </summary>
    public LegacyTextSettings? SurDataHeader { get; set; }

    /// <summary>
    /// 监管者数据表头文本样式。
    /// </summary>
    public LegacyTextSettings? HunDataHeader { get; set; }

    /// <summary>
    /// 求生者数据文本样式。
    /// </summary>
    public LegacyTextSettings? SurData { get; set; }

    /// <summary>
    /// 监管者数据文本样式。
    /// </summary>
    public LegacyTextSettings? HunData { get; set; }
}

/// <summary>
/// 旧版小部件窗口设置。
/// </summary>
public sealed class LegacyWidgetsWindowSettings
{
    /// <summary>
    /// 窗口大小。
    /// </summary>
    public WindowSize? WindowSize { get; set; }

    /// <summary>
    /// 地图 BP 背景图片 URI。
    /// </summary>
    public string? MapBpBgUri { get; set; }

    /// <summary>
    /// 地图 BP V2 背景图片 URI。
    /// </summary>
    public string? MapBpV2BgUri { get; set; }

    /// <summary>
    /// 地图 BP V2 选角边框图片 URI。
    /// </summary>
    public string? MapBpV2PickingBorderImageUri { get; set; }

    /// <summary>
    /// 是否启用阵营图标黑色版本。
    /// </summary>
    public bool IsCampIconBlackVerEnabled { get; set; }

    /// <summary>
    /// BP 概览背景图片 URI。
    /// </summary>
    public string? BpOverviewBgUri { get; set; }

    /// <summary>
    /// 当前 Ban 锁定图片 URI。
    /// </summary>
    public string? CurrentBanLockImageUri { get; set; }

    /// <summary>
    /// 全局 Ban 锁定图片 URI。
    /// </summary>
    public string? GlobalBanLockImageUri { get; set; }

    /// <summary>
    /// 地图 BP V2 选角边框颜色。
    /// </summary>
    [JsonPropertyName("MapBpV2_PickingBorderColor")]
    public string? MapBpV2_PickingBorderColor { get; set; }

    /// <summary>
    /// 背景颜色。
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// 是否允许窗口透明。
    /// </summary>
    public bool AllowsWindowTransparency { get; set; }

    /// <summary>
    /// 小部件窗口文本样式设置。
    /// </summary>
    public LegacyWidgetsWindowTextSettings? TextSettings { get; set; }
}

/// <summary>
/// 旧版小部件窗口文本样式设置。
/// </summary>
public sealed class LegacyWidgetsWindowTextSettings
{
    /// <summary>
    /// 地图 BP 地图名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapBp_MapName { get; set; }

    /// <summary>
    /// 地图 BP 选角文字文本样式。
    /// </summary>
    public LegacyTextSettings? MapBp_PickWord { get; set; }

    /// <summary>
    /// 地图 BP Ban 文字文本样式。
    /// </summary>
    public LegacyTextSettings? MapBp_BanWord { get; set; }

    /// <summary>
    /// 地图 BP 队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapBp_TeamName { get; set; }

    /// <summary>
    /// 地图 BP V2 地图名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapBpV2_MapName { get; set; }

    /// <summary>
    /// 地图 BP V2 队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? MapBpV2_TeamName { get; set; }

    /// <summary>
    /// 地图 BP V2 阵营文字文本样式。
    /// </summary>
    public LegacyTextSettings? MapBpV2_CampWords { get; set; }

    /// <summary>
    /// BP 概览队伍名称文本样式。
    /// </summary>
    public LegacyTextSettings? BpOverview_TeamName { get; set; }

    /// <summary>
    /// BP 概览对局进度文本样式。
    /// </summary>
    public LegacyTextSettings? BpOverview_GameProgress { get; set; }

    /// <summary>
    /// BP 概览小比分文本样式。
    /// </summary>
    public LegacyTextSettings? BpOverview_GameScores { get; set; }
}