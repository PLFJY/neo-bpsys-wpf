using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Creates safe in-memory defaults for Designer v3 Add Control.
/// </summary>
public class FrontedControlDefaultConfigFactory
{
    private static readonly IReadOnlySet<string> AddableControlTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Text",
            "LocalizedText",
            "Image",
            "MapNameText",
            "GameProgressText",
            "TalentTraitDisplay",
            "GlobalScoreRow",
            "CurrentBanDisplay",
            "BanSlotDisplay",
            "MapV2Display"
        };

    /// <summary>
    /// Gets built-in control types exposed by normal Add Control.
    /// </summary>
    public IReadOnlySet<string> GetAddableControlTypes() => AddableControlTypes;

    /// <summary>
    /// Returns whether a ControlType can be created by normal Add Control.
    /// </summary>
    public bool CanCreate(string controlType) => AddableControlTypes.Contains(controlType);

    /// <summary>
    /// Creates a default config and places it around the requested logical center.
    /// </summary>
    public FrontedControlConfigBase Create(
        string controlType,
        FrontedCanvasDesignDocument document,
        double? centerX = null,
        double? centerY = null)
    {
        if (!CanCreate(controlType))
        {
            throw new NotSupportedException($"Unsupported control type '{controlType}'.");
        }

        var config = CreateDefault(controlType);
        config.ZIndex = GetNextZIndex(document);
        ApplyPlacement(config, document, centerX, centerY);
        return config;
    }

    private static FrontedControlConfigBase CreateDefault(string controlType)
    {
        return controlType switch
        {
            "Text" => new TextFrontedControlConfig
            {
                Text = "Text",
                Width = 160,
                Height = 40,
                FontSize = 24,
                Color = "#FFFFFFFF",
                TextAlignment = "Center"
            },
            "LocalizedText" => new LocalizedTextControlConfig
            {
                LocalizationKey = "Text",
                FallbackText = "Localized Text",
                Width = 200,
                Height = 40,
                FontSize = 24,
                Color = "#FFFFFFFF",
                TextAlignment = "Center"
            },
            "Image" => new ImageFrontedControlConfig
            {
                Width = 120,
                Height = 120,
                SizingMode = ImageSizingMode.Auto,
                Stretch = "Uniform"
            },
            "MapNameText" => new MapNameTextControlConfig
            {
                Width = 240,
                Height = 40,
                FontSize = 24,
                Color = "#FFFFFFFF",
                TextAlignment = "Center",
                EmptyText = "Map"
            },
            "GameProgressText" => new GameProgressTextControlConfig
            {
                Width = 260,
                Height = 56,
                FontSize = 24,
                Color = "#FFFFFFFF",
                TextAlignment = "Center",
                UseLineBreak = false
            },
            "TalentTraitDisplay" => new TalentTraitDisplayControlConfig
            {
                DisplayKind = TalentTraitDisplayKind.SurvivorTalent,
                PlayerIndex = 0,
                Width = 180,
                Height = 40,
                IconSize = 36,
                IconGap = 0
            },
            "GlobalScoreRow" => new GlobalScoreRowControlConfig
            {
                TeamType = TeamType.HomeTeam,
                Width = 540,
                Height = 40,
                FontSize = 24,
                Color = "#FFFFFFFF",
                ShowCampIcon = true
            },
            "CurrentBanDisplay" => new CurrentBanDisplayControlConfig
            {
                Camp = Camp.Sur,
                Index = 0,
                Width = 70,
                Height = 36,
                Stretch = "Uniform"
            },
            "BanSlotDisplay" => new BanSlotDisplayControlConfig
            {
                SlotKind = BanSlotKind.Current,
                Camp = Camp.Sur,
                Index = 0,
                Width = 48,
                Height = 48,
                Stretch = "Uniform"
            },
            "MapV2Display" => new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                Width = 151,
                Height = 160
            },
            _ => throw new NotSupportedException($"Unsupported control type '{controlType}'.")
        };
    }

    private static int GetNextZIndex(FrontedCanvasDesignDocument document)
    {
        return document.Controls.Count == 0
            ? 1
            : document.Controls.Max(item => item.Config.ZIndex) + 1;
    }

    private static void ApplyPlacement(
        FrontedControlConfigBase config,
        FrontedCanvasDesignDocument document,
        double? centerX,
        double? centerY)
    {
        var width = config.Width ?? FrontedDesignerGeometryHelper.MinHitWidth;
        var height = config.Height ?? FrontedDesignerGeometryHelper.MinHitHeight;
        var canvasWidth = document.CanvasConfig.CanvasWidth;
        var canvasHeight = document.CanvasConfig.CanvasHeight;
        var x = centerX ?? canvasWidth / 2D;
        var y = centerY ?? canvasHeight / 2D;

        config.Left = FrontedDesignerGeometryHelper.Snap(Math.Clamp(x - width / 2D, 0D, Math.Max(0D, canvasWidth - width)));
        config.Top = FrontedDesignerGeometryHelper.Snap(Math.Clamp(y - height / 2D, 0D, Math.Max(0D, canvasHeight - height)));
    }
}
