using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.Legacy;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Applies legacy frontend TextSettings to Designer v3 control configs.
/// </summary>
public static class LegacyFrontedTextStyleMigrator
{
    private const string BundledFontPrefix = "pack://application:,,,/Assets/Fonts/";

    /// <summary>
    /// Returns whether a window has any legacy text styles.
    /// </summary>
    public static bool HasLegacyTextStyles(string window, LegacySettings legacySettings)
    {
        return window switch
        {
            "BpWindow" => legacySettings.BpWindowSettings?.TextSettings is not null,
            "CutSceneWindow" => legacySettings.CutSceneWindowSettings?.TextSettings is not null,
            "ScoreSurWindow" or "ScoreHunWindow" or "ScoreGlobalWindow" => legacySettings.ScoreWindowSettings?.TextSettings is not null,
            "GameDataWindow" => legacySettings.GameDataWindowSettings?.TextSettings is not null,
            "WidgetsWindow" => legacySettings.WidgetsWindowSettings?.TextSettings is not null,
            _ => false
        };
    }

    /// <summary>
    /// Normalizes legacy font family references into Designer v3 font family strings.
    /// </summary>
    public static string? NormalizeLegacyFontFamilySite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.StartsWith("pack://application:", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        if (text.StartsWith("./#", StringComparison.Ordinal))
        {
            return BundledFontPrefix + "#" + text[3..];
        }

        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            return BundledFontPrefix + text;
        }

