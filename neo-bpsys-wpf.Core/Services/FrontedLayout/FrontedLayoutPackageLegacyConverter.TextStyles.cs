#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器的TextStyles逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private static IEnumerable<(string Source, LegacyTextSettings Style)> EnumerateLegacyTextSettings(LegacySettings settings)
    {
        foreach (var setting in new object?[]
                 {
                     settings.BpWindowSettings?.TextSettings,
                     settings.CutSceneWindowSettings?.TextSettings,
                     settings.ScoreWindowSettings?.TextSettings,
                     settings.GameDataWindowSettings?.TextSettings,
                     settings.WidgetsWindowSettings?.TextSettings
                 })
        {
            if (setting is null)
            {
                continue;
            }

            foreach (var property in setting.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetValue(setting) is LegacyTextSettings textStyle)
                {
                    yield return ($"{setting.GetType().Name}.{property.Name}", textStyle);
                }
            }
        }
    }

    private static LegacyTextStyleDefaults GetTextStyleDefaults(string sourceKey)
    {
        const string white = "#FFFFFFFF";
        const string notoSans = "pack://application:,,,/Assets/Fonts/#Noto Sans";
        const string pop = "pack://application:,,,/Assets/Fonts/#华康POP1体W5";
        const string hanyi = "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简";

        return sourceKey switch
        {
            "BpWindow.Timer" => new(null, null, null, null, pop, "Bold", white, 58),
            "BpWindow.TeamName" => new(null, null, null, null, notoSans, "Normal", white, 16),
            "BpWindow.GameScores" => new(null, null, null, null, pop, "Bold", white, 26),
            "BpWindow.MajorPoints" => new(null, null, null, null, "Arial", "Medium", white, 20),
            "BpWindow.PlayerId" => new("Left", null, null, null, notoSans, "Normal", white, 16),
            "BpWindow.MapName" => new(null, null, null, null, hanyi, "Normal", white, 20),
            "BpWindow.GameProgress" => new(null, null, null, null, pop, "Normal", white, 16),
            "CutSceneWindow.TeamName" => new(null, null, null, "WrapWithOverflow", notoSans, "Bold", white, 28),
            "CutSceneWindow.MajorPoints" => new(null, null, null, null, "Arial", "Bold", white, 28),
            "CutSceneWindow.SurPlayerId" => new("Left", null, null, null, notoSans, "Normal", white, 18),
            "CutSceneWindow.HunPlayerId" => new(null, null, null, null, notoSans, "Normal", white, 30),
            "CutSceneWindow.MapName" => new(null, null, null, null, hanyi, "Normal", white, 24),
            "CutSceneWindow.GameProgress" => new(null, null, null, null, pop, "Normal", white, 22),
            "ScoreWindow.GameScores" => new(null, null, null, null, pop, "Normal", white, 100),
            "ScoreWindow.MajorPoints" => new(null, null, null, null, pop, "Normal", white, 38),
            "ScoreWindow.TeamName" => new(null, null, null, null, pop, "Normal", white, 32),
            "ScoreWindow.ScoreGlobal_TeamName" => new(null, null, null, null, pop, "Normal", white, 24),
            "ScoreWindow.ScoreGlobal_Data" => new(null, null, null, null, "Arial", "Bold", white, 24),
            "ScoreWindow.ScoreGlobal_Total" => new(null, null, null, null, pop, "Bold", white, 40),
            "GameDataWindow.TeamName" => new(null, null, null, "WrapWithOverflow", notoSans, "Normal", white, 32),
            "GameDataWindow.GameScores" => new(null, null, null, null, pop, "Bold", white, 80),
            "GameDataWindow.MajorPoints" => new(null, null, null, null, "Arial", "Bold", white, 30),
            "GameDataWindow.PlayerId" => new("Left", null, null, null, notoSans, "Normal", white, 22),
            "GameDataWindow.MapName" => new(null, null, null, null, hanyi, "Normal", white, 22),
            "GameDataWindow.GameProgress" => new(null, null, null, null, pop, "Normal", white, 20),
            "GameDataWindow.SurDataHeader" => new(null, null, null, null, notoSans, "Normal", white, 16),
            "GameDataWindow.HunDataHeader" => new("Left", null, null, null, notoSans, "Normal", white, 16),
            "GameDataWindow.SurData" => new(null, null, null, null, pop, "Normal", white, 22),
            "GameDataWindow.HunData" => new("Right", null, "Right", null, pop, "Normal", white, 22),
            "WidgetsWindow.BpOverview_TeamName" => new(null, null, null, "WrapWithOverflow", notoSans, "Normal", white, 22),
            "WidgetsWindow.BpOverview_GameProgress" => new(null, null, null, null, pop, "Normal", white, 22),
            "WidgetsWindow.BpOverview_GameScores" => new(null, null, null, null, pop, "Normal", white, 50),
            _ => new(null, null, null, null, notoSans, "Normal", white, 16)
        };
    }

    private static void ApplyLegacyTextStyleOverrides(
        FrontedCanvasConfig config,
        LegacyLayoutMapping mapping,
        LegacySettings? legacySettings,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (legacySettings is null)
        {
            return;
        }

        var key = new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas);
        if (!LegacyControlBlueprints.TryGetValue(key, out var blueprints))
        {
            return;
        }

        foreach (var blueprint in blueprints)
        {
            if (blueprint.Status is not LegacyControlBlueprintStatus.Mapped
                and not LegacyControlBlueprintStatus.Aggregated
                || !config.Controls.TryGetValue(blueprint.TargetName, out var control))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(blueprint.TextStyleSourceKey)
                && TryGetLegacyTextStyle(legacySettings, blueprint.TextStyleSourceKey, out var style)
                && style is not null
                && control is IFrontedTextStyleConfig textControl)
            {
                LegacyFrontedTextStyleMigrator.ApplyTextStyle(textControl, style);
                messages.Add(Info(CodeTextSettingsApplied,
                    Args(new
                    {
                        SourceWindow = mapping.SourceWindow,
                        SourceCanvas = mapping.SourceCanvas,
                        ControlName = blueprint.TargetName,
                        TextStyleKey = blueprint.TextStyleSourceKey
                    })));
            }

            if (control is MapV2DisplayControlConfig map)
            {
                ApplyMapV2LegacyTextStyle(map, legacySettings, blueprint, messages, mapping);
            }
        }
    }

    private static void ApplyMapV2LegacyTextStyle(
        MapV2DisplayControlConfig map,
        LegacySettings legacySettings,
        LegacyControlBlueprint blueprint,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages,
        LegacyLayoutMapping mapping)
    {
        if (TryGetLegacyTextStyle(legacySettings, "WidgetsWindow.MapBpV2_MapName", out var mapNameStyle)
            && mapNameStyle is not null)
        {
            LegacyFrontedTextStyleMigrator.ApplyMapV2TextStyle(
                map, mapNameStyle, LegacyMapV2TextStyleTarget.MapName);

            messages.Add(Info(CodeTextSettingsApplied,
                Args(new
                {
                    SourceWindow = mapping.SourceWindow,
                    SourceCanvas = mapping.SourceCanvas,
                    ControlName = blueprint.TargetName,
                    TextStyleKey = "WidgetsWindow.MapBpV2_MapName"
                })));
        }

        if (TryGetLegacyTextStyle(legacySettings, "WidgetsWindow.MapBpV2_TeamName", out var teamNameStyle)
            && teamNameStyle is not null)
        {
            LegacyFrontedTextStyleMigrator.ApplyMapV2TextStyle(
                map, teamNameStyle, LegacyMapV2TextStyleTarget.TeamName);

            messages.Add(Info(CodeTextSettingsApplied,
                Args(new
                {
                    SourceWindow = mapping.SourceWindow,
                    SourceCanvas = mapping.SourceCanvas,
                    ControlName = blueprint.TargetName,
                    TextStyleKey = "WidgetsWindow.MapBpV2_TeamName"
                })));
        }

        if (TryGetLegacyTextStyle(legacySettings, "WidgetsWindow.MapBpV2_CampWords", out var campStyle)
            && campStyle is not null)
        {
            LegacyFrontedTextStyleMigrator.ApplyMapV2TextStyle(
                map, campStyle, LegacyMapV2TextStyleTarget.CampName);

            messages.Add(Info(CodeTextSettingsApplied,
                Args(new
                {
                    SourceWindow = mapping.SourceWindow,
                    SourceCanvas = mapping.SourceCanvas,
                    ControlName = blueprint.TargetName,
                    TextStyleKey = "WidgetsWindow.MapBpV2_CampWords"
                })));
        }
    }

    private static bool TryGetLegacyTextStyle(
        LegacySettings legacySettings,
        string sourceKey,
        out LegacyTextSettings? style)
    {
        style = sourceKey switch
        {
            "BpWindow.Timer" => legacySettings.BpWindowSettings?.TextSettings?.Timer,
            "BpWindow.TeamName" => legacySettings.BpWindowSettings?.TextSettings?.TeamName,
            "BpWindow.GameScores" => legacySettings.BpWindowSettings?.TextSettings?.GameScores,
            "BpWindow.MajorPoints" => legacySettings.BpWindowSettings?.TextSettings?.MajorPoints,
            "BpWindow.PlayerId" => legacySettings.BpWindowSettings?.TextSettings?.PlayerId,
            "BpWindow.MapName" => legacySettings.BpWindowSettings?.TextSettings?.MapName,
            "BpWindow.GameProgress" => legacySettings.BpWindowSettings?.TextSettings?.GameProgress,
            "CutSceneWindow.TeamName" => legacySettings.CutSceneWindowSettings?.TextSettings?.TeamName,
            "CutSceneWindow.MajorPoints" => legacySettings.CutSceneWindowSettings?.TextSettings?.MajorPoints,
            "CutSceneWindow.SurPlayerId" => legacySettings.CutSceneWindowSettings?.TextSettings?.SurPlayerId,
            "CutSceneWindow.HunPlayerId" => legacySettings.CutSceneWindowSettings?.TextSettings?.HunPlayerId,
            "CutSceneWindow.MapName" => legacySettings.CutSceneWindowSettings?.TextSettings?.MapName,
            "CutSceneWindow.GameProgress" => legacySettings.CutSceneWindowSettings?.TextSettings?.GameProgress,
            "ScoreWindow.GameScores" => legacySettings.ScoreWindowSettings?.TextSettings?.GameScores,
            "ScoreWindow.MajorPoints" => legacySettings.ScoreWindowSettings?.TextSettings?.MajorPoints,
            "ScoreWindow.TeamName" => legacySettings.ScoreWindowSettings?.TextSettings?.TeamName,
            "ScoreWindow.ScoreGlobal_TeamName" => legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_TeamName,
            "ScoreWindow.ScoreGlobal_Data" => legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_Data,
            "ScoreWindow.ScoreGlobal_Total" => legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_Total,
            "GameDataWindow.TeamName" => legacySettings.GameDataWindowSettings?.TextSettings?.TeamName,
            "GameDataWindow.GameScores" => legacySettings.GameDataWindowSettings?.TextSettings?.GameScores,
            "GameDataWindow.MajorPoints" => legacySettings.GameDataWindowSettings?.TextSettings?.MajorPoints,
            "GameDataWindow.PlayerId" => legacySettings.GameDataWindowSettings?.TextSettings?.PlayerId,
            "GameDataWindow.MapName" => legacySettings.GameDataWindowSettings?.TextSettings?.MapName,
            "GameDataWindow.GameProgress" => legacySettings.GameDataWindowSettings?.TextSettings?.GameProgress,
            "GameDataWindow.SurDataHeader" => legacySettings.GameDataWindowSettings?.TextSettings?.SurDataHeader,
            "GameDataWindow.HunDataHeader" => legacySettings.GameDataWindowSettings?.TextSettings?.HunDataHeader,
            "GameDataWindow.SurData" => legacySettings.GameDataWindowSettings?.TextSettings?.SurData,
            "GameDataWindow.HunData" => legacySettings.GameDataWindowSettings?.TextSettings?.HunData,
            "WidgetsWindow.BpOverview_TeamName" => legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_TeamName,
            "WidgetsWindow.BpOverview_GameProgress" => legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_GameProgress,
            "WidgetsWindow.BpOverview_GameScores" => legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_GameScores,
            "WidgetsWindow.MapBpV2_MapName" => legacySettings.WidgetsWindowSettings?.TextSettings?.MapBpV2_MapName,
            "WidgetsWindow.MapBpV2_TeamName" => legacySettings.WidgetsWindowSettings?.TextSettings?.MapBpV2_TeamName,
            "WidgetsWindow.MapBpV2_CampWords" => legacySettings.WidgetsWindowSettings?.TextSettings?.MapBpV2_CampWords,
            _ => null
        };

        return style is not null;
    }

    private static bool ReadBoolSpecialProperty(LegacyControlBlueprint blueprint, string key) =>
        blueprint.SpecialProperties.TryGetValue(key, out var value)
        && bool.TryParse(value, out var result)
        && result;

    private static double ReadDoubleSpecialProperty(LegacyControlBlueprint blueprint, string key) =>
        blueprint.SpecialProperties.TryGetValue(key, out var value)
        && double.TryParse(value, out var result)
            ? result
            : 0D;

    private static double? ReadNullableDoubleSpecialProperty(LegacyControlBlueprint blueprint, string key) =>
        blueprint.SpecialProperties.TryGetValue(key, out var value)
        && double.TryParse(value, out var result)
            ? result
            : null;

    private static FrontedTextBindingExpression? CreateTextBinding(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : new FrontedTextBindingExpression
            {
                Sources =
                [
                    new FrontedBindingSourceConfig
                    {
                        Path = path
                    }
                ]
            };
    }

}

#pragma warning restore CS1591
