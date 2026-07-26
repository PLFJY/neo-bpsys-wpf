using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// 内置控件固定 Part 定义解析器，为内置控件提供 Part 定义。
/// </summary>
/// <remarks>
/// <para>
/// BorderedImage 等内置控件的内部 Part（如内层 Image）的 resize 走通用 <see cref="Geometry.FixedPartGeometryTarget"/>。
/// MapV2Display 支持 5 个固定内部部件。
/// 该解析器为这些控件提供 Part 定义。
/// </para>
/// <para>
/// Part Storage 映射到 Config 的现有字段，JSON 不变：
/// <list type="bullet">
/// <item>BorderedImage.Image：Width=<c>ImageWidth</c>、Height=<c>ImageHeight</c>、Capabilities=Resize。</item>
/// <item>MapV2Display.TeamName/MapCard/MapName/CampName/PickingBorder：
/// X/Y/Width/Height 通过 <c>InternalParts</c> 列表项的 CLR 属性读写，
/// 项键=<c>Part.ToString()</c>，Capabilities=MoveAndResize。</item>
/// </list>
/// </para>
/// </remarks>
internal static class BuiltInPartDefinitionResolver
{
    /// <summary>
    /// 返回给定 Config 可用的固定 Part 定义列表。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>Part 定义列表；无可用 Part 时返回空列表。</returns>
    public static IReadOnlyList<FrontedV3PartDefinition> GetParts(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config is BorderedImageFrontedControlConfig)
        {
            return new[]
            {
                new FrontedV3PartDefinition(
                    id: "Image",
                    capabilities: FrontedV3PartCapabilities.Resize,
                    widthStorage: FrontedV3Storage.ClrProperty("ImageWidth"),
                    heightStorage: FrontedV3Storage.ClrProperty("ImageHeight"))
            };
        }

        if (config is MapV2DisplayControlConfig mapV2Config)
        {
            MapV2InternalPartLayoutHelper.EnsureParts(mapV2Config);
            return CreateMapV2Parts();
        }

        return Array.Empty<FrontedV3PartDefinition>();
    }

    /// <summary>
    /// 返回给定 Config 是否有可用的固定 Part。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>有可用 Part 时为 <see langword="true"/>。</returns>
    public static bool HasParts(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config is BorderedImageFrontedControlConfig or MapV2DisplayControlConfig;
    }

    /// <summary>
    /// 按 Id 查找给定 Config 的 Part 定义。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="partId">Part 标识。</param>
    /// <returns>匹配的 Part 定义；未找到时为 <see langword="null"/>。</returns>
    public static FrontedV3PartDefinition? FindPart(FrontedControlConfigBase config, string partId)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(partId);

        foreach (var part in GetParts(config))
        {
            if (string.Equals(part.Id, partId, StringComparison.Ordinal))
            {
                return part;
            }
        }

        return null;
    }

    private static FrontedV3PartDefinition[] CreateMapV2Parts()
    {
        var partIds = new[]
        {
            MapV2InternalStylePart.TeamName,
            MapV2InternalStylePart.MapCard,
            MapV2InternalStylePart.MapName,
            MapV2InternalStylePart.CampName,
            MapV2InternalStylePart.PickingBorder
        };

        var definitions = new FrontedV3PartDefinition[partIds.Length];
        for (var i = 0; i < partIds.Length; i++)
        {
            var partId = partIds[i].ToString();
            definitions[i] = new FrontedV3PartDefinition(
                id: partId,
                capabilities: FrontedV3PartCapabilities.MoveAndResize,
                widthStorage: FrontedV3Storage.CollectionItemProperty(
                    config => ((MapV2DisplayControlConfig)config).InternalParts,
                    item => ((MapV2InternalPartLayoutConfig)item).Part.ToString()!,
                    partId,
                    nameof(MapV2InternalPartLayoutConfig.Width)),
                heightStorage: FrontedV3Storage.CollectionItemProperty(
                    config => ((MapV2DisplayControlConfig)config).InternalParts,
                    item => ((MapV2InternalPartLayoutConfig)item).Part.ToString()!,
                    partId,
                    nameof(MapV2InternalPartLayoutConfig.Height)),
                xStorage: FrontedV3Storage.CollectionItemProperty(
                    config => ((MapV2DisplayControlConfig)config).InternalParts,
                    item => ((MapV2InternalPartLayoutConfig)item).Part.ToString()!,
                    partId,
                    nameof(MapV2InternalPartLayoutConfig.X)),
                yStorage: FrontedV3Storage.CollectionItemProperty(
                    config => ((MapV2DisplayControlConfig)config).InternalParts,
                    item => ((MapV2InternalPartLayoutConfig)item).Part.ToString()!,
                    partId,
                    nameof(MapV2InternalPartLayoutConfig.Y)));
        }

        return definitions;
    }
}