        return text switch
        {
            "汉仪第五人格体简" => BundledFontPrefix + "#汉仪第五人格体简",
            "华康POP1体W5" => BundledFontPrefix + "#华康POP1体W5",
            "Noto Sans" => BundledFontPrefix + "#Noto Sans",
            _ => text
        };
    }

    /// <summary>
    /// Applies legacy TextSettings to one v3 canvas config.
    /// </summary>
    public static void Apply(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacySettings legacySettings,
        ICollection<string>? diagnostics = null)
    {
        switch (window)
        {
            case "BpWindow":
                ApplyBpWindow(config, window, canvas, legacySettings.BpWindowSettings?.TextSettings, diagnostics);
                break;
            case "CutSceneWindow":
                ApplyCutSceneWindow(config, window, canvas, legacySettings.CutSceneWindowSettings?.TextSettings, diagnostics);
                break;
            case "ScoreSurWindow":
                ApplyScoreSideWindow(config, window, canvas, "Sur", legacySettings.ScoreWindowSettings?.TextSettings, diagnostics);
                break;
            case "ScoreHunWindow":
                ApplyScoreSideWindow(config, window, canvas, "Hun", legacySettings.ScoreWindowSettings?.TextSettings, diagnostics);
                break;
            case "ScoreGlobalWindow":
                ApplyScoreGlobalWindow(config, window, canvas, legacySettings.ScoreWindowSettings?.TextSettings, diagnostics);
                break;
            case "GameDataWindow":
                ApplyGameDataWindow(config, window, canvas, legacySettings.GameDataWindowSettings?.TextSettings, diagnostics);
                break;
            case "WidgetsWindow":
                ApplyWidgetsWindow(config, window, canvas, legacySettings.WidgetsWindowSettings, diagnostics);
                break;
        }
    }

    private static void ApplyBpWindow(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyBpWindowTextSettings? settings,
        ICollection<string>? diagnostics)
    {
        if (settings is null)
        {
            return;
        }

        ApplyByPredicate(config, window, canvas, settings.Timer, "BpWindowSettings.TextSettings.Timer", name => IsExact(name, "Timer"), diagnostics);
        ApplyByPredicate(config, window, canvas, settings.TeamName, "BpWindowSettings.TextSettings.TeamName", IsTeamName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.GameScores, "BpWindowSettings.TextSettings.GameScores", IsGameScores, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MajorPoints, "BpWindowSettings.TextSettings.MajorPoints", IsMajorPoints, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.PlayerId, "BpWindowSettings.TextSettings.PlayerId", IsAnyPlayerNameOrId, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MapName, "BpWindowSettings.TextSettings.MapName", IsMapName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.GameProgress, "BpWindowSettings.TextSettings.GameProgress", IsGameProgress, diagnostics);
    }

    private static void ApplyCutSceneWindow(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyCutSceneWindowTextSettings? settings,
        ICollection<string>? diagnostics)
    {
        if (settings is null)
        {
            return;
        }

        ApplyByPredicate(config, window, canvas, settings.TeamName, "CutSceneWindowSettings.TextSettings.TeamName", IsTeamName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MajorPoints, "CutSceneWindowSettings.TextSettings.MajorPoints", IsMajorPoints, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.SurPlayerId, "CutSceneWindowSettings.TextSettings.SurPlayerId", IsSurPlayerNameOrId, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.HunPlayerId, "CutSceneWindowSettings.TextSettings.HunPlayerId", IsHunPlayerNameOrId, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MapName, "CutSceneWindowSettings.TextSettings.MapName", IsMapName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.GameProgress, "CutSceneWindowSettings.TextSettings.GameProgress", IsGameProgress, diagnostics);
    }

    private static void ApplyScoreSideWindow(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        string camp,
        LegacyScoreWindowTextSettings? settings,
        ICollection<string>? diagnostics)
    {
        if (settings is null)
        {
            return;
        }

        ApplyByPredicate(config, window, canvas, settings.TeamName, "ScoreWindowSettings.TextSettings.TeamName", name => IsExact(name, $"{camp}TeamName"), diagnostics);
        ApplyByPredicate(config, window, canvas, settings.GameScores, "ScoreWindowSettings.TextSettings.GameScores", name => IsExact(name, $"GameScores{camp}"), diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MajorPoints, "ScoreWindowSettings.TextSettings.MajorPoints", name => IsExact(name, $"{camp}TeamMajorPoint"), diagnostics);
    }

    private static void ApplyScoreGlobalWindow(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyScoreWindowTextSettings? settings,
        ICollection<string>? diagnostics)
    {
        if (settings is null)
        {
            return;
        }

        ApplyByPredicate(config, window, canvas, settings.ScoreGlobal_TeamName, "ScoreWindowSettings.TextSettings.ScoreGlobal_TeamName", IsHomeAwayTeamName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.ScoreGlobal_Data, "ScoreWindowSettings.TextSettings.ScoreGlobal_Data", IsGlobalScoreData, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.ScoreGlobal_Total, "ScoreWindowSettings.TextSettings.ScoreGlobal_Total", IsHomeAwayScoreTotal, diagnostics);
    }

    private static void ApplyGameDataWindow(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyGameDataWindowTextSettings? settings,
        ICollection<string>? diagnostics)
    {
        if (settings is null)
        {
            return;
        }

        ApplyByPredicate(config, window, canvas, settings.TeamName, "GameDataWindowSettings.TextSettings.TeamName", IsTeamName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.GameScores, "GameDataWindowSettings.TextSettings.GameScores", IsGameScores, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MajorPoints, "GameDataWindowSettings.TextSettings.MajorPoints", IsMajorPoints, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.PlayerId, "GameDataWindowSettings.TextSettings.PlayerId", IsAnyPlayerNameOrId, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.MapName, "GameDataWindowSettings.TextSettings.MapName", IsMapName, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.GameProgress, "GameDataWindowSettings.TextSettings.GameProgress", IsGameProgress, diagnostics);
        ApplyByPredicate(config, window, canvas, settings.SurDataHeader, "GameDataWindowSettings.TextSettings.SurDataHeader", name => Contains(name, "SurDataHeader"), diagnostics);
        ApplyByPredicate(config, window, canvas, settings.HunDataHeader, "GameDataWindowSettings.TextSettings.HunDataHeader", name => Contains(name, "HunDataHeader"), diagnostics);
        ApplyByPredicate(config, window, canvas, settings.SurData, "GameDataWindowSettings.TextSettings.SurData", name => Contains(name, "SurData") && !Contains(name, "SurDataHeader"), diagnostics);
        ApplyByPredicate(config, window, canvas, settings.HunData, "GameDataWindowSettings.TextSettings.HunData", name => Contains(name, "HunData") && !Contains(name, "HunDataHeader"), diagnostics);
    }

    private static void ApplyWidgetsWindow(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyWidgetsWindowSettings? settings,
        ICollection<string>? diagnostics)
    {
        var text = settings?.TextSettings;
        if (text is null && settings is null)
        {
            return;
        }

        switch (canvas)
        {
            case "MapBpCanvas":
                ApplyByPredicate(config, window, canvas, text?.MapBp_MapName, "WidgetsWindowSettings.TextSettings.MapBp_MapName", IsMapBpMapName, diagnostics);
                ApplyByPredicate(config, window, canvas, text?.MapBp_PickWord, "WidgetsWindowSettings.TextSettings.MapBp_PickWord", name => IsExact(name, "PickWord"), diagnostics);
                ApplyByPredicate(config, window, canvas, text?.MapBp_BanWord, "WidgetsWindowSettings.TextSettings.MapBp_BanWord", name => IsExact(name, "BanWord"), diagnostics);
                ApplyByPredicate(config, window, canvas, text?.MapBp_TeamName, "WidgetsWindowSettings.TextSettings.MapBp_TeamName", IsTeamName, diagnostics);
                break;
            case "BpOverViewCanvas":
                ApplyByPredicate(config, window, canvas, text?.BpOverview_TeamName, "WidgetsWindowSettings.TextSettings.BpOverview_TeamName", name => IsExact(name, "SurTeamNameInOverview") || IsExact(name, "HunTeamNameInOverview"), diagnostics);
                ApplyByPredicate(config, window, canvas, text?.BpOverview_GameProgress, "WidgetsWindowSettings.TextSettings.BpOverview_GameProgress", IsGameProgress, diagnostics);
                ApplyByPredicate(config, window, canvas, text?.BpOverview_GameScores, "WidgetsWindowSettings.TextSettings.BpOverview_GameScores", name => IsGameScores(name) || IsExact(name, "RatioChar"), diagnostics);
                break;
            case "MapV2Canvas":
                ApplyMapV2(config, window, canvas, settings, diagnostics);
                break;
        }
    }

    private static void ApplyMapV2(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyWidgetsWindowSettings? settings,
        ICollection<string>? diagnostics)
    {
        foreach (var (name, control) in config.Controls)
        {
            if (control is not MapV2DisplayControlConfig map)
            {
                continue;
            }

            if (settings?.TextSettings?.MapBpV2_MapName is not null)
            {
                ApplyMapV2MapNameStyle(map, settings.TextSettings.MapBpV2_MapName);
                diagnostics?.Add($"Legacy text style applied: {window}/{canvas}/{name} <- WidgetsWindowSettings.TextSettings.MapBpV2_MapName");
            }

            if (settings?.TextSettings?.MapBpV2_TeamName is not null)
            {
                ApplyMapV2TeamNameStyle(map, settings.TextSettings.MapBpV2_TeamName);
                diagnostics?.Add($"Legacy text style applied: {window}/{canvas}/{name} <- WidgetsWindowSettings.TextSettings.MapBpV2_TeamName");
            }

            if (settings?.TextSettings?.MapBpV2_CampWords is not null)
            {
                ApplyMapV2CampNameStyle(map, settings.TextSettings.MapBpV2_CampWords);
                diagnostics?.Add($"Legacy text style applied: {window}/{canvas}/{name} <- WidgetsWindowSettings.TextSettings.MapBpV2_CampWords");
            }
        }
    }

    private static void ApplyByPredicate(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        LegacyTextSettings? style,
        string source,
        Func<string, bool> predicate,
        ICollection<string>? diagnostics)
    {
        if (style is null)
        {
            return;
        }

        var applied = false;
        foreach (var (name, control) in config.Controls)
        {
            if (!predicate(name) || control is not IFrontedTextStyleConfig textControl)
            {
                continue;
            }

            ApplyTextStyle(textControl, style);
            applied = true;
            diagnostics?.Add($"Legacy text style applied: {window}/{canvas}/{name} <- {source}");
        }

        if (!applied)
        {
            diagnostics?.Add($"Legacy text style had no v3 target: {window}/{canvas} <- {source}");
        }
    }

    private static void ApplyTextStyle(IFrontedTextStyleConfig target, LegacyTextSettings style)
    {
        if (!string.IsNullOrWhiteSpace(style.Color))
        {
            target.Color = style.Color.Trim();
        }

        var fontFamily = NormalizeLegacyFontFamilySite(style.FontFamilySite);
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            target.FontFamily = fontFamily;
        }

        if (style.FontSize > 0)
        {
            target.FontSize = style.FontSize;
        }

        target.FontWeight = style.FontWeight.ToString();
    }

    private static void ApplyMapV2MapNameStyle(MapV2DisplayControlConfig target, LegacyTextSettings style)
    {
        target.MapNameColor = FirstNonEmpty(style.Color, target.MapNameColor);
        target.MapNameFontFamily = FirstNonEmpty(NormalizeLegacyFontFamilySite(style.FontFamilySite), target.MapNameFontFamily);
        target.MapNameFontWeight = style.FontWeight.ToString();
        if (style.FontSize > 0)
        {
            target.MapNameFontSize = style.FontSize;
        }
    }

    private static void ApplyMapV2TeamNameStyle(MapV2DisplayControlConfig target, LegacyTextSettings style)
    {
        target.TeamNameColor = FirstNonEmpty(style.Color, target.TeamNameColor);
        target.TeamNameFontFamily = FirstNonEmpty(NormalizeLegacyFontFamilySite(style.FontFamilySite), target.TeamNameFontFamily);
        target.TeamNameFontWeight = style.FontWeight.ToString();
        if (style.FontSize > 0)
        {
            target.TeamNameFontSize = style.FontSize;
        }
    }

    private static void ApplyMapV2CampNameStyle(MapV2DisplayControlConfig target, LegacyTextSettings style)
    {
        target.CampNameColor = FirstNonEmpty(style.Color, target.CampNameColor);
        target.CampNameFontFamily = FirstNonEmpty(NormalizeLegacyFontFamilySite(style.FontFamilySite), target.CampNameFontFamily);
        target.CampNameFontWeight = style.FontWeight.ToString();
        if (style.FontSize > 0)
        {
            target.CampNameFontSize = style.FontSize;
        }
    }

    private static string? FirstNonEmpty(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool IsTeamName(string name) =>
        IsExact(name, "SurTeamName")
        || IsExact(name, "HunTeamName")
        || IsExact(name, "HomeTeamName")
        || IsExact(name, "AwayTeamName")
        || EndsWith(name, "TeamName");

    private static bool IsHomeAwayTeamName(string name) =>
        IsExact(name, "HomeTeamName") || IsExact(name, "AwayTeamName");

    private static bool IsGameScores(string name) =>
        IsExact(name, "GameScoresSur")
        || IsExact(name, "GameScoresHun")
        || IsExact(name, "HomeGameScores")
        || IsExact(name, "AwayGameScores")
        || Contains(name, "GameScores");

    private static bool IsMajorPoints(string name) =>
        IsExact(name, "SurTeamMajorPoint")
        || IsExact(name, "HunTeamMajorPoint")
        || IsExact(name, "HomeTeamMajorPoint")
        || IsExact(name, "AwayTeamMajorPoint")
        || Contains(name, "MajorPoint");

    private static bool IsAnyPlayerNameOrId(string name) =>
        IsSurPlayerNameOrId(name) || IsHunPlayerNameOrId(name) || Contains(name, "PlayerId") || Contains(name, "PlayerName");

    private static bool IsSurPlayerNameOrId(string name) =>
        name.StartsWith("SurId", StringComparison.OrdinalIgnoreCase)
        || Contains(name, "SurPlayerId")
        || Contains(name, "SurPlayerName");

    private static bool IsHunPlayerNameOrId(string name) =>
        name.StartsWith("HunId", StringComparison.OrdinalIgnoreCase)
        || Contains(name, "HunPlayerId")
        || Contains(name, "HunPlayerName");

    private static bool IsMapName(string name) =>
        IsExact(name, "MapName") || IsExact(name, "MapNameText") || Contains(name, "MapName");

    private static bool IsMapBpMapName(string name) =>
        IsExact(name, "PickedMapName") || IsExact(name, "BannedMapName") || IsExact(name, "MapNameText") || Contains(name, "MapName");

    private static bool IsGameProgress(string name) =>
        IsExact(name, "GameProgress") || IsExact(name, "GameProgressText");

    private static bool IsGlobalScoreData(string name) =>
        IsExact(name, "HomeGlobalScoreRow")
        || IsExact(name, "AwayGlobalScoreRow")
        || Contains(name, "ScoreData")
        || Contains(name, "GlobalScore");

    private static bool IsHomeAwayScoreTotal(string name) =>
        IsExact(name, "HomeScoreTotal") || IsExact(name, "AwayScoreTotal");

    private static bool IsExact(string name, string value) =>
        string.Equals(name, value, StringComparison.OrdinalIgnoreCase);

    private static bool EndsWith(string name, string suffix) =>
        name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string name, string fragment) =>
        name.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
