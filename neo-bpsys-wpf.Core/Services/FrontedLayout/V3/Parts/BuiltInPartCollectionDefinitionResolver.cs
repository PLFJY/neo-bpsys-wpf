using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// 内置控件 PartCollection 定义解析器，为内置控件提供集合定义。
/// </summary>
/// <remarks>
/// <para>
/// GlobalScoreRow 等内置控件的 <c>Cells</c> 集合的几何操作走通用 <see cref="Geometry.CollectionItemGeometryTarget"/>。
/// 该解析器为这些控件提供 PartCollection 定义。
/// </para>
/// <para>
/// Collection Storage 映射到 Config 的现有集合字段，JSON 不变：
/// <list type="bullet">
/// <item>GlobalScoreRow.Cells：FixedTemplate 策略，项键=<c>Cell.Id</c>，几何属性=<c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>，
/// 模板补齐=<see cref="GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(GlobalScoreRowControlConfig, bool)"/>（BO5 模板，确保全部 12 个 Cell 存在）。</item>
/// </list>
/// </para>
/// </remarks>
internal static class BuiltInPartCollectionDefinitionResolver
{
    /// <summary>
    /// 返回给定 Config 可用的 PartCollection 定义列表。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>集合定义列表；无可用集合时返回空列表。</returns>
    public static IReadOnlyList<FrontedV3PartCollectionDefinition> GetCollections(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config is GlobalScoreRowControlConfig)
        {
            return new[]
            {
                CreateGlobalScoreCellsCollection()
            };
        }

        return Array.Empty<FrontedV3PartCollectionDefinition>();
    }

    /// <summary>
    /// 返回给定 Config 是否有可用的 PartCollection。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>有可用集合时为 <see langword="true"/>。</returns>
    public static bool HasCollections(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config is GlobalScoreRowControlConfig;
    }

    /// <summary>
    /// 按 Id 查找给定 Config 的 PartCollection 定义。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <returns>匹配的集合定义；未找到时为 <see langword="null"/>。</returns>
    public static FrontedV3PartCollectionDefinition? FindCollection(FrontedControlConfigBase config, string collectionId)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(collectionId);

        foreach (var collection in GetCollections(config))
        {
            if (string.Equals(collection.Id, collectionId, StringComparison.Ordinal))
            {
                return collection;
            }
        }

        return null;
    }

    private static FrontedV3PartCollectionDefinition CreateGlobalScoreCellsCollection()
    {
        return new FrontedV3PartCollectionDefinition(
            id: "Cells",
            strategy: FrontedV3PartCollectionStrategy.FixedTemplate,
            itemCapabilities: FrontedV3PartCapabilities.MoveAndResize,
            collectionGetter: config => ((GlobalScoreRowControlConfig)config).Cells,
            itemKeySelector: item => ((GlobalScoreCellConfig)item).Id,
            ensureTemplateItems: config =>
                GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(
                    (GlobalScoreRowControlConfig)config,
                    isBo3Mode: false));
    }
}
