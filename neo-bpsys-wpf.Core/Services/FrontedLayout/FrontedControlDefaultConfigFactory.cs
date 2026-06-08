using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Reflection;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Creates safe in-memory defaults for Designer v3 Add Control.
/// </summary>
public class FrontedControlDefaultConfigFactory
{
    private readonly IFrontedControlRegistry? _controlRegistry;
    private readonly IFrontedDesignerLocalizationService _localizationService;

    private static readonly IReadOnlySet<string> AddableControlTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Text",
            "LocalizedText",
            "Image",
            "BorderedImage",
            "Rectangle",
            "Polygon",
            "BackgroundTintRectangle",
            "BackgroundTintPolygon",
            "MapNameText",
            "GameProgressText",
            "TalentTraitDisplay",
            "GlobalScoreRow",
            "MapV2Display"
        };

    public FrontedControlDefaultConfigFactory()
        : this(null, new FrontedDesignerLocalizationService())
    {
    }

    public FrontedControlDefaultConfigFactory(
        IFrontedControlRegistry? controlRegistry,
        IFrontedDesignerLocalizationService? localizationService = null)
    {
        _controlRegistry = controlRegistry;
        _localizationService = localizationService ?? new FrontedDesignerLocalizationService();
    }

    /// <summary>
    /// Gets built-in control types exposed by normal Add Control.
    /// </summary>
    public IReadOnlySet<string> GetAddableControlTypes() => AddableControlTypes;

    /// <summary>
    /// Returns whether a ControlType can be created by normal Add Control.
    /// </summary>
    public bool CanCreate(string controlType) =>
        AddableControlTypes.Contains(controlType)
        || CanCreatePlugin(controlType);

    public IReadOnlyList<FrontedAddControlCatalogGroup> GetCatalog()
    {
        var builtIn = new FrontedAddControlCatalogGroup
        {
            DisplayName = _localizationService.GetDesignerText("BasicControls", "Basic Controls"),
            Items = AddableControlTypes
                .Select(controlType => new FrontedAddControlCatalogItem
                {
                    ControlType = controlType,
                    DisplayName = _localizationService.GetControlTypeDisplayName(controlType),
                    IsAvailable = true
                })
                .ToArray()
        };

        var pluginGroups = (_controlRegistry?.GetPluginDescriptors() ?? [])
            .Select(CreatePluginCatalogItem)
            .Where(item => item.IsAvailable)
            .GroupBy(item => item.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FrontedAddControlCatalogGroup
            {
                DisplayName = group.First().PluginDisplayName ?? group.Key,
                PackageId = group.Key,
                IsPlugin = true,
                Items = group.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray()
            })
            .ToArray();

        return [builtIn, .. pluginGroups];
    }

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

        var config = FrontedPluginControlType.IsPluginControlType(controlType)
            ? CreatePluginDefault(controlType)
            : CreateDefault(controlType);
        config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
        config.ZIndex = GetNextZIndex(document);
        ApplyPlacement(config, document, centerX, centerY);
        return config;
    }

    private bool CanCreatePlugin(string controlType)
    {
        return _controlRegistry?.GetPluginDescriptor(controlType) is { } descriptor
               && TryCreatePluginDefault(descriptor, out _);
    }

    private FrontedControlConfigBase CreatePluginDefault(string controlType)
    {
        var descriptor = _controlRegistry?.GetPluginDescriptor(controlType)
            ?? throw new NotSupportedException($"Unsupported plugin control type '{controlType}'.");

        if (TryCreatePluginDefault(descriptor, out var config))
        {
            return config;
        }

        throw new NotSupportedException($"Plugin control type '{controlType}' does not provide a safe default config.");
    }

    private FrontedAddControlCatalogItem CreatePluginCatalogItem(IFrontedPluginControlDescriptor descriptor)
    {
        var isAvailable = TryCreatePluginDefault(descriptor, out _);
        var displayName = ResolveDescriptorText(descriptor.DisplayNameKey, descriptor.ControlTypeName);
        return new FrontedAddControlCatalogItem
        {
            ControlType = descriptor.FullControlType,
            DisplayName = displayName,
            Description = ResolveDescriptorText(descriptor.DescriptionKey, string.Empty),
            Icon = descriptor.Icon,
            IsPlugin = true,
            PackageId = descriptor.PackageId,
            PluginDisplayName = descriptor.PackageId,
            IsAvailable = isAvailable,
            UnavailableReason = isAvailable
                ? null
                : _localizationService.GetDesignerText("Designer.PluginControlUnavailable", "Plugin control unavailable")
        };
    }

    private string ResolveDescriptorText(string? key, string fallback)
    {
        return string.IsNullOrWhiteSpace(key)
            ? fallback
            : _localizationService.GetDesignerText(key, fallback);
    }

    private static bool TryCreatePluginDefault(
        IFrontedPluginControlDescriptor descriptor,
        out FrontedControlConfigBase config)
    {
        config = null!;
        var createDefaultConfig = descriptor.GetType()
            .GetProperty(nameof(FrontedPluginControlDescriptor<FrontedControlConfigBase>.CreateDefaultConfig), BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(descriptor) as Delegate;

        if (createDefaultConfig is not null)
        {
            if (createDefaultConfig.DynamicInvoke() is FrontedControlConfigBase created)
            {
                created.ControlType = descriptor.FullControlType;
                config = created;
                return true;
            }

            return false;
        }

        if (descriptor.ConfigType.GetConstructor(Type.EmptyTypes) is null
            || !typeof(FrontedControlConfigBase).IsAssignableFrom(descriptor.ConfigType))
        {
            return false;
        }

        config = (FrontedControlConfigBase)(Activator.CreateInstance(descriptor.ConfigType)
            ?? throw new InvalidOperationException($"Plugin config '{descriptor.ConfigType.Name}' could not be created."));
        config.ControlType = descriptor.FullControlType;
        return true;
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
            "BorderedImage" => new BorderedImageFrontedControlConfig
            {
                Width = 120,
                Height = 120,
                SizingMode = ImageSizingMode.FillContainer,
                Stretch = "UniformToFill",
                ClipToBounds = true
            },
            "Rectangle" => new RectangleFrontedControlConfig
            {
                Width = 160,
                Height = 80,
                FillMode = ShapeFillMode.Solid,
                FillColor = "#FFFFFFFF"
            },
            "Polygon" => new PolygonFrontedControlConfig
            {
                Width = 160,
                Height = 120,
                FillMode = ShapeFillMode.Solid,
                FillColor = "#FFFFFFFF"
            },
            "BackgroundTintRectangle" => new BackgroundTintRectangleFrontedControlConfig
            {
                Width = 160,
                Height = 80,
                TintColor = "#FFFFFFFF",
                TintStrength = 1D,
                TextureStrength = 0.45D,
                TintMode = BackgroundTintMode.LuminanceColorize
            },
            "BackgroundTintPolygon" => new BackgroundTintPolygonFrontedControlConfig
            {
                Width = 160,
                Height = 120,
                TintColor = "#FFFFFFFF",
                TintStrength = 1D,
                TextureStrength = 0.45D,
                TintMode = BackgroundTintMode.LuminanceColorize
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
                DisplayMode = Core.Enums.GameProgressTextDisplayMode.Inline,
                VerticalLanguageMode = Core.Enums.GameProgressVerticalLanguageMode.Auto,
                LatinVerticalMode = Core.Enums.GameProgressLatinVerticalMode.RotateBlock,
                NumberStyle = Core.Enums.GameProgressNumberStyle.Auto,
                VerticalDirection = Core.Enums.GameProgressVerticalDirection.Auto,
                DisplayLanguage = Core.Enums.LanguageKey.FollowApp,
                GroupSpacing = 8,
                PaddingLeft = 0,
                PaddingTop = 0,
                PaddingRight = 0,
                PaddingBottom = 0,
                ShowSeparator = false,
                SeparatorThickness = 1
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
                Width = 1080,
                Height = 40,
                FontFamily = "Arial",
                FontWeight = "Bold",
                FontSize = 24,
                Color = "#FFFFFFFF",
                ShowCampIcon = true,
                Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate()
            },
            "MapV2Display" => new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                Width = 151,
                Height = 160,
                MapBorderNormalColor = "#FF2B483B",
                MapBorderBannedColor = "#FF9C3E2F"
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
