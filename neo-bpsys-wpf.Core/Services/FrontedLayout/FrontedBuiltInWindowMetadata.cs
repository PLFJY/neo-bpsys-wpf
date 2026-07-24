using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 提供内置 v3 前台窗口的显示元数据（分组、排序、本地化显示名）。
/// </summary>
/// <remarks>
/// 该类型替代旧 <c>FrontedWindowRegistryService.GetBuiltInV3Windows</c> 中硬编码的内置窗口清单，
/// 供 <see cref="FrontedV3LayoutWindowRegistryExtensions.AddFrontedV3LayoutWindow"/> 在注册内置窗口时查阅。
/// </remarks>
internal static class FrontedBuiltInWindowMetadata
{
    /// <summary>
    /// 尝试获取指定内置窗口名的显示元数据。
    /// </summary>
    /// <param name="windowName">内置窗口名，例如 <c>BpWindow</c>。</param>
    /// <param name="groupKey">稳定的管理分组键。</param>
    /// <param name="displayOrder">管理分组内的显示顺序。</param>
    /// <param name="i18nDisplayNames">按语言键索引的本地化显示名。</param>
    /// <returns>找到元数据时返回 <see langword="true"/>。</returns>
    public static bool TryGetMetadata(
        string windowName,
        out string groupKey,
        out int displayOrder,
        out IReadOnlyDictionary<LanguageKey, string>? i18nDisplayNames)
    {
        var entry = windowName switch
        {
            "BpWindow" => ("BuiltIn", 0, CreateI18n("BP 主窗口", "BP Main Window", "BP メインウィンドウ")),
            "CutSceneWindow" => ("BuiltIn", 100, CreateI18n("过场窗口", "Cut Scene Window", "カットシーンウィンドウ")),
            "ScoreSurWindow" => ("BuiltIn", 300, CreateI18n("求生者游戏内比分窗口", "Survivor Score in Gane Window", "サバイバー小スコアウィンドウ")),
            "ScoreHunWindow" => ("BuiltIn", 400, CreateI18n("监管者游戏内比分窗口", "Hunter Score in Gane Window", "ハンター小スコアウィンドウ")),
            "ScoreGlobalWindow" => ("BuiltIn", 500, CreateI18n("全局比分窗口", "Global Score Window", "全体スコアウィンドウ")),
            "GameDataWindow" => ("BuiltIn", 600, CreateI18n("赛后数据窗口", "Post-match Data Window", "試合後データウィンドウ")),
            "BpOverviewWindow" => ("BuiltIn", 700, CreateI18n("BP 总览窗口", "BP Overview Window", "BP 概要ウィンドウ")),
            "MapV2Window" => ("BuiltIn", 710, CreateI18n("地图 BP v2 窗口", "Map BP v2 Window", "マップ BP v2 ウィンドウ")),
            _ => ((string, int, IReadOnlyDictionary<LanguageKey, string>?)?)null
        };

        if (entry is { } value)
        {
            groupKey = value.Item1;
            displayOrder = value.Item2;
            i18nDisplayNames = value.Item3;
            return true;
        }

        groupKey = "BuiltIn";
        displayOrder = int.MaxValue;
        i18nDisplayNames = null;
        return false;
    }

    private static IReadOnlyDictionary<LanguageKey, string> CreateI18n(string zhHans, string enUs, string jaJp)
    {
        return new Dictionary<LanguageKey, string>
        {
            [LanguageKey.zh_Hans] = zhHans,
            [LanguageKey.en_US] = enUs,
            [LanguageKey.ja_JP] = jaJp
        };
    }
}
