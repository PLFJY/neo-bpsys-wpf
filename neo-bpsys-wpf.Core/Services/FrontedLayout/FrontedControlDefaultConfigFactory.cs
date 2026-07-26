using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为设计器 v3 添加控件创建安全的内存默认配置。
/// </summary>
/// <remarks>
/// 通过 <see cref="IFrontedV3ControlRegistry"/> 解析 <see cref="FrontedV3ControlRegistration"/>，
/// 调用 Registration 的 <see cref="FrontedV3ControlRegistration.CreateDefaultConfig"/> 创建默认配置。
/// 不再维护硬编码白名单或类型 switch。
/// </remarks>
public class FrontedControlDefaultConfigFactory
{
    private readonly IFrontedV3ControlRegistry? _v3ControlRegistry;
    private readonly IFrontedDesignerLocalizationService _localizationService;

    /// <summary>
    /// 使用默认本地化初始化工厂，不绑定 V3 Registry（仅用于无 DI 的测试场景）。
    /// </summary>
    public FrontedControlDefaultConfigFactory()
        : this(null, new FrontedDesignerLocalizationService())
    {
    }

    /// <summary>
    /// 使用 V3 Registry 和本地化服务初始化工厂。
    /// </summary>
    /// <param name="v3ControlRegistry">V3 控件注册表。</param>
    /// <param name="localizationService">Designer 本地化服务。</param>
    public FrontedControlDefaultConfigFactory(
        IFrontedV3ControlRegistry? v3ControlRegistry,
        IFrontedDesignerLocalizationService? localizationService = null)
    {
        _v3ControlRegistry = v3ControlRegistry;
        _localizationService = localizationService ?? new FrontedDesignerLocalizationService();
    }

    /// <summary>
    /// 获取普通添加控件公开的内置控件类型集合。
    /// </summary>
    /// <returns>内置控件 CanonicalControlType 集合。</returns>
    public IReadOnlySet<string> GetAddableControlTypes()
    {
        if (_v3ControlRegistry is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return _v3ControlRegistry.GetRegistrations()
            .Where(registration => registration.IsBuiltIn)
            .Select(registration => registration.CanonicalControlType)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 返回 ControlType 是否可通过普通添加控件创建。
    /// </summary>
    /// <param name="controlType">控件的 CanonicalControlType。</param>
    /// <returns>已注册时为 <see langword="true"/>。</returns>
    public bool CanCreate(string controlType) =>
        _v3ControlRegistry?.GetRegistration(controlType) is not null;

    /// <summary>
    /// 获取添加控件的目录分组（内置 + 插件）。
    /// </summary>
    /// <returns>目录分组列表。</returns>
    public IReadOnlyList<FrontedAddControlCatalogGroup> GetCatalog()
    {
        var registrations = _v3ControlRegistry?.GetRegistrations() ?? [];

        var builtInItems = registrations
            .Where(registration => registration.IsBuiltIn)
            .OrderBy(registration => registration.Metadata.DisplayOrder ?? int.MaxValue)
            .Select(registration => new FrontedAddControlCatalogItem
            {
                ControlType = registration.CanonicalControlType,
                DisplayName = ResolveControlDisplayName(registration),
                Description = ResolveControlDescription(registration),
                Icon = registration.Metadata.Icon,
                IsAvailable = true
            })
            .ToArray();

        var builtIn = new FrontedAddControlCatalogGroup
        {
            DisplayName = _localizationService.GetDesignerText("BasicControls", "Basic Controls"),
            Items = builtInItems
        };

        var pluginGroups = registrations
            .Where(registration => !registration.IsBuiltIn)
            .Select(registration => new FrontedAddControlCatalogItem
            {
                ControlType = registration.CanonicalControlType,
                DisplayName = ResolveControlDisplayName(registration),
                Description = ResolveControlDescription(registration),
                Icon = registration.Metadata.Icon,
                IsPlugin = true,
                PackageId = registration.PackageId,
                PluginDisplayName = registration.PackageId,
                IsAvailable = true
            })
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
    /// 解析控件在目录中的显示名称：优先使用 <see cref="FrontedV3ControlMetadata.DisplayNameKey"/>，
    /// 其次回退到本地化服务按 CanonicalControlType 推导，最后回退到 LocalControlId。
    /// </summary>
    /// <param name="registration">控件注册信息。</param>
    /// <returns>控件的本地化显示名称。</returns>
    private string ResolveControlDisplayName(FrontedV3ControlRegistration registration)
    {
        var key = registration.Metadata.DisplayNameKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            return _localizationService.GetDesignerText(key, registration.LocalControlId);
        }

        return _localizationService.GetControlTypeDisplayName(registration.CanonicalControlType);
    }

    /// <summary>
    /// 解析控件在目录中的描述：仅当 <see cref="FrontedV3ControlMetadata.DescriptionKey"/> 非空时
    /// 通过本地化服务查询；未声明时返回空字符串（目录不显示描述）。
    /// </summary>
    /// <param name="registration">控件注册信息。</param>
    /// <returns>控件的本地化描述；未声明时为 <see cref="string.Empty"/>。</returns>
    private string ResolveControlDescription(FrontedV3ControlRegistration registration)
    {
        var key = registration.Metadata.DescriptionKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return _localizationService.GetDesignerText(key, string.Empty);
    }

    /// <summary>
    /// 创建默认配置并将其放置在请求的逻辑中心周围。
    /// </summary>
    /// <param name="controlType">控件的 CanonicalControlType。</param>
    /// <param name="document">目标设计文档。</param>
    /// <param name="centerX">放置中心 X（可选）。</param>
    /// <param name="centerY">放置中心 Y（可选）。</param>
    /// <returns>创建的默认配置实例。</returns>
    /// <exception cref="NotSupportedException">当 ControlType 未注册时抛出。</exception>
    public FrontedControlConfigBase Create(
        string controlType,
        FrontedCanvasDesignDocument document,
        double? centerX = null,
        double? centerY = null)
    {
        var registration = _v3ControlRegistry?.GetRegistration(controlType)
            ?? throw new NotSupportedException($"Unsupported control type '{controlType}'.");

        var config = registration.CreateDefaultConfig();
        config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
        config.ZIndex = GetNextZIndex(document);

        // 应用 Attribute 声明的默认根尺寸；未声明时保持 null，由 ApplyPlacement 按最小命中框回退。
        if (config.Width is null && registration.Metadata.DefaultWidth is { } defaultWidth)
        {
            config.Width = defaultWidth;
        }

        if (config.Height is null && registration.Metadata.DefaultHeight is { } defaultHeight)
        {
            config.Height = defaultHeight;
        }

        ApplyPlacement(config, document, centerX, centerY);
        return config;
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
