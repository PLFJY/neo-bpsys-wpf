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
/// 旧版前台布局包转换器的Settings逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private LegacySettings? ReadLegacySettings(
        ILegacyFrontendInputSource source,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!File.Exists(source.ConfigPath))
        {
            return null;
        }

        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeConfigJsonTooLarge));
                return null;
            }

            var legacySettings = JsonSerializer.Deserialize<LegacySettings>(stream, _jsonOptions);
            if (legacySettings is not null)
            {
                foreach (var (styleSource, style) in EnumerateLegacyTextSettings(legacySettings))
                {
                    foreach (var field in style.InvalidFields)
                    {
                        messages.Add(Warning(CodeTextSettingsFieldInvalid,
                            Args(new { Source = styleSource, Field = field })));
                    }
                }
            }

            return legacySettings;
        }
        catch (Exception ex)
        {
            messages.Add(Info(CodeTextSettingsReadFailed,
                Args(new { Reason = ex.Message })));
            return null;
        }
    }

    private static IReadOnlySet<string> ReadLegacyPropertySet(
        ILegacyFrontendInputSource source,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!File.Exists(source.ConfigPath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeConfigJsonTooLarge));
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var root = JsonNode.Parse(stream) as JsonObject;
            var properties = new HashSet<string>(StringComparer.Ordinal);
            if (root is null)
            {
                return properties;
            }

            foreach (var settings in root)
            {
                if (settings.Value is not JsonObject settingsObject)
                {
                    continue;
                }

                foreach (var property in settingsObject)
                {
                    properties.Add($"{settings.Key}.{property.Key}");
                }
            }

            return properties;
        }
        catch (Exception ex)
        {
            messages.Add(Info(CodeWindowSettingsInspectFailed,
                Args(new { Reason = ex.Message })));
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void ApplyLegacyWindowSettings(
        FrontedWindowConfig target,
        LegacyLayoutMapping mapping,
        LegacySettings? legacySettings,
        IReadOnlySet<string> legacyPropertySet,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (mapping.FixedCanvasWidth.HasValue && mapping.FixedCanvasHeight.HasValue)
        {
            target.WindowSettings.WindowWidth = mapping.FixedCanvasWidth.Value;
            target.WindowSettings.WindowHeight = mapping.FixedCanvasHeight.Value;
            target.CanvasSettings.CanvasWidth = mapping.FixedCanvasWidth.Value;
            target.CanvasSettings.CanvasHeight = mapping.FixedCanvasHeight.Value;
        }

        if (legacySettings is null)
        {
            return;
        }

        var (windowSize, backgroundColor, allowTransparency) = mapping.TargetWindow switch
        {
            "BpWindow" => (
                legacySettings.BpWindowSettings?.WindowSize,
                legacySettings.BpWindowSettings?.BackgroundColor,
                HasLegacyProperty(legacyPropertySet, "BpWindowSettings", "AllowsWindowTransparency")
                    ? legacySettings.BpWindowSettings?.AllowsWindowTransparency
                    : null),
            "ScoreSurWindow" or "ScoreHunWindow" => (
                legacySettings.ScoreWindowSettings?.ScoreInGameWindowSize,
                null,
                null),
            "ScoreGlobalWindow" => (
                legacySettings.ScoreWindowSettings?.ScoreGlobalWindowSize,
                legacySettings.ScoreWindowSettings?.ScoreGlobalWindowBackgroundColor,
                HasLegacyProperty(legacyPropertySet, "ScoreWindowSettings", "AllowsScoreGlobalWindowTransparency")
                    ? legacySettings.ScoreWindowSettings?.AllowsScoreGlobalWindowTransparency
                    : null),
            "CutSceneWindow" => (
                legacySettings.CutSceneWindowSettings?.WindowSize,
                null,
                null),
            "GameDataWindow" => (
                legacySettings.GameDataWindowSettings?.WindowSize,
                null,
                null),
            "BpOverviewWindow" or "MapV2Window" => (
                null,
                legacySettings.WidgetsWindowSettings?.BackgroundColor,
                HasLegacyProperty(legacyPropertySet, "WidgetsWindowSettings", "AllowsWindowTransparency")
                    ? legacySettings.WidgetsWindowSettings?.AllowsWindowTransparency
                    : null),
            _ => (null, null, null)
        };

        if (windowSize is not null && !mapping.FixedCanvasWidth.HasValue)
        {
            if (double.IsFinite(windowSize.Width) && windowSize.Width > 0D)
            {
                target.WindowSettings.WindowWidth = windowSize.Width;
            }

            if (double.IsFinite(windowSize.Height) && windowSize.Height > 0D)
            {
                target.WindowSettings.WindowHeight = windowSize.Height;
            }

            if (IsPositiveFinite(windowSize.Width)
                && IsPositiveFinite(windowSize.Height)
                && (!AreClose(windowSize.Width, target.CanvasSettings.CanvasWidth)
                    || !AreClose(windowSize.Height, target.CanvasSettings.CanvasHeight)))
            {
                messages.Add(Info(CodeWindowSizeDiffersFromCanvas,
                    Args(new
                    {
                        TargetWindow = mapping.TargetWindow,
                        WindowSize = $"{windowSize.Width}x{windowSize.Height}",
                        CanvasSize = $"{target.CanvasSettings.CanvasWidth}x{target.CanvasSettings.CanvasHeight}"
                    })));
            }
        }

        if (allowTransparency.HasValue)
        {
            target.WindowSettings.AllowsTransparency = allowTransparency.Value;
        }

        if (!string.IsNullOrWhiteSpace(backgroundColor))
        {
            target.WindowSettings.BackgroundColor = backgroundColor;
        }
        else if (allowTransparency == true)
        {
            target.WindowSettings.BackgroundColor = "#00000000";
        }
        else if (allowTransparency == false)
        {
            target.WindowSettings.BackgroundColor = DefaultOpaqueBackgroundColor;
        }

        if (mapping.TargetWindow == "CutSceneWindow"
            && legacySettings.CutSceneWindowSettings?.IsBlackTalentAndTraitEnable == true)
        {
            foreach (var control in target.ControlLayout.Controls.Values.OfType<TalentTraitDisplayControlConfig>())
            {
                // 2.x switches the black icon asset variant. The v3 control uses the same icon
                // alpha masks, so its explicit tint is the equivalent serializable representation.
                control.Color = "#FF000000";
            }
        }

        if (mapping.TargetWindow == "ScoreGlobalWindow"
            && legacySettings.ScoreWindowSettings?.IsCampIconBlackVerEnabled == true)
        {
            foreach (var control in target.ControlLayout.Controls.Values.OfType<GlobalScoreRowControlConfig>())
            {
                control.CampIconColor = GlobalScoreCampIconColor.Black;
            }
        }
    }

    private static bool HasLegacyProperty(IReadOnlySet<string> legacyPropertySet, string settingsName, string propertyName)
    {
        return legacyPropertySet.Contains($"{settingsName}.{propertyName}");
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0D;
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.01D;
    }

    private static LegacyWindowDefaults GetLegacyWindowDefaults(LegacyLayoutMapping mapping)
    {
        if (mapping.FixedCanvasWidth.HasValue && mapping.FixedCanvasHeight.HasValue)
        {
            return new LegacyWindowDefaults(
                mapping.FixedCanvasWidth.Value,
                mapping.FixedCanvasHeight.Value,
                mapping.FixedCanvasWidth.Value,
                mapping.FixedCanvasHeight.Value,
                GetLegacyBackgroundImage(mapping.TargetWindow));
        }

        return mapping.TargetWindow switch
        {
            "ScoreSurWindow" => new LegacyWindowDefaults(480, 152, 480, 152, "Resources/scoreSur.png"),
            "ScoreHunWindow" => new LegacyWindowDefaults(480, 152, 480, 152, "Resources/scoreHun.png"),
            "ScoreGlobalWindow" => new LegacyWindowDefaults(1440, 195, 1440, 195, "Resources/scoreGlobal.png"),
            "CutSceneWindow" => new LegacyWindowDefaults(1440, 810, 1440, 810, "Resources/cutScene.png"),
            "GameDataWindow" => new LegacyWindowDefaults(1440, 810, 1440, 810, "Resources/gameData.png"),
            "BpWindow" => new LegacyWindowDefaults(1440, 810, 1440, 810, "Resources/bp.png"),
            _ => new LegacyWindowDefaults(1440, 810, 1440, 810, GetLegacyBackgroundImage(mapping.TargetWindow))
        };
    }

    private static string? GetLegacyBackgroundImage(string? targetWindow)
    {
        return targetWindow switch
        {
            "BpOverviewWindow" => "Resources/bpOverview.png",
            "MapV2Window" => "Resources/mapBpV2.png",
            _ => null
        };
    }

    private static void ApplyCanvasConfig(FrontedWindowConfig target, FrontedCanvasConfig source)
    {
        target.Version = 3;
        target.CanvasSettings.CanvasWidth = source.CanvasWidth;
        target.CanvasSettings.CanvasHeight = source.CanvasHeight;
        target.CanvasSettings.BackgroundImage = source.BackgroundImage;
        target.CanvasSettings.EnableBoModeStates = source.EnableBoModeStates;
        target.CanvasSettings.BoModeStates = source.BoModeStates;
        target.ControlLayout.RequiredPlugins = source.RequiredPlugins;
        target.ControlLayout.Controls = source.Controls;
    }

}

#pragma warning restore CS1591
